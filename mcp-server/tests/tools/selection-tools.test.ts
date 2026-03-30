import { describe, it, expect, vi } from 'vitest';
import type { NamedPipeClient } from '../../src/transport/named-pipe-client.js';
import type { PipeResponse } from '../../src/types/solidworks.js';
import {
  swSelectByName,
  swListEntities,
  swSelectEntity,
  swClearSelection,
  type SwSelectionResult,
  type SwSelectableEntityInfo,
} from '../../src/tools/selection-tools.js';

// ── Mock factory ──────────────────────────────────────────────

function mockClient(result: unknown = null, error?: { code: number; message: string }) {
  const resp: PipeResponse = {
    id: 'test-1',
    result: error ? undefined : result,
    error,
  };
  return {
    request: vi.fn().mockResolvedValue(resp),
  } as unknown as NamedPipeClient;
}

const fakeSuccess: SwSelectionResult = { success: true, message: 'Selected' };
const fakeEntities: SwSelectableEntityInfo[] = [
  { index: 0, entityType: 'Edge', componentName: null, box: [0, 0, 0, 0.01, 0, 0] },
];

// ── swSelectByName ────────────────────────────────────────────

describe('swSelectByName', () => {
  it('calls sw.select.by_name with name and selType', async () => {
    const client = mockClient(fakeSuccess);
    const result = await swSelectByName(client, { name: '前视基准面', selType: 'PLANE' });
    expect(result.success).toBe(true);
    expect(client.request).toHaveBeenCalledWith('sw.select.by_name', {
      name: '前视基准面',
      selType: 'PLANE',
    });
  });

  it('returns the result from bridge', async () => {
    const client = mockClient({ success: false, message: 'Not found' });
    const result = await swSelectByName(client, { name: 'Missing_Entity', selType: 'FACE' });
    expect(result.success).toBe(false);
    expect(result.message).toBe('Not found');
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'connection lost' });
    await expect(swSelectByName(client, { name: 'x', selType: 'PLANE' })).rejects.toThrow(
      'sw.select.by_name failed',
    );
  });
});

// ── swClearSelection ──────────────────────────────────────────

describe('swClearSelection', () => {
  it('calls sw.select.clear with no params', async () => {
    const client = mockClient(null);
    await swClearSelection(client);
    expect(client.request).toHaveBeenCalledWith('sw.select.clear');
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'oops' });
    await expect(swClearSelection(client)).rejects.toThrow('sw.select.clear failed');
  });
});

// ── swListEntities ────────────────────────────────────────────

describe('swListEntities', () => {
  it('calls sw.select.list_entities with optional filters', async () => {
    const client = mockClient(fakeEntities);
    const result = await swListEntities(client, { entityType: 'Edge', componentName: 'Part1-1' });
    expect(result).toEqual(fakeEntities);
    expect(client.request).toHaveBeenCalledWith('sw.select.list_entities', {
      entityType: 'Edge',
      componentName: 'Part1-1',
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'oops' });
    await expect(swListEntities(client, {})).rejects.toThrow('sw.select.list_entities failed');
  });
});

// ── swSelectEntity ────────────────────────────────────────────

describe('swSelectEntity', () => {
  it('calls sw.select.entity with indexed selection params', async () => {
    const client = mockClient(fakeSuccess);
    const result = await swSelectEntity(client, {
      entityType: 'Face',
      index: 2,
      append: true,
      mark: 1,
      componentName: 'Part1-2',
    });
    expect(result).toEqual(fakeSuccess);
    expect(client.request).toHaveBeenCalledWith('sw.select.entity', {
      entityType: 'Face',
      index: 2,
      append: true,
      mark: 1,
      componentName: 'Part1-2',
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'oops' });
    await expect(swSelectEntity(client, { entityType: 'Vertex', index: 0 })).rejects.toThrow(
      'sw.select.entity failed',
    );
  });
});
