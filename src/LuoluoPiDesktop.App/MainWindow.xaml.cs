using System.Windows;
using LuoluoPiDesktop.Core.Models;
using LuoluoPiDesktop.Core.Services;
using LuoluoPiDesktop.ViewModels;

namespace LuoluoPiDesktop;

public partial class MainWindow : Window
{
    private readonly ISettingsService _settings;

    public MainWindow(MainViewModel vm, ISettingsService settings)
    {
        InitializeComponent();
        _settings  = settings;
        DataContext = vm;

        var ws = settings.Current.MainWindowState;
        Left   = ws.Left;
        Top    = ws.Top;
        Width  = ws.Width;
        Height = ws.Height;
    }

    protected override void OnClosed(EventArgs e)
    {
        var ws = _settings.Current.MainWindowState;
        ws.Left   = Left;
        ws.Top    = Top;
        ws.Width  = ActualWidth;
        ws.Height = ActualHeight;
        _settings.Save();
        base.OnClosed(e);
    }
}
