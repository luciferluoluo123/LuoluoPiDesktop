namespace LuoluoPiDesktop.Core.Models;

public sealed record ModelDescriptor(
    string                 ModelId,
    string                 DisplayName,
    AgentModelCapabilities Capabilities);
