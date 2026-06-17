namespace LuoluoPiDesktop.Core.Models;

/// <summary>A user message to send to the active agent turn.</summary>
public sealed record AgentMessageRequest(
    string  Content,
    string? ModelId = null);
