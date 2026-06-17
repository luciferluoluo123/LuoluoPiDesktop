namespace LuoluoPiDesktop.Core.Models;

public sealed class AppSettings
{
    public string CodexExePath { get; set; } =
        @"C:\Users\lucif\AppData\Local\OpenAI\Codex\bin\f1c7ee7a13db5fed\codex.exe";

    public string ShellPath { get; set; } =
        @"D:\Program Files\Git\bin\bash.exe";

    public string DefaultModel { get; set; } = "gpt-5.5";

    public List<ProjectEntry> Projects { get; set; } = [];

    public WindowState MainWindowState { get; set; } = new();
}

public sealed class WindowState
{
    public double Left   { get; set; } = 100;
    public double Top    { get; set; } = 100;
    public double Width  { get; set; } = 1200;
    public double Height { get; set; } = 750;
}
