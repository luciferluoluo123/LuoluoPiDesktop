using System.Windows;
using System.Windows.Threading;
using LuoluoPiDesktop.Core.Services;
using LuoluoPiDesktop.Infrastructure.Services;
using LuoluoPiDesktop.ViewModels;
using WpfApplication = System.Windows.Application;

namespace LuoluoPiDesktop;

public partial class App : WpfApplication
{
    private IAppLogger?       _logger;
    private ISettingsService? _settings;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _logger   = new FileAppLogger();
        _settings = new SettingsService();

        _logger.Info("Application starting");

        DispatcherUnhandledException               += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;

        var projects = new ProjectService(_settings, _logger);
        projects.RefreshAll();

        var runtimeFactory = new AgentRuntimeFactory(_settings, _logger);
        var vm  = new MainViewModel(_settings, projects, _logger, runtimeFactory);
        var win = new MainWindow(vm, _settings);
        win.Show();

        _logger.Info("Main window shown");
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error("Unhandled UI exception", e.Exception);
        System.Windows.MessageBox.Show(
            $"发生未处理的错误：\n\n{e.Exception.Message}\n\n详情已写入日志。",
            "Luoluo Pi Desktop — 错误",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            _logger?.Error("Unhandled domain exception", ex);
    }
}
