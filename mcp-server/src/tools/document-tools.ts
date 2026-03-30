import { z } from 'zod';
import type { NamedPipeClient } from '../transport/named-pipe-client.js';

// ── Response shapes from C# bridge ─────────────────────────────

export interface SwDocumentInfo {
  path: string;
  title: string;
  type: number; // 1=Part, 2=Assembly, 3=Drawing
}

// ── Zod input schemas (validated by MCP SDK) ───────────────────

export const ConnectSchema = z.object({}).describe('Connect to SolidWorks (no params needed)');

export const DisconnectSchema = z.object({}).describe('Disconnect from SolidWorks');

export const NewDocumentSchema = z.object({
  type: z
    .enum(['Part', 'Assembly', 'Drawing'])
    .default('Part')
    .describe('Document type to create'),
  templatePath: z
    .string()
    .optional()
    .describe('Full path to template file (.prtdot / .asmdot / .drwdot). Uses SW default if omitted.'),
});

export const OpenDocumentSchema = z.object({
  path: z.string().min(1).describe('Full path to the SolidWorks file to open'),
});

export const CloseDocumentSchema = z.object({
  path: z.string().min(1).describe('Full path of the open document to close'),
});

export const SaveDocumentSchema = z.object({
  path: z.string().min(1).describe('Full path of the open document to save'),
});

export const ListDocumentsSchema = z
  .object({})
  .describe('List all currently open documents');

export const GetActiveDocumentSchema = z
  .object({})
  .describe('Get the currently active document');

// ── Tool handler functions ──────────────────────────────────────

export async function swConnect(client: NamedPipeClient): Promise<{ connected: boolean }> {
  const resp = await client.request('sw.connect');
  if (resp.error) throw new Error(`sw.connect failed: ${resp.error.message}`);
  return resp.result as { connected: boolean };
}

export async function swDisconnect(client: NamedPipeClient): Promise<{ connected: boolean }> {
  const resp = await client.request('sw.disconnect');
  if (resp.error) throw new Error(`sw.disconnect failed: ${resp.error.message}`);
  return resp.result as { connected: boolean };
}

export async function swNewDocument(
  client: NamedPipeClient,
  params: z.infer<typeof NewDocumentSchema>,
): Promise<SwDocumentInfo> {
  const resp = await client.request('sw.new_document', {
    type: params.type,
    ...(params.templatePath ? { templatePath: params.templatePath } : {}),
  });
  if (resp.error) throw new Error(`sw.new_document failed: ${resp.error.message}`);
  return resp.result as SwDocumentInfo;
}

export async function swOpenDocument(
  client: NamedPipeClient,
  params: z.infer<typeof OpenDocumentSchema>,
): Promise<SwDocumentInfo> {
  const resp = await client.request('sw.open_document', { path: params.path });
  if (resp.error) throw new Error(`sw.open_document failed: ${resp.error.message}`);
  return resp.result as SwDocumentInfo;
}

export async function swCloseDocument(
  client: NamedPipeClient,
  params: z.infer<typeof CloseDocumentSchema>,
): Promise<{ closed: boolean }> {
  const resp = await client.request('sw.close_document', { path: params.path });
  if (resp.error) throw new Error(`sw.close_document failed: ${resp.error.message}`);
  return resp.result as { closed: boolean };
}

export async function swSaveDocument(
  client: NamedPipeClient,
  params: z.infer<typeof SaveDocumentSchema>,
): Promise<{ saved: boolean }> {
  const resp = await client.request('sw.save_document', { path: params.path });
  if (resp.error) throw new Error(`sw.save_document failed: ${resp.error.message}`);
  return resp.result as { saved: boolean };
}

export async function swListDocuments(client: NamedPipeClient): Promise<SwDocumentInfo[]> {
  const resp = await client.request('sw.list_documents');
  if (resp.error) throw new Error(`sw.list_documents failed: ${resp.error.message}`);
  return (resp.result as SwDocumentInfo[]) ?? [];
}

export async function swGetActiveDocument(
  client: NamedPipeClient,
): Promise<SwDocumentInfo | null> {
  const resp = await client.request('sw.get_active_document');
  if (resp.error) throw new Error(`sw.get_active_document failed: ${resp.error.message}`);
  return (resp.result as SwDocumentInfo | null) ?? null;
}
