# Phase 0 — Pi RPC 协议验证报告

**日期：** 2026-06-15
**状态：** PASS

---

## 1. 验证目标

确认 Codex (Pi Agent) 能否被独立程序稳定启动、通信、中止和关闭，
为后续 WPF 桌面客户端提供可靠通信基础。

---

## 2. 环境信息

| 项目 | 值 |
|------|-----|
| Codex 版本 | 0.140.0-alpha.2 |
| Codex 可执行文件 | `C:\Users\lucif\AppData\Local\OpenAI\Codex\bin\f1c7ee7a13db5fed\codex.exe` |
| 默认模型 | gpt-5.5 |
| Shell 路径 | `D:\Program Files\Git\bin\bash.exe` |
| Node.js 版本 | v24.16.0 |
| .NET | 未安装（Phase 1 前需安装 .NET 8） |
| Git | 2.54.0.windows.1 |
| 操作系统 | Windows 10 Pro |

---

## 3. RPC 通信协议

### 3.1 启动命令

```
codex.exe app-server --listen stdio://
```

- 不打开浏览器
- 不监听 TCP 端口
- 通信完全通过 stdin/stdout

### 3.2 消息格式

**换行符分隔的 JSON-RPC 2.0（NDJSON）**

每条消息是一个完整 JSON 对象，以 `\n` 结尾。

```
{"id":1,"method":"initialize","params":{...}}\n
{"id":1,"result":{...}}\n
{"method":"turn/started","params":{...}}\n
```

### 3.3 核心方法序列

```
client → server: initialize
server → client: response(result)

client → server: thread/start { cwd: "..." }
server → client: response { thread: { id, path, cwd, ... } }
server → client: notification thread/started

client → server: turn/start { threadId, input: [{ type: "text", text: "..." }] }
server → client: response { turn: { id, status: "inProgress" } }
server → client: notification turn/started { turn: { id } }
server → client: notification item/agentMessage/delta { delta: "...", turnId, threadId }
  ... (多次)
server → client: notification turn/completed { turn: { id, status: "completed" } }

client → server: turn/interrupt { threadId, turnId }
server → client: notification turn/completed { turn: { id, status: "interrupted" } }
```

### 3.4 关键字段提取规则

| 数据 | 来源 |
|------|------|
| threadId | `thread/start` response → `result.thread.id` |
| turnId | `turn/start` response → `result.turn.id` |
| 流式文本 | `item/agentMessage/delta` notification → `params.delta` |
| 任务完成 | `turn/completed` notification → `params.turn.status === "completed"` |
| 任务中止确认 | `turn/completed` notification → `params.turn.status === "interrupted"` |

### 3.5 会话文件路径

每次 `thread/start` 都会创建本地 JSONL 文件：

```
C:\Users\lucif\.codex\sessions\{YEAR}\{MM}\{DD}\rollout-{datetime}-{id}.jsonl
```

---

## 4. 验证结果

| 验收项 | 结果 |
|--------|------|
| 能启动 Codex RPC | ✓ |
| 不打开浏览器 | ✓ |
| 不监听 Web 端口 | ✓ |
| 能发送消息 | ✓ |
| 能收到完整回复 | ✓ |
| 能收到流式事件（item/agentMessage/delta） | ✓ |
| 能中止当前任务（turn/interrupt） | ✓ |
| 中止后状态为 "interrupted" | ✓ |
| 能正常关闭 Codex 子进程 | ✓ |
| 退出后无残留进程 | ✓ |
| D 盘 Git Bash 路径工作正常 | ✓（settings.json 已配置 D:\Program Files\Git\bin\bash.exe） |
| 中文回复不乱码 | ✓ |

---

## 5. 交付物

- `phase-00-rpc-proof/PiRpcProbe/probe.mjs` — 验证脚本（Node.js）
- `samples/rpc_samples.jsonl` — 真实 RPC 请求/响应样本
- `docs/02_Pi-RPC协议验证.md` — 本文档

---

## 6. 已知问题

1. `initialize` response 的 `result` 为空对象 `{}`，无 `serverInfo`。这是正常现象，握手仍然成功。
2. 流式 `item/agentMessage/delta` 事件较少（模型可能在 `item/completed` 时才一次性输出），对于简短回复无法区分"流式"和"批量"。长回复需要进一步验证真实流式行为。

---

## 7. Phase 1 前置条件

| 条件 | 状态 |
|------|------|
| 安装 .NET 8 SDK | **待完成** |
| 验证 .NET 8 WPF 可编译 | **待完成** |
| 确认 Visual Studio 或 dotnet CLI 可用 | **待完成** |

---

## 8. 结论

**PASS — 建议进入 Phase 1。**

Codex app-server 的 stdio JSON-RPC 协议完全可以被独立程序控制，通信稳定，中止功能正常，无残留进程。
Phase 1 开始前需要先安装 .NET 8 SDK。
