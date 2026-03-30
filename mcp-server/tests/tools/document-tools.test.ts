import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { NamedPipeClient } from '../../src/transport/named-pipe-client.js';
import type { PipeResponse } from '../../src/types/solidworks.js';
import {
  swConnect,
  swDisconnect,
  swNewDocument,
  swOpenDocument,
  swCloseDocument,
  swSaveDocument,
  swListDocuments,
  swGetActiveDocument,
  type SwDocumentInfo,
} from '../../src/tools/document-tools.js';

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

const fakeDoc: SwDocumentInfo = { path: 'C:\\model.sldprt', title: 'model', type: 1 };

// ── sw.connect ────────────────────────────────────────────────

describe('swConnect', () => {
  it('calls sw.connect and returns connected:true', async () => {
    const client = mockClient({ connected: true });
    const result = await swConnect(client);
    expect(result.connected).toBe(true);
    expect(client.request).toHaveBeenCalledWith('sw.connect');
  });

  it('throws when bridge returns an error', async () => {
    const client = mockClient(null, { code: -32603, message: 'internal error' });
    await expect(swConnect(client)).rejects.toThrow('sw.connect failed');
  });
});

// ── sw.disconnect ─────────────────────────────────────────────

describe('swDisconnect', () => {
  it('calls sw.disconnect and returns connected:false', async () => {
    const client = mockClient({ connected: false });
    const result = await swDisconnect(client);
    expect(result.connected).toBe(false);
    expect(client.request).toHaveBeenCalledWith('sw.disconnect');
  });

  it('throws when bridge returns an error', async () => {
    const client = mockClient(null, { code: -32603, message: 'oops' });
    await expect(swDisconnect(client)).rejects.toThrow('sw.disconnect failed');
  });
});

// ── sw.new_document ───────────────────────────────────────────

describe('swNewDocument', () => {
  it('sends Part type with no template', async () => {
    const client = mockClient(fakeDoc);
    const result = await swNewDocument(client, { type: 'Part' });
    expect(result).toEqual(fakeDoc);
    expect(client.request).toHaveBeenCalledWith('sw.new_document', { type: 'Part' });
  });

  it('sends Assembly type', async () => {
    const asmDoc: SwDocumentInfo = { path: 'C:\\asm.sldasm', title: 'asm', type: 2 };
    const client = mockClient(asmDoc);
    const result = await swNewDocument(client, { type: 'Assembly' });
    expect(result.type).toBe(2);
    expect(client.request).toHaveBeenCalledWith('sw.new_document', { type: 'Assembly' });
  });

  it('sends Drawing type', async () => {
    const drwDoc: SwDocumentInfo = { path: 'C:\\drw.slddrw', title: 'drw', type: 3 };
    const client = mockClient(drwDoc);
    const result = await swNewDocument(client, { type: 'Drawing' });
    expect(result.type).toBe(3);
  });

  it('includes templatePath when provided', async () => {
    const client = mockClient(fakeDoc);
    const tpl = 'C:\\templates\\part.prtdot';
    await swNewDocument(client, { type: 'Part', templatePath: tpl });
    expect(client.request).toHaveBeenCalledWith('sw.new_document', {
      type: 'Part',
      templatePath: tpl,
    });
  });

  it('does NOT include templatePath when omitted', async () => {
    const client = mockClient(fakeDoc);
    await swNewDocument(client, { type: 'Part' });
    const callArgs = (client.request as ReturnType<typeof vi.fn>).mock.calls[0][1];
    expect(callArgs).not.toHaveProperty('templatePath');
  });

  it('throws when bridge returns an error', async () => {
    const client = mockClient(null, { code: -32001, message: 'SW failed' });
    await expect(swNewDocument(client, { type: 'Part' })).rejects.toThrow('sw.new_document failed');
  });
});

// ── sw.open_document ──────────────────────────────────────────

describe('swOpenDocument', () => {
  it('sends correct path and returns doc info', async () => {
    const client = mockClient(fakeDoc);
    const result = await swOpenDocument(client, { path: 'C:\\model.sldprt' });
    expect(result).toEqual(fakeDoc);
    expect(client.request).toHaveBeenCalledWith('sw.open_document', {
      path: 'C:\\model.sldprt',
    });
  });

  it('throws when bridge returns an error', async () => {
    const client = mockClient(null, { code: -32001, message: 'File not found' });
    await expect(swOpenDocument(client, { path: 'C:\\missing.sldprt' })).rejects.toThrow(
      'sw.open_document failed',
    );
  });
});

// ── sw.close_document ─────────────────────────────────────────

describe('swCloseDocument', () => {
  it('sends correct path and returns closed:true', async () => {
    const client = mockClient({ closed: true });
    const result = await swCloseDocument(client, { path: 'C:\\model.sldprt' });
    expect(result.closed).toBe(true);
    expect(client.request).toHaveBeenCalledWith('sw.close_document', {
      path: 'C:\\model.sldprt',
    });
  });

  it('throws when bridge returns an error', async () => {
    const client = mockClient(null, { code: -32001, message: 'doc not open' });
    await expect(swCloseDocument(client, { path: 'C:\\x.sldprt' })).rejects.toThrow(
      'sw.close_document failed',
    );
  });
});

// ── sw.save_document ──────────────────────────────────────────

describe('swSaveDocument', () => {
  it('sends correct path and returns saved:true', async () => {
    const client = mockClient({ saved: true });
    const result = await swSaveDocument(client, { path: 'C:\\model.sldprt' });
    expect(result.saved).toBe(true);
    expect(client.request).toHaveBeenCalledWith('sw.save_document', {
      path: 'C:\\model.sldprt',
    });
  });

  it('throws when bridge returns an error', async () => {
    const client = mockClient(null, { code: -32001, message: 'save error' });
    await expect(swSaveDocument(client, { path: 'C:\\x.sldprt' })).rejects.toThrow(
      'sw.save_document failed',
    );
  });
});

// ── sw.list_documents ─────────────────────────────────────────

describe('swListDocuments', () => {
  it('returns array of document infos', async () => {
    const docs: SwDocumentInfo[] = [
      { path: 'C:\\a.sldprt', title: 'a', type: 1 },
      { path: 'C:\\b.sldasm', title: 'b', type: 2 },
    ];
    const client = mockClient(docs);
    const result = await swListDocuments(client);
    expect(result).toHaveLength(2);
    expect(result[0].path).toBe('C:\\a.sldprt');
    expect(result[1].type).toBe(2);
  });

  it('returns empty array when no documents open', async () => {
    const client = mockClient([]);
    const result = await swListDocuments(client);
    expect(result).toEqual([]);
  });

  it('returns empty array when result is null', async () => {
    const client = mockClient(null);
    const result = await swListDocuments(client);
    expect(result).toEqual([]);
  });

  it('throws when bridge returns an error', async () => {
    const client = mockClient(null, { code: -32000, message: 'not connected' });
    await expect(swListDocuments(client)).rejects.toThrow('sw.list_documents failed');
  });
});

// ── sw.get_active_document ────────────────────────────────────

describe('swGetActiveDocument', () => {
  it('returns active document info', async () => {
    const client = mockClient(fakeDoc);
    const result = await swGetActiveDocument(client);
    expect(result).toEqual(fakeDoc);
    expect(client.request).toHaveBeenCalledWith('sw.get_active_document');
  });

  it('returns null when no document is active', async () => {
    const client = mockClient(null);
    const result = await swGetActiveDocument(client);
    expect(result).toBeNull();
  });

  it('throws when bridge returns an error', async () => {
    const client = mockClient(null, { code: -32000, message: 'not connected' });
    await expect(swGetActiveDocument(client)).rejects.toThrow('sw.get_active_document failed');
  });
});
