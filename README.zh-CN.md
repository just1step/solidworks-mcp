# SolidWorks MCP Server

语言切换：[English](./README.md) | 简体中文

SolidWorks MCP Server 用来把本机 SolidWorks 能力暴露成一个仅限 Windows 的 MCP 服务。

这个项目对外只交付一个可执行文件：`SolidWorksMcpApp.exe`。

## Demo 演示

![SolidWorks MCP demo](docs/media/demo.gif)

## 它是什么

- `SolidWorksMcpApp.exe` 是唯一支持的启动入口。
- 程序以托盘 Hub 形式常驻，持有共享的 SolidWorks COM / STA 会话。
- MCP 客户端通过同一个 exe 的 `--proxy` 模式接入。
- 正式链路是 `client -> proxy -> hub -> MCP tools -> SolidWorks`。

## 环境要求

- Windows 10/11 x64
- 本机已安装 SolidWorks，并且 `SldWorks.Application` 可用于 COM 激活
- 如果是源码附近运行，需要 .NET 8 Runtime；正式发布版本是 self-contained 单文件 exe
- 一个支持 stdio 的 MCP 客户端，例如 VS Code 或 Claude Desktop

## SolidWorks 版本支持

| SolidWorks 版本 | 支持状态 | 高风险修改工作流 |
| --- | --- | --- |
| 2024 | Certified | Enabled |
| 2025 | Targeted | Enabled |
| 2026 | Experimental | Blocked |
| 早于 2024 | Unsupported | Blocked |
| 晚于 2026 | Unsupported | Blocked |

## 快速开始

1. 从 [Releases](../../releases) 下载 `SolidWorksMcpApp.exe`。
2. 启动 exe，并确认托盘图标出现。
3. 通过托盘菜单导出客户端配置。
4. 把配置加到你的 MCP 客户端里。

本地使用和测试时，不要直接启动 `SolidWorksBridge`。

## 开发

构建：

```powershell
dotnet build app/SolidWorksMcpApp/SolidWorksMcpApp.csproj -c Release
```

运行非集成测试：

```powershell
dotnet test bridge/SolidWorksBridge.sln --configuration Release --filter "Category!=Integration"
```

通过真实 hub/proxy 链路运行 SolidWorks 集成测试：

```powershell
scripts/test-integration.bat
```

## 仓库结构

- `app/SolidWorksMcpApp/`：托盘程序、Hub/Proxy host、MCP 工具注册、打包入口
- `bridge/SolidWorksBridge/`：SolidWorks COM 服务和 workflow 实现
- `bridge/SolidWorksBridge.Tests/`：单测与集成测试

## 日志

如果工具调用失败，请检查 exe 同级目录下的 `logs/`。

## 许可证

本项目使用 [MIT License](./LICENSE)。
