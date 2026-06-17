namespace LuoluoPiDesktop.ViewModels;

public sealed class ToolBubbleViewModel : ViewModelBase
{
    // ── 标识 ──────────────────────────────────────────────────────
    public string ItemId   { get; }
    public string ToolType { get; }   // "shell" | "file"

    // ── 标题行 ────────────────────────────────────────────────────
    private string _label = string.Empty;
    public string Label
    {
        get => _label;
        set => SetField(ref _label, value);
    }

    // ── 输出内容 ──────────────────────────────────────────────────
    private string _output = string.Empty;
    public string Output
    {
        get => _output;
        set => SetField(ref _output, value);
    }

    // ── 状态 ─────────────────────────────────────────────────────
    private bool _isRunning = true;
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            SetField(ref _isRunning, value);
            OnPropertyChanged(nameof(ExitBadge));
        }
    }

    private int? _exitCode;
    public int? ExitCode
    {
        get => _exitCode;
        private set
        {
            SetField(ref _exitCode, value);
            OnPropertyChanged(nameof(ExitBadge));
            OnPropertyChanged(nameof(IsSuccess));
        }
    }

    public bool   IsSuccess => _exitCode == 0;
    public string ExitBadge => _isRunning
        ? "运行中…"
        : (_exitCode is null ? "完成" : $"exit {_exitCode}");

    // ── 展开/折叠 ─────────────────────────────────────────────────
    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public RelayCommand ToggleExpandCommand { get; }

    public ToolBubbleViewModel(string itemId, string toolType, string label)
    {
        ItemId   = itemId;
        ToolType = toolType;
        Label    = label;
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    public void AppendOutput(string chunk)
    {
        Output += chunk;
    }

    public void Complete(int? exitCode)
    {
        ExitCode  = exitCode;
        IsRunning = false;
    }
}
