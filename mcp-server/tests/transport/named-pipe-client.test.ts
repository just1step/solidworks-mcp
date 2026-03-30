import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as net from 'node:net';
import { NamedPipeClient } from '../../src/transport/named-pipe-client.js';
import type { PipeRequest, PipeResponse } from '../../src/types/solidworks.js';

/**
 * Helper: create a mock pipe server that echoes or handles requests.
 */
function createMockPipeServer(
  pipeName: string,
  handler: (req: PipeRequest) => PipeResponse
): net.Server {
  const server = net.createServer((socket) => {
    let buffer = Buffer.alloc(0);

    socket.on('data', (data) => {
      buffer = Buffer.concat([buffer, data]);

      while (buffer.length >= 4) {
        const msgLength = buffer.readInt32LE(0);
        if (buffer.length < 4 + msgLength) break;

        const bodyBytes = buffer.subarray(4, 4 + msgLength);
        buffer = buffer.subarray(4 + msgLength);

        const request: PipeRequest = JSON.parse(bodyBytes.toString('utf-8'));
        const response = handler(request);

        const responseBody = Buffer.from(JSON.stringify(response), 'utf-8');
        const lengthBuf = Buffer.alloc(4);
        lengthBuf.writeInt32LE(responseBody.length, 0);

        socket.write(lengthBuf);
        socket.write(responseBody);
      }
    });
  });

  server.listen(`\\\\.\\pipe\\${pipeName}`);
  return server;
}

function closeMockServer(server: net.Server): Promise<void> {
  return new Promise((resolve) => server.close(() => resolve()));
}

/** Generate a unique pipe name per test to avoid conflicts */
function uniquePipeName(): string {
  return `SWMcpTest_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;
}

describe('NamedPipeClient', () => {
  let server: net.Server | null = null;
  let client: NamedPipeClient | null = null;

  afterEach(async () => {
    client?.disconnect();
    client = null;
    if (server) {
      await closeMockServer(server);
      server = null;
    }
  });

  // --- Connection Tests ---

  it('should connect to a mock pipe server', async () => {
    const pipeName = uniquePipeName();
    server = createMockPipeServer(pipeName, (req) => ({
      id: req.id,
      result: { pong: true },
    }));

    client = new NamedPipeClient({ pipeName, reconnect: false });
    expect(client.connected).toBe(false);

    await client.connect();
    expect(client.connected).toBe(true);
  });

  it('should fail to connect when no server is listening', async () => {
    client = new NamedPipeClient({
      pipeName: 'NonExistentPipe_12345',
      connectTimeout: 1000,
      reconnect: false,
    });

    await expect(client.connect()).rejects.toThrow();
  });

  it('should return correct pipePath', () => {
    client = new NamedPipeClient({ pipeName: 'TestPipe' });
    expect(client.pipePath).toBe('\\\\.\\pipe\\TestPipe');
  });

  // --- Request-Response Tests ---

  it('should send request and receive correlated response', async () => {
    const pipeName = uniquePipeName();
    server = createMockPipeServer(pipeName, (req) => ({
      id: req.id,
      result: { method: req.method, echo: true },
    }));

    client = new NamedPipeClient({ pipeName, reconnect: false });
    await client.connect();

    const response = await client.request('ping', { hello: 'world' });
    expect(response.id).toBeDefined();
    expect(response.error).toBeUndefined();
    expect(response.result).toBeDefined();
  });

  it('should handle multiple sequential requests', async () => {
    const pipeName = uniquePipeName();
    let counter = 0;
    server = createMockPipeServer(pipeName, (req) => ({
      id: req.id,
      result: { seq: ++counter },
    }));

    client = new NamedPipeClient({ pipeName, reconnect: false });
    await client.connect();

    const r1 = await client.request('test1');
    const r2 = await client.request('test2');
    const r3 = await client.request('test3');

    expect((r1.result as { seq: number }).seq).toBe(1);
    expect((r2.result as { seq: number }).seq).toBe(2);
    expect((r3.result as { seq: number }).seq).toBe(3);
  });

  it('should handle concurrent requests with proper ID correlation', async () => {
    const pipeName = uniquePipeName();
    server = createMockPipeServer(pipeName, (req) => ({
      id: req.id,
      result: { method: req.method },
    }));

    client = new NamedPipeClient({ pipeName, reconnect: false });
    await client.connect();

    const [r1, r2, r3] = await Promise.all([
      client.request('method_a'),
      client.request('method_b'),
      client.request('method_c'),
    ]);

    expect((r1.result as { method: string }).method).toBe('method_a');
    expect((r2.result as { method: string }).method).toBe('method_b');
    expect((r3.result as { method: string }).method).toBe('method_c');
  });

  it('should return error response from server', async () => {
    const pipeName = uniquePipeName();
    server = createMockPipeServer(pipeName, (req) => ({
      id: req.id,
      error: { code: -32601, message: 'Method not found' },
    }));

    client = new NamedPipeClient({ pipeName, reconnect: false });
    await client.connect();

    const response = await client.request('unknown_method');
    expect(response.error).toBeDefined();
    expect(response.error!.code).toBe(-32601);
    expect(response.error!.message).toBe('Method not found');
  });

  // --- Error Handling Tests ---

  it('should throw when requesting without connection', async () => {
    client = new NamedPipeClient({ reconnect: false });
    await expect(client.request('test')).rejects.toThrow('Not connected');
  });

  it('should timeout on slow server response', async () => {
    const pipeName = uniquePipeName();
    // Server that accepts but never responds
    const sockets: net.Socket[] = [];
    server = net.createServer((s) => { sockets.push(s); });
    server.listen(`\\\\.\\pipe\\${pipeName}`);

    client = new NamedPipeClient({
      pipeName,
      requestTimeout: 500,
      reconnect: false,
    });
    await client.connect();

    await expect(client.request('slow')).rejects.toThrow('timeout');

    // Clean up sockets so server.close() doesn't hang
    client.disconnect();
    for (const s of sockets) s.destroy();
  });

  // --- Disconnect Tests ---

  it('should clean up on disconnect', async () => {
    const pipeName = uniquePipeName();
    server = createMockPipeServer(pipeName, (req) => ({
      id: req.id,
      result: null,
    }));

    client = new NamedPipeClient({ pipeName, reconnect: false });
    await client.connect();
    expect(client.connected).toBe(true);

    client.disconnect();
    expect(client.connected).toBe(false);
  });

  it('should reject pending requests on disconnect', async () => {
    const pipeName = uniquePipeName();
    // Server that never responds
    const sockets: net.Socket[] = [];
    server = net.createServer((s) => { sockets.push(s); });
    server.listen(`\\\\.\\pipe\\${pipeName}`);

    client = new NamedPipeClient({
      pipeName,
      requestTimeout: 5000,
      reconnect: false,
    });
    await client.connect();

    const requestPromise = client.request('pending');
    // Disconnect while request is pending
    client.disconnect();

    await expect(requestPromise).rejects.toThrow('disconnected');

    for (const s of sockets) s.destroy();
  });

  // --- Message Framing Tests ---

  it('should handle messages split across multiple data events', async () => {
    const pipeName = uniquePipeName();

    // Custom server that sends response in tiny chunks
    server = net.createServer((socket) => {
      let buffer = Buffer.alloc(0);
      socket.on('data', (data) => {
        buffer = Buffer.concat([buffer, data]);
        while (buffer.length >= 4) {
          const len = buffer.readInt32LE(0);
          if (buffer.length < 4 + len) break;
          const body = buffer.subarray(4, 4 + len);
          buffer = buffer.subarray(4 + len);

          const req = JSON.parse(body.toString('utf-8'));
          const resp = JSON.stringify({ id: req.id, result: { ok: true } });
          const respBuf = Buffer.from(resp, 'utf-8');
          const lenBuf = Buffer.alloc(4);
          lenBuf.writeInt32LE(respBuf.length, 0);

          const full = Buffer.concat([lenBuf, respBuf]);
          // Send in 2-byte chunks with delays
          let offset = 0;
          const interval = setInterval(() => {
            if (offset >= full.length) {
              clearInterval(interval);
              return;
            }
            const end = Math.min(offset + 2, full.length);
            socket.write(full.subarray(offset, end));
            offset = end;
          }, 5);
        }
      });
    });
    server.listen(`\\\\.\\pipe\\${pipeName}`);

    client = new NamedPipeClient({ pipeName, reconnect: false });
    await client.connect();

    const response = await client.request('chunked');
    expect(response.result).toEqual({ ok: true });
  });
});
