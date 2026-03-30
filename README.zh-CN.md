# SolidWorks MCP Server

[![English](https://img.shields.io/badge/English-README-black)](./README.md)

SolidWorks MCP Server 是一个面向 Windows 的 SolidWorks MCP 服务，供 VS Code、Copilot 以及其他 MCP 客户端调用。

项目由两层组成：

- Node.js MCP Server：负责 MCP stdio、参数校验和 tool 注册。
- C# Bridge：负责通过本地 Named Pipe 调用 SolidWorks COM API。

## 项目目标

- 给 AI 代理提供稳定的 SolidWorks 自动化接口。
- 把 CAD 操作拆成小而明确的工具。
- 把 MCP 协议层和 SolidWorks COM 执行层解耦。
- 同时支持源码开发、npm 分发和 VS Code 安装。

## 当前能力

- 文档工具：连接、创建、打开、保存、关闭、列出、查询活动文档。
- 选择工具：按名称选择、拓扑枚举、按索引精确选择、清空选择。
- 草图工具：点、椭圆、多边形、文本、线、圆、矩形、圆弧。
- 特征工具：拉伸、切除、旋转、圆角、倒角、抽壳、简单孔。
- 装配工具：插入组件、列出组件、核心配合工具。

## 环境要求

- Windows
- 本机已安装 SolidWorks
- 源码构建需要 .NET 8 SDK
- Node.js 18+
- 如果要在编辑器里使用，建议使用支持 MCP 的 VS Code

当前 bridge 仍然直接引用本机 SolidWorks Interop DLL，所以分发目标仍然是“已安装 SolidWorks 的 Windows 机器”。

## 快速开始

### 从源码开发运行

```powershell
cd mcp-server
npm install
npm run build

cd ..\bridge
dotnet build SolidWorksBridge.sln
```

运行 MCP server：

```powershell
cd mcp-server
node dist/index.js
```

服务会优先复用已有的 `SolidWorksMcpBridge` pipe，必要时自动拉起 bridge。

### 在 VS Code 中本地开发使用

仓库已经自带 [.vscode/mcp.json](.vscode/mcp.json)，适合仓库内开发调试。

1. 先构建 bridge 和 MCP server。
2. 用 VS Code 打开仓库根目录。
3. 执行 `MCP: List Servers`。
4. 首次提示时信任 `solidworks-mcp-server`。

### 通过 npm 在 VS Code 中使用

发布到 npm 后，推荐先安装全局命令：

```powershell
npm install -g solidworks-mcp
```

然后在用户级或工作区级 `mcp.json` 中注册服务：

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

如果 VS Code 在 Windows 下没有继承 npm 全局 bin 目录，建议直接写入 shim 的绝对路径：

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

如果你不想全局安装，也可以直接使用 `npx`：

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

## npm 分发

可发布的 npm 包位于 [mcp-server/package.json](mcp-server/package.json)。

包内包含：

- `dist/` 编译后的 MCP server
- `vendor/bridge/` 发布后的 `SolidWorksBridge.exe` 产物
- `bin/solidworks-mcp-server.cmd` Windows CLI 入口

本地生成 tarball：

```powershell
cd mcp-server
npm pack
```

`npm pack` 会触发 `prepack`，自动完成：

1. 构建 TypeScript MCP server。
2. 以 Release 模式发布 C# bridge。
3. 把 bridge 打包进 `mcp-server/vendor/bridge`。

未来发布到 npm 后，推荐安装方式为：

```powershell
npm install -g solidworks-mcp
solidworks-mcp-server
```

如果你需要覆盖 bridge 路径，可以设置 `SOLIDWORKS_MCP_BRIDGE_ROOT`，其值应指向包含 `SolidWorksBridge.exe` 的目录。

## 仓库结构

```text
solidworks-mcp-server/
├─ bridge/            # C# COM bridge 和测试
├─ mcp-server/        # 可发布 npm 包
├─ scripts/           # 仓库级构建与测试脚本
└─ .vscode/           # 本地开发用 MCP 配置
```

## 测试

Node 测试：

```powershell
cd mcp-server
npm test
```

Bridge 测试：

```powershell
cd bridge
dotnet test SolidWorksBridge.sln
```

## 开发说明

- MCP 当前仍通过 `content[].text` 返回 JSON 文本结果。
- `sw_extrude` 和 `sw_extrude_cut` 仍要求在草图编辑态下直接调用。
- 仓库里的 `.vscode/mcp.json` 保留给本地开发使用；最终用户可以直接把已发布的 npm 包接入 VS Code。

## 发布检查清单

1. 在 [mcp-server](mcp-server) 里执行 `npm test`。
2. 在 [bridge](bridge) 里执行 `dotnet test SolidWorksBridge.sln`。
3. 在 [mcp-server](mcp-server) 里执行 `npm pack` 并检查 tarball。