import { describe, it, expect, vi } from 'vitest';
import type { NamedPipeClient } from '../../src/transport/named-pipe-client.js';
import type { PipeResponse } from '../../src/types/solidworks.js';
import {
  swInsertSketch,
  swFinishSketch,
  swAddPoint,
  swAddEllipse,
  swAddPolygon,
  swAddText,
  swAddLine,
  swAddCircle,
  swAddRectangle,
  swAddArc,
  type SwSketchEntityInfo,
} from '../../src/tools/sketch-tools.js';

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

const fakeLine: SwSketchEntityInfo = { type: 'Line', id: 'line-1' };
const fakeCircle: SwSketchEntityInfo = { type: 'Circle', id: 'circle-1' };
const fakeRect: SwSketchEntityInfo = { type: 'Rectangle', id: 'rect-1' };
const fakeArc: SwSketchEntityInfo = { type: 'Arc', id: 'arc-1' };
const fakePoint: SwSketchEntityInfo = { type: 'Point', id: 'point-1' };
const fakeEllipse: SwSketchEntityInfo = { type: 'Ellipse', id: 'ellipse-1' };
const fakePolygon: SwSketchEntityInfo = { type: 'Polygon', id: 'polygon-1' };
const fakeText: SwSketchEntityInfo = { type: 'Text', id: 'text-1' };

// ── swInsertSketch ─────────────────────────────────────────────

describe('swInsertSketch', () => {
  it('calls sw.sketch.insert with no params', async () => {
    const client = mockClient(null);
    await swInsertSketch(client);
    expect(client.request).toHaveBeenCalledWith('sw.sketch.insert');
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'no active doc' });
    await expect(swInsertSketch(client)).rejects.toThrow('sw.sketch.insert failed');
  });
});

// ── swFinishSketch ─────────────────────────────────────────────

describe('swFinishSketch', () => {
  it('calls sw.sketch.finish with no params', async () => {
    const client = mockClient(null);
    await swFinishSketch(client);
    expect(client.request).toHaveBeenCalledWith('sw.sketch.finish');
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'not in sketch mode' });
    await expect(swFinishSketch(client)).rejects.toThrow('sw.sketch.finish failed');
  });
});

// ── swAddPoint ────────────────────────────────────────────────

describe('swAddPoint', () => {
  it('calls sw.sketch.add_point with coordinates', async () => {
    const client = mockClient(fakePoint);
    const result = await swAddPoint(client, { x: 0.01, y: 0.02 });
    expect(result).toEqual(fakePoint);
    expect(client.request).toHaveBeenCalledWith('sw.sketch.add_point', {
      x: 0.01,
      y: 0.02,
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'no sketch open' });
    await expect(swAddPoint(client, { x: 0, y: 0 })).rejects.toThrow(
      'sw.sketch.add_point failed',
    );
  });
});

// ── swAddEllipse ──────────────────────────────────────────────

describe('swAddEllipse', () => {
  it('calls sw.sketch.add_ellipse with center, major, and minor points', async () => {
    const client = mockClient(fakeEllipse);
    const result = await swAddEllipse(client, {
      cx: 0,
      cy: 0,
      majorX: 0.03,
      majorY: 0,
      minorX: 0,
      minorY: 0.01,
    });
    expect(result).toEqual(fakeEllipse);
    expect(client.request).toHaveBeenCalledWith('sw.sketch.add_ellipse', {
      cx: 0,
      cy: 0,
      majorX: 0.03,
      majorY: 0,
      minorX: 0,
      minorY: 0.01,
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'no sketch open' });
    await expect(swAddEllipse(client, {
      cx: 0,
      cy: 0,
      majorX: 0.03,
      majorY: 0,
      minorX: 0,
      minorY: 0.01,
    })).rejects.toThrow('sw.sketch.add_ellipse failed');
  });
});

// ── swAddPolygon ──────────────────────────────────────────────

describe('swAddPolygon', () => {
  it('calls sw.sketch.add_polygon with polygon parameters', async () => {
    const client = mockClient(fakePolygon);
    const result = await swAddPolygon(client, {
      cx: 0,
      cy: 0,
      x: 0.02,
      y: 0,
      sides: 6,
      inscribed: true,
    });
    expect(result).toEqual(fakePolygon);
    expect(client.request).toHaveBeenCalledWith('sw.sketch.add_polygon', {
      cx: 0,
      cy: 0,
      x: 0.02,
      y: 0,
      sides: 6,
      inscribed: true,
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'no sketch open' });
    await expect(swAddPolygon(client, {
      cx: 0,
      cy: 0,
      x: 0.02,
      y: 0,
      sides: 6,
      inscribed: true,
    })).rejects.toThrow('sw.sketch.add_polygon failed');
  });
});

// ── swAddText ─────────────────────────────────────────────────

describe('swAddText', () => {
  it('calls sw.sketch.add_text with anchor and content', async () => {
    const client = mockClient(fakeText);
    const result = await swAddText(client, {
      x: 0.01,
      y: 0.02,
      text: 'HELLO',
    });
    expect(result).toEqual(fakeText);
    expect(client.request).toHaveBeenCalledWith('sw.sketch.add_text', {
      x: 0.01,
      y: 0.02,
      text: 'HELLO',
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'no sketch open' });
    await expect(swAddText(client, { x: 0.01, y: 0.02, text: 'HELLO' })).rejects.toThrow(
      'sw.sketch.add_text failed',
    );
  });
});

// ── swAddLine ─────────────────────────────────────────────────

describe('swAddLine', () => {
  it('calls sw.sketch.add_line with coordinates', async () => {
    const client = mockClient(fakeLine);
    const result = await swAddLine(client, { x1: 0, y1: 0, x2: 0.1, y2: 0 });
    expect(result).toEqual(fakeLine);
    expect(client.request).toHaveBeenCalledWith('sw.sketch.add_line', {
      x1: 0,
      y1: 0,
      x2: 0.1,
      y2: 0,
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'no sketch open' });
    await expect(swAddLine(client, { x1: 0, y1: 0, x2: 1, y2: 0 })).rejects.toThrow(
      'sw.sketch.add_line failed',
    );
  });
});

// ── swAddCircle ───────────────────────────────────────────────

describe('swAddCircle', () => {
  it('calls sw.sketch.add_circle with center and radius', async () => {
    const client = mockClient(fakeCircle);
    const result = await swAddCircle(client, { cx: 0, cy: 0, radius: 0.05 });
    expect(result).toEqual(fakeCircle);
    expect(client.request).toHaveBeenCalledWith('sw.sketch.add_circle', {
      cx: 0,
      cy: 0,
      radius: 0.05,
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'error' });
    await expect(swAddCircle(client, { cx: 0, cy: 0, radius: 0.01 })).rejects.toThrow(
      'sw.sketch.add_circle failed',
    );
  });
});

// ── swAddRectangle ────────────────────────────────────────────

describe('swAddRectangle', () => {
  it('calls sw.sketch.add_rectangle with corners', async () => {
    const client = mockClient(fakeRect);
    const result = await swAddRectangle(client, { x1: -0.05, y1: -0.03, x2: 0.05, y2: 0.03 });
    expect(result).toEqual(fakeRect);
    expect(client.request).toHaveBeenCalledWith('sw.sketch.add_rectangle', {
      x1: -0.05,
      y1: -0.03,
      x2: 0.05,
      y2: 0.03,
    });
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'error' });
    await expect(
      swAddRectangle(client, { x1: 0, y1: 0, x2: 0.1, y2: 0.1 }),
    ).rejects.toThrow('sw.sketch.add_rectangle failed');
  });
});

// ── swAddArc ──────────────────────────────────────────────────

describe('swAddArc', () => {
  it('calls sw.sketch.add_arc with all parameters', async () => {
    const client = mockClient(fakeArc);
    const result = await swAddArc(client, {
      cx: 0,
      cy: 0,
      x1: 0.05,
      y1: 0,
      x2: 0,
      y2: 0.05,
      direction: 1,
    });
    expect(result).toEqual(fakeArc);
    expect(client.request).toHaveBeenCalledWith('sw.sketch.add_arc', {
      cx: 0,
      cy: 0,
      x1: 0.05,
      y1: 0,
      x2: 0,
      y2: 0.05,
      direction: 1,
    });
  });

  it('accepts clockwise direction (-1)', async () => {
    const client = mockClient(fakeArc);
    await swAddArc(client, { cx: 0, cy: 0, x1: 0, y1: 0.05, x2: 0.05, y2: 0, direction: -1 });
    expect(client.request).toHaveBeenCalledWith('sw.sketch.add_arc', expect.objectContaining({
      direction: -1,
    }));
  });

  it('throws when bridge returns error', async () => {
    const client = mockClient(null, { code: -32603, message: 'error' });
    await expect(
      swAddArc(client, { cx: 0, cy: 0, x1: 0, y1: 0, x2: 0, y2: 0, direction: 1 }),
    ).rejects.toThrow('sw.sketch.add_arc failed');
  });
});
