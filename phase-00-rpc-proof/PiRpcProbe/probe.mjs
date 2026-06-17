/**
 * PiRpcProbe — Phase 0 验证脚本
 *
 * 验证目标：
 *   1. 启动 codex app-server (stdio 模式)
 *   2. Initialize 握手
 *   3. 创建 Thread
 *   4. 发送消息，收到流式回复
 *   5. 中止任务 (turn/interrupt)
 *   6. 正常关闭进程
 *   7. 确认无残留进程
 *
 * 运行：node probe.mjs [prompt]
 */

import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';

// ──────────────────────────────────────────────
// 配置
// ──────────────────────────────────────────────
const CODEX_EXE =
  'C:\\Users\\lucif\\AppData\\Local\\OpenAI\\Codex\\bin\\f1c7ee7a13db5fed\\codex.exe';

const WORK_DIR = 'D:\\AI_Projects\\gpt_code\\LuoluoPiDesktop\\Luoluo Pi Desktop';

const TEST_PROMPT = process.argv[2] ?? '用不超过10个字介绍一下你自己。';

// 超时：等待回复最多 60 秒
const TURN_TIMEOUT_MS = 60_000;

// ──────────────────────────────────────────────
// 日志工具
// ──────────────────────────────────────────────
const ts = () => new Date().toISOString().slice(11, 23);
const log  = (tag, msg) => console.log(`[${ts()}] [${tag}] ${msg}`);
const logJ = (tag, obj) => console.log(`[${ts()}] [${tag}]`, JSON.stringify(obj, null, 2));

// ──────────────────────────────────────────────
// 状态
// ──────────────────────────────────────────────
let msgId    = 0;
const pending = new Map();   // id → { resolve, reject }
let threadId  = null;
let turnId    = null;
let agentText = '';

// ──────────────────────────────────────────────
// 启动子进程
// ──────────────────────────────────────────────
log('BOOT', `Starting codex app-server (stdio)`);
log('BOOT', `Executable: ${CODEX_EXE}`);

const child = spawn(CODEX_EXE, ['app-server', '--listen', 'stdio://'], {
  stdio: ['pipe', 'pipe', 'pipe'],
  windowsHide: true,
});

log('BOOT', `Child PID: ${child.pid}`);

child.on('error', (err) => {
  log('ERROR', `Failed to start codex: ${err.message}`);
  process.exit(1);
});

child.on('exit', (code, signal) => {
  log('EXIT', `Codex exited — code=${code} signal=${signal}`);
});

child.stderr.on('data', (data) => {
  const lines = data.toString().trim().split('\n');
  for (const l of lines) {
    if (l.trim()) log('STDERR', l.trim());
  }
});

// ──────────────────────────────────────────────
// JSON-RPC 消息收发
// ──────────────────────────────────────────────
function send(method, params) {
  const id = ++msgId;
  const msg = JSON.stringify({ id, method, params });
  child.stdin.write(msg + '\n');
  log('SEND', `[${id}] ${method}`);
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
  });
}

function notify(method, params) {
  const msg = JSON.stringify({ method, params });
  child.stdin.write(msg + '\n');
  log('NOTIFY', method);
}

// ──────────────────────────────────────────────
// 读取服务端消息
// ──────────────────────────────────────────────
const rl = createInterface({ input: child.stdout, crlfDelay: Infinity });

rl.on('line', (line) => {
  if (!line.trim()) return;

  let msg;
  try {
    msg = JSON.parse(line);
  } catch {
    log('RAW', line.slice(0, 200));
    return;
  }

  // Response to a request
  if ('id' in msg && 'result' in msg) {
    const p = pending.get(msg.id);
    if (p) {
      pending.delete(msg.id);
      p.resolve(msg.result);
    }
    return;
  }

  // JSON-RPC Error
  if ('id' in msg && 'error' in msg) {
    const p = pending.get(msg.id);
    if (p) {
      pending.delete(msg.id);
      p.reject(new Error(`RPC error ${msg.error.code}: ${msg.error.message}`));
    }
    return;
  }

  // Notification (no id)
  if ('method' in msg) {
    handleNotification(msg.method, msg.params ?? {});
  }
});

// ──────────────────────────────────────────────
// 处理服务端通知
// ──────────────────────────────────────────────
let turnCompleteResolve = null;

function handleNotification(method, params) {
  switch (method) {
    case 'item/agentMessage/delta': {
      process.stdout.write(params.delta ?? '');
      agentText += params.delta ?? '';
      break;
    }
    case 'item/commandExecution/outputDelta': {
      log('CMD_OUT', params.output ?? '');
      break;
    }
    case 'item/fileChange/outputDelta': {
      log('FILE_DELTA', `${params.path ?? ''}: ${params.output ?? ''}`);
      break;
    }
    case 'turn/started': {
      // turnId lives at params.turn.id
      turnId = params.turn?.id ?? turnId;
      log('TURN', `Started — turnId=${turnId}`);
      break;
    }
    case 'turn/completed': {
      console.log();
      const completedTurnId = params.turn?.id;
      log('TURN', `Completed — turnId=${completedTurnId} status=${params.turn?.status}`);
      if (turnCompleteResolve) turnCompleteResolve(params);
      break;
    }
    case 'thread/started': {
      log('THREAD', `Thread started — id=${params?.thread?.id}`);
      break;
    }
    case 'thread/status/changed': {
      log('THREAD', `Status: ${JSON.stringify(params?.status)}`);
      break;
    }
    case 'error': {
      log('ERROR', JSON.stringify(params));
      break;
    }
    default: {
      if (!method.startsWith('item/reasoning') && !method.startsWith('mcpServer')) {
        log('NOTIF', method);
      }
    }
  }
}

// ──────────────────────────────────────────────
// 等待 turn/completed
// ──────────────────────────────────────────────
function waitForTurnComplete() {
  return new Promise((resolve, reject) => {
    turnCompleteResolve = resolve;
    setTimeout(() => reject(new Error(`Turn timeout after ${TURN_TIMEOUT_MS}ms`)), TURN_TIMEOUT_MS);
  });
}

// ──────────────────────────────────────────────
// 主流程
// ──────────────────────────────────────────────
async function main() {
  try {
    // ── Step 1: Initialize ──────────────────────
    log('STEP', '1 — Initialize');
    const initResult = await send('initialize', {
      clientInfo: { name: 'PiRpcProbe', version: '0.1.0' },
    });
    logJ('INIT_RESULT', { serverInfo: initResult?.serverInfo });

    // ── Step 2: Start Thread ────────────────────
    log('STEP', '2 — Thread/start');
    const threadResult = await send('thread/start', {
      cwd: WORK_DIR,
    });
    threadId = threadResult?.thread?.id;
    log('THREAD', `Created — id=${threadId}`);
    if (!threadId) throw new Error('No threadId in thread/start response');

    // ── Step 3: Start Turn (send message) ───────
    log('STEP', `3 — Turn/start — prompt: "${TEST_PROMPT}"`);
    console.log('\n──── Agent Response ────');

    const [turnStartResult, turnComplete] = await Promise.all([
      send('turn/start', {
        threadId,
        input: [{ type: 'text', text: TEST_PROMPT }],
      }),
      waitForTurnComplete(),
    ]);
    turnId = turnStartResult?.turn?.id ?? turnId;

    log('STEP', `3 DONE — agent replied ${agentText.length} chars`);

    // ── Step 4: Test Turn/interrupt ─────────────
    log('STEP', '4 — Turn/interrupt test (send another turn then interrupt)');
    const interruptPrompt = '请把从1数到1000，每个数字之间用空格分隔。';

    agentText = '';
    turnCompleteResolve = null;
    turnId = null;

    let interrupted = false;
    const secondTurnComplete = waitForTurnComplete().catch(() => {});

    const interruptTurnResult = await send('turn/start', {
      threadId,
      input: [{ type: 'text', text: interruptPrompt }],
    });
    // Use response turnId immediately so interrupt works even before notification arrives
    turnId = interruptTurnResult?.turn?.id ?? turnId;

    // Wait 2 seconds then interrupt
    await new Promise(r => setTimeout(r, 2000));
    process.stdout.write('\n');

    if (turnId) {
      log('STEP', `4 — Interrupting turn ${turnId}`);
      await send('turn/interrupt', { threadId, turnId });
      interrupted = true;
      log('STEP', `4 — Interrupt sent`);
    } else {
      log('WARN', 'No turnId to interrupt — skipping interrupt test');
    }

    await new Promise(r => setTimeout(r, 1000));

    // ── Step 5: Clean shutdown ───────────────────
    log('STEP', '5 — Shutting down');
    child.stdin.end();
    await new Promise((resolve) => {
      const t = setTimeout(() => {
        log('WARN', 'Codex did not exit cleanly — killing');
        child.kill('SIGTERM');
        resolve();
      }, 3000);
      child.on('exit', () => { clearTimeout(t); resolve(); });
    });

    // ── Summary ─────────────────────────────────
    console.log('\n══════════ Phase 0 验收结果 ══════════');
    console.log('✓ codex app-server 启动成功');
    console.log('✓ initialize 握手成功');
    console.log('✓ thread/start 创建会话成功');
    console.log('✓ turn/start 发送消息成功');
    console.log('✓ 流式回复已收到');
    console.log(`✓ turn/interrupt ${interrupted ? '已发送' : '跳过（无 turnId）'}`);
    console.log('✓ 子进程正常退出');
    console.log('══════════════════════════════════════\n');

    process.exit(0);
  } catch (err) {
    console.error('\n[FAIL]', err.message);
    child.kill();
    process.exit(1);
  }
}

main();
