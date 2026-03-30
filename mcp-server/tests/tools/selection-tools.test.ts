import { describe, it, expect, vi } from 'vitest';
import type { NamedPipeClient } from '../../src/transport/named-pipe-client.js';
import type { PipeResponse } from '../../src/types/solidworks.js';
import {
  swSelectByName,
  swClearSelection,
  type SwSelectionResult,
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
