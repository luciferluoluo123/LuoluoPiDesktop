using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using LuoluoPiDesktop.Core.Models;
using LuoluoPiDesktop.Core.Services;

namespace LuoluoPiDesktop.Infrastructure.Services;

/// <summary>
/// 管理一个 codex app-server 子进程，通过 NDJSON-RPC (stdio) 与其通信。
/// 所有事件在后台线程触发，调用方负责切换到 UI 线程。
/// </summary>
public sealed class CodexRpcService : IAsyncDisposable
{
    private readonly string     _codexExe;
    private readonly IAppLogger _logger;

    private Process?       _proc;
    private StreamWriter?  _stdin;
    private Task?          _readTask;
    private CancellationTokenSource _cts = new();

    private int _nextId;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();

    private string? _threadId;
    private string? _currentTurnId;

    // ── 事件（后台线程触发）──────────────────────────────────────────────
    public event Action<CodexState>?      StateChanged;
    public event Action<string>?          TextDelta;
    public event Action<bool>?            TurnCompleted;           // bool = wasInterrupted
    public event Action<string>?          AgentItemCompleted;      // 完整 agentMessage 文本
    public event Action<int?>?            ProcessExited;

    // 工具调用
    public event Action<string, string>?  ToolCallStarted;         // (itemId, cmdLabel)
    public event Action<string, string>?  ToolCallOutputDelta;     // (itemId, outputChunk)
    public event Action<string, int?>?    ToolCallCompleted;       // (itemId, exitCode)
    public event Action<string, string>?  FileChangeStarted;       // (itemId, filePath)
    public event Action<string, string>?  FileChangeDelta;         // (itemId, deltaText)

    // ── 公开状态 ─────────────────────────────────────────────────────────
    public CodexState State { get; private set; } = CodexState.NotStarted;
    public string?    ThreadId => _threadId;
    public string?    CurrentTurnId => _currentTurnId;

    public CodexRpcService(string codexExe, IAppLogger logger)
    {
        _codexExe = codexExe;
        _logger   = logger;
    }

    // ── 启动 ─────────────────────────────────────────────────────────────

    public async Task StartAsync(string workingDir, CancellationToken ct = default)
    {
        SetState(CodexState.Starting);
        _logger.Info($"CodexRpcService: starting — cwd={workingDir}");

        var psi = new ProcessStartInfo(_codexExe, "app-server --listen stdio://")
        {
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            StandardInputEncoding  = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            UseShellExecute        = false,
            CreateNoWindow         = true,
            WorkingDirectory       = workingDir,
        };

        _proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start codex process");
        _logger.Info($"CodexRpcService: PID={_proc.Id}");

        _stdin = new StreamWriter(_proc.StandardInput.BaseStream, new UTF8Encoding(false))
        {
            AutoFlush = true,
        };

        _proc.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                _logger.Info($"[codex stderr] {e.Data}");
        };
        _proc.BeginErrorReadLine();

        _proc.Exited += (_, _) =>
        {
            _logger.Warn($"CodexRpcService: process exited code={_proc.ExitCode}");
            SetState(CodexState.Error);
            ProcessExited?.Invoke(_proc.ExitCode);
        };
        _proc.EnableRaisingEvents = true;

        _cts = new CancellationTokenSource();
        _readTask = Task.Run(() => ReadLoopAsync(_proc.StandardOutput, _cts.Token));

        // Initialize handshake
        await SendRpcAsync("initialize", new
        {
            clientInfo = new { name = "LuoluoPiDesktop", version = "0.1.0" }
        }, ct);

        // Start thread with project working dir — approvalPolicy=never 让命令自动执行
        var threadResult = await SendRpcAsync("thread/start", new
        {
            cwd            = workingDir,
            approvalPolicy = "never",
        }, ct);
        _threadId = threadResult.TryGetProperty("thread", out var th)
            ? th.TryGetProperty("id", out var id) ? id.GetString() : null
            : null;

        if (_threadId is null)
            throw new InvalidOperationException("thread/start returned no thread id");

        _logger.Info($"CodexRpcService: threadId={_threadId}");
        SetState(CodexState.Idle);
    }

    // ── 发送消息 ──────────────────────────────────────────────────────────

    public async Task SendMessageAsync(string text, CancellationToken ct = default)
    {
        if (_threadId is null) throw new InvalidOperationException("Not initialized");
        SetState(CodexState.Thinking);

        var result = await SendRpcAsync("turn/start", new
        {
            threadId = _threadId,
            input    = new[] { new { type = "text", text } },
        }, ct);

        // Capture turnId from response immediately (notifications may arrive later)
        if (result.TryGetProperty("turn", out var t) && t.TryGetProperty("id", out var tid))
            _currentTurnId = tid.GetString();
    }

    // ── 中止 ─────────────────────────────────────────────────────────────

    public async Task InterruptAsync()
    {
        if (_threadId is null || _currentTurnId is null) return;
        _logger.Info($"CodexRpcService: interrupting turnId={_currentTurnId}");
        try
        {
            await SendRpcAsync("turn/interrupt", new
            {
                threadId = _threadId,
                turnId   = _currentTurnId,
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.Warn($"CodexRpcService: interrupt failed — {ex.Message}");
        }
    }

    // ── 停止 ─────────────────────────────────────────────────────────────

    public async Task StopAsync()
    {
        SetState(CodexState.Stopping);
        _logger.Info("CodexRpcService: stopping");

        _cts.Cancel();

        try { _stdin?.Close(); } catch { }

        if (_proc != null && !_proc.HasExited)
        {
            if (!_proc.WaitForExit(3000))
            {
                _logger.Warn("CodexRpcService: killing process");
                _proc.Kill(entireProcessTree: true);
            }
        }

        if (_readTask != null)
            await _readTask.ConfigureAwait(false);

        SetState(CodexState.Stopped);
    }

    public async ValueTask DisposeAsync()
    {
        if (State is not (CodexState.Stopped or CodexState.Error or CodexState.NotStarted))
            await StopAsync();

        _proc?.Dispose();
        _stdin?.Dispose();
        _cts.Dispose();
    }

    // ── JSON-RPC 发送 ─────────────────────────────────────────────────────

    private async Task<JsonElement> SendRpcAsync(string method, object parameters, CancellationToken ct)
    {
        var id  = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
        _pending[id]  = tcs;

        var json = JsonSerializer.Serialize(new { id, method, @params = parameters });
        _logger.Info($"[RPC→] [{id}] {method}");

        _stdin!.WriteLine(json);

        try
        {
            return await tcs.Task;
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    // ── 读取循环 ─────────────────────────────────────────────────────────

    private async Task ReadLoopAsync(StreamReader reader, CancellationToken ct)
    {
        _logger.Info("CodexRpcService: read loop started");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    // Response
                    if (root.TryGetProperty("id", out var idEl) &&
                        root.TryGetProperty("result", out var resultEl))
                    {
                        if (idEl.TryGetInt32(out var id) &&
                            _pending.TryRemove(id, out var tcs))
                        {
                            _logger.Info($"[RPC←] [{id}] result");
                            tcs.TrySetResult(resultEl.Clone());
                        }
                        continue;
                    }

                    // Error response
                    if (root.TryGetProperty("id", out idEl) &&
                        root.TryGetProperty("error", out var errEl))
                    {
                        if (idEl.TryGetInt32(out var id) &&
                            _pending.TryRemove(id, out var tcs))
                        {
                            var msg = errEl.TryGetProperty("message", out var m)
                                ? m.GetString() ?? "RPC error" : "RPC error";
                            _logger.Warn($"[RPC←] [{id}] error: {msg}");
                            tcs.TrySetException(new Exception($"RPC error: {msg}"));
                        }
                        continue;
                    }

                    // Notification
                    if (root.TryGetProperty("method", out var methodEl))
                    {
                        var method = methodEl.GetString() ?? string.Empty;
                        var prms   = root.TryGetProperty("params", out var p)
                            ? p.Clone() : default;
                        HandleNotification(method, prms);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.Warn($"CodexRpcService: JSON parse error — {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.Error("CodexRpcService: read loop exception", ex);
        }
        finally
        {
            _logger.Info("CodexRpcService: read loop ended");
        }
    }

    // ── 通知处理 ──────────────────────────────────────────────────────────

    private void HandleNotification(string method, JsonElement prms)
    {
        switch (method)
        {
            case "item/agentMessage/delta":
            {
                var delta = prms.TryGetProperty("delta", out var d) ? d.GetString() ?? "" : "";
                // Also grab turnId if not yet set
                if (_currentTurnId is null &&
                    prms.TryGetProperty("turnId", out var tid))
                    _currentTurnId = tid.GetString();
                if (delta.Length > 0)
                    TextDelta?.Invoke(delta);
                break;
            }

            case "item/started":
            {
                if (!prms.TryGetProperty("item", out var item)) break;
                if (!item.TryGetProperty("type", out var typeProp)) break;
                var itemType = typeProp.GetString();
                var itemId   = item.TryGetProperty("id", out var idp) ? idp.GetString() ?? "" : "";

                if (itemType == "commandExecution")
                {
                    // command label — field confirmed as "command" from item/completed raw JSON
                    var cmd = item.TryGetProperty("command", out var c2) ? c2.GetString() :
                              item.TryGetProperty("cmd",     out var c1) ? c1.GetString() :
                              item.TryGetProperty("cmdArgs", out var c3) ? c3.GetString() : null;
                    _logger.Info($"[Tool] commandExecution started id={itemId} cmd={cmd}");
                    ToolCallStarted?.Invoke(itemId, cmd ?? "shell");
                    SetState(CodexState.ToolRunning);
                }
                else if (itemType is "fileChange" or "fileWrite" or "fileRead")
                {
                    var path = item.TryGetProperty("path", out var pp) ? pp.GetString() ?? "" : "";
                    _logger.Info($"[Tool] {itemType} started id={itemId} path={path}");
                    FileChangeStarted?.Invoke(itemId, path);
                }
                break;
            }

            case "item/completed":
            {
                if (!prms.TryGetProperty("item", out var item)) break;
                if (!item.TryGetProperty("type", out var typeProp)) break;
                var itemType = typeProp.GetString();
                var itemId   = item.TryGetProperty("id", out var idp) ? idp.GetString() ?? "" : "";

                if (itemType == "agentMessage")
                {
                    var text = item.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                    if (text.Length > 0)
                        AgentItemCompleted?.Invoke(text);
                }
                else if (itemType == "commandExecution")
                {
                    int? exitCode = null;
                    if (item.TryGetProperty("exitCode", out var ec) && ec.TryGetInt32(out var ecInt))
                        exitCode = ecInt;

                    // 实际字段名为 aggregatedOutput（由 item keys 日志确认）
                    var output = item.TryGetProperty("aggregatedOutput", out var o1) ? o1.GetString() : null;

                    _logger.Info($"[Tool] commandExecution completed id={itemId} exitCode={exitCode} outputLen={output?.Length ?? 0}");

                    if (!string.IsNullOrEmpty(output))
                        ToolCallOutputDelta?.Invoke(itemId, output);

                    ToolCallCompleted?.Invoke(itemId, exitCode);
                    SetState(CodexState.Thinking);
                }
                else if (itemType is "fileChange" or "fileWrite" or "fileRead")
                {
                    ToolCallCompleted?.Invoke(itemId, null);
                }
                break;
            }

            case "turn/started":
            {
                // Override turnId from notification (may arrive before response)
                if (prms.TryGetProperty("turn", out var turn) &&
                    turn.TryGetProperty("id", out var tid))
                    _currentTurnId = tid.GetString();
                SetState(CodexState.Thinking);
                break;
            }

            case "turn/completed":
            {
                var status = "";
                if (prms.TryGetProperty("turn", out var turn) &&
                    turn.TryGetProperty("status", out var s))
                    status = s.GetString() ?? "";
                _logger.Info($"CodexRpcService: turn completed — status={status}");
                _currentTurnId = null;
                SetState(CodexState.Idle);
                TurnCompleted?.Invoke(status == "interrupted");
                break;
            }

            case "item/commandExecution/outputDelta":
            {
                var itemId = prms.TryGetProperty("itemId", out var iid) ? iid.GetString() ?? "" : "";
                var output = prms.TryGetProperty("output", out var o)   ? o.GetString()   ?? "" : "";
                _logger.Info($"[Tool] outputDelta id={itemId} len={output.Length}");
                if (output.Length > 0)
                    ToolCallOutputDelta?.Invoke(itemId, output);
                SetState(CodexState.ToolRunning);
                break;
            }

            case "item/fileChange/outputDelta":
            {
                var itemId = prms.TryGetProperty("itemId", out var iid) ? iid.GetString() ?? "" : "";
                var output = prms.TryGetProperty("output", out var o)   ? o.GetString()   ?? "" : "";
                if (output.Length > 0)
                    FileChangeDelta?.Invoke(itemId, output);
                break;
            }

            case "thread/status/changed":
            {
                // active → Thinking, idle → Idle
                if (prms.TryGetProperty("status", out var s) &&
                    s.TryGetProperty("type", out var t))
                {
                    var st = t.GetString();
                    if (st == "idle" && State != CodexState.Idle)
                        SetState(CodexState.Idle);
                }
                break;
            }

            default:
            {
                // 过滤掉高频 delta 类通知和 reasoning
                if (!method.StartsWith("item/agentMessage") &&
                    !method.StartsWith("item/reasoning") &&
                    !method.StartsWith("mcpServer"))
                {
                    _logger.Info($"[NOTIF] {method}");
                }
                break;
            }
        }
    }

    private void SetState(CodexState newState)
    {
        if (State == newState) return;
        State = newState;
        StateChanged?.Invoke(newState);
    }
}
