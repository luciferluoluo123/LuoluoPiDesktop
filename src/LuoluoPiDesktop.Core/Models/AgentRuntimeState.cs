namespace LuoluoPiDesktop.Core.Models;

public enum AgentRuntimeState
{
    NotStarted,
    Starting,
    Idle,
    Thinking,               // model generating text
    ExecutingTool,          // tool execution in progress
    WaitingForConfirmation, // reserved: approval flow
    Stopping,
    Stopped,
    Faulted,
}
