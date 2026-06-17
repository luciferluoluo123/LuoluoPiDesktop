using System.Collections.ObjectModel;
using LuoluoPiDesktop.Core.Models;
using LuoluoPiDesktop.Core.Services;
using WpfApplication = System.Windows.Application;

namespace LuoluoPiDesktop.ViewModels;

public sealed class ChatViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly IAgentRuntimeFactory _runtimeFactory;
    private readonly IAppLogger           _logger;

    private IAgentRuntime?        _runtime;
    private ChatBubbleViewModel?  _streamingBubble;

    // ── 绑定集合（ChatBubbleViewModel | ToolBubbleViewModel）─────────
    public ObservableCollection<object> Bubbles { get; } = [];

    private readonly Dictionary<string, ToolBubbleViewModel> _activeTools = new();

    // ── 供 View 代码后台订阅，实现自动滚动 ───────────────────────────
    public event Action? ScrollRequested;

    // ── 运行时状态 ────────────────────────────────────────────────────
    private AgentRuntimeState _runtimeState = AgentRuntimeState.NotStarted;
    public AgentRuntimeState RuntimeState
    {
        get => _runtimeState;
        private set
        {
            if (!SetField(ref _runtimeState, value)) return;
            OnPropertyChanged(nameof(StateLabel));
            OnPropertyChanged(nameof(IsReady));
            OnPropertyChanged(nameof(IsBusy));
            SendCommand.RaiseCanExecuteChanged();
            InterruptCommand.RaiseCanExecuteChanged();
        }
    }

    public string StateLabel => _runtimeState switch
    {
        AgentRuntimeState.NotStarted             => "未连接",
        AgentRuntimeState.Starting               => "正在启动…",
        AgentRuntimeState.Idle                   => "就绪",
        AgentRuntimeState.Thinking               => "思考中…",
        AgentRuntimeState.ExecutingTool          => "工具执行中…",
        AgentRuntimeState.WaitingForConfirmation => "等待确认…",
        AgentRuntimeState.Stopping               => "正在停止…",
        AgentRuntimeState.Stopped                => "已断开",
        AgentRuntimeState.Faulted                => "错误",
        _                                        => "未知",
    };

    public bool IsReady => _runtimeState == AgentRuntimeState.Idle;
    public bool IsBusy  => _runtimeState is AgentRuntimeState.Thinking or AgentRuntimeState.ExecutingTool;

    // ── 当前项目名 ────────────────────────────────────────────────────
    private string _projectName = "（未选择项目）";
    public string ProjectName
    {
        get => _projectName;
        private set => SetField(ref _projectName, value);
    }

    // ── 输入框 ────────────────────────────────────────────────────────
    private string _inputText = string.Empty;
    public string InputText
    {
        get => _inputText;
        set
        {
            SetField(ref _inputText, value);
            SendCommand.RaiseCanExecuteChanged();
        }
    }

    // ── Commands ──────────────────────────────────────────────────────
    public NotifyRelayCommand SendCommand      { get; }
    public NotifyRelayCommand InterruptCommand { get; }

    public ChatViewModel(IAgentRuntimeFactory runtimeFactory, IAppLogger logger)
    {
        _runtimeFactory = runtimeFactory;
        _logger         = logger;

        SendCommand = new NotifyRelayCommand(
            () => _ = ExecuteSendAsync(),
            () => IsReady && !string.IsNullOrWhiteSpace(InputText));

        InterruptCommand = new NotifyRelayCommand(
            () => _ = ExecuteInterruptAsync(),
            () => IsBusy);
    }

    // ── 切换项目（MainViewModel 调用）────────────────────────────────

    public async Task SetProjectAsync(ProjectEntry entry)
    {
        await DisposeRuntimeAsync();

        Bubbles.Clear();
        ProjectName  = entry.Name;
        RuntimeState = AgentRuntimeState.Starting;

        _runtime = _runtimeFactory.CreateDefault();
        _runtime.StateChanged += OnRuntimeStateChanged;

        try
        {
            var context = new AgentProjectContext(entry.LocalPath, entry.Name);
            await _runtime.StartAsync(context);
            AddSystemMessage($"已连接项目：{entry.Name}");
            ScrollRequested?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.Error("ChatViewModel: StartAsync failed", ex);
            AddSystemMessage($"启动失败：{ex.Message}");
            ScrollRequested?.Invoke();
        }
    }

    // ── 发送 ─────────────────────────────────────────────────────────

    private async Task ExecuteSendAsync()
    {
        if (_runtime is null || !IsReady || string.IsNullOrWhiteSpace(InputText)) return;

        var text = InputText;
        InputText = string.Empty;
        UiInvoke(() =>
        {
            Bubbles.Add(new ChatBubbleViewModel(isUser: true, text));
            ScrollRequested?.Invoke();
        });

        var request = new AgentMessageRequest(text);

        try
        {
            await foreach (var ev in _runtime.SendMessageAsync(request))
                DispatchEvent(ev);
        }
        catch (Exception ex)
        {
            _logger.Error("ChatViewModel: SendMessageAsync failed", ex);
            UiInvoke(() =>
            {
                AddSystemMessage($"发送失败：{ex.Message}");
                ScrollRequested?.Invoke();
            });
        }
    }

    // ── 中止 ─────────────────────────────────────────────────────────

    private async Task ExecuteInterruptAsync()
    {
        if (_runtime is null) return;
        try
        {
            await _runtime.CancelCurrentTaskAsync();
        }
        catch (Exception ex)
        {
            _logger.Warn($"ChatViewModel: interrupt failed — {ex.Message}");
        }
    }

    // ── 事件分发 ──────────────────────────────────────────────────────

    private void DispatchEvent(AgentEvent ev)
    {
        switch (ev)
        {
            case AgentTextDelta td:
                UiInvoke(() =>
                {
                    if (_streamingBubble is null)
                    {
                        _streamingBubble = new ChatBubbleViewModel(isUser: false) { IsStreaming = true };
                        Bubbles.Add(_streamingBubble);
                    }
                    _streamingBubble.AppendText(td.Text);
                    ScrollRequested?.Invoke();
                });
                break;

            case AgentToolStarted ts:
                UiInvoke(() =>
                {
                    var bubble = new ToolBubbleViewModel(ts.ToolCallId, ts.ToolType, ts.ToolName);
                    _activeTools[ts.ToolCallId] = bubble;
                    Bubbles.Add(bubble);
                    ScrollRequested?.Invoke();
                });
                break;

            case AgentToolUpdated tu:
                UiInvoke(() =>
                {
                    if (tu.Output is not null && _activeTools.TryGetValue(tu.ToolCallId, out var b))
                    {
                        b.AppendOutput(tu.Output);
                        ScrollRequested?.Invoke();
                    }
                });
                break;

            case AgentToolCompleted tc:
                UiInvoke(() =>
                {
                    if (_activeTools.TryGetValue(tc.ToolCallId, out var b))
                    {
                        b.Complete(tc.ExitCode);
                        _activeTools.Remove(tc.ToolCallId);
                    }
                });
                break;

            case AgentStateChanged sc:
                UiInvoke(() => RuntimeState = sc.State);
                break;

            case AgentError ae:
                UiInvoke(() =>
                {
                    AddSystemMessage($"错误：{ae.UserMessage}");
                    ScrollRequested?.Invoke();
                });
                break;

            case AgentTaskCompleted atc:
                UiInvoke(() =>
                {
                    if (_streamingBubble is not null)
                    {
                        _streamingBubble.IsStreaming = false;
                        _streamingBubble = null;
                    }
                    if (!atc.Success)
                        AddSystemMessage("（已中断）");
                    ScrollRequested?.Invoke();
                });
                break;
        }
    }

    // ── 内部工具 ──────────────────────────────────────────────────────

    private void OnRuntimeStateChanged(AgentRuntimeState state)
    {
        UiInvoke(() => RuntimeState = state);
    }

    private void AddSystemMessage(string text)
    {
        Bubbles.Add(new ChatBubbleViewModel(isUser: false, $"【系统】{text}"));
    }

    private static void UiInvoke(Action action)
    {
        var dispatcher = WpfApplication.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }

    private async Task DisposeRuntimeAsync()
    {
        if (_runtime is not null)
        {
            _runtime.StateChanged -= OnRuntimeStateChanged;
            await _runtime.DisposeAsync();
            _runtime = null;
        }
        _streamingBubble = null;
        _activeTools.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeRuntimeAsync();
    }
}
