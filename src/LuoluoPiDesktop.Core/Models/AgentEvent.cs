namespace LuoluoPiDesktop.Core.Models;

public abstract record AgentEvent(DateTimeOffset Timestamp);

/// <summary>Model text streaming delta.</summary>
public sealed record AgentTextDelta(
    string         Text,
    DateTimeOffset Timestamp) : AgentEvent(Timestamp);

/// <summary>Reasoning / thinking delta (hidden chain-of-thought).</summary>
public sealed record AgentThinkingDelta(
    string         Text,
    DateTimeOffset Timestamp) : AgentEvent(Timestamp);

/// <summary>A tool invocation has been started.</summary>
public sealed record AgentToolStarted(
    string         ToolCallId,
    string         ToolType,    // "shell" | "file"
    string         ToolName,    // display label: command string or file path
    DateTimeOffset Timestamp) : AgentEvent(Timestamp);

/// <summary>Incremental output from a running tool.</summary>
public sealed record AgentToolUpdated(
    string         ToolCallId,
    string?        Output,
    DateTimeOffset Timestamp) : AgentEvent(Timestamp);

/// <summary>A tool invocation has finished.</summary>
public sealed record AgentToolCompleted(
    string         ToolCallId,
    int?           ExitCode,
    bool           Success,
    DateTimeOffset Timestamp) : AgentEvent(Timestamp);

/// <summary>Runtime state changed (emitted alongside IAgentRuntime.StateChanged event).</summary>
public sealed record AgentStateChanged(
    AgentRuntimeState State,
    DateTimeOffset    Timestamp) : AgentEvent(Timestamp);

/// <summary>A recoverable error occurred during the current turn.</summary>
public sealed record AgentError(
    string         UserMessage,
    Exception?     Exception,
    DateTimeOffset Timestamp) : AgentEvent(Timestamp);

/// <summary>Current turn (task) has completed.</summary>
public sealed record AgentTaskCompleted(
    bool           Success,     // false = interrupted or errored
    DateTimeOffset Timestamp) : AgentEvent(Timestamp);
