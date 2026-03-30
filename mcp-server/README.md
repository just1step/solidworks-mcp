# solidworks-mcp-server

Windows-only MCP server for driving SolidWorks through a C# COM bridge.

## Install

```powershell
npm install -g solidworks-mcp-server
```

## Requirements

- Windows
- SolidWorks installed locally
- Node.js 18+
- .NET 8 runtime if you rebuild the bridge from source

## Package contents

- `dist/`: compiled MCP server
- `vendor/bridge/`: published `SolidWorksBridge.exe` bundle
- `bin/solidworks-mcp-server.js`: CLI entrypoint

## Run

```powershell
solidworks-mcp-server
```

The server uses stdio for MCP and starts the bundled bridge automatically. If you need to override the bridge location, set `SOLIDWORKS_MCP_BRIDGE_ROOT` to a folder containing `SolidWorksBridge.exe`.

## Development

Use the repository root documentation for local development, tests, VS Code integration, and release workflow.