import { z } from 'zod';
import type { NamedPipeClient } from '../transport/named-pipe-client.js';

// ── Response shapes from C# bridge ─────────────────────────────

export interface SwComponentInfo {
  name: string;   // e.g. "Part1-1"
  path: string;   // full file path
}

// ── Shared schemas ──────────────────────────────────────────────

export const MateAlignSchema = z
  .enum(['None', 'AntiAligned', 'Closest'])
  .default('Closest')
  .describe('Mate alignment: None, AntiAligned, or Closest');

// ── Zod input schemas ───────────────────────────────────────────

export const InsertComponentSchema = z.object({
  filePath: z
    .string()
    .min(1)
    .describe('Full path to the part or assembly file to insert (.sldprt / .sldasm)'),
  x: z.number().default(0).describe('X position to insert the component (meters)'),
  y: z.number().default(0).describe('Y position to insert the component (meters)'),
  z: z.number().default(0).describe('Z position to insert the component (meters)'),
});

export const AddMateCoincidentSchema = z.object({
  align: MateAlignSchema.optional(),
});

export const AddMateConcentricSchema = z.object({
  align: MateAlignSchema.optional(),
});

export const AddMateParallelSchema = z.object({
  align: MateAlignSchema.optional(),
});

export const AddMateDistanceSchema = z.object({
  distance: z.number().nonnegative().describe('Mate distance (meters)'),
  align: MateAlignSchema.optional(),
});

export const AddMateAngleSchema = z.object({
  angleDegrees: z.number().describe('Mate angle (degrees)'),
  align: MateAlignSchema.optional(),
});

export const ListComponentsSchema = z
  .object({})
  .describe('List all top-level components in the active assembly');

// ── Tool handler functions ──────────────────────────────────────

export async function swInsertComponent(
  client: NamedPipeClient,
  params: z.input<typeof InsertComponentSchema>,
): Promise<SwComponentInfo> {
  const resp = await client.request('sw.assembly.insert_component', {
    filePath: params.filePath,
    x: params.x ?? 0,
    y: params.y ?? 0,
    z: params.z ?? 0,
  });
  if (resp.error) throw new Error(`sw.assembly.insert_component failed: ${resp.error.message}`);
  return resp.result as SwComponentInfo;
}

export async function swAddMateCoincident(
  client: NamedPipeClient,
  params: z.infer<typeof AddMateCoincidentSchema>,
): Promise<void> {
  const resp = await client.request('sw.assembly.add_mate_coincident', {
    ...(params.align ? { align: params.align } : {}),
  });
  if (resp.error) throw new Error(`sw.assembly.add_mate_coincident failed: ${resp.error.message}`);
}

export async function swAddMateConcentric(
  client: NamedPipeClient,
  params: z.infer<typeof AddMateConcentricSchema>,
): Promise<void> {
  const resp = await client.request('sw.assembly.add_mate_concentric', {
    ...(params.align ? { align: params.align } : {}),
  });
  if (resp.error) throw new Error(`sw.assembly.add_mate_concentric failed: ${resp.error.message}`);
}

export async function swAddMateParallel(
  client: NamedPipeClient,
  params: z.infer<typeof AddMateParallelSchema>,
): Promise<void> {
  const resp = await client.request('sw.assembly.add_mate_parallel', {
    ...(params.align ? { align: params.align } : {}),
  });
  if (resp.error) throw new Error(`sw.assembly.add_mate_parallel failed: ${resp.error.message}`);
}

export async function swAddMateDistance(
  client: NamedPipeClient,
  params: z.infer<typeof AddMateDistanceSchema>,
): Promise<void> {
  const resp = await client.request('sw.assembly.add_mate_distance', {
    distance: params.distance,
    ...(params.align ? { align: params.align } : {}),
  });
  if (resp.error) throw new Error(`sw.assembly.add_mate_distance failed: ${resp.error.message}`);
}

export async function swAddMateAngle(
  client: NamedPipeClient,
  params: z.infer<typeof AddMateAngleSchema>,
): Promise<void> {
  const resp = await client.request('sw.assembly.add_mate_angle', {
    angleDegrees: params.angleDegrees,
    ...(params.align ? { align: params.align } : {}),
  });
  if (resp.error) throw new Error(`sw.assembly.add_mate_angle failed: ${resp.error.message}`);
}

export async function swListComponents(
  client: NamedPipeClient,
): Promise<SwComponentInfo[]> {
  const resp = await client.request('sw.assembly.list_components');
  if (resp.error) throw new Error(`sw.assembly.list_components failed: ${resp.error.message}`);
  return (resp.result as SwComponentInfo[]) ?? [];
}
