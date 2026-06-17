using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace LuoluoPiDesktop.Views;

public partial class ProjectListView : WpfUserControl
{
    public ProjectListView() => InitializeComponent();
}

/// <summary>true → Visible, false → Collapsed</summary>
public sealed class BoolToVisibleConverter : IValueConverter
{
    public static readonly BoolToVisibleConverter Instance = new();
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>true → Collapsed, false → Visible（目录存在时隐藏警告）</summary>
public sealed class BoolToCollapsedConverter : IValueConverter
{
    public static readonly BoolToCollapsedConverter Instance = new();
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
