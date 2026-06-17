using LuoluoPiDesktop.Core.Models;

namespace LuoluoPiDesktop.Core.Services;

/// <summary>
/// Creates IAgentRuntime instances by runtimeId.
/// Each call to Create returns a fresh, not-yet-started runtime.
/// Only "pi-rpc" is supported in Phase 4.5.
/// </summary>
public interface IAgentRuntimeFactory
{
    IReadOnlyList<string> SupportedRuntimeIds { get; }

    /// <summary>The runtimeId to use when no explicit choice is made.</summary>
    string DefaultRuntimeId { get; }

    IAgentRuntime Create(string runtimeId);
    IAgentRuntime CreateDefault() => Create(DefaultRuntimeId);
}
