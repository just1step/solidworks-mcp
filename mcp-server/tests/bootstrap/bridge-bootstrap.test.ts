import { describe, expect, it, vi } from 'vitest';
import {
  ensureBridgeReady,
  ensurePipeClientReady,
} from '../../src/bootstrap/bridge-bootstrap.js';

describe('bridge bootstrap', () => {
  it('does not start bridge when pipe is already ready', async () => {
    const startBridgeProcess = vi.fn<() => Promise<void>>().mockResolvedValue();
    const probePipe = vi.fn<() => Promise<boolean>>().mockResolvedValue(true);

    await ensureBridgeReady({
      probePipe,
      startBridgeProcess,
    });

    expect(probePipe).toHaveBeenCalledTimes(1);
    expect(startBridgeProcess).not.toHaveBeenCalled();
  });

  it('starts bridge and waits until the pipe becomes ready', async () => {
    const startBridgeProcess = vi.fn<() => Promise<void>>().mockResolvedValue();
    const probePipe = vi
      .fn<() => Promise<boolean>>()
      .mockResolvedValueOnce(false)
      .mockResolvedValueOnce(false)
      .mockResolvedValueOnce(true);

    await ensureBridgeReady({
      probePipe,
      startBridgeProcess,
      retryDelayMs: 0,
      startupTimeoutMs: 100,
    });

    expect(startBridgeProcess).toHaveBeenCalledTimes(1);
    expect(probePipe).toHaveBeenCalledTimes(3);
  });

  it('connects the pipe client after ensuring bridge readiness', async () => {
    const client = {
      connected: false,
      connect: vi.fn<() => Promise<void>>().mockResolvedValue(),
    };
    const startBridgeProcess = vi.fn<() => Promise<void>>().mockResolvedValue();
    const probePipe = vi.fn<() => Promise<boolean>>().mockResolvedValue(true);

    await ensurePipeClientReady(client, {
      probePipe,
      startBridgeProcess,
    });

    expect(startBridgeProcess).not.toHaveBeenCalled();
    expect(client.connect).toHaveBeenCalledTimes(1);
  });

  it('retries the real client connection after bridge startup', async () => {
    const client = {
      connected: false,
      connect: vi
        .fn<() => Promise<void>>()
        .mockRejectedValueOnce(new Error('connect failed'))
        .mockRejectedValueOnce(new Error('pipe not ready'))
        .mockResolvedValueOnce(),
    };
    const startBridgeProcess = vi.fn<() => Promise<void>>().mockResolvedValue();
    const probePipe = vi
      .fn<() => Promise<boolean>>()
      .mockResolvedValueOnce(false)
      .mockResolvedValueOnce(true);

    await ensurePipeClientReady(client, {
      probePipe,
      startBridgeProcess,
      retryDelayMs: 0,
      startupTimeoutMs: 100,
    });

    expect(startBridgeProcess).toHaveBeenCalledTimes(1);
    expect(client.connect).toHaveBeenCalledTimes(3);
  });

  it('does nothing when the pipe client is already connected', async () => {
    const client = {
      connected: true,
      connect: vi.fn<() => Promise<void>>().mockResolvedValue(),
    };
    const startBridgeProcess = vi.fn<() => Promise<void>>().mockResolvedValue();
    const probePipe = vi.fn<() => Promise<boolean>>().mockResolvedValue(true);

    await ensurePipeClientReady(client, {
      probePipe,
      startBridgeProcess,
    });

    expect(probePipe).not.toHaveBeenCalled();
    expect(startBridgeProcess).not.toHaveBeenCalled();
    expect(client.connect).not.toHaveBeenCalled();
  });
});