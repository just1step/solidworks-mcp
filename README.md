# SolidWorks MCP Server

[![Simplified Chinese](https://img.shields.io/badge/%E7%AE%80%E4%BD%93%E4%B8%AD%E6%96%87-README-blue)](./README.zh-CN.md)

SolidWorks MCP Server exposes SolidWorks desktop automation as a Windows-only MCP server for VS Code, Copilot, and other MCP clients.

## Demo

![SolidWorks MCP demo](docs/media/demo.gif)

It ships as one Windows exe with two runtime modes:

- Hub mode: tray app hosting the shared SolidWorks COM/STA world.
- Proxy mode: stdio MCP endpoint used by VS Code, Copilot, Claude, and other MCP clients.

The proxy talks to the tray hub through a local named pipe and will auto-start the hub if it is not already running.

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

- Windows 10/11
- SolidWorks installed locally
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (or .NET 8 SDK for source builds)
- VS Code or Claude Desktop for MCP client support

## Quick start

### Run from release binary

Download `SolidWorksMcpApp.exe` from the [Releases](../../releases) page and double-click it.  
The server appears as a tray icon. Right-click to export config for Claude Desktop or VS Code.

### Build from source

```powershell
cd app/SolidWorksMcpApp
dotnet build -c Release
```

The exe will be at `bin/Release/net8.0-windows/win-x64/SolidWorksMcpApp.exe`.

### Use in VS Code

Right-click the tray icon → **Export VS Code Config** and paste into your VS Code `settings.json`,  
or use the auto-written entry (the server writes the config automatically on first launch).

### Use with Claude Desktop

Right-click the tray icon → **导出 Claude 配置** and paste into `%APPDATA%\Claude\claude_desktop_config.json`,  
or use the auto-written entry (the server writes the config automatically on first launch).

## Repository layout

```text
solidworks-mcp-server/
├─ app/SolidWorksMcpApp/  # C# MCP server exe (tray app, stdio transport)
├─ bridge/               # C# SolidWorks COM bridge and tests
└─ .vscode/              # workspace-local MCP config for development
```

## Tests

```powershell
cd bridge
dotnet test SolidWorksBridge.sln
```

## Development notes

- The MCP server returns JSON text payloads in `content[].text`.
- MCP clients connect to `SolidWorksMcpApp.exe --proxy`; the proxy auto-wakes the tray hub on demand.
- Feature creation expects the sketch to be open when calling extrude / extrude-cut.
- Error logs are written to `logs/{MachineName}_{timestamp}.txt` next to the exe.
- The tray icon auto-writes `mcpServers.solidworks` into Claude Desktop and VS Code settings on first launch.

## Release checklist

1. Run `dotnet test SolidWorksBridge.sln` in [bridge](bridge).
2. Run `dotnet build -c Release` in [app/SolidWorksMcpApp](app/SolidWorksMcpApp).
3. Attach `SolidWorksMcpApp.exe` to the GitHub release.

## GitHub Actions

- `.github/workflows/ci.yml` — builds and tests the .NET project on every push/PR.
- `.github/workflows/solidworks-self-hosted.yml` — manual workflow for a Windows self-hosted runner with SolidWorks installed.