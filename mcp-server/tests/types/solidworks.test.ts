import { describe, it, expect } from 'vitest';
import {
  PipeErrorCodes,
  SwDocumentType,
  SwExportFormat,
  SwReferencePlane,
  SwMateType,
  swDocTypeFromExtension,
  isPipeResponse,
  isPipeError,
} from '../../src/types/solidworks.js';

describe('PipeErrorCodes', () => {
  it('should match C# PipeErrorCodes values', () => {
    expect(PipeErrorCodes.MethodNotFound).toBe(-32601);
    expect(PipeErrorCodes.InvalidParams).toBe(-32602);
    expect(PipeErrorCodes.InternalError).toBe(-32603);
    expect(PipeErrorCodes.SolidWorksNotConnected).toBe(-32000);
    expect(PipeErrorCodes.SolidWorksOperationFailed).toBe(-32001);
  });
});

describe('SwDocumentType', () => {
  it('should have correct numeric values', () => {
    expect(SwDocumentType.Part).toBe(1);
    expect(SwDocumentType.Assembly).toBe(2);
    expect(SwDocumentType.Drawing).toBe(3);
  });
});

describe('swDocTypeFromExtension', () => {
  it('should map .sldprt to Part', () => {
    expect(swDocTypeFromExtension('.sldprt')).toBe(SwDocumentType.Part);
    expect(swDocTypeFromExtension('sldprt')).toBe(SwDocumentType.Part);
    expect(swDocTypeFromExtension('.SLDPRT')).toBe(SwDocumentType.Part);
  });

  it('should map .sldasm to Assembly', () => {
    expect(swDocTypeFromExtension('.sldasm')).toBe(SwDocumentType.Assembly);
  });

  it('should map .slddrw to Drawing', () => {
    expect(swDocTypeFromExtension('.slddrw')).toBe(SwDocumentType.Drawing);
  });

  it('should return null for unknown extensions', () => {
    expect(swDocTypeFromExtension('.step')).toBeNull();
    expect(swDocTypeFromExtension('.txt')).toBeNull();
    expect(swDocTypeFromExtension('')).toBeNull();
  });
});

describe('SwExportFormat', () => {
  it('should have string values', () => {
    expect(SwExportFormat.STEP).toBe('step');
    expect(SwExportFormat.STL).toBe('stl');
    expect(SwExportFormat.PDF).toBe('pdf');
    expect(SwExportFormat.PNG).toBe('png');
  });
});

describe('isPipeResponse', () => {
  it('should return true for valid PipeResponse', () => {
    expect(isPipeResponse({ id: 'req-1', result: { ok: true } })).toBe(true);
    expect(isPipeResponse({ id: 'req-2', error: { code: -1, message: 'fail' } })).toBe(true);
  });

  it('should return false for invalid objects', () => {
    expect(isPipeResponse(null)).toBe(false);
    expect(isPipeResponse(undefined)).toBe(false);
    expect(isPipeResponse({})).toBe(false);
    expect(isPipeResponse({ id: 123 })).toBe(false); // id not string
    expect(isPipeResponse('string')).toBe(false);
  });
});

describe('isPipeError', () => {
  it('should return true for valid PipeError', () => {
    expect(isPipeError({ code: -32601, message: 'Not found' })).toBe(true);
  });

  it('should return false for invalid objects', () => {
    expect(isPipeError(null)).toBe(false);
    expect(isPipeError({})).toBe(false);
    expect(isPipeError({ code: 'str', message: 123 })).toBe(false);
  });
});

describe('SwReferencePlane', () => {
  it('should have correct string values', () => {
    expect(SwReferencePlane.Front).toBe('Front');
    expect(SwReferencePlane.Top).toBe('Top');
    expect(SwReferencePlane.Right).toBe('Right');
  });
});

describe('SwMateType', () => {
  it('should have correct numeric values', () => {
    expect(SwMateType.Coincident).toBe(0);
    expect(SwMateType.Distance).toBe(5);
    expect(SwMateType.Angle).toBe(6);
  });
});
