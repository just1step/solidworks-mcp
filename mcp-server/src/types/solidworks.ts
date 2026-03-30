/**
 * IPC message types matching C# PipeMessage models.
 * Protocol: [4-byte LE length][UTF-8 JSON body]
 */

// --- IPC Protocol Types ---

export interface PipeRequest {
  id: string;
  method: string;
  params?: Record<string, unknown>;
}

export interface PipeResponse {
  id: string;
  result?: unknown;
  error?: PipeError;
}

export interface PipeError {
  code: number;
  message: string;
}

// --- Error Codes (matching C# PipeErrorCodes) ---

export const PipeErrorCodes = {
  MethodNotFound: -32601,
  InvalidParams: -32602,
  InternalError: -32603,
  SolidWorksNotConnected: -32000,
  SolidWorksOperationFailed: -32001,
} as const;

// --- SolidWorks Document Types ---

export enum SwDocumentType {
  Part = 1,
  Assembly = 2,
  Drawing = 3,
}

export function swDocTypeFromExtension(ext: string): SwDocumentType | null {
  switch (ext.toLowerCase().replace('.', '')) {
    case 'sldprt':
      return SwDocumentType.Part;
    case 'sldasm':
      return SwDocumentType.Assembly;
    case 'slddrw':
      return SwDocumentType.Drawing;
    default:
      return null;
  }
}

// --- Export Formats ---

export enum SwExportFormat {
  STEP = 'step',
  IGES = 'iges',
  STL = 'stl',
  ThreeMF = '3mf',
  Parasolid = 'x_t',
  PDF = 'pdf',
  DWG = 'dwg',
  DXF = 'dxf',
  PNG = 'png',
  BMP = 'bmp',
}

// --- Type Guards ---

export function isPipeResponse(obj: unknown): obj is PipeResponse {
  return (
    typeof obj === 'object' &&
    obj !== null &&
    'id' in obj &&
    typeof (obj as PipeResponse).id === 'string'
  );
}

export function isPipeError(error: unknown): error is PipeError {
  return (
    typeof error === 'object' &&
    error !== null &&
    'code' in error &&
    'message' in error &&
    typeof (error as PipeError).code === 'number' &&
    typeof (error as PipeError).message === 'string'
  );
}

// --- Sketch Plane References ---

export enum SwReferencePlane {
  Front = 'Front',
  Top = 'Top',
  Right = 'Right',
}

// --- Mate Types ---

export enum SwMateType {
  Coincident = 0,
  Concentric = 1,
  Perpendicular = 2,
  Parallel = 3,
  Tangent = 4,
  Distance = 5,
  Angle = 6,
}
