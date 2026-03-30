import * as net from 'node:net';
import { randomUUID } from 'node:crypto';
import type { PipeRequest, PipeResponse } from '../types/solidworks.js';
import { isPipeResponse } from '../types/solidworks.js';

export interface NamedPipeClientOptions {
  pipeName?: string;
  connectTimeout?: number;
  requestTimeout?: number;
  reconnect?: boolean;
  reconnectDelay?: number;
}

const DEFAULT_OPTIONS: Required<NamedPipeClientOptions> = {
  pipeName: 'SolidWorksMcpBridge',
  connectTimeout: 10000,
  requestTimeout: 30000,
  reconnect: true,
  reconnectDelay: 2000,
};

/**
 * Named Pipe client for communicating with the C# SolidWorks Bridge.
 * Protocol: [4-byte LE length prefix][UTF-8 JSON body]
 * Supports request-response correlation via message IDs.
 */
export class NamedPipeClient {
  private socket: net.Socket | null = null;
  private readonly options: Required<NamedPipeClientOptions>;
  private readonly pending = new Map<string, {
    resolve: (value: PipeResponse) => void;
    reject: (reason: Error) => void;
    timer: ReturnType<typeof setTimeout>;
  }>();
  private receiveBuffer = Buffer.alloc(0);
  private _connected = false;
  private _connecting = false;

  constructor(options: NamedPipeClientOptions = {}) {
    this.options = { ...DEFAULT_OPTIONS, ...options };
  }

  get connected(): boolean {
    return this._connected;
  }

  get pipePath(): string {
    return `\\\\.\\pipe\\${this.options.pipeName}`;
  }

  /**
   * Connect to the Named Pipe server.
   */
  async connect(): Promise<void> {
    if (this._connected) return;
    if (this._connecting) return;

    this._connecting = true;

    return new Promise<void>((resolve, reject) => {
      const timer = setTimeout(() => {
        this.socket?.destroy();
        this._connecting = false;
        reject(new Error(`Connection timeout after ${this.options.connectTimeout}ms`));
      }, this.options.connectTimeout);

      this.socket = net.connect(this.pipePath, () => {
        clearTimeout(timer);
        this._connected = true;
        this._connecting = false;
        resolve();
      });

      this.socket.on('data', (data: Buffer) => this.onData(data));

      this.socket.on('error', (err: Error) => {
        clearTimeout(timer);
        if (this._connecting) {
          this._connecting = false;
          reject(err);
        }
        this.handleDisconnect(err);
      });

      this.socket.on('close', () => {
        this.handleDisconnect(new Error('Pipe closed'));
      });
    });
  }

  /**
   * Send a request and wait for the correlated response.
   */
  async request(method: string, params?: Record<string, unknown>): Promise<PipeResponse> {
    if (!this._connected || !this.socket) {
      throw new Error('Not connected to pipe server');
    }

    const id = randomUUID();
    const req: PipeRequest = { id, method, params };

    return new Promise<PipeResponse>((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pending.delete(id);
        reject(new Error(`Request timeout after ${this.options.requestTimeout}ms for method: ${method}`));
      }, this.options.requestTimeout);

      this.pending.set(id, { resolve, reject, timer });

      try {
        this.writeMessage(req);
      } catch (err) {
        clearTimeout(timer);
        this.pending.delete(id);
        reject(err);
      }
    });
  }

  /**
   * Disconnect from the pipe server.
   */
  disconnect(): void {
    this._connected = false;
    this._connecting = false;

    // Reject all pending requests
    for (const [id, entry] of this.pending) {
      clearTimeout(entry.timer);
      entry.reject(new Error('Client disconnected'));
      this.pending.delete(id);
    }

    if (this.socket) {
      this.socket.removeAllListeners();
      this.socket.destroy();
      this.socket = null;
    }

    this.receiveBuffer = Buffer.alloc(0);
  }

  /**
   * Write a length-prefixed JSON message to the pipe.
   */
  private writeMessage(msg: PipeRequest): void {
    const bodyStr = JSON.stringify(msg);
    const bodyBuf = Buffer.from(bodyStr, 'utf-8');
    const lengthBuf = Buffer.alloc(4);
    lengthBuf.writeInt32LE(bodyBuf.length, 0);

    this.socket!.write(lengthBuf);
    this.socket!.write(bodyBuf);
  }

  /**
   * Handle incoming data: buffer and extract complete messages.
   */
  private onData(data: Buffer): void {
    this.receiveBuffer = Buffer.concat([this.receiveBuffer, data]);

    while (this.receiveBuffer.length >= 4) {
      const msgLength = this.receiveBuffer.readInt32LE(0);

      if (msgLength <= 0 || msgLength > 10 * 1024 * 1024) {
        // Invalid message, clear buffer
        this.receiveBuffer = Buffer.alloc(0);
        return;
      }

      if (this.receiveBuffer.length < 4 + msgLength) {
        break; // Wait for more data
      }

      const bodyBytes = this.receiveBuffer.subarray(4, 4 + msgLength);
      this.receiveBuffer = this.receiveBuffer.subarray(4 + msgLength);

      try {
        const json = bodyBytes.toString('utf-8');
        const response: unknown = JSON.parse(json);

        if (isPipeResponse(response)) {
          this.resolveResponse(response);
        }
      } catch {
        // Malformed message, skip
      }
    }
  }

  /**
   * Match a response to its pending request by ID.
   */
  private resolveResponse(response: PipeResponse): void {
    const entry = this.pending.get(response.id);
    if (entry) {
      clearTimeout(entry.timer);
      this.pending.delete(response.id);
      entry.resolve(response);
    }
  }

  /**
   * Handle pipe disconnection.
   */
  private handleDisconnect(err: Error): void {
    const wasConnected = this._connected;
    this._connected = false;

    // Reject all pending
    for (const [id, entry] of this.pending) {
      clearTimeout(entry.timer);
      entry.reject(new Error(`Pipe disconnected: ${err.message}`));
      this.pending.delete(id);
    }

    if (this.socket) {
      this.socket.removeAllListeners();
      this.socket.destroy();
      this.socket = null;
    }

    this.receiveBuffer = Buffer.alloc(0);

    if (wasConnected && this.options.reconnect) {
      setTimeout(() => {
        this.connect().catch(() => {
          // Reconnect failed silently; will retry on next request
        });
      }, this.options.reconnectDelay);
    }
  }
}
