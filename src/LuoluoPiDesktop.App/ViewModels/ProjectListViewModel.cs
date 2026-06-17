using System.Collections.ObjectModel;
using System.Windows;
using LuoluoPiDesktop.Core.Models;
using LuoluoPiDesktop.Core.Services;

namespace LuoluoPiDesktop.ViewModels;

public sealed class ProjectListViewModel : ViewModelBase
{
    private readonly IProjectService       _projects;
    private readonly IAppLogger            _logger;
    private readonly Action<ProjectEntry>  _onStartChat;

    // 界面绑定集合（按最后使用时间排序）
    public ObservableCollection<ProjectEntryViewModel> Items { get; } = [];

    private ProjectEntryViewModel? _selected;
    public ProjectEntryViewModel? Selected
    {
        get => _selected;
        set
        {
            SetField(ref _selected, value);
            OnPropertyChanged(nameof(HasSelection));
            StartChatCommand?.RaiseCanExecuteChanged();
        }
    }

    public bool HasSelection => _selected != null;

    // 输入框
    private string _inputPath = string.Empty;
    public string InputPath
    {
        get => _inputPath;
        set
        {
            SetField(ref _inputPath, value);
            AddCommand.RaiseCanExecuteChanged();
        }
    }

    // 重命名输入
    private string _renameText = string.Empty;
    public string RenameText
    {
        get => _renameText;
        set => SetField(ref _renameText, value);
    }

    private bool _isRenaming;
    public bool IsRenaming
    {
        get => _isRenaming;
        set => SetField(ref _isRenaming, value);
    }

    // 状态栏
    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    private bool _isError;
    public bool IsError
    {
        get => _isError;
        set => SetField(ref _isError, value);
    }

    // Commands
    public NotifyRelayCommand AddCommand          { get; }
    public RelayCommand       BrowseCommand       { get; }
    public NotifyRelayCommand RemoveCommand       { get; }
    public NotifyRelayCommand StartRenameCommand  { get; }
    public NotifyRelayCommand ConfirmRenameCommand{ get; }
    public RelayCommand       CancelRenameCommand { get; }
    public RelayCommand       RefreshCommand      { get; }
    public NotifyRelayCommand OpenFolderCommand   { get; }
    public NotifyRelayCommand StartChatCommand    { get; }

    public ProjectListViewModel(IProjectService projects, IAppLogger logger,
                                Action<ProjectEntry> onStartChat)
    {
        _projects    = projects;
        _logger      = logger;
        _onStartChat = onStartChat;

        AddCommand           = new NotifyRelayCommand(ExecuteAdd,          () => !string.IsNullOrWhiteSpace(InputPath));
        BrowseCommand        = new RelayCommand(ExecuteBrowse);
        RemoveCommand        = new NotifyRelayCommand(ExecuteRemove,        () => _selected != null);
        StartRenameCommand   = new NotifyRelayCommand(ExecuteStartRename,   () => _selected != null);
        ConfirmRenameCommand = new NotifyRelayCommand(ExecuteConfirmRename, () => _selected != null && !string.IsNullOrWhiteSpace(RenameText));
        CancelRenameCommand  = new RelayCommand(() => IsRenaming = false);
        RefreshCommand       = new RelayCommand(ExecuteRefresh);
        OpenFolderCommand    = new NotifyRelayCommand(ExecuteOpenFolder,    () => _selected != null);
        StartChatCommand     = new NotifyRelayCommand(ExecuteStartChat,     () => _selected != null && _selected.Entry.PathExists);

        Reload();
    }

    // ── 公开方法：供 MainViewModel 切换时通知 ──────────────────────────

    /// <summary>当前选中项目（供 Phase 3 获取工作目录）</summary>
    public ProjectEntry? ActiveProject => _selected?.Entry;

    // ── 内部方法 ─────────────────────────────────────────────────────────

    private void Reload()
    {
        Items.Clear();
        var sorted = _projects.Projects
            .OrderByDescending(p => p.LastUsedAt)
            .ThenBy(p => p.Name);
        foreach (var p in sorted)
            Items.Add(new ProjectEntryViewModel(p));
    }

    private void ExecuteAdd()
    {
        try
        {
            var entry = _projects.Add(InputPath);
            var vm    = new ProjectEntryViewModel(entry);
            Items.Insert(0, vm);
            Selected      = vm;
            InputPath     = string.Empty;
            SetStatus($"已添加：{entry.Name}", error: false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, error: true);
            _logger.Warn($"Add project failed: {ex.Message}");
        }
    }

    private void ExecuteBrowse()
    {
        // WPF 没有内建文件夹对话框，用 WinForms FolderBrowserDialog
        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description         = "选择项目目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        if (!string.IsNullOrWhiteSpace(InputPath) &&
            System.IO.Directory.Exists(InputPath))
            dlg.InitialDirectory = InputPath;

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            InputPath = dlg.SelectedPath;
    }

    private void ExecuteRemove()
    {
        if (_selected is null) return;

        var result = System.Windows.MessageBox.Show(
            $"从列表中移除项目【{_selected.Name}】？\n（不会删除磁盘上的文件）",
            "确认移除", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question);
        if (result != MessageBoxResult.OK) return;

        try
        {
            _projects.Remove(_selected.Entry.Id);
            Items.Remove(_selected);
            Selected = null;
            SetStatus("项目已移除。", error: false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    private void ExecuteStartRename()
    {
        if (_selected is null) return;
        RenameText = _selected.Name;
        IsRenaming = true;
    }

    private void ExecuteConfirmRename()
    {
        if (_selected is null || string.IsNullOrWhiteSpace(RenameText)) return;
        try
        {
            _projects.Rename(_selected.Entry.Id, RenameText);
            _selected.RefreshFromEntry();
            IsRenaming = false;
            SetStatus($"已重命名为：{RenameText}", error: false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, error: true);
        }
    }

    private void ExecuteRefresh()
    {
        _projects.RefreshAll();
        foreach (var vm in Items)
            vm.RefreshFromEntry();
        SetStatus($"已刷新（{DateTime.Now:HH:mm:ss}）", error: false);
    }

    private void ExecuteStartChat()
    {
        if (_selected is null) return;
        _projects.Touch(_selected.Entry.Id);
        _selected.RefreshFromEntry();
        _onStartChat(_selected.Entry);
    }

    private void ExecuteOpenFolder()
    {
        if (_selected is null) return;
        if (!System.IO.Directory.Exists(_selected.Entry.LocalPath))
        {
            SetStatus("目录不存在，无法打开。", error: true);
            return;
        }
        System.Diagnostics.Process.Start("explorer.exe", _selected.Entry.LocalPath);
    }

    private void SetStatus(string msg, bool error)
    {
        StatusMessage = msg;
        IsError       = error;
    }
}

// ── 单项 ViewModel ────────────────────────────────────────────────────────

public sealed class ProjectEntryViewModel : ViewModelBase
{
    public ProjectEntry Entry { get; }

    public string   Name         => Entry.Name;
    public string   LocalPath    => Entry.LocalPath;
    public bool     PathExists   => Entry.PathExists;
    public bool     IsGitRepo    => Entry.IsGitRepo;
    public bool     HasAgentsMd  => Entry.HasAgentsMd;
    public string   GitBranch    => Entry.GitBranch;
    public DateTime LastUsedAt   => Entry.LastUsedAt;

    public string LastUsedDisplay =>
        Entry.LastUsedAt == DateTime.MinValue ? "从未使用" :
        $"最后使用：{Entry.LastUsedAt:yyyy-MM-dd HH:mm}";

    public string GitBadge =>
        Entry.IsGitRepo ? $"git: {Entry.GitBranch}" : "非 Git 仓库";

    public string AgentsBadge =>
        Entry.HasAgentsMd ? "AGENTS.md ✓" : "无 AGENTS.md";

    public ProjectEntryViewModel(ProjectEntry entry)
    {
        Entry = entry;
    }

    public void RefreshFromEntry()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(PathExists));
        OnPropertyChanged(nameof(IsGitRepo));
        OnPropertyChanged(nameof(HasAgentsMd));
        OnPropertyChanged(nameof(GitBranch));
        OnPropertyChanged(nameof(LastUsedAt));
        OnPropertyChanged(nameof(LastUsedDisplay));
        OnPropertyChanged(nameof(GitBadge));
        OnPropertyChanged(nameof(AgentsBadge));
    }
}
