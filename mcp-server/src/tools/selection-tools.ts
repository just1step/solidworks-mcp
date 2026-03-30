import { z } from 'zod';
import type { NamedPipeClient } from '../transport/named-pipe-client.js';

// ── Response shapes from C# bridge ─────────────────────────────

export interface SwSelectionResult {
  success: boolean;
  message: string;
}

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
