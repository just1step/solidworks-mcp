import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from '@modelcontextprotocol/sdk/types.js';
import { z } from 'zod';
import { NamedPipeClient } from './transport/named-pipe-client.js';

// ── Tool modules ──────────────────────────────────────────────
import {
  ConnectSchema,
  DisconnectSchema,
  NewDocumentSchema,
  OpenDocumentSchema,
  CloseDocumentSchema,
  SaveDocumentSchema,
  ListDocumentsSchema,
  GetActiveDocumentSchema,
  swConnect,
  swDisconnect,
  swNewDocument,
  swOpenDocument,
  swCloseDocument,
  swSaveDocument,
  swListDocuments,
  swGetActiveDocument,
} from './tools/document-tools.js';

import {
  SelectByNameSchema,
  ClearSelectionSchema,
  swSelectByName,
  swClearSelection,
} from './tools/selection-tools.js';

import {
  InsertSketchSchema,
  FinishSketchSchema,
  AddLineSchema,
  AddCircleSchema,
  AddRectangleSchema,
  AddArcSchema,
  swInsertSketch,
  swFinishSketch,
  swAddLine,
  swAddCircle,
  swAddRectangle,
  swAddArc,
} from './tools/sketch-tools.js';

import {
  ExtrudeSchema,
  ExtrudeCutSchema,
  RevolveSchema,
  FilletSchema,
  ChamferSchema,
  ShellSchema,
  SimpleHoleSchema,
  swExtrude,
  swExtrudeCut,
  swRevolve,
  swFillet,
  swChamfer,
  swShell,
  swSimpleHole,
} from './tools/feature-tools.js';

import {
  InsertComponentSchema,
  AddMateCoincidentSchema,
  AddMateConcentricSchema,
  AddMateParallelSchema,
  AddMateDistanceSchema,
  AddMateAngleSchema,
  ListComponentsSchema,
  swInsertComponent,
  swAddMateCoincident,
  swAddMateConcentric,
  swAddMateParallel,
  swAddMateDistance,
  swAddMateAngle,
  swListComponents,
} from './tools/assembly-tools.js';

// ── Server metadata ───────────────────────────────────────────
export const SERVER_NAME = 'solidworks-mcp-server';
export const SERVER_VERSION = '0.1.0';

// ── Tool definitions ──────────────────────────────────────────

const TOOLS = [
  // Document
  { name: 'sw_connect', description: 'Connect to the running SolidWorks instance', inputSchema: ConnectSchema },
  { name: 'sw_disconnect', description: 'Disconnect from SolidWorks', inputSchema: DisconnectSchema },
  { name: 'sw_new_document', description: 'Create a new SolidWorks document (Part, Assembly, or Drawing)', inputSchema: NewDocumentSchema },
  { name: 'sw_open_document', description: 'Open an existing SolidWorks document from disk', inputSchema: OpenDocumentSchema },
  { name: 'sw_close_document', description: 'Close an open SolidWorks document', inputSchema: CloseDocumentSchema },
  { name: 'sw_save_document', description: 'Save an open SolidWorks document', inputSchema: SaveDocumentSchema },
  { name: 'sw_list_documents', description: 'List all currently open SolidWorks documents', inputSchema: ListDocumentsSchema },
  { name: 'sw_get_active_document', description: 'Get the currently active SolidWorks document', inputSchema: GetActiveDocumentSchema },
  // Selection
  { name: 'sw_select_by_name', description: 'Select an entity in SolidWorks by name and type (e.g. plane, face, edge)', inputSchema: SelectByNameSchema },
  { name: 'sw_clear_selection', description: 'Clear all current selections in SolidWorks', inputSchema: ClearSelectionSchema },
  // Sketch
  { name: 'sw_insert_sketch', description: 'Open a new sketch on the currently selected plane or face', inputSchema: InsertSketchSchema },
  { name: 'sw_finish_sketch', description: 'Close (finish) the currently open sketch', inputSchema: FinishSketchSchema },
  { name: 'sw_add_line', description: 'Add a line segment to the open sketch', inputSchema: AddLineSchema },
  { name: 'sw_add_circle', description: 'Add a circle to the open sketch', inputSchema: AddCircleSchema },
  { name: 'sw_add_rectangle', description: 'Add a rectangle to the open sketch', inputSchema: AddRectangleSchema },
  { name: 'sw_add_arc', description: 'Add an arc to the open sketch', inputSchema: AddArcSchema },
  // Feature
  { name: 'sw_extrude', description: 'Extrude the current open sketch into a 3D boss feature. Call while sketch is in edit mode (do not finish sketch first).', inputSchema: ExtrudeSchema },
  { name: 'sw_extrude_cut', description: 'Extrude-cut the current open sketch to remove material. Call while sketch is in edit mode.', inputSchema: ExtrudeCutSchema },
  { name: 'sw_revolve', description: 'Revolve the current open sketch around the selected axis', inputSchema: RevolveSchema },
  { name: 'sw_fillet', description: 'Apply a fillet to the selected edges', inputSchema: FilletSchema },
  { name: 'sw_chamfer', description: 'Apply a chamfer to the selected edges', inputSchema: ChamferSchema },
  { name: 'sw_shell', description: 'Shell the 3D body, removing the selected open faces', inputSchema: ShellSchema },
  { name: 'sw_simple_hole', description: 'Create a simple hole at the selected point on a face', inputSchema: SimpleHoleSchema },
  // Assembly
  { name: 'sw_insert_component', description: 'Insert a part or sub-assembly into the active assembly', inputSchema: InsertComponentSchema },
  { name: 'sw_add_mate_coincident', description: 'Add a Coincident mate between two selected entities in the assembly', inputSchema: AddMateCoincidentSchema },
  { name: 'sw_add_mate_concentric', description: 'Add a Concentric mate between two selected circular entities', inputSchema: AddMateConcentricSchema },
  { name: 'sw_add_mate_parallel', description: 'Add a Parallel mate between two selected planar entities', inputSchema: AddMateParallelSchema },
  { name: 'sw_add_mate_distance', description: 'Add a Distance mate between two selected entities', inputSchema: AddMateDistanceSchema },
  { name: 'sw_add_mate_angle', description: 'Add an Angle mate between two selected entities', inputSchema: AddMateAngleSchema },
  { name: 'sw_list_components', description: 'List all top-level components in the active assembly', inputSchema: ListComponentsSchema },
] as const;

// ── Convert Zod schema to MCP JSON Schema ─────────────────────

function zodToJsonSchema(schema: z.ZodTypeAny): Record<string, unknown> {
  // Light-weight converter for the subset of Zod used here
  if (schema instanceof z.ZodObject) {
    const shape = schema.shape as Record<string, z.ZodTypeAny>;
    const properties: Record<string, unknown> = {};
    const required: string[] = [];
    for (const [key, val] of Object.entries(shape)) {
      properties[key] = zodToJsonSchema(val as z.ZodTypeAny);
      const isOptional =
        val instanceof z.ZodOptional ||
        val instanceof z.ZodDefault;
      if (!isOptional) required.push(key);
    }
    const result: Record<string, unknown> = { type: 'object', properties };
    if (required.length > 0) result.required = required;
    return result;
  }
  if (schema instanceof z.ZodString) return { type: 'string', description: schema.description };
  if (schema instanceof z.ZodNumber) return { type: 'number', description: schema.description };
  if (schema instanceof z.ZodBoolean) return { type: 'boolean', description: schema.description };
  if (schema instanceof z.ZodEnum) return { type: 'string', enum: (schema as z.ZodEnum<[string, ...string[]]>).options, description: schema.description };
  if (schema instanceof z.ZodOptional) return zodToJsonSchema((schema as z.ZodOptional<z.ZodTypeAny>).unwrap());
  if (schema instanceof z.ZodDefault) return zodToJsonSchema((schema as z.ZodDefault<z.ZodTypeAny>)._def.innerType);
  return {};
}

// ── Main ──────────────────────────────────────────────────────

async function main(): Promise<void> {
  const client = new NamedPipeClient({ pipeName: 'SolidWorksMcpBridge' });

  const server = new Server(
    { name: SERVER_NAME, version: SERVER_VERSION },
    { capabilities: { tools: {} } },
  );

  // List tools
  server.setRequestHandler(ListToolsRequestSchema, async () => ({
    tools: TOOLS.map((t) => ({
      name: t.name,
      description: t.description,
      inputSchema: zodToJsonSchema(t.inputSchema as unknown as z.ZodTypeAny),
    })),
  }));

  // Call tool
  server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;
    const params = (args ?? {}) as Record<string, unknown>;

    try {
      let result: unknown;

      switch (name) {
        // Document
        case 'sw_connect':      result = await swConnect(client); break;
        case 'sw_disconnect':   result = await swDisconnect(client); break;
        case 'sw_new_document': result = await swNewDocument(client, NewDocumentSchema.parse(params)); break;
        case 'sw_open_document': result = await swOpenDocument(client, OpenDocumentSchema.parse(params)); break;
        case 'sw_close_document': result = await swCloseDocument(client, CloseDocumentSchema.parse(params)); break;
        case 'sw_save_document': result = await swSaveDocument(client, SaveDocumentSchema.parse(params)); break;
        case 'sw_list_documents': result = await swListDocuments(client); break;
        case 'sw_get_active_document': result = await swGetActiveDocument(client); break;
        // Selection
        case 'sw_select_by_name': result = await swSelectByName(client, SelectByNameSchema.parse(params)); break;
        case 'sw_clear_selection': await swClearSelection(client); result = { cleared: true }; break;
        // Sketch
        case 'sw_insert_sketch': await swInsertSketch(client); result = { ok: true }; break;
        case 'sw_finish_sketch': await swFinishSketch(client); result = { ok: true }; break;
        case 'sw_add_line':      result = await swAddLine(client, AddLineSchema.parse(params)); break;
        case 'sw_add_circle':    result = await swAddCircle(client, AddCircleSchema.parse(params)); break;
        case 'sw_add_rectangle': result = await swAddRectangle(client, AddRectangleSchema.parse(params)); break;
        case 'sw_add_arc':       result = await swAddArc(client, AddArcSchema.parse(params)); break;
        // Feature
        case 'sw_extrude':      result = await swExtrude(client, ExtrudeSchema.parse(params)); break;
        case 'sw_extrude_cut':  result = await swExtrudeCut(client, ExtrudeCutSchema.parse(params)); break;
        case 'sw_revolve':      result = await swRevolve(client, RevolveSchema.parse(params)); break;
        case 'sw_fillet':       result = await swFillet(client, FilletSchema.parse(params)); break;
        case 'sw_chamfer':      result = await swChamfer(client, ChamferSchema.parse(params)); break;
        case 'sw_shell':        result = await swShell(client, ShellSchema.parse(params)); break;
        case 'sw_simple_hole':  result = await swSimpleHole(client, SimpleHoleSchema.parse(params)); break;
        // Assembly
        case 'sw_insert_component':     result = await swInsertComponent(client, InsertComponentSchema.parse(params)); break;
        case 'sw_add_mate_coincident':  await swAddMateCoincident(client, AddMateCoincidentSchema.parse(params)); result = { ok: true }; break;
        case 'sw_add_mate_concentric':  await swAddMateConcentric(client, AddMateConcentricSchema.parse(params)); result = { ok: true }; break;
        case 'sw_add_mate_parallel':    await swAddMateParallel(client, AddMateParallelSchema.parse(params)); result = { ok: true }; break;
        case 'sw_add_mate_distance':    await swAddMateDistance(client, AddMateDistanceSchema.parse(params)); result = { ok: true }; break;
        case 'sw_add_mate_angle':       await swAddMateAngle(client, AddMateAngleSchema.parse(params)); result = { ok: true }; break;
        case 'sw_list_components':      result = await swListComponents(client); break;
        default:
          return {
            content: [{ type: 'text' as const, text: `Unknown tool: ${name}` }],
            isError: true,
          };
      }

      return {
        content: [{ type: 'text' as const, text: JSON.stringify(result, null, 2) }],
      };
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      return {
        content: [{ type: 'text' as const, text: `Error: ${message}` }],
        isError: true,
      };
    }
  });

  const transport = new StdioServerTransport();
  await server.connect(transport);
}

// Run only when this is the entry-point module (not during tests)
if (process.argv[1] && !process.env.VITEST) {
  main().catch((err) => {
    console.error('Fatal error:', err);
    process.exit(1);
  });
}
