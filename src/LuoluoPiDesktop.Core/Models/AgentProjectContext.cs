namespace LuoluoPiDesktop.Core.Models;

/// <summary>
/// Immutable context passed to IAgentRuntime.StartAsync.
/// RootPath must be an explicit project directory — runtime must not expand beyond it.
/// </summary>
public sealed record AgentProjectContext(
    string                              RootPath,
    string                              Name,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null);
