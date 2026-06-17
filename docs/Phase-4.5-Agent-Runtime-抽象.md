# Phase 4.5 — Agent Runtime 与模型能力抽象

## 目标

将 `UI → CodexRpcService` 的直接耦合重构为 `UI → IAgentRuntime → PiRpcRuntime → CodexRpcService`，同时保留 Phase 4 的全部功能（流式文本、工具调用卡片、中断）。

---

## 新增文件一览

### Core 契约层（`LuoluoPiDesktop.Core`）

| 文件 | 说明 |
|---|---|
| `Models/AgentRuntimeState.cs` | 统一运行时状态枚举（NotStarted/Starting/Idle/Thinking/ExecutingTool/Stopping/Stopped/Faulted） |
| `Models/AgentEvent.cs` | 事件层次（8 个 record 子类） |
| `Models/AgentProjectContext.cs` | 项目上下文，含强制 RootPath |
| `Models/AgentMessageRequest.cs` | 用户消息请求 |
| `Models/ModelDescriptor.cs` | 模型描述符 |
| `Models/AgentModelCapabilities.cs` | `[Flags]` 能力枚举 |
| `Services/IAgentRuntime.cs` | Runtime 抽象接口（含 `IAsyncDisposable`） |
| `Services/IAgentRuntimeFactory.cs` | 工厂接口（含 `DefaultRuntimeId` + `CreateDefault()`） |

### Infrastructure 实现层（`LuoluoPiDesktop.Infrastructure`）

| 文件 | 说明 |
|---|---|
| `Services/PiRpcRuntime.cs` | 包装 `CodexRpcService`，用 `Channel<AgentEvent>` 将 Pi 事件桥接为 `IAsyncEnumerable<AgentEvent>` |
| `Services/AgentRuntimeFactory.cs` | 工厂实现，从 `ISettingsService.Current.CodexExePath` 读路径 |

### Tests（`LuoluoPiDesktop.Tests`）

| 文件 | 说明 |
|---|---|
| `FakeAgentRuntime.cs` | 测试替身，可预设事件序列 |
| `AgentEventTests.cs` | 事件层次断言（8 个 record 子类） |
| `PiRpcRuntimeMappingTests.cs` | `MapState` 8 路映射 + FakeRuntime 状态机测试 |

---

## 架构变更

### 之前（Phase 4）

```
ChatViewModel
  └─ CodexRpcService  (Infrastructure 类型直接注入)
       └─ 10 个 Pi 专有 Action<> 事件
```

### 之后（Phase 4.5）

```
App.xaml.cs（组合根）
  └─ AgentRuntimeFactory（实现 IAgentRuntimeFactory）
       └─ MainViewModel
            └─ ChatViewModel（依赖 IAgentRuntimeFactory）
                 └─ PiRpcRuntime（实现 IAgentRuntime）
                      └─ CodexRpcService（Pi 专有，封装在 Infrastructure 内部）
```

### ChatViewModel 不再直接依赖 Infrastructure

- 删除：`using LuoluoPiDesktop.Infrastructure.Services;`
- 删除：`private CodexRpcService? _rpc;`
- 新增：`private IAgentRuntime? _runtime;`（通过 `IAgentRuntimeFactory.CreateDefault()` 创建）
- 构造函数：从 `(ISettingsService, IAppLogger)` 改为 `(IAgentRuntimeFactory, IAppLogger)`

---

## AgentEvent 层次

```
AgentEvent (abstract record)
├── AgentTextDelta       — 模型流式文本片段
├── AgentThinkingDelta   — 推理/思维链片段（预留）
├── AgentToolStarted     — 工具调用开始（ToolCallId, ToolType, ToolName）
├── AgentToolUpdated     — 工具输出增量
├── AgentToolCompleted   — 工具完成（ExitCode, Success）
├── AgentStateChanged    — 运行时状态变更（State）
├── AgentError           — 可恢复错误（UserMessage, Exception?）
└── AgentTaskCompleted   — 本轮结束（Success: false = 被中断）
```

---

## PiRpcRuntime 实现要点

- `Channel<AgentEvent>` 作为桥接器：Pi 事件处理器写入 channel，`SendMessageAsync` 读取 channel 并 `yield return`
- `_turnChannel` 字段（volatile）：允许 `OnProcessExited` 在 Pi 进程意外退出时向通道写入 `AgentError` 并关闭通道，防止消费端永久挂起
- 持久化订阅（StartAsync 内）：`StateChanged` + `ProcessExited`，dispose 时解绑
- 临时订阅（SendMessageAsync 内）：`TextDelta/TurnCompleted/ToolCall*/FileChange*`，finally 块解绑
- `MapState` 将 `CodexState`（Pi 专有）映射到 `AgentRuntimeState`（领域通用）

---

## XAML 变更

`ChatView.xaml` DataTrigger 绑定路径更新：

| 之前 | 之后 |
|---|---|
| `{Binding CodexState}` | `{Binding RuntimeState}` |
| Value="ToolRunning" | Value="ExecutingTool" |
| Value="Error" | Value="Faulted" |

---

## 测试结果

```
测试总数: 22
通过数:   22
失败数:    0
总时间:  2.84 秒
```

---

## 禁止事项（Phase 4.5 规范约束）

- 禁止：ViewModel 直接 `new PiRpcRuntime()` 或引用 Pi DTO/JSON 类型
- 禁止：接入第二个 Agent（PiRpcRuntime 是唯一实现）
- 禁止：修改 CodexRpcService 或 Pi Agent 协议
- 禁止：开发 Phase 5-10 功能
