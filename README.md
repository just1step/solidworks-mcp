# SolidWorks MCP Server

Language: English | [简体中文](./README.zh-CN.md)

SolidWorks MCP Server exposes local SolidWorks automation as a Windows-only MCP server.

This project ships as a single user-facing executable: `SolidWorksMcpApp.exe`.

## Demo

![SolidWorks MCP demo](docs/media/demo.gif)

## What It Is

- `SolidWorksMcpApp.exe` is the only supported entrypoint.
- The app runs a tray-based Hub that owns the shared SolidWorks COM/STA session.
- MCP clients connect through the same exe in `--proxy` mode.
- The supported path is `client -> proxy -> hub -> MCP tools -> SolidWorks`.

## Requirements

- Windows 10/11 x64
- SolidWorks installed locally, with `SldWorks.Application` available for COM activation
- .NET 8 Runtime for source-adjacent app builds; the published release binary is packaged as a self-contained single exe
- An MCP client that supports stdio, such as VS Code or Claude Desktop

## SolidWorks Version Support

| SolidWorks version | Support status | High-risk mutation workflows |
| --- | --- | --- |
| 2024 | Certified | Enabled |
| 2025 | Targeted | Enabled |
| 2026 | Experimental | Blocked |
| Older than 2024 | Unsupported | Blocked |
| Newer than 2026 | Unsupported | Blocked |

## Quick Start

1. Download `SolidWorksMcpApp.exe` from [Releases](../../releases).
2. Start the exe and confirm the tray icon appears.
3. Export client config from the tray menu.
4. Add that config to your MCP client.

For local use and testing, do not start `SolidWorksBridge` directly.

## Development

Build:

```powershell
dotnet build app/SolidWorksMcpApp/SolidWorksMcpApp.csproj -c Release
```

Run non-integration tests:

```powershell
dotnet test bridge/SolidWorksBridge.sln --configuration Release --filter "Category!=Integration"
```

Run SolidWorks integration tests through the real hub/proxy path:

```powershell
scripts/test-integration.bat
```

## Repository Layout

- `app/SolidWorksMcpApp/`: tray app, Hub/Proxy host, MCP tool registration, packaging entrypoint
- `bridge/SolidWorksBridge/`: SolidWorks COM-facing services and workflow logic
- `bridge/SolidWorksBridge.Tests/`: unit and integration tests

## Logs

If a tool call fails, check the `logs/` directory next to the exe.

## License

This project is licensed under the [MIT License](./LICENSE).
