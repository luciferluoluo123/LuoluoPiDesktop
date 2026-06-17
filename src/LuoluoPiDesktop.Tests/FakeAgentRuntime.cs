using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LuoluoPiDesktop.Core.Models;
using LuoluoPiDesktop.Core.Services;

namespace LuoluoPiDesktop.Tests;

/// <summary>
/// Test double for IAgentRuntime.
/// Lets tests push events into the stream and control state transitions.
/// </summary>
public sealed class FakeAgentRuntime : IAgentRuntime
{
    public string            RuntimeId   { get; init; } = "fake";
    public string            DisplayName { get; init; } = "Fake Agent";
    public AgentRuntimeState State       { get; private set; } = AgentRuntimeState.NotStarted;

    public event Action<AgentRuntimeState>? StateChanged;

    public AgentProjectContext? LastStartedProject { get; private set; }
    public int StopCallCount    { get; private set; }
    public int DisposeCallCount { get; private set; }

    // Controls what SendMessageAsync yields
    public List<AgentEvent> EventsToYield { get; } = [];
    public bool FailOnSend { get; set; }

    public Task StartAsync(AgentProjectContext project, CancellationToken cancellationToken = default)
    {
        LastStartedProject = project;
        SetState(AgentRuntimeState.Idle);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<AgentEvent> SendMessageAsync(
        AgentMessageRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (FailOnSend) throw new InvalidOperationException("FakeAgentRuntime: simulated send failure");

        SetState(AgentRuntimeState.Thinking);

        foreach (var ev in EventsToYield)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return ev;
            await Task.Yield();
        }

        // Always end with TaskCompleted if not already in list
        if (!EventsToYield.Any(e => e is AgentTaskCompleted))
        {
            yield return new AgentTaskCompleted(true, DateTimeOffset.Now);
        }

        SetState(AgentRuntimeState.Idle);
    }

    public Task CancelCurrentTaskAsync(CancellationToken cancellationToken = default)
    {
        SetState(AgentRuntimeState.Idle);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ModelDescriptor>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ModelDescriptor> list =
        [
            new("fake-model", "Fake Model", AgentModelCapabilities.TextGeneration)
        ];
        return Task.FromResult(list);
    }

    public Task SelectModelAsync(string modelId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopCallCount++;
        SetState(AgentRuntimeState.Stopped);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisposeCallCount++;
        return ValueTask.CompletedTask;
    }

    public void SimulateStateChange(AgentRuntimeState state) => SetState(state);

    private void SetState(AgentRuntimeState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(state);
    }
}
