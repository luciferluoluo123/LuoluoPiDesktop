namespace LuoluoPiDesktop.ViewModels;

public sealed class ChatBubbleViewModel : ViewModelBase
{
    public bool IsUser { get; }

    private string _text = string.Empty;
    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    private bool _isStreaming;
    public bool IsStreaming
    {
        get => _isStreaming;
        set => SetField(ref _isStreaming, value);
    }

    public ChatBubbleViewModel(bool isUser, string initialText = "")
    {
        IsUser = isUser;
        _text  = initialText;
    }

    public void AppendText(string delta)
    {
        Text += delta;
    }
}
