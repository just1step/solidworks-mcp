import { describe, it, expect, vi } from 'vitest';
import type { NamedPipeClient } from '../../src/transport/named-pipe-client.js';
import type { PipeResponse } from '../../src/types/solidworks.js';
import {
  swExtrude,
  swExtrudeCut,
  swRevolve,
  swFillet,
  swChamfer,
  swShell,
  swSimpleHole,
  type SwFeatureInfo,
} from '../../src/tools/feature-tools.js';

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

const fakeExtrude: SwFeatureInfo = { name: 'Boss-Extrude1', type: 'Extrude' };
const fakeCut: SwFeatureInfo = { name: 'Cut-Extrude1', type: 'ExtrudeCut' };
const fakeRevolve: SwFeatureInfo = { name: 'Revolve1', type: 'Revolve' };
const fakeFillet: SwFeatureInfo = { name: 'Fillet1', type: 'Fillet' };
const fakeChamfer: SwFeatureInfo = { name: 'Chamfer1', type: 'Chamfer' };
const fakeShell: SwFeatureInfo = { name: 'Shell1', type: 'Shell' };
const fakeHole: SwFeatureInfo = { name: 'Hole1', type: 'SimpleHole' };

// ── swExtrude ─────────────────────────────────────────────────

describe('swExtrude', () => {
  it('calls sw.feature.extrude with depth', async () => {
    const client = mockClient(fakeExtrude);
    const result = await swExtrude(client, { depth: 0.02 });
    expect(result).toEqual(fakeExtrude);
    expect(client.request).toHaveBeenCalledWith('sw.feature.extrude', { depth: 0.02 });
  });

  it('includes endCondition when provided', async () => {
    const client = mockClient(fakeExtrude);
    await swExtrude(client, { depth: 0.01, endCondition: 'ThroughAll' });
    expect(client.request).toHaveBeenCalledWith('sw.feature.extrude', {
      depth: 0.01,
      endCondition: 'ThroughAll',
    });
  });

  it('includes flipDirection when true', async () => {
    const client = mockClient(fakeExtrude);
    await swExtrude(client, { depth: 0.01, flipDirection: true });
    expect(client.request).toHaveBeenCalledWith('sw.feature.extrude', {
      depth: 0.01,
      flipDirection: true,
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'no sketch' });
    await expect(swExtrude(client, { depth: 0.01 })).rejects.toThrow('sw.feature.extrude failed');
  });
});

// ── swExtrudeCut ──────────────────────────────────────────────

describe('swExtrudeCut', () => {
  it('calls sw.feature.extrude_cut with depth', async () => {
    const client = mockClient(fakeCut);
    const result = await swExtrudeCut(client, { depth: 0.01 });
    expect(result).toEqual(fakeCut);
    expect(client.request).toHaveBeenCalledWith('sw.feature.extrude_cut', { depth: 0.01 });
  });

  it('includes endCondition ThroughAll', async () => {
    const client = mockClient(fakeCut);
    await swExtrudeCut(client, { depth: 0.01, endCondition: 'ThroughAll' });
    expect(client.request).toHaveBeenCalledWith('sw.feature.extrude_cut', {
      depth: 0.01,
      endCondition: 'ThroughAll',
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'error' });
    await expect(swExtrudeCut(client, { depth: 0.01 })).rejects.toThrow(
      'sw.feature.extrude_cut failed',
    );
  });
});

// ── swRevolve ─────────────────────────────────────────────────

describe('swRevolve', () => {
  it('calls sw.feature.revolve with angleDegrees', async () => {
    const client = mockClient(fakeRevolve);
    const result = await swRevolve(client, { angleDegrees: 360 });
    expect(result).toEqual(fakeRevolve);
    expect(client.request).toHaveBeenCalledWith('sw.feature.revolve', {
      angleDegrees: 360,
      isCut: false,
    });
  });

  it('passes isCut=true when specified', async () => {
    const revCut: SwFeatureInfo = { name: 'RevCut1', type: 'RevolveCut' };
    const client = mockClient(revCut);
    await swRevolve(client, { angleDegrees: 180, isCut: true });
    expect(client.request).toHaveBeenCalledWith('sw.feature.revolve', {
      angleDegrees: 180,
      isCut: true,
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'error' });
    await expect(swRevolve(client, { angleDegrees: 360 })).rejects.toThrow(
      'sw.feature.revolve failed',
    );
  });
});

// ── swFillet ──────────────────────────────────────────────────

describe('swFillet', () => {
  it('calls sw.feature.fillet with radius', async () => {
    const client = mockClient(fakeFillet);
    const result = await swFillet(client, { radius: 0.003 });
    expect(result).toEqual(fakeFillet);
    expect(client.request).toHaveBeenCalledWith('sw.feature.fillet', { radius: 0.003 });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'no edge selected' });
    await expect(swFillet(client, { radius: 0.001 })).rejects.toThrow('sw.feature.fillet failed');
  });
});

// ── swChamfer ─────────────────────────────────────────────────

describe('swChamfer', () => {
  it('calls sw.feature.chamfer with distance', async () => {
    const client = mockClient(fakeChamfer);
    const result = await swChamfer(client, { distance: 0.002 });
    expect(result).toEqual(fakeChamfer);
    expect(client.request).toHaveBeenCalledWith('sw.feature.chamfer', { distance: 0.002 });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'error' });
    await expect(swChamfer(client, { distance: 0.001 })).rejects.toThrow(
      'sw.feature.chamfer failed',
    );
  });
});

// ── swShell ───────────────────────────────────────────────────

describe('swShell', () => {
  it('calls sw.feature.shell with thickness', async () => {
    const client = mockClient(fakeShell);
    const result = await swShell(client, { thickness: 0.002 });
    expect(result).toEqual(fakeShell);
    expect(client.request).toHaveBeenCalledWith('sw.feature.shell', { thickness: 0.002 });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'no face selected' });
    await expect(swShell(client, { thickness: 0.001 })).rejects.toThrow(
      'sw.feature.shell failed',
    );
  });
});

// ── swSimpleHole ──────────────────────────────────────────────

describe('swSimpleHole', () => {
  it('calls sw.feature.simple_hole with diameter and depth', async () => {
    const client = mockClient(fakeHole);
    const result = await swSimpleHole(client, { diameter: 0.01, depth: 0.02 });
    expect(result).toEqual(fakeHole);
    expect(client.request).toHaveBeenCalledWith('sw.feature.simple_hole', {
      diameter: 0.01,
      depth: 0.02,
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'error' });
    await expect(swSimpleHole(client, { diameter: 0.01, depth: 0.01 })).rejects.toThrow(
      'sw.feature.simple_hole failed',
    );
  });
});
