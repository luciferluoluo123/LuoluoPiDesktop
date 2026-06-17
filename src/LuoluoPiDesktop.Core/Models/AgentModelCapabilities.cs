namespace LuoluoPiDesktop.Core.Models;

[Flags]
public enum AgentModelCapabilities
{
    None           = 0,
    TextGeneration = 1 << 0,
    ToolUse        = 1 << 1,
    Streaming      = 1 << 2,
    Reasoning      = 1 << 3,
}
