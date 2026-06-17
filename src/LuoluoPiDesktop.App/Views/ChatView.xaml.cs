using LuoluoPiDesktop.ViewModels;
using WpfKey          = System.Windows.Input.Key;
using WpfKeyboard     = System.Windows.Input.Keyboard;
using WpfModifiers    = System.Windows.Input.ModifierKeys;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfUserControl  = System.Windows.Controls.UserControl;

namespace LuoluoPiDesktop.Views;

public partial class ChatView : WpfUserControl
{
    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender,
        System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ChatViewModel old)
            old.ScrollRequested -= ScrollToEnd;
        if (e.NewValue is ChatViewModel vm)
            vm.ScrollRequested += ScrollToEnd;
    }

    private void ScrollToEnd()
    {
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            () => ChatScrollViewer.ScrollToEnd());
    }

    private void OnInputKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == WpfKey.Enter && WpfKeyboard.Modifiers == WpfModifiers.None &&
            DataContext is ChatViewModel vm)
        {
            if (vm.SendCommand.CanExecute(null))
                vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }
}
