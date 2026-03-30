import { describe, it, expect } from 'vitest';
import { SERVER_NAME, SERVER_VERSION } from '../src/index.js';

describe('mcp-server setup', () => {
  it('should export server metadata', () => {
    expect(SERVER_NAME).toBe('solidworks-mcp-server');
    expect(SERVER_VERSION).toBe('0.1.0');
  });
});
