using System.Windows.Input;

namespace LuoluoPiDesktop.ViewModels;

public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? _) => canExecute?.Invoke() ?? true;
    public void Execute(object? _)    => execute();
}

/// <summary>带手动 RaiseCanExecuteChanged 的 RelayCommand，用于属性驱动场景。</summary>
public sealed class NotifyRelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? _) => canExecute?.Invoke() ?? true;
    public void Execute(object? _)    => execute();

    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T>(Action<T?> execute, Func<T?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? p) => canExecute?.Invoke((T?)p) ?? true;
    public void Execute(object? p)    => execute((T?)p);
}
