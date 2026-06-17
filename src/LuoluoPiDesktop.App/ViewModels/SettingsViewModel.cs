using LuoluoPiDesktop.Core.Services;

namespace LuoluoPiDesktop.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IAppLogger _logger;

    public string CodexExePath
    {
        get => _settings.Current.CodexExePath;
        set
        {
            if (_settings.Current.CodexExePath == value) return;
            _settings.Current.CodexExePath = value;
            OnPropertyChanged();
        }
    }

    public string ShellPath
    {
        get => _settings.Current.ShellPath;
        set
        {
            if (_settings.Current.ShellPath == value) return;
            _settings.Current.ShellPath = value;
            OnPropertyChanged();
        }
    }

    public string DefaultModel
    {
        get => _settings.Current.DefaultModel;
        set
        {
            if (_settings.Current.DefaultModel == value) return;
            _settings.Current.DefaultModel = value;
            OnPropertyChanged();
        }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public RelayCommand SaveCommand { get; }

    public SettingsViewModel(ISettingsService settings, IAppLogger logger)
    {
        _settings = settings;
        _logger   = logger;

        SaveCommand = new RelayCommand(SaveSettings);
    }

    private void SaveSettings()
    {
        _settings.Save();
        _logger.Info("Settings saved");
        StatusMessage = $"已保存（{DateTime.Now:HH:mm:ss}）";
    }
}
