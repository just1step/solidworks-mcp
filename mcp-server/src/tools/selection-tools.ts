import { z } from 'zod';
import type { NamedPipeClient } from '../transport/named-pipe-client.js';

// ── Response shapes from C# bridge ─────────────────────────────

export interface SwSelectionResult {
  success: boolean;
  message: string;
}

export interface SwSelectableEntityInfo {
  index: number;
  entityType: 'Face' | 'Edge' | 'Vertex';
  componentName: string | null;
  box: number[] | null;
}

export const SelectableEntityTypeSchema = z
  .enum(['Face', 'Edge', 'Vertex'])
  .describe('Topology entity kind: Face, Edge, or Vertex');

// ── Zod input schemas ───────────────────────────────────────────

export const SelectByNameSchema = z.object({
  name: z.string().min(1).describe('Name of the entity to select (e.g. "前视基准面")'),
  selType: z
    .string()
    .min(1)
    .describe(
      'Selection type string used by SolidWorks, e.g. "PLANE", "EDGE", "FACE", "VERTEX"',
    ),
});

export const ClearSelectionSchema = z.object({}).describe('Clear all current selections');

export const ListEntitiesSchema = z.object({
  entityType: SelectableEntityTypeSchema.optional(),
  componentName: z
    .string()
    .min(1)
    .optional()
    .describe('Optional component name filter when the active document is an assembly'),
});

export const SelectEntitySchema = z.object({
  entityType: SelectableEntityTypeSchema,
  index: z.number().int().nonnegative().describe('Entity index returned by sw_list_entities'),
  append: z.boolean().default(false).describe('Append to the current selection if true'),
  mark: z.number().int().default(0).describe('Selection mark used by downstream SolidWorks APIs'),
  componentName: z
    .string()
    .min(1)
    .optional()
    .describe('Optional component name filter when the active document is an assembly'),
});

// ── Tool handler functions ──────────────────────────────────────

export async function swSelectByName(
  client: NamedPipeClient,
  params: z.infer<typeof SelectByNameSchema>,
): Promise<SwSelectionResult> {
  const resp = await client.request('sw.select.by_name', {
    name: params.name,
    selType: params.selType,
  });
  if (resp.error) throw new Error(`sw.select.by_name failed: ${resp.error.message}`);
  return resp.result as SwSelectionResult;
}

export async function swClearSelection(client: NamedPipeClient): Promise<void> {
  const resp = await client.request('sw.select.clear');
  if (resp.error) throw new Error(`sw.select.clear failed: ${resp.error.message}`);
}

export async function swListEntities(
  client: NamedPipeClient,
  params: z.input<typeof ListEntitiesSchema> = {},
): Promise<SwSelectableEntityInfo[]> {
  const resp = await client.request('sw.select.list_entities', {
    ...(params.entityType ? { entityType: params.entityType } : {}),
    ...(params.componentName ? { componentName: params.componentName } : {}),
  });
  if (resp.error) throw new Error(`sw.select.list_entities failed: ${resp.error.message}`);
  return (resp.result as SwSelectableEntityInfo[]) ?? [];
}

export async function swSelectEntity(
  client: NamedPipeClient,
  params: z.input<typeof SelectEntitySchema>,
): Promise<SwSelectionResult> {
  const resp = await client.request('sw.select.entity', {
    entityType: params.entityType,
    index: params.index,
    append: params.append ?? false,
    mark: params.mark ?? 0,
    ...(params.componentName ? { componentName: params.componentName } : {}),
  });
  if (resp.error) throw new Error(`sw.select.entity failed: ${resp.error.message}`);
  return resp.result as SwSelectionResult;
}
