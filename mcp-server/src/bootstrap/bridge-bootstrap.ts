import { spawn } from 'node:child_process';
import * as fs from 'node:fs';
import * as net from 'node:net';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';

export const PIPE_NAME = 'SolidWorksMcpBridge';

const DEFAULT_PROBE_TIMEOUT_MS = 500;
const DEFAULT_STARTUP_TIMEOUT_MS = 15000;
const DEFAULT_RETRY_DELAY_MS = 500;

export interface PipeConnectable {
  readonly connected: boolean;
  connect(): Promise<void>;
}

export interface BridgeBootstrapOptions {
  pipeName?: string;
  probeTimeoutMs?: number;
  startupTimeoutMs?: number;
  retryDelayMs?: number;
  probePipe?: () => Promise<boolean>;
  startBridgeProcess?: () => Promise<void>;
}

interface BridgeLaunchPlan {
  command: string;
  args: string[];
  cwd: string;
}

const BRIDGE_ENV_ROOT = 'SOLIDWORKS_MCP_BRIDGE_ROOT';

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function tryConnectClient(client: PipeConnectable): Promise<boolean> {
  try {
    await client.connect();
    return true;
  } catch {
    return false;
  }
}

function getMcpServerDir(): string {
  const moduleDir = path.dirname(fileURLToPath(import.meta.url));
  return path.resolve(moduleDir, '..', '..');
}

function getConfiguredBridgeRoot(): string | null {
  const configuredRoot = process.env[BRIDGE_ENV_ROOT]?.trim();
  if (!configuredRoot) {
    return null;
  }

  return path.resolve(configuredRoot);
}

function getBundledBridgeDir(): string {
  return path.resolve(getMcpServerDir(), 'vendor', 'bridge');
}

function getBridgeProjectDir(): string {
  return path.resolve(getMcpServerDir(), '..', 'bridge', 'SolidWorksBridge');
}

function getBridgeSearchRoots(): string[] {
  return Array.from(
    new Set([
      getConfiguredBridgeRoot(),
      getBridgeProjectDir(),
      getBundledBridgeDir(),
    ].filter((root): root is string => Boolean(root))),
  );
}

function resolveBridgeLaunchPlanFromRoot(bridgeRoot: string): BridgeLaunchPlan | null {
  const packagedExe = path.join(bridgeRoot, 'SolidWorksBridge.exe');
  if (fs.existsSync(packagedExe)) {
    return {
      command: packagedExe,
      args: [],
      cwd: path.dirname(packagedExe),
    };
  }

  const releaseExe = path.join(
    bridgeRoot,
    'bin',
    'Release',
    'net8.0-windows',
    'win-x64',
    'SolidWorksBridge.exe',
  );

  if (fs.existsSync(releaseExe)) {
    return {
      command: releaseExe,
      args: [],
      cwd: path.dirname(releaseExe),
    };
  }

  const debugExe = path.join(
    bridgeRoot,
    'bin',
    'Debug',
    'net8.0-windows',
    'win-x64',
    'SolidWorksBridge.exe',
  );

  if (fs.existsSync(debugExe)) {
    return {
      command: debugExe,
      args: [],
      cwd: path.dirname(debugExe),
    };
  }

  const projectFile = path.join(bridgeRoot, 'SolidWorksBridge.csproj');
  if (fs.existsSync(projectFile)) {
    return {
      command: 'dotnet',
      args: ['run', '--project', projectFile],
      cwd: path.dirname(projectFile),
    };
  }

  return null;
}

export function resolveBridgeLaunchPlan(): BridgeLaunchPlan {
  const searchRoots = getBridgeSearchRoots();
  for (const bridgeRoot of searchRoots) {
    const plan = resolveBridgeLaunchPlanFromRoot(bridgeRoot);
    if (plan) {
      return plan;
    }
  }

  throw new Error(
    `SolidWorks bridge launch target not found. Checked: ${searchRoots.join(', ')}. Set ${BRIDGE_ENV_ROOT}, run scripts\\deploy-local.bat, or bundle the bridge into vendor\\bridge.`,
  );
}

export function probeNamedPipe(
  pipeName: string,
  timeoutMs = DEFAULT_PROBE_TIMEOUT_MS,
): Promise<boolean> {
  const pipePath = `\\\\.\\pipe\\${pipeName}`;

  return new Promise<boolean>((resolve) => {
    const socket = net.connect(pipePath);
    let settled = false;

    const finish = (connected: boolean): void => {
      if (settled) {
        return;
      }

      settled = true;
      socket.removeAllListeners();
      socket.destroy();
      resolve(connected);
    };

    const timer = setTimeout(() => finish(false), timeoutMs);

    socket.once('connect', () => {
      clearTimeout(timer);
      finish(true);
    });

    socket.once('error', () => {
      clearTimeout(timer);
      finish(false);
    });

    socket.once('close', () => {
      clearTimeout(timer);
      finish(false);
    });
  });
}

export async function startBridgeProcess(): Promise<void> {
  const plan = resolveBridgeLaunchPlan();

  await new Promise<void>((resolve, reject) => {
    const child = spawn(plan.command, plan.args, {
      cwd: plan.cwd,
      detached: true,
      stdio: 'ignore',
      windowsHide: true,
    });

    child.once('error', reject);
    child.once('spawn', () => {
      child.unref();
      resolve();
    });
  });
}

export async function ensureBridgeReady(
  options: BridgeBootstrapOptions = {},
): Promise<void> {
  const pipeName = options.pipeName ?? PIPE_NAME;
  const probePipe = options.probePipe ?? (() => probeNamedPipe(pipeName, options.probeTimeoutMs));
  const startBridge = options.startBridgeProcess ?? startBridgeProcess;
  const startupTimeoutMs = options.startupTimeoutMs ?? DEFAULT_STARTUP_TIMEOUT_MS;
  const retryDelayMs = options.retryDelayMs ?? DEFAULT_RETRY_DELAY_MS;

  if (await probePipe()) {
    return;
  }

  await startBridge();

  const deadline = Date.now() + startupTimeoutMs;
  while (Date.now() < deadline) {
    if (await probePipe()) {
      return;
    }

    await sleep(retryDelayMs);
  }

  throw new Error(
    `Timed out waiting for SolidWorks bridge pipe "${pipeName}" after ${startupTimeoutMs}ms.`,
  );
}

export async function ensurePipeClientReady(
  client: PipeConnectable,
  options: BridgeBootstrapOptions = {},
): Promise<void> {
  if (client.connected) {
    return;
  }

  if (await tryConnectClient(client)) {
    return;
  }

  await ensureBridgeReady(options);

  const startupTimeoutMs = options.startupTimeoutMs ?? DEFAULT_STARTUP_TIMEOUT_MS;
  const retryDelayMs = options.retryDelayMs ?? DEFAULT_RETRY_DELAY_MS;
  const deadline = Date.now() + startupTimeoutMs;

  while (Date.now() < deadline) {
    if (await tryConnectClient(client)) {
      return;
    }

    await sleep(retryDelayMs);
  }

  throw new Error(
    `Timed out connecting MCP client to bridge pipe "${options.pipeName ?? PIPE_NAME}" after ${startupTimeoutMs}ms.`,
  );
}