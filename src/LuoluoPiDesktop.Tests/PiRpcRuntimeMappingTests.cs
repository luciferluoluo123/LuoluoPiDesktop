using LuoluoPiDesktop.Core.Models;
using LuoluoPiDesktop.Infrastructure.Services;

namespace LuoluoPiDesktop.Tests;

public sealed class PiRpcRuntimeMappingTests
{
    [Theory]
    [InlineData(CodexState.NotStarted,  AgentRuntimeState.NotStarted)]
    [InlineData(CodexState.Starting,    AgentRuntimeState.Starting)]
    [InlineData(CodexState.Idle,        AgentRuntimeState.Idle)]
    [InlineData(CodexState.Thinking,    AgentRuntimeState.Thinking)]
    [InlineData(CodexState.ToolRunning, AgentRuntimeState.ExecutingTool)]
    [InlineData(CodexState.Stopping,    AgentRuntimeState.Stopping)]
    [InlineData(CodexState.Stopped,     AgentRuntimeState.Stopped)]
    [InlineData(CodexState.Error,       AgentRuntimeState.Faulted)]
    public void MapState_TranslatesAllCodexStates(CodexState piState, AgentRuntimeState expected)
    {
        var actual = PiRpcRuntime.MapState(piState);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FakeRuntime_StartsInNotStarted()
    {
        var fake = new FakeAgentRuntime();
        Assert.Equal(AgentRuntimeState.NotStarted, fake.State);
    }

    [Fact]
    public async Task FakeRuntime_StartAsync_TransitionsToIdle()
    {
        var fake = new FakeAgentRuntime();
        await fake.StartAsync(new AgentProjectContext("C:\\proj", "Test"));
        Assert.Equal(AgentRuntimeState.Idle, fake.State);
    }

    [Fact]
    public async Task FakeRuntime_SendMessageAsync_YieldsConfiguredEvents()
    {
        var fake = new FakeAgentRuntime();
        fake.EventsToYield.Add(new AgentTextDelta("Hello", DateTimeOffset.Now));
        fake.EventsToYield.Add(new AgentTaskCompleted(true, DateTimeOffset.Now));

        await fake.StartAsync(new AgentProjectContext("C:\\proj", "Test"));

        var events = new List<AgentEvent>();
        await foreach (var ev in fake.SendMessageAsync(new AgentMessageRequest("hi")))
            events.Add(ev);

        Assert.Contains(events, e => e is AgentTextDelta { Text: "Hello" });
        Assert.Contains(events, e => e is AgentTaskCompleted { Success: true });
    }

    [Fact]
    public async Task FakeRuntime_StateChanged_FiredOnTransition()
    {
        var fake = new FakeAgentRuntime();
        var states = new List<AgentRuntimeState>();
        fake.StateChanged += s => states.Add(s);

        await fake.StartAsync(new AgentProjectContext("C:\\proj", "Test"));

        Assert.Contains(AgentRuntimeState.Idle, states);
    }

    [Fact]
    public void AgentRuntimeFactory_CreateDefault_ReturnsPiRpcRuntime()
    {
        // Smoke test: factory instantiation doesn't throw
        // (we can't actually call Create without a valid exe path,
        //  but we can verify the factory type chain compiles)
        var factory = new AgentRuntimeFactory(new StubSettingsService(), new NullLogger());
        Assert.Contains(PiRpcRuntime.PiRuntimeId, factory.SupportedRuntimeIds);
        Assert.Equal(PiRpcRuntime.PiRuntimeId, factory.DefaultRuntimeId);
    }
}

// ── Test stubs ────────────────────────────────────────────────────────────────

file sealed class NullLogger : LuoluoPiDesktop.Core.Services.IAppLogger
{
    public void Info (string msg)               { }
    public void Warn (string msg)               { }
    public void Error(string msg, Exception? ex){ }
}

file sealed class StubSettingsService : LuoluoPiDesktop.Core.Services.ISettingsService
{
    public LuoluoPiDesktop.Core.Models.AppSettings Current { get; } = new();
    public void Save()   { }
    public void Reload() { }
}
