import { z } from 'zod';
import type { NamedPipeClient } from '../transport/named-pipe-client.js';

// ── Response shapes from C# bridge ─────────────────────────────

export interface SwSketchEntityInfo {
  type: string;   // "Line" | "Circle" | "Rectangle" | "Arc"
  id: string;     // segment identifier
}

// ── Zod input schemas ───────────────────────────────────────────

export const InsertSketchSchema = z
  .object({})
  .describe('Open a new sketch on the currently selected plane or face');

export const FinishSketchSchema = z
  .object({})
  .describe('Close (finish) the currently open sketch');

export const AddPointSchema = z.object({
  x: z.number().describe('Point X coordinate (meters)'),
  y: z.number().describe('Point Y coordinate (meters)'),
});

export const AddEllipseSchema = z.object({
  cx: z.number().describe('Ellipse center X coordinate (meters)'),
  cy: z.number().describe('Ellipse center Y coordinate (meters)'),
  majorX: z.number().describe('Major-axis point X coordinate (meters)'),
  majorY: z.number().describe('Major-axis point Y coordinate (meters)'),
  minorX: z.number().describe('Minor-axis point X coordinate (meters)'),
  minorY: z.number().describe('Minor-axis point Y coordinate (meters)'),
});

export const AddPolygonSchema = z.object({
  cx: z.number().describe('Polygon center X coordinate (meters)'),
  cy: z.number().describe('Polygon center Y coordinate (meters)'),
  x: z.number().describe('A point on the polygon perimeter X coordinate (meters)'),
  y: z.number().describe('A point on the polygon perimeter Y coordinate (meters)'),
  sides: z.number().int().min(3).describe('Number of polygon sides'),
  inscribed: z.boolean().default(true).describe('True for inscribed polygon, false for circumscribed polygon'),
});

export const AddTextSchema = z.object({
  x: z.number().describe('Text anchor X coordinate (meters)'),
  y: z.number().describe('Text anchor Y coordinate (meters)'),
  text: z.string().min(1).describe('Sketch text content'),
});

export const AddLineSchema = z.object({
  x1: z.number().describe('Start X coordinate (meters)'),
  y1: z.number().describe('Start Y coordinate (meters)'),
  x2: z.number().describe('End X coordinate (meters)'),
  y2: z.number().describe('End Y coordinate (meters)'),
});

export const AddCircleSchema = z.object({
  cx: z.number().describe('Center X coordinate (meters)'),
  cy: z.number().describe('Center Y coordinate (meters)'),
  radius: z.number().positive().describe('Circle radius (meters)'),
});

export const AddRectangleSchema = z.object({
  x1: z.number().describe('First corner X coordinate (meters)'),
  y1: z.number().describe('First corner Y coordinate (meters)'),
  x2: z.number().describe('Opposite corner X coordinate (meters)'),
  y2: z.number().describe('Opposite corner Y coordinate (meters)'),
});

export const AddArcSchema = z.object({
  cx: z.number().describe('Center X coordinate (meters)'),
  cy: z.number().describe('Center Y coordinate (meters)'),
  x1: z.number().describe('Start point X coordinate (meters)'),
  y1: z.number().describe('Start point Y coordinate (meters)'),
  x2: z.number().describe('End point X coordinate (meters)'),
  y2: z.number().describe('End point Y coordinate (meters)'),
  direction: z
    .number()
    .int()
    .refine((v) => v === 1 || v === -1, { message: 'direction must be 1 (CCW) or -1 (CW)' })
    .describe('Arc direction: 1 = counter-clockwise, -1 = clockwise'),
});

// ── Tool handler functions ──────────────────────────────────────

export async function swInsertSketch(client: NamedPipeClient): Promise<void> {
  const resp = await client.request('sw.sketch.insert');
  if (resp.error) throw new Error(`sw.sketch.insert failed: ${resp.error.message}`);
}

export async function swFinishSketch(client: NamedPipeClient): Promise<void> {
  const resp = await client.request('sw.sketch.finish');
  if (resp.error) throw new Error(`sw.sketch.finish failed: ${resp.error.message}`);
}

export async function swAddPoint(
  client: NamedPipeClient,
  params: z.infer<typeof AddPointSchema>,
): Promise<SwSketchEntityInfo> {
  const resp = await client.request('sw.sketch.add_point', params);
  if (resp.error) throw new Error(`sw.sketch.add_point failed: ${resp.error.message}`);
  return resp.result as SwSketchEntityInfo;
}

export async function swAddEllipse(
  client: NamedPipeClient,
  params: z.infer<typeof AddEllipseSchema>,
): Promise<SwSketchEntityInfo> {
  const resp = await client.request('sw.sketch.add_ellipse', params);
  if (resp.error) throw new Error(`sw.sketch.add_ellipse failed: ${resp.error.message}`);
  return resp.result as SwSketchEntityInfo;
}

export async function swAddPolygon(
  client: NamedPipeClient,
  params: z.infer<typeof AddPolygonSchema>,
): Promise<SwSketchEntityInfo> {
  const resp = await client.request('sw.sketch.add_polygon', params);
  if (resp.error) throw new Error(`sw.sketch.add_polygon failed: ${resp.error.message}`);
  return resp.result as SwSketchEntityInfo;
}

export async function swAddText(
  client: NamedPipeClient,
  params: z.infer<typeof AddTextSchema>,
): Promise<SwSketchEntityInfo> {
  const resp = await client.request('sw.sketch.add_text', params);
  if (resp.error) throw new Error(`sw.sketch.add_text failed: ${resp.error.message}`);
  return resp.result as SwSketchEntityInfo;
}

export async function swAddLine(
  client: NamedPipeClient,
  params: z.infer<typeof AddLineSchema>,
): Promise<SwSketchEntityInfo> {
  const resp = await client.request('sw.sketch.add_line', params);
  if (resp.error) throw new Error(`sw.sketch.add_line failed: ${resp.error.message}`);
  return resp.result as SwSketchEntityInfo;
}

export async function swAddCircle(
  client: NamedPipeClient,
  params: z.infer<typeof AddCircleSchema>,
): Promise<SwSketchEntityInfo> {
  const resp = await client.request('sw.sketch.add_circle', params);
  if (resp.error) throw new Error(`sw.sketch.add_circle failed: ${resp.error.message}`);
  return resp.result as SwSketchEntityInfo;
}

export async function swAddRectangle(
  client: NamedPipeClient,
  params: z.infer<typeof AddRectangleSchema>,
): Promise<SwSketchEntityInfo> {
  const resp = await client.request('sw.sketch.add_rectangle', params);
  if (resp.error) throw new Error(`sw.sketch.add_rectangle failed: ${resp.error.message}`);
  return resp.result as SwSketchEntityInfo;
}

export async function swAddArc(
  client: NamedPipeClient,
  params: z.infer<typeof AddArcSchema>,
): Promise<SwSketchEntityInfo> {
  const resp = await client.request('sw.sketch.add_arc', params);
  if (resp.error) throw new Error(`sw.sketch.add_arc failed: ${resp.error.message}`);
  return resp.result as SwSketchEntityInfo;
}
