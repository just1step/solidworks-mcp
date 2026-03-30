# SolidWorks MCP Server

[![Simplified Chinese](https://img.shields.io/badge/%E7%AE%80%E4%BD%93%E4%B8%AD%E6%96%87-README-blue)](./README.zh-CN.md)

SolidWorks MCP Server exposes SolidWorks desktop automation as a Windows-only MCP server for VS Code, Copilot, and other MCP clients.

It ships as two layers:

- A Node.js MCP server that validates tool input and speaks stdio MCP.
- A C# bridge that talks to SolidWorks through COM over a local named pipe.

## Why this repo exists

- Give AI agents a stable SolidWorks automation surface.
- Keep CAD actions small, explicit, and testable.
- Separate MCP concerns from SolidWorks COM concerns.
- Support both local development and packaged distribution.

## What is included

- Document tools: connect, create, open, save, close, list, inspect.
- Selection tools: select by name, enumerate topology, exact entity selection, clear selection.
- Sketch tools: point, ellipse, polygon, text, line, circle, rectangle, arc.
- Feature tools: extrude, extrude cut, revolve, fillet, chamfer, shell, simple hole.
- Assembly tools: insert component, list components, and the core mate set.

## Requirements

- Windows
- SolidWorks installed locally
- .NET 8 SDK for source builds
- Node.js 18+
- VS Code with MCP support if you want editor integration

The bridge still references the local SolidWorks Interop DLLs, so packaging is Windows-only and assumes a machine with SolidWorks installed.

## Quick start

### Development from source

```powershell
cd mcp-server
npm install
npm run build

cd ..\bridge
dotnet build SolidWorksBridge.sln
```

Run the MCP server from the repo:

```powershell
cd mcp-server
node dist/index.js
```

The server will try to reuse an existing `SolidWorksMcpBridge` pipe, and if needed it will auto-start the bridge.

### Use in VS Code during development

This repo already includes [.vscode/mcp.json](.vscode/mcp.json) for workspace-local development.

1. Build the bridge and the MCP server.
2. Open the repository in VS Code.
3. Run `MCP: List Servers`.
4. Trust `solidworks-mcp-server` when prompted.

### Use in VS Code from npm

After publishing to npm, the lightweight install flow is:

```powershell
npm install -g solidworks-mcp
```

Then register the server in your user or workspace `mcp.json`:

```json
{
	"servers": {
		"solidworks-mcp-server": {
			"type": "stdio",
			"command": "solidworks-mcp-server"
		}
	}
}
```

If VS Code does not inherit your npm global bin folder on Windows, point to the shim explicitly instead:

```json
{
	"servers": {
		"solidworks-mcp-server": {
			"type": "stdio",
			"command": "C:/Users/<you>/AppData/Roaming/npm/solidworks-mcp-server.cmd"
		}
	}
}
```

If you prefer not to install globally, use `npx` instead:

```json
{
	"servers": {
		"solidworks-mcp-server": {
			"type": "stdio",
			"command": "C:/Program Files/nodejs/npx.cmd",
			"args": ["-y", "solidworks-mcp"]
		}
	}
}
```

## npm distribution

The publishable npm package lives in [mcp-server/package.json](mcp-server/package.json).

What it packages:

- `dist/` compiled MCP server output
- `vendor/bridge/` published `SolidWorksBridge.exe` bundle
- `bin/solidworks-mcp-server.cmd` Windows CLI entrypoint

Build a local tarball:

```powershell
cd mcp-server
npm pack
```

`npm pack` runs `prepack`, which now:

1. Builds the TypeScript MCP server.
2. Publishes the C# bridge in Release mode.
3. Bundles the bridge into `mcp-server/vendor/bridge`.

After publishing to npm, the intended install flow is:

```powershell
npm install -g solidworks-mcp
solidworks-mcp-server
```

If you need to override the packaged bridge location, set `SOLIDWORKS_MCP_BRIDGE_ROOT` to a directory containing `SolidWorksBridge.exe`.

## Repository layout

```text
solidworks-mcp-server/
├─ bridge/            # C# COM bridge and tests
├─ mcp-server/        # publishable npm package
├─ scripts/           # repo-level build and test helpers
└─ .vscode/           # workspace-local MCP config for development
```

## Tests

Node tests:

```powershell
cd mcp-server
npm test
```

Bridge tests:

```powershell
cd bridge
dotnet test SolidWorksBridge.sln
```

## Development notes

- The MCP server currently returns JSON text payloads in `content[].text`.
- Feature creation still expects `sw_extrude` and `sw_extrude_cut` to run while the sketch is open.
- The repo keeps `.vscode/mcp.json` for local development; end users can point VS Code at the published npm package instead.

## Release checklist

1. Run `npm test` in [mcp-server](mcp-server).
2. Run `dotnet test SolidWorksBridge.sln` in [bridge](bridge).
3. Run `npm pack` in [mcp-server](mcp-server) and verify the tarball.