using LuoluoPiDesktop.Core.Models;
using LuoluoPiDesktop.Core.Services;

namespace LuoluoPiDesktop.Infrastructure.Services;

/// <summary>
/// Creates IAgentRuntime instances. Currently only "pi-rpc" is supported.
/// Reads CodexExePath from ISettingsService at Create() time so settings changes
/// are picked up on the next project switch without restarting the app.
/// </summary>
public sealed class AgentRuntimeFactory : IAgentRuntimeFactory
{
    private readonly ISettingsService _settings;
    private readonly IAppLogger       _logger;

    public IReadOnlyList<string> SupportedRuntimeIds { get; } = [PiRpcRuntime.PiRuntimeId];
    public string DefaultRuntimeId => PiRpcRuntime.PiRuntimeId;

    public AgentRuntimeFactory(ISettingsService settings, IAppLogger logger)
    {
        _settings = settings;
        _logger   = logger;
    }

    public IAgentRuntime Create(string runtimeId)
    {
        if (runtimeId == PiRpcRuntime.PiRuntimeId)
            return new PiRpcRuntime(_settings.Current.CodexExePath, _logger);

        throw new NotSupportedException(
            $"Runtime '{runtimeId}' is not supported. Supported: {string.Join(", ", SupportedRuntimeIds)}");
    }
}
