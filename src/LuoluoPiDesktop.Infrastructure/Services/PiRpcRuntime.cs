using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LuoluoPiDesktop.Core.Models;
using LuoluoPiDesktop.Core.Services;

namespace LuoluoPiDesktop.Infrastructure.Services;

/// <summary>
/// IAgentRuntime implementation that wraps CodexRpcService.
/// Translates Pi-specific events into the domain AgentEvent hierarchy.
/// One instance per project session; dispose to shut down the Pi subprocess.
/// </summary>
public sealed class PiRpcRuntime : IAgentRuntime
{
    public const string PiRuntimeId = "pi-rpc";

    private readonly string     _codexExePath;
    private readonly IAppLogger _logger;

    private CodexRpcService? _service;

    // Persistent subscriptions stored so we can unsubscribe on dispose
    private Action<CodexState>? _onPiStateChanged;
    private Action<int?>?       _onProcessExited;

    // Active turn channel — written from event handlers, read by SendMessageAsync
    private volatile ChannelWriter<AgentEvent>? _turnChannel;

    // ── IAgentRuntime contract ────────────────────────────────────────────

    public string            RuntimeId   => PiRuntimeId;
    public string            DisplayName => "Pi Agent (Codex RPC)";
    public AgentRuntimeState State       { get; private set; } = AgentRuntimeState.NotStarted;

    public event Action<AgentRuntimeState>? StateChanged;

    public PiRpcRuntime(string codexExePath, IAppLogger logger)
    {
        _codexExePath = codexExePath;
        _logger       = logger;
    }

    // ── StartAsync ────────────────────────────────────────────────────────

    public async Task StartAsync(AgentProjectContext project, CancellationToken cancellationToken = default)
    {
        SetState(AgentRuntimeState.Starting);
        _logger.Info($"PiRpcRuntime: starting — project={project.Name} root={project.RootPath}");

        _onPiStateChanged = piState => SetState(MapState(piState));
        _onProcessExited  = code =>
        {
            _logger.Warn($"PiRpcRuntime: Pi process exited code={code}");
            SetState(AgentRuntimeState.Faulted);

            // If a turn is in progress, signal the consumer so it doesn't hang
            var writer = _turnChannel;
            if (writer is not null)
            {
                _turnChannel = null;
                writer.TryWrite(new AgentError($"Pi 进程意外退出（code={code}）", null, DateTimeOffset.Now));
                writer.TryComplete();
            }
        };

        _service = new CodexRpcService(_codexExePath, _logger);
        _service.StateChanged  += _onPiStateChanged;
        _service.ProcessExited += _onProcessExited;

        await _service.StartAsync(project.RootPath, cancellationToken);
    }

    // ── SendMessageAsync ──────────────────────────────────────────────────

    public async IAsyncEnumerable<AgentEvent> SendMessageAsync(
        AgentMessageRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_service is null)
            throw new InvalidOperationException("PiRpcRuntime: StartAsync must be called first");

        var channel = Channel.CreateUnbounded<AgentEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        // Bridge: Pi events → channel
        void OnTextDelta(string text) =>
            channel.Writer.TryWrite(new AgentTextDelta(text, DateTimeOffset.Now));

        void OnTurnCompleted(bool interrupted)
        {
            _turnChannel = null;
            channel.Writer.TryWrite(new AgentTaskCompleted(!interrupted, DateTimeOffset.Now));
            channel.Writer.TryComplete();
        }

        void OnToolStarted(string itemId, string label) =>
            channel.Writer.TryWrite(new AgentToolStarted(itemId, "shell", label, DateTimeOffset.Now));

        void OnToolOutputDelta(string itemId, string chunk) =>
            channel.Writer.TryWrite(new AgentToolUpdated(itemId, chunk, DateTimeOffset.Now));

        void OnToolCompleted(string itemId, int? exitCode) =>
            channel.Writer.TryWrite(new AgentToolCompleted(itemId, exitCode, exitCode == 0, DateTimeOffset.Now));

        void OnFileStarted(string itemId, string path) =>
            channel.Writer.TryWrite(new AgentToolStarted(itemId, "file",
                string.IsNullOrEmpty(path) ? "文件变更" : path, DateTimeOffset.Now));

        void OnFileChangeDelta(string itemId, string delta) =>
            channel.Writer.TryWrite(new AgentToolUpdated(itemId, delta, DateTimeOffset.Now));

        _service.TextDelta            += OnTextDelta;
        _service.TurnCompleted        += OnTurnCompleted;
        _service.ToolCallStarted      += OnToolStarted;
        _service.ToolCallOutputDelta  += OnToolOutputDelta;
        _service.ToolCallCompleted    += OnToolCompleted;
        _service.FileChangeStarted    += OnFileStarted;
        _service.FileChangeDelta      += OnFileChangeDelta;

        _turnChannel = channel.Writer;

        try
        {
            await _service.SendMessageAsync(request.Content, cancellationToken);

            await foreach (var ev in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                yield return ev;
        }
        finally
        {
            _turnChannel = null;

            _service.TextDelta            -= OnTextDelta;
            _service.TurnCompleted        -= OnTurnCompleted;
            _service.ToolCallStarted      -= OnToolStarted;
            _service.ToolCallOutputDelta  -= OnToolOutputDelta;
            _service.ToolCallCompleted    -= OnToolCompleted;
            _service.FileChangeStarted    -= OnFileStarted;
            _service.FileChangeDelta      -= OnFileChangeDelta;
        }
    }

    // ── CancelCurrentTaskAsync ────────────────────────────────────────────

    public async Task CancelCurrentTaskAsync(CancellationToken cancellationToken = default)
    {
        if (_service is null) return;
        try
        {
            await _service.InterruptAsync();
        }
        catch (Exception ex)
        {
            _logger.Warn($"PiRpcRuntime: interrupt failed — {ex.Message}");
        }
    }

    // ── GetModelsAsync ────────────────────────────────────────────────────

    public Task<IReadOnlyList<ModelDescriptor>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        // Pi RPC has no getModels endpoint; return a static conservative list
        IReadOnlyList<ModelDescriptor> models =
        [
            new("gpt-5.5", "GPT-5.5 (Pi)",
                AgentModelCapabilities.TextGeneration | AgentModelCapabilities.ToolUse |
                AgentModelCapabilities.Streaming | AgentModelCapabilities.Reasoning),
            new("gpt-4o", "GPT-4o (Pi)",
                AgentModelCapabilities.TextGeneration | AgentModelCapabilities.ToolUse |
                AgentModelCapabilities.Streaming),
        ];
        return Task.FromResult(models);
    }

    // ── SelectModelAsync ──────────────────────────────────────────────────

    public Task SelectModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        // Model is chosen per-session via AppSettings.DefaultModel; cannot change mid-session
        _logger.Info($"PiRpcRuntime: SelectModelAsync(modelId={modelId}) — no-op, restart session to apply");
        return Task.CompletedTask;
    }

    // ── StopAsync ─────────────────────────────────────────────────────────

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_service is null) return;
        SetState(AgentRuntimeState.Stopping);
        await _service.StopAsync();
    }

    // ── DisposeAsync ──────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_service is not null)
        {
            if (_onPiStateChanged is not null) _service.StateChanged  -= _onPiStateChanged;
            if (_onProcessExited  is not null) _service.ProcessExited -= _onProcessExited;

            await _service.DisposeAsync();
            _service = null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetState(AgentRuntimeState newState)
    {
        if (State == newState) return;
        State = newState;
        StateChanged?.Invoke(newState);
    }

    public static AgentRuntimeState MapState(CodexState piState) => piState switch
    {
        CodexState.NotStarted  => AgentRuntimeState.NotStarted,
        CodexState.Starting    => AgentRuntimeState.Starting,
        CodexState.Idle        => AgentRuntimeState.Idle,
        CodexState.Thinking    => AgentRuntimeState.Thinking,
        CodexState.ToolRunning => AgentRuntimeState.ExecutingTool,
        CodexState.Stopping    => AgentRuntimeState.Stopping,
        CodexState.Stopped     => AgentRuntimeState.Stopped,
        CodexState.Error       => AgentRuntimeState.Faulted,
        _                      => AgentRuntimeState.Faulted,
    };
}
