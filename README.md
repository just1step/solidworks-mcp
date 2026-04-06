# SolidWorks MCP Server

Language: English | [简体中文](./README.zh-CN.md)

SolidWorks MCP Server exposes SolidWorks desktop automation as a Windows-only MCP server for VS Code, Copilot, Claude Desktop, and other MCP clients. The project is delivered as a single Windows exe with a tray-based Hub and an auto-waking Proxy entrypoint for MCP stdio clients.

## Demo

![SolidWorks MCP demo](docs/media/demo.gif)

The demo highlights the full interaction loop:

- launch the tray-based Hub once and keep SolidWorks COM work on a shared STA thread;
- let VS Code or Claude Desktop start Proxy instances on demand through `--proxy`;
- inspect connections, status, and recent logs from the tray monitor window;
- call SolidWorks document, sketch, feature, selection, and assembly tools through MCP.

## Quick Navigation

- [Architecture At A Glance](#architecture-at-a-glance)
- [Usage Guide](#usage-guide)
- [Development Guide](#development-guide)
- [Issue Guide](#issue-guide)
- [Related Projects And Libraries](#related-projects-and-libraries)

## Architecture At A Glance

The product should be understood as one user-facing executable with internal layers, not as separate apps that are started independently.

Core runtime model:

- `SolidWorksMcpApp.exe` is the only supported startup entrypoint for local use and development.
- The tray process is the Hub. It owns the shared SolidWorks COM connection, the shared STA execution thread, logs, and client/session monitoring.
- MCP clients launch the same exe in `--proxy` mode. That Proxy process connects back to the Hub through a local named pipe and relays stdio MCP traffic.
- `SolidWorksBridge` is an internal implementation layer used by the tray app. It provides the SolidWorks COM-facing services and pipe/message handling, but it is not a standalone product entrypoint.

High-level flow:

1. Start `SolidWorksMcpApp.exe`.
2. The Hub initializes shared services and waits for local MCP clients.
3. VS Code, Copilot, or Claude starts a Proxy session when needed.
4. The Proxy forwards MCP requests to the Hub.
5. The Hub executes tool calls against SolidWorks through the internal bridge/services layer.

Repository mapping:

- `app/SolidWorksMcpApp/`: tray UI, Hub/Proxy host, MCP tool registration, logging, and packaging entrypoint.
- `bridge/SolidWorksBridge/`: internal SolidWorks COM services, message handling, and pipe server primitives used by the app.
- `bridge/SolidWorksBridge.Tests/`: unit and integration tests for the bridge/service behavior.

Startup rule:

- Do not start `SolidWorksBridge` directly.
- Start `SolidWorksMcpApp.exe`, then let MCP clients connect through the supported Hub/Proxy path.

## Usage Guide

### Requirements

- Windows 10/11
- SolidWorks installed locally
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) for release binaries, or .NET 8 SDK for source builds
- VS Code, Copilot, Claude Desktop, or another MCP client that supports stdio servers

### Compatibility And Dependency Notes

- Official target platform: Windows 10/11 x64. The shipped binary is `net8.0-windows` and `win-x64` self-contained.
- Windows 8/8.1 and 32-bit Windows are not official targets for this build.
- The bridge is compiled against `SolidWorks.Interop.sldworks` and `SolidWorks.Interop.swconst` version `32.1.0`.
- SolidWorks must be installed locally and registered for COM activation through `SldWorks.Application`.
- The tray UI follows the Windows UI language only for Chinese vs non-Chinese; non-Chinese systems fall back to English.
- SolidWorks UI language is separate from Windows language. The app can query the active SolidWorks language through `ISldWorks.GetCurrentLanguage()`.
- Reference plane names are localized by SolidWorks. To avoid hard-coded plane names, the bridge now enumerates the active document's `RefPlane` features and their selection names directly from the feature tree.
- After a successful SolidWorks connect, the server automatically captures the current SolidWorks language and the active document's reference plane snapshot and writes that payload into the session log for bug analysis.
- Older or much newer SolidWorks major versions may work, but they are not yet declared as fully validated in this repository.

### Download And Run

1. Download `SolidWorksMcpApp.exe` from the [Releases](../../releases) page.
2. Double-click the exe.
3. Confirm that the tray icon appears.
4. Right-click the tray icon to open the monitor, export client config, pause the server, or inspect logs.

The server has two runtime modes behind the same exe:

- Hub mode: the tray process that owns the shared SolidWorks COM/STA environment.
- Proxy mode: the stdio MCP endpoint started by MCP clients with `--proxy`.

The Proxy connects to the Hub through a local named pipe and auto-starts the Hub if it is not already running.

### Connect VS Code

1. Start `SolidWorksMcpApp.exe`.
2. Right-click the tray icon and choose **Export VS Code MCP Config**.
3. Paste the generated JSON into your VS Code `mcp.json`. Use `.vscode/mcp.json` for workspace scope, or the user-profile `mcp.json` opened by the `MCP: Open User Configuration` command.
4. In VS Code, verify that the `solidworks` MCP server is visible and enabled.

The app also auto-writes the default user-profile VS Code MCP entry to `%APPDATA%\Code\User\mcp.json` on first launch.

### Connect Claude Desktop

1. Start `SolidWorksMcpApp.exe`.
2. Right-click the tray icon and choose **Export Claude Config**.
3. Paste the generated snippet into `%APPDATA%\Claude\claude_desktop_config.json`, or use the auto-written entry that the app creates on first launch.
4. Restart Claude Desktop if needed and verify that the MCP server is detected.

### Verify The Server Is Healthy

- Use the tray icon text and menu to confirm the server is running.
- Open the monitor window to inspect connected clients and recent logs.
- If something fails, check `logs/{MachineName}_{timestamp}.txt` next to the exe.

### Common Tool Flows

Once the server is connected to SolidWorks, the current document toolset covers more than basic open/save operations.

- Save or export the active document to another path with `SaveDocumentAs`. The output extension decides the format, so the same tool can target native SolidWorks files and common interchange formats such as `sldprt`, `sldasm`, `slddrw`, `step`, `stp`, and `stl`.
- Undo the latest model operations with `Undo(steps)`, including multi-step rollback such as `steps = 3`.
- Switch to a standard orientation with `ShowStandardView`, using values such as `front`, `top`, `right`, or `isometric`.
- Rotate the current model view with `RotateView(xDegrees, yDegrees, zDegrees)` when you need incremental camera control instead of a standard view preset.
- Export the current viewport to a PNG file with `ExportCurrentViewPng`. When `includeBase64Data = true`, the tool result also contains the PNG payload as base64 for MCP clients that can render image data directly.
- For FeatureManager inspection or cleanup flows, call `GetEditState` first. If the active document is still editing a sketch, exit sketch mode before `ListFeatureTree`, `DeleteFeatureByName`, or `DeleteUnusedSketches`.

Typical examples:

```text
SaveDocumentAs(outputPath="C:\\temp\\gearbox.step")
Undo(steps=2)
ShowStandardView(view="top")
RotateView(xDegrees=15, zDegrees=45)
ExportCurrentViewPng(outputPath="C:\\temp\\gearbox.png", width=1600, height=900)
GetEditState()
```

## Development Guide

### Repository Layout

```text
solidworks-mcp-server/
├─ app/SolidWorksMcpApp/          # Tray app, MCP server host, tray UI, packaging entrypoint
├─ bridge/SolidWorksBridge/       # SolidWorks COM-facing services and runtime library
├─ bridge/SolidWorksBridge.Tests/ # Unit tests for bridge services
├─ docs/media/                    # README demo assets
├─ .github/workflows/             # CI, beta, release, and self-hosted workflows
└─ .vscode/                       # Local tasks and launch config for development
```

### Build

Build the tray app from source:

```powershell
cd app/SolidWorksMcpApp
dotnet build -c Release
```

The main output is:

- `bin/Release/net8.0-windows/win-x64/SolidWorksMcpApp.exe`

You can also use the local VS Code task in [`.vscode/tasks.json`](./.vscode/tasks.json):

- `Build SolidWorks MCP App`
- `Start SolidWorks MCP App`

For local manual testing, start the tray app directly. The repository no longer maintains a separate `deploy-local.bat` or standalone bridge startup script because the tray app is the authoritative runtime entrypoint.

### Test

Run the bridge test suite:

```powershell
cd bridge
dotnet test SolidWorksBridge.sln
```

For CI and fast local validation, the main non-integration command is:

```powershell
dotnet test bridge/SolidWorksBridge.sln --configuration Release --filter "Category!=Integration"
```

### Package

Build a single-file Windows package locally:

```powershell
dotnet publish app/SolidWorksMcpApp/SolidWorksMcpApp.csproj -c Release -r win-x64 --self-contained true
```

The current packaging flow is optimized so the published output can be a single exe for end users.

### Release And CI

- [`.github/workflows/ci.yml`](./.github/workflows/ci.yml) validates pull requests.
- [`.github/workflows/beta.yml`](./.github/workflows/beta.yml) builds a beta single-file exe artifact on every push.
- [`.github/workflows/release.yml`](./.github/workflows/release.yml) publishes a single release exe asset on each GitHub Release.
- [`.github/workflows/solidworks-self-hosted.yml`](./.github/workflows/solidworks-self-hosted.yml) runs the self-hosted SolidWorks workflow when a real SolidWorks environment is required.

## Issue Guide

Before opening an issue:

- check whether the problem still reproduces on the latest `main` branch or latest release binary;
- search existing [Issues](../../issues) to avoid duplicates;
- collect the relevant log file from the `logs/` directory next to the exe;
- note whether the problem happens in Hub startup, client connection, or an actual SolidWorks tool call.

When submitting an issue, include:

- Windows version;
- SolidWorks version;
- MCP client name and version, such as VS Code or Claude Desktop;
- exact reproduction steps;
- expected behavior and actual behavior;
- error messages, screenshots, or the relevant log excerpt;
- the auto-captured SolidWorks context entry from the session log, including language and reference plane data when available;
- whether the problem reproduces with the published exe or only from source.

Use these links:

- [Browse existing issues](../../issues)
- [Open a new issue](../../issues/new)

## Related Projects And Libraries

- [app/SolidWorksMcpApp](./app/SolidWorksMcpApp) — the Windows tray app and MCP hosting layer.
- [bridge/SolidWorksBridge](./bridge/SolidWorksBridge) — the SolidWorks COM-facing implementation used by the app.
- [bridge/SolidWorksBridge.Tests](./bridge/SolidWorksBridge.Tests) — unit tests for the bridge behavior.
- [Model Context Protocol](https://modelcontextprotocol.io/) — the protocol specification used by MCP clients and servers.
- [ModelContextProtocol on NuGet](https://www.nuget.org/packages/ModelContextProtocol/) — the .NET package used by this project.
- [SOLIDWORKS API Help](https://help.solidworks.com/) — the official SolidWorks API reference.

## Current Tooling Coverage

- Document tools: connect, create, open, save, save-as/export, close, undo, list, inspect, viewport PNG export.
- View tools: standard orientation switching and incremental view rotation.
- Selection tools: select by name, enumerate topology, exact entity selection, clear selection.
- Sketch tools: point, ellipse, polygon, text, line, circle, rectangle, arc.
- Feature tools: extrude, extrude cut, revolve, fillet, chamfer, shell.
- Assembly tools: insert component, list components, and the core mate set.