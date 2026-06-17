using LuoluoPiDesktop.Core.Models;

namespace LuoluoPiDesktop.Tests;

public sealed class AgentEventTests
{
    [Fact]
    public void AgentTextDelta_IsAgentEvent()
    {
        var ev = new AgentTextDelta("hello", DateTimeOffset.Now);
        Assert.IsAssignableFrom<AgentEvent>(ev);
        Assert.Equal("hello", ev.Text);
    }

    [Fact]
    public void AgentToolStarted_PreservesFields()
    {
        var ts = DateTimeOffset.UtcNow;
        var ev = new AgentToolStarted("id-1", "shell", "ls -la", ts);

        Assert.Equal("id-1",   ev.ToolCallId);
        Assert.Equal("shell",  ev.ToolType);
        Assert.Equal("ls -la", ev.ToolName);
        Assert.Equal(ts,       ev.Timestamp);
    }

    [Fact]
    public void AgentToolCompleted_SuccessFlag_ZeroExitCode()
    {
        var ev = new AgentToolCompleted("id-1", ExitCode: 0, Success: true, DateTimeOffset.Now);
        Assert.True(ev.Success);
        Assert.Equal(0, ev.ExitCode);
    }

    [Fact]
    public void AgentToolCompleted_SuccessFlag_NonZeroExitCode()
    {
        var ev = new AgentToolCompleted("id-2", ExitCode: 1, Success: false, DateTimeOffset.Now);
        Assert.False(ev.Success);
    }

    [Fact]
    public void AgentTaskCompleted_PreservesSuccess()
    {
        var success    = new AgentTaskCompleted(true,  DateTimeOffset.Now);
        var interrupted = new AgentTaskCompleted(false, DateTimeOffset.Now);
        Assert.True(success.Success);
        Assert.False(interrupted.Success);
    }

    [Fact]
    public void AgentError_PreservesMessage()
    {
        var ex = new InvalidOperationException("test");
        var ev = new AgentError("user message", ex, DateTimeOffset.Now);
        Assert.Equal("user message", ev.UserMessage);
        Assert.Same(ex, ev.Exception);
    }

    [Fact]
    public void AgentStateChanged_PreservesState()
    {
        var ev = new AgentStateChanged(AgentRuntimeState.Thinking, DateTimeOffset.Now);
        Assert.Equal(AgentRuntimeState.Thinking, ev.State);
    }

    [Fact]
    public void PatternMatch_WorksForAllSubtypes()
    {
        AgentEvent[] events =
        [
            new AgentTextDelta("t", DateTimeOffset.Now),
            new AgentThinkingDelta("r", DateTimeOffset.Now),
            new AgentToolStarted("1", "shell", "cmd", DateTimeOffset.Now),
            new AgentToolUpdated("1", "out", DateTimeOffset.Now),
            new AgentToolCompleted("1", 0, true, DateTimeOffset.Now),
            new AgentStateChanged(AgentRuntimeState.Idle, DateTimeOffset.Now),
            new AgentError("e", null, DateTimeOffset.Now),
            new AgentTaskCompleted(true, DateTimeOffset.Now),
        ];

        // All events should be matchable without exceptions
        foreach (var ev in events)
        {
            var label = ev switch
            {
                AgentTextDelta      => "text",
                AgentThinkingDelta  => "thinking",
                AgentToolStarted    => "toolStarted",
                AgentToolUpdated    => "toolUpdated",
                AgentToolCompleted  => "toolCompleted",
                AgentStateChanged   => "stateChanged",
                AgentError          => "error",
                AgentTaskCompleted  => "taskCompleted",
                _                   => "unknown",
            };
            Assert.NotEqual("unknown", label);
        }
    }
}
