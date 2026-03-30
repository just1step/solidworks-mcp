import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { afterAll, beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';

const execFileAsync = promisify(execFile);

const repoRoot = 'D:/WorkSpace/solidworks-mcp-server';
const nodeExe = 'C:/Program Files/nodejs/node.exe';
const nodeDir = 'C:/Program Files/nodejs';
const npmCli = 'C:/Program Files/nodejs/node_modules/npm/bin/npm-cli.js';
const helperProject = `${repoRoot}/bridge/SolidWorksBridge.AcceptanceHelper/SolidWorksBridge.AcceptanceHelper.csproj`;
const helperExe = `${repoRoot}/bridge/SolidWorksBridge.AcceptanceHelper/bin/Release/net8.0-windows/win-x64/SolidWorksBridge.AcceptanceHelper.exe`;
const processEnv = {
  ...process.env,
  PATH: `${nodeDir};${process.env.PATH ?? ''}`,
};

const expectedTools = [
  'sw_connect',
  'sw_disconnect',
  'sw_new_document',
  'sw_open_document',
  'sw_close_document',
  'sw_save_document',
  'sw_list_documents',
  'sw_get_active_document',
  'sw_select_by_name',
  'sw_list_entities',
  'sw_select_entity',
  'sw_clear_selection',
  'sw_insert_sketch',
  'sw_finish_sketch',
  'sw_add_line',
  'sw_add_circle',
  'sw_add_rectangle',
  'sw_add_arc',
  'sw_extrude',
  'sw_extrude_cut',
  'sw_revolve',
  'sw_fillet',
  'sw_chamfer',
  'sw_shell',
  'sw_simple_hole',
  'sw_insert_component',
  'sw_add_mate_coincident',
  'sw_add_mate_concentric',
  'sw_add_mate_parallel',
  'sw_add_mate_distance',
  'sw_add_mate_angle',
  'sw_list_components',
] as const;

describe.sequential('SolidWorks MCP acceptance', () => {
  let transport: StdioClientTransport;
  let client: Client;
  let serverStderr = '';

  beforeAll(async () => {
    await execFileAsync('powershell.exe', [
      '-NoProfile',
      '-Command',
      "$processes = Get-Process SolidWorksBridge -ErrorAction SilentlyContinue; if ($processes) { $processes | Stop-Process -Force }; exit 0",
    ], { cwd: repoRoot, env: processEnv });
    await execFileAsync('dotnet', ['build', `${repoRoot}/bridge/SolidWorksBridge.sln`, '-c', 'Release'], { cwd: repoRoot, env: processEnv });
    await execFileAsync('dotnet', ['build', helperProject, '-c', 'Release'], { cwd: repoRoot, env: processEnv });
    await execFileAsync(nodeExe, [npmCli, 'run', 'build'], { cwd: `${repoRoot}/mcp-server`, env: processEnv });

    transport = new StdioClientTransport({
      command: nodeExe,
      args: [`${repoRoot}/mcp-server/dist/index.js`],
      cwd: `${repoRoot}/mcp-server`,
      stderr: 'pipe',
    });

    if (transport.stderr) {
      transport.stderr.on('data', (chunk) => {
        serverStderr += chunk.toString();
      });
    }

    client = new Client({ name: 'solidworks-mcp-acceptance', version: '1.0.0' }, { capabilities: {} });
    await client.connect(transport);
  });

  afterAll(async () => {
    if (transport) {
      await transport.close();
    }
  });

  beforeEach(async () => {
    serverStderr = '';
    await runHelper('reset-session');
    await callJsonTool('sw_connect', {});
  });

  test('lists the full tool catalog', async () => {
    const result = await client.listTools();
    const toolNames = result.tools.map((tool) => tool.name).sort();
    expect(toolNames).toEqual([...expectedTools].sort());
  });

  test('accepts document lifecycle tools through MCP', async () => {
    const created = await callJsonTool('sw_new_document', { type: 'Part' });
    expect(created.type).toBe(1);

    const activeAfterCreate = await callJsonTool('sw_get_active_document', {});
    expect(activeAfterCreate.title).toBeTruthy();

    const listed = await callJsonTool('sw_list_documents', {});
    expect(Array.isArray(listed)).toBe(true);
    expect(listed.length).toBeGreaterThanOrEqual(1);

    const savedPart = await runHelper<{ path: string }>('create-saved-part');
    const opened = await callJsonTool('sw_open_document', { path: savedPart.path });
    expect(opened.path.toLowerCase()).toBe(savedPart.path.toLowerCase());

    const saved = await callJsonTool('sw_save_document', { path: savedPart.path });
    expect(saved.saved).toBe(true);

    const closed = await callJsonTool('sw_close_document', { path: savedPart.path });
    expect(closed.closed).toBe(true);
  });

  test('accepts connect and disconnect semantics through MCP', async () => {
    const disconnected = await callJsonTool('sw_disconnect', {});
    expect(disconnected.connected).toBe(false);

    const listResult = await client.callTool({ name: 'sw_list_documents', arguments: {} });
    expect(listResult.isError).toBe(true);

    const reconnected = await callJsonTool('sw_connect', {});
    expect(reconnected.connected).toBe(true);
  });

  test('accepts selection and sketch tools through MCP', async () => {
    await callJsonTool('sw_new_document', { type: 'Part' });

    const selected = await callJsonTool('sw_select_by_name', { name: '前视基准面', selType: 'PLANE' })
      .catch(() => callJsonTool('sw_select_by_name', { name: 'Front Plane', selType: 'PLANE' }));
    expect(selected.success).toBe(true);

    const insert = await callJsonTool('sw_insert_sketch', {});
    expect(insert.ok).toBe(true);

    const line = await callJsonTool('sw_add_line', { x1: 0, y1: 0, x2: 0.05, y2: 0 });
    expect(line.type).toBe('Line');

    const circle = await callJsonTool('sw_add_circle', { cx: 0.02, cy: 0.02, radius: 0.005 });
    expect(circle.type).toBe('Circle');

    const rectangle = await callJsonTool('sw_add_rectangle', { x1: -0.02, y1: -0.01, x2: 0.02, y2: 0.01 });
    expect(rectangle.type).toBe('Rectangle');

    const arc = await callJsonTool('sw_add_arc', { cx: 0, cy: 0, x1: 0.02, y1: 0, x2: 0, y2: 0.02, direction: 1 });
    expect(arc.type).toBe('Arc');

    const finished = await callJsonTool('sw_finish_sketch', {});
    expect(finished.ok).toBe(true);

    const cleared = await callJsonTool('sw_clear_selection', {});
    expect(cleared.cleared).toBe(true);
  });

  test('accepts extrude and extrude_cut through MCP', async () => {
    await callJsonTool('sw_new_document', { type: 'Part' });
    await selectFrontPlane();
    await callJsonTool('sw_insert_sketch', {});
    await callJsonTool('sw_add_rectangle', { x1: -0.03, y1: -0.02, x2: 0.03, y2: 0.02 });
    const extrude = await callJsonTool('sw_extrude', { depth: 0.01 });
    expect(extrude.type).toBe('Extrude');

    await selectFrontPlane();
    await callJsonTool('sw_insert_sketch', {});
    await callJsonTool('sw_add_circle', { cx: 0, cy: 0, radius: 0.005 });
    const cut = await callJsonTool('sw_extrude_cut', { depth: 0.05, endCondition: 'ThroughAll' });
    expect(cut.type).toBe('ExtrudeCut');
  });

  test('accepts list_entities and select_entity through MCP', async () => {
    await callJsonTool('sw_new_document', { type: 'Part' });
    await selectFrontPlane();
    await callJsonTool('sw_insert_sketch', {});
    await callJsonTool('sw_add_rectangle', { x1: -0.02, y1: -0.015, x2: 0.02, y2: 0.015 });
    await callJsonTool('sw_extrude', { depth: 0.01 });

    const faces = await callJsonTool<Array<{ index: number; entityType: string; box: number[] | null }>>('sw_list_entities', {
      entityType: 'Face',
    });
    expect(faces.length).toBeGreaterThan(0);
    expect(faces[0].entityType).toBe('Face');

    const selected = await callJsonTool<{ success: boolean; message: string }>('sw_select_entity', {
      entityType: 'Face',
      index: faces[0].index,
      append: false,
      mark: 0,
    });
    expect(selected.success).toBe(true);
  });

  test('accepts revolve through MCP after helper preparation', async () => {
    await runHelper('prepare-revolve');
    const revolve = await callJsonTool('sw_revolve', { angleDegrees: 360 });
    expect(revolve.type).toBe('Revolve');
  });

  test('accepts fillet through MCP after helper preparation', async () => {
    await runHelper('prepare-fillet');
    const fillet = await callJsonTool('sw_fillet', { radius: 0.001 });
    expect(fillet.type).toBe('Fillet');
  });

  test('accepts chamfer through MCP after helper preparation', async () => {
    await runHelper('prepare-chamfer');
    const chamfer = await callJsonTool('sw_chamfer', { distance: 0.001 });
    expect(chamfer.type).toBe('Chamfer');
  });

  test('accepts shell through MCP after helper preparation', async () => {
    await runHelper('prepare-shell');
    const shell = await callJsonTool('sw_shell', { thickness: 0.001 });
    expect(shell.type).toBe('Shell');
  });

  test('accepts simple_hole through MCP after helper preparation', async () => {
    await runHelper('prepare-simple-hole');
    const hole = await callJsonTool('sw_simple_hole', { diameter: 0.005, depth: 0.01 });
    expect(hole.type).toBe('SimpleHole');
  });

  test('accepts component insertion and listing through MCP', async () => {
    const savedPart = await runHelper<{ path: string }>('create-saved-part');
    await callJsonTool('sw_new_document', { type: 'Assembly' });
    const inserted = await callJsonTool('sw_insert_component', { filePath: savedPart.path, x: 0, y: 0, z: 0 });
    expect(inserted.name).toBeTruthy();

    const components = await callJsonTool('sw_list_components', {});
    expect(Array.isArray(components)).toBe(true);
    expect(components.some((component: { path: string }) => component.path.toLowerCase() === savedPart.path.toLowerCase())).toBe(true);
  });

  test('accepts coincident mate through MCP', async () => {
    await runHelper('prepare-mate-coincident');
    const result = await callJsonTool('sw_add_mate_coincident', {});
    expect(result.ok).toBe(true);
  });

  test('accepts parallel mate through MCP', async () => {
    await runHelper('prepare-mate-parallel');
    const result = await callJsonTool('sw_add_mate_parallel', {});
    expect(result.ok).toBe(true);
  });

  test('accepts distance mate through MCP', async () => {
    await runHelper('prepare-mate-distance');
    const result = await callJsonTool('sw_add_mate_distance', { distance: 0.01 });
    expect(result.ok).toBe(true);
  });

  test('accepts angle mate through MCP', async () => {
    await runHelper('prepare-mate-angle');
    const result = await callJsonTool('sw_add_mate_angle', { angleDegrees: 15 });
    expect(result.ok).toBe(true);
  });

  test('accepts concentric mate through MCP', async () => {
    await runHelper('prepare-mate-concentric');
    const result = await callJsonTool('sw_add_mate_concentric', {});
    expect(result.ok).toBe(true);
  });

  async function selectFrontPlane(): Promise<void> {
    const chineseAttempt = await client.callTool({
      name: 'sw_select_by_name',
      arguments: { name: '前视基准面', selType: 'PLANE' },
    });

    if (!chineseAttempt.isError) {
      return;
    }

    await callJsonTool('sw_select_by_name', { name: 'Front Plane', selType: 'PLANE' });
  }

  async function callJsonTool<T>(name: string, args: Record<string, unknown>): Promise<T> {
    const result = await client.callTool({ name, arguments: args });
    if (result.isError) {
      const detail = result.content.map((item) => ('text' in item ? item.text : item.type)).join('\n');
      throw new Error(`${name} failed: ${detail}\n${serverStderr}`);
    }

    const textItem = result.content.find((item) => item.type === 'text');
    if (!textItem || !('text' in textItem)) {
      throw new Error(`${name} did not return a text payload.`);
    }

    return JSON.parse(textItem.text) as T;
  }

  async function runHelper<T = Record<string, unknown>>(command: string): Promise<T> {
    const { stdout, stderr } = await execFileAsync(helperExe, [command], { cwd: repoRoot, env: processEnv });
    if (stderr.trim().length > 0) {
      throw new Error(stderr);
    }

    return JSON.parse(stdout) as T;
  }
});