namespace LuoluoPiDesktop.Core.Models;

public enum CodexState
{
    NotStarted,
    Starting,
    Idle,
    Thinking,      // 模型生成中
    ToolRunning,   // 工具执行中
    Stopping,
    Stopped,
    Error,
}
