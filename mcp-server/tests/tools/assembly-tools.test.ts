import { describe, it, expect, vi } from 'vitest';
import type { NamedPipeClient } from '../../src/transport/named-pipe-client.js';
import type { PipeResponse } from '../../src/types/solidworks.js';
import {
  swInsertComponent,
  swAddMateCoincident,
  swAddMateConcentric,
  swAddMateParallel,
  swAddMateDistance,
  swAddMateAngle,
  swListComponents,
  type SwComponentInfo,
} from '../../src/tools/assembly-tools.js';

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

const fakeComp: SwComponentInfo = { name: 'Part1-1', path: 'C:\\Part1.sldprt' };

// ── swInsertComponent ─────────────────────────────────────────

describe('swInsertComponent', () => {
  it('calls sw.assembly.insert_component with filePath and default position', async () => {
    const client = mockClient(fakeComp);
    const result = await swInsertComponent(client, { filePath: 'C:\\Part1.sldprt' });
    expect(result).toEqual(fakeComp);
    expect(client.request).toHaveBeenCalledWith('sw.assembly.insert_component', {
      filePath: 'C:\\Part1.sldprt',
      x: 0,
      y: 0,
      z: 0,
    });
  });

  it('passes custom position coordinates', async () => {
    const client = mockClient(fakeComp);
    await swInsertComponent(client, { filePath: 'C:\\Part1.sldprt', x: 0.1, y: 0.2, z: 0.3 });
    expect(client.request).toHaveBeenCalledWith('sw.assembly.insert_component', {
      filePath: 'C:\\Part1.sldprt',
      x: 0.1,
      y: 0.2,
      z: 0.3,
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'file not found' });
    await expect(swInsertComponent(client, { filePath: 'C:\\missing.sldprt' })).rejects.toThrow(
      'sw.assembly.insert_component failed',
    );
  });
});

// ── swAddMateCoincident ───────────────────────────────────────

describe('swAddMateCoincident', () => {
  it('calls sw.assembly.add_mate_coincident with no params when align omitted', async () => {
    const client = mockClient(null);
    await swAddMateCoincident(client, {});
    expect(client.request).toHaveBeenCalledWith('sw.assembly.add_mate_coincident', {});
  });

  it('includes align when provided', async () => {
    const client = mockClient(null);
    await swAddMateCoincident(client, { align: 'AntiAligned' });
    expect(client.request).toHaveBeenCalledWith('sw.assembly.add_mate_coincident', {
      align: 'AntiAligned',
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'mate failed' });
    await expect(swAddMateCoincident(client, {})).rejects.toThrow(
      'sw.assembly.add_mate_coincident failed',
    );
  });
});

// ── swAddMateConcentric ───────────────────────────────────────

describe('swAddMateConcentric', () => {
  it('calls sw.assembly.add_mate_concentric', async () => {
    const client = mockClient(null);
    await swAddMateConcentric(client, {});
    expect(client.request).toHaveBeenCalledWith('sw.assembly.add_mate_concentric', {});
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'error' });
    await expect(swAddMateConcentric(client, {})).rejects.toThrow(
      'sw.assembly.add_mate_concentric failed',
    );
  });
});

// ── swAddMateParallel ─────────────────────────────────────────

describe('swAddMateParallel', () => {
  it('calls sw.assembly.add_mate_parallel', async () => {
    const client = mockClient(null);
    await swAddMateParallel(client, {});
    expect(client.request).toHaveBeenCalledWith('sw.assembly.add_mate_parallel', {});
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'error' });
    await expect(swAddMateParallel(client, {})).rejects.toThrow(
      'sw.assembly.add_mate_parallel failed',
    );
  });
});

// ── swAddMateDistance ─────────────────────────────────────────

describe('swAddMateDistance', () => {
  it('calls sw.assembly.add_mate_distance with distance', async () => {
    const client = mockClient(null);
    await swAddMateDistance(client, { distance: 0.05 });
    expect(client.request).toHaveBeenCalledWith('sw.assembly.add_mate_distance', { distance: 0.05 });
  });

  it('includes align when provided', async () => {
    const client = mockClient(null);
    await swAddMateDistance(client, { distance: 0.1, align: 'None' });
    expect(client.request).toHaveBeenCalledWith('sw.assembly.add_mate_distance', {
      distance: 0.1,
      align: 'None',
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'error' });
    await expect(swAddMateDistance(client, { distance: 0.01 })).rejects.toThrow(
      'sw.assembly.add_mate_distance failed',
    );
  });
});

// ── swAddMateAngle ────────────────────────────────────────────

describe('swAddMateAngle', () => {
  it('calls sw.assembly.add_mate_angle with angleDegrees', async () => {
    const client = mockClient(null);
    await swAddMateAngle(client, { angleDegrees: 90 });
    expect(client.request).toHaveBeenCalledWith('sw.assembly.add_mate_angle', {
      angleDegrees: 90,
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'error' });
    await expect(swAddMateAngle(client, { angleDegrees: 45 })).rejects.toThrow(
      'sw.assembly.add_mate_angle failed',
    );
  });
});

// ── swListComponents ──────────────────────────────────────────

describe('swListComponents', () => {
  it('returns list of components from bridge', async () => {
    const comps: SwComponentInfo[] = [
      { name: 'Part1-1', path: 'C:\\Part1.sldprt' },
      { name: 'Part2-1', path: 'C:\\Part2.sldprt' },
    ];
    const client = mockClient(comps);
    const result = await swListComponents(client);
    expect(result).toHaveLength(2);
    expect(result[0].name).toBe('Part1-1');
  });

  it('returns empty array when bridge returns null', async () => {
    const client = mockClient(null);
    const result = await swListComponents(client);
    expect(result).toEqual([]);
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'not an assembly' });
    await expect(swListComponents(client)).rejects.toThrow('sw.assembly.list_components failed');
  });
});
