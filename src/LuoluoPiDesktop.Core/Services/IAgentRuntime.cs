using LuoluoPiDesktop.Core.Models;

namespace LuoluoPiDesktop.Core.Services;

/// <summary>
/// Abstraction for any AI agent backend.
/// All implementations must:
///   - be scoped to one project session (StartAsync → dispose cycle)
///   - enforce RootPath boundary — never read/write beyond the provided project context
///   - dispatch StateChanged on the thread that detects the state change
/// </summary>
public interface IAgentRuntime : IAsyncDisposable
{
    string            RuntimeId   { get; }
    string            DisplayName { get; }
    AgentRuntimeState State       { get; }

    /// <summary>Fired on background thread whenever State changes.</summary>
    event Action<AgentRuntimeState>? StateChanged;

    Task StartAsync(AgentProjectContext project, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a user message and stream back agent events until the turn completes.
    /// The sequence always ends with <see cref="AgentTaskCompleted"/>.
    /// </summary>
    IAsyncEnumerable<AgentEvent> SendMessageAsync(AgentMessageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Request early cancellation of the current turn (interrupt).</summary>
    Task CancelCurrentTaskAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModelDescriptor>> GetModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>Select the model for the next turn. May be a no-op if the backend doesn't support mid-session switching.</summary>
    Task SelectModelAsync(string modelId, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
