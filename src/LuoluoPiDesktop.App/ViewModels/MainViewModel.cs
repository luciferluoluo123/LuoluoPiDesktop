using LuoluoPiDesktop.Core.Models;
using LuoluoPiDesktop.Core.Services;

namespace LuoluoPiDesktop.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IAppLogger _logger;

    private ViewModelBase _currentPage;
    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => SetField(ref _currentPage, value);
    }

    public ProjectListViewModel ProjectListVM { get; }
    public ChatViewModel        ChatVM        { get; }
    public SettingsViewModel    SettingsVM    { get; }

    public RelayCommand ShowProjectsCommand { get; }
    public RelayCommand ShowChatCommand     { get; }
    public RelayCommand ShowSettingsCommand { get; }

    public MainViewModel(ISettingsService settings, IProjectService projects,
        IAppLogger logger, IAgentRuntimeFactory runtimeFactory)
    {
        _logger = logger;

        ChatVM        = new ChatViewModel(runtimeFactory, logger);
        SettingsVM    = new SettingsViewModel(settings, logger);
        ProjectListVM = new ProjectListViewModel(projects, logger, StartChatWithProject);

        _currentPage = ProjectListVM;

        ShowProjectsCommand = new RelayCommand(() => CurrentPage = ProjectListVM);
        ShowChatCommand     = new RelayCommand(() => CurrentPage = ChatVM);
        ShowSettingsCommand = new RelayCommand(() => CurrentPage = SettingsVM);

        _logger.Info("MainViewModel initialized");
    }

    private void StartChatWithProject(ProjectEntry entry)
    {
        _ = ChatVM.SetProjectAsync(entry);
        CurrentPage = ChatVM;
    }
}
