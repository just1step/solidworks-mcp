import { z } from 'zod';
import type { NamedPipeClient } from '../transport/named-pipe-client.js';

// ── Response shapes from C# bridge ─────────────────────────────

export interface SwFeatureInfo {
  name: string;   // SolidWorks feature name, e.g. "Boss-Extrude1"
  type: string;   // "Extrude" | "ExtrudeCut" | "Revolve" | "RevolveCut" | "Fillet" | "Chamfer" | "Shell" | "SimpleHole"
}

// ── Shared enum ─────────────────────────────────────────────────

/** End condition for extrude/cut. Matches C# EndCondition enum values (swEndConditions_e). */
export const EndConditionSchema = z
  .enum(['Blind', 'ThroughAll', 'MidPlane'])
  .default('Blind')
  .describe('End condition: Blind (fixed depth), ThroughAll, or MidPlane');

// ── Zod input schemas ───────────────────────────────────────────

export const ExtrudeSchema = z.object({
  depth: z.number().positive().describe('Extrusion depth (meters). Used when endCondition=Blind.'),
  endCondition: EndConditionSchema.optional(),
  flipDirection: z
    .boolean()
    .default(false)
    .describe('Flip the extrude direction if true'),
});

export const ExtrudeCutSchema = z.object({
  depth: z.number().positive().describe('Cut depth (meters). Used when endCondition=Blind.'),
  endCondition: EndConditionSchema.optional(),
  flipDirection: z
    .boolean()
    .default(false)
    .describe('Flip the cut direction if true'),
});

export const RevolveSchema = z.object({
  angleDegrees: z
    .number()
    .positive()
    .max(360)
    .describe('Revolve angle in degrees (0–360)'),
  isCut: z
    .boolean()
    .default(false)
    .describe('If true, creates a revolve cut; otherwise a boss revolve'),
});

export const FilletSchema = z.object({
  radius: z.number().positive().describe('Fillet radius (meters)'),
});

export const ChamferSchema = z.object({
  distance: z.number().positive().describe('Chamfer distance (meters)'),
});

export const ShellSchema = z.object({
  thickness: z.number().positive().describe('Shell wall thickness (meters)'),
});

export const SimpleHoleSchema = z.object({
  diameter: z.number().positive().describe('Hole diameter (meters)'),
  depth: z.number().positive().describe('Hole depth (meters)'),
});

// ── Tool handler functions ──────────────────────────────────────

export async function swExtrude(
  client: NamedPipeClient,
  params: z.input<typeof ExtrudeSchema>,
): Promise<SwFeatureInfo> {
  const resp = await client.request('sw.feature.extrude', {
    depth: params.depth,
    ...(params.endCondition ? { endCondition: params.endCondition } : {}),
    ...(params.flipDirection !== undefined ? { flipDirection: params.flipDirection } : {}),
  });
  if (resp.error) throw new Error(`sw.feature.extrude failed: ${resp.error.message}`);
  return resp.result as SwFeatureInfo;
}

export async function swExtrudeCut(
  client: NamedPipeClient,
  params: z.input<typeof ExtrudeCutSchema>,
): Promise<SwFeatureInfo> {
  const resp = await client.request('sw.feature.extrude_cut', {
    depth: params.depth,
    ...(params.endCondition ? { endCondition: params.endCondition } : {}),
    ...(params.flipDirection !== undefined ? { flipDirection: params.flipDirection } : {}),
  });
  if (resp.error) throw new Error(`sw.feature.extrude_cut failed: ${resp.error.message}`);
  return resp.result as SwFeatureInfo;
}

export async function swRevolve(
  client: NamedPipeClient,
  params: z.input<typeof RevolveSchema>,
): Promise<SwFeatureInfo> {
  const resp = await client.request('sw.feature.revolve', {
    angleDegrees: params.angleDegrees,
    isCut: params.isCut ?? false,
  });
  if (resp.error) throw new Error(`sw.feature.revolve failed: ${resp.error.message}`);
  return resp.result as SwFeatureInfo;
}

export async function swFillet(
  client: NamedPipeClient,
  params: z.infer<typeof FilletSchema>,
): Promise<SwFeatureInfo> {
  const resp = await client.request('sw.feature.fillet', { radius: params.radius });
  if (resp.error) throw new Error(`sw.feature.fillet failed: ${resp.error.message}`);
  return resp.result as SwFeatureInfo;
}

export async function swChamfer(
  client: NamedPipeClient,
  params: z.infer<typeof ChamferSchema>,
): Promise<SwFeatureInfo> {
  const resp = await client.request('sw.feature.chamfer', { distance: params.distance });
  if (resp.error) throw new Error(`sw.feature.chamfer failed: ${resp.error.message}`);
  return resp.result as SwFeatureInfo;
}

export async function swShell(
  client: NamedPipeClient,
  params: z.infer<typeof ShellSchema>,
): Promise<SwFeatureInfo> {
  const resp = await client.request('sw.feature.shell', { thickness: params.thickness });
  if (resp.error) throw new Error(`sw.feature.shell failed: ${resp.error.message}`);
  return resp.result as SwFeatureInfo;
}

export async function swSimpleHole(
  client: NamedPipeClient,
  params: z.infer<typeof SimpleHoleSchema>,
): Promise<SwFeatureInfo> {
  const resp = await client.request('sw.feature.simple_hole', {
    diameter: params.diameter,
    depth: params.depth,
  });
  if (resp.error) throw new Error(`sw.feature.simple_hole failed: ${resp.error.message}`);
  return resp.result as SwFeatureInfo;
}
