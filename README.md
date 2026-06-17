# Luoluo Pi Desktop

A Windows desktop GUI client for AI coding agents, built with WPF (.NET 8).

The goal is simple: **bring powerful AI agent CLIs to users who prefer a native desktop interface**. No terminal required. Today that means OpenAI Codex — tomorrow it means Claude Code, local models, and more.

---

## The problem this solves

Tools like [OpenAI Codex CLI](https://github.com/openai/codex) and [Claude Code](https://docs.anthropic.com/en/docs/claude-code) are incredibly capable, but they're terminal-only. If you want to:

- Chat with an AI agent while it reads, writes, and runs code in your project
- See tool calls and file changes in real time
- Switch between projects without restarting everything
- Give non-technical team members access to AI agent workflows

...you're stuck with the terminal. Luoluo Pi Desktop fixes that.

---

## Features

- **Streaming chat UI** — see AI responses and tool outputs appear in real time, rendered as chat bubbles
- **Tool call visualization** — shell commands and file changes are shown inline as they happen, with exit codes
- **Project sidebar** — open and switch between local project folders; each session is sandboxed to its root path
- **Interrupt button** — cancel a running task mid-way instantly
- **Settings panel** — configure agent executable path, shell path, and default model without touching any config file
- **Pluggable agent backend** — the `IAgentRuntime` abstraction means new backends can be added without touching the UI

---

## Supported backends

| Backend | Status | Notes |
|---------|--------|-------|
| OpenAI Codex CLI | ✅ Implemented | Via `app-server` stdio RPC mode |
| Claude Code | 🔜 Planned | `IAgentRuntime` interface ready |
| Local models (Ollama etc.) | 🔜 Planned | |

Adding a new backend means implementing one interface (`IAgentRuntime`) and registering it in `AgentRuntimeFactory` — the UI layer requires zero changes.

---

## Architecture

The project follows a clean three-layer architecture:

```
LuoluoPiDesktop.App/            # WPF UI layer (Views + ViewModels, XAML)
LuoluoPiDesktop.Core/           # Domain: interfaces + models, no dependencies
LuoluoPiDesktop.Infrastructure/ # Implementations: RPC services, file I/O
```

The key abstraction is `IAgentRuntime` in `Core`:

```csharp
public interface IAgentRuntime : IAsyncDisposable
{
    string            RuntimeId   { get; }
    string            DisplayName { get; }
    AgentRuntimeState State       { get; }

    event Action<AgentRuntimeState>? StateChanged;

    Task StartAsync(AgentProjectContext project, CancellationToken ct = default);
    IAsyncEnumerable<AgentEvent> SendMessageAsync(AgentMessageRequest request, CancellationToken ct = default);
    Task CancelCurrentTaskAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ModelDescriptor>> GetModelsAsync(CancellationToken ct = default);
    Task SelectModelAsync(string modelId, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
```

The current Codex implementation (`PiRpcRuntime`) communicates with the Codex subprocess via **NDJSON-RPC 2.0 over stdin/stdout**:

```
codex.exe app-server --listen stdio://
```

No browser, no TCP port — just a clean subprocess pipe. See [`docs/02_Pi-RPC协议验证.md`](docs/02_Pi-RPC协议验证.md) for the full protocol spec and verification report.

---

## Requirements

- Windows 10 or later (x64)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- At least one supported agent CLI installed and authenticated:
  - [OpenAI Codex CLI](https://github.com/openai/codex)

---

## Getting started

### Option A — Download the release

Download `LuoluoPiDesktop.exe` from the [Releases](../../releases) page and run it directly (no installation needed).

### Option B — Build from source

```bash
git clone https://github.com/YOUR_USERNAME/LuoluoPiDesktop.git
cd LuoluoPiDesktop
dotnet build "Luoluo Pi Desktop/src/LuoluoPiDesktop.App/LuoluoPiDesktop.App.csproj"
dotnet run --project "Luoluo Pi Desktop/src/LuoluoPiDesktop.App/LuoluoPiDesktop.App.csproj"
```

### First-time configuration

1. Launch the app and open **Settings**
2. **Codex executable path** — e.g. `C:\Users\<you>\AppData\Local\OpenAI\Codex\bin\<version>\codex.exe`
3. **Shell path** — e.g. `C:\Program Files\Git\bin\bash.exe`
4. **Default model** — e.g. `gpt-5.5`
5. Click **Save**, then open a project folder from the left panel

---

## Project structure

```
Luoluo Pi Desktop/
├── src/
│   ├── LuoluoPiDesktop.App/            # WPF app, Views, ViewModels
│   ├── LuoluoPiDesktop.Core/           # Interfaces, domain models, AgentEvent types
│   ├── LuoluoPiDesktop.Infrastructure/ # CodexRpcService, PiRpcRuntime, AgentRuntimeFactory
│   └── LuoluoPiDesktop.Tests/          # Unit tests
├── docs/
│   ├── 02_Pi-RPC协议验证.md            # Codex RPC protocol spec & verification
│   └── Phase-4.5-Agent-Runtime-抽象.md # Agent runtime abstraction design notes
├── phase-00-rpc-proof/                 # Node.js probe used to validate the RPC protocol
└── samples/
    └── rpc_samples.jsonl               # Real RPC request/response samples
```

---

## Roadmap

- [x] Phase 0 — RPC protocol verification (Codex stdio communication proven)
- [x] Phase 1 — Basic chat with streaming text
- [x] Phase 2 — Tool call visualization (shell commands, file changes)
- [x] Phase 3 — Project management sidebar
- [x] Phase 4 — `IAgentRuntime` abstraction layer
- [ ] Phase 4.5 — Agent runtime refactor & Claude Code backend
- [ ] Phase 5 — Model selector UI, multi-session support
- [ ] Phase 6 — Release packaging & auto-update

---

## Contributing

Pull requests welcome. If you're implementing a new `IAgentRuntime` backend (Claude Code, Ollama, etc.), open an issue first to align on the interface contract.

---

## License

MIT
