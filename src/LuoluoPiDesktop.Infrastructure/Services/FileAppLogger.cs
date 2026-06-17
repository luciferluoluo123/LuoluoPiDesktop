using System.IO;
using LuoluoPiDesktop.Core.Services;

namespace LuoluoPiDesktop.Infrastructure.Services;

public sealed class FileAppLogger : IAppLogger
{
    private readonly string _logPath;
    private readonly object _lock = new();

    public FileAppLogger()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "LuoluoPiDesktop", "logs");
        Directory.CreateDirectory(dir);
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        _logPath = Path.Combine(dir, $"app-{date}.log");
    }

    public void Info(string message)  => Write("INFO ", message, null);
    public void Warn(string message)  => Write("WARN ", message, null);
    public void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private void Write(string level, string message, Exception? ex)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        var line = $"[{ts}] [{level}] {message}";
        if (ex != null)
            line += Environment.NewLine + ex.ToString();

        lock (_lock)
        {
            try { File.AppendAllText(_logPath, line + Environment.NewLine); }
            catch { /* 日志失败不影响主流程 */ }
        }
    }
}
