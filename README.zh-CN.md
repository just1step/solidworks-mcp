# SolidWorks MCP Server

语言切换：[English](./README.md) | 简体中文

SolidWorks MCP Server 是面向 Windows 的 SolidWorks MCP 桌面自动化服务，供 VS Code、Copilot、Claude Desktop 以及其他 MCP 客户端调用。项目以单一 exe 交付，内部包含托盘 Hub 和按需拉起的 Proxy 两种运行模式。

## Demo 演示

![SolidWorks MCP demo](docs/media/demo.gif)

这段演示主要体现了完整使用链路：

- 启动托盘 Hub，并把 SolidWorks COM 调用统一串行到共享 STA 线程；
- 让 VS Code 或 Claude Desktop 按需拉起 `--proxy` 模式；
- 通过托盘监控面板查看连接状态、服务状态和最近日志；
- 通过 MCP 调用文档、草图、特征、选择、装配等工具。

## 快速导航

- [使用指引](#使用指引)
- [开发指引](#开发指引)
- [提交 Issue 指引](#提交-issue-指引)
- [关联库与参考链接](#关联库与参考链接)

## 使用指引

### 环境要求

- Windows 10/11
- 本机已安装 SolidWorks
- [.NET 8 运行时](https://dotnet.microsoft.com/download/dotnet/8.0)；如果要从源码构建，则需要 .NET 8 SDK
- 支持 stdio MCP Server 的客户端，例如 VS Code、Copilot、Claude Desktop

### 兼容性与依赖说明

- 官方目标平台是 Windows 10/11 x64；当前交付的二进制是 `net8.0-windows`、`win-x64` 自包含单文件程序。
- Windows 8/8.1 和 32 位 Windows 不属于当前版本的官方支持范围。
- bridge 当前编译时依赖 `SolidWorks.Interop.sldworks` 和 `SolidWorks.Interop.swconst`，版本为 `32.1.0`。
- 本机必须安装可正常注册 COM 的 SolidWorks，程序通过 `SldWorks.Application` 连接或拉起 SolidWorks。
- 托盘界面语言目前只区分中文和非中文；非中文系统统一回退为英文界面。
- SolidWorks 自身的 UI 语言与 Windows 语言是两个维度。程序可以通过 `ISldWorks.GetCurrentLanguage()` 获取当前 SolidWorks 使用语言。
- 基准面名称会随 SolidWorks 语言本地化变化。为避免硬编码中英文名称，bridge 现已通过当前文档特征树枚举 `RefPlane` 特征及其可用于选择的名称。
- 在成功连接 SolidWorks 之后，服务会自动采集当前 SolidWorks 语言和活动文档中的基准面快照，并写入当前会话日志，便于后续排查问题。
- 对更旧或明显更新的 SolidWorks 主版本，目前还没有在仓库中声明为“完全验证通过”。

### 直接运行

1. 从 [Releases](../../releases) 下载 `SolidWorksMcpApp.exe`。
2. 双击启动 exe。
3. 确认系统托盘中出现 SolidWorks MCP 图标。
4. 右键托盘图标，可打开监控面板、导出客户端配置、暂停服务或查看日志。

同一个 exe 在运行时包含两种模式：

- Hub 模式：托盘常驻进程，持有共享的 SolidWorks COM / STA 执行环境。
- Proxy 模式：供 MCP 客户端通过 `--proxy` 拉起的 stdio 入口。

Proxy 通过本地 Named Pipe 与 Hub 通信；如果 Hub 尚未启动，Proxy 会自动唤醒它。

### 在 VS Code 中使用

1. 启动 `SolidWorksMcpApp.exe`。
2. 右键托盘图标，选择 **导出 VS Code 配置**。
3. 将生成内容粘贴到 VS Code 的 `settings.json`，或者直接使用程序首次启动时自动写入的配置。
4. 在 VS Code 中确认 `solidworks` MCP Server 已被识别并启用。

### 在 Claude Desktop 中使用

1. 启动 `SolidWorksMcpApp.exe`。
2. 右键托盘图标，选择 **导出 Claude 配置**。
3. 将生成内容粘贴到 `%APPDATA%\Claude\claude_desktop_config.json`，或者直接使用程序首次启动时自动写入的配置。
4. 如有需要，重启 Claude Desktop 并确认 MCP Server 已加载。

### 如何确认服务正常

- 通过托盘菜单中的状态文案确认服务是否处于运行态。
- 打开监控面板查看当前连接的客户端和最近日志。
- 如果调用失败，检查 exe 同级目录的 `logs/{MachineName}_{timestamp}.txt`。

## 开发指引

### 仓库结构

```text
solidworks-mcp-server/
├─ app/SolidWorksMcpApp/          # 托盘程序、MCP Host、打包入口
├─ bridge/SolidWorksBridge/       # SolidWorks COM 侧实现
├─ bridge/SolidWorksBridge.Tests/ # Bridge 单元测试
├─ docs/media/                    # README 中使用的演示资源
├─ .github/workflows/             # CI、beta、release、自托管工作流
└─ .vscode/                       # 本地开发任务与启动配置
```

### 本地构建

```powershell
cd app/SolidWorksMcpApp
dotnet build -c Release
```

主程序输出位置：

- `bin/Release/net8.0-windows/win-x64/SolidWorksMcpApp.exe`

如果你使用 VS Code，也可以直接运行 [`.vscode/tasks.json`](./.vscode/tasks.json) 里的任务：

- `Build SolidWorks MCP App`

### 本地测试

运行 bridge 测试：

```powershell
cd bridge
dotnet test SolidWorksBridge.sln
```

若只做快速校验，可使用：

```powershell
dotnet test bridge/SolidWorksBridge.sln --configuration Release --filter "Category!=Integration"
```

### 本地打包

生成单文件 Windows 包：

```powershell
dotnet publish app/SolidWorksMcpApp/SolidWorksMcpApp.csproj -c Release -r win-x64 --self-contained true
```

当前打包流程已经优化为用户最终只需下载一个 exe 即可运行。

### CI / Release 指引

- [`.github/workflows/ci.yml`](./.github/workflows/ci.yml)：PR 校验。
- [`.github/workflows/beta.yml`](./.github/workflows/beta.yml)：每次 push 构建 beta 单文件 exe 工件。
- [`.github/workflows/release.yml`](./.github/workflows/release.yml)：每次 GitHub Release 构建并上传单个正式 exe 资产。
- [`.github/workflows/solidworks-self-hosted.yml`](./.github/workflows/solidworks-self-hosted.yml)：需要真实 SolidWorks 环境时使用的自托管工作流。

## 提交 Issue 指引

提交 Issue 之前，建议先做这些检查：

- 在最新 `main` 分支或最新 release 二进制上确认问题仍然存在；
- 先搜索已有 [Issues](../../issues)，避免重复提交；
- 收集 exe 同级 `logs/` 目录下的相关日志；
- 确认问题发生在 Hub 启动、客户端连接，还是具体某个工具调用阶段。

提交 Issue 时，建议包含：

- Windows 版本；
- SolidWorks 版本；
- MCP 客户端名称和版本；
- 复现步骤；
- 预期结果与实际结果；
- 错误信息、截图或关键日志片段；
- 当前会话日志中自动记录的 SolidWorks 上下文信息，包括语言和基准面快照；
- 问题是在发布版 exe 上复现，还是只在源码运行时复现。

可使用以下入口：

- [查看已有 Issues](../../issues)
- [新建 Issue](../../issues/new)

## 关联库与参考链接

- [app/SolidWorksMcpApp](./app/SolidWorksMcpApp) — 当前面向用户交付的 Windows 托盘应用与 MCP Host。
- [bridge/SolidWorksBridge](./bridge/SolidWorksBridge) — 负责 SolidWorks COM 交互的实现层。
- [bridge/SolidWorksBridge.Tests](./bridge/SolidWorksBridge.Tests) — bridge 层测试用例。
- [Model Context Protocol 官网](https://modelcontextprotocol.io/) — MCP 协议说明。
- [ModelContextProtocol NuGet](https://www.nuget.org/packages/ModelContextProtocol/) — 本项目使用的 .NET MCP 包。
- [SOLIDWORKS API Help](https://help.solidworks.com/) — 官方 SolidWorks API 参考文档。

## 当前工具覆盖范围

- 文档工具：连接、创建、打开、保存、关闭、列出、查询活动文档。
- 选择工具：按名称选择、拓扑枚举、按索引精确选择、清空选择。
- 草图工具：点、椭圆、多边形、文本、线、圆、矩形、圆弧。
- 特征工具：拉伸、切除、旋转、圆角、倒角、抽壳、简单孔。
- 装配工具：插入组件、列出组件、核心配合工具。