# SolidWorks MCP Server

SolidWorks MCP Server 是一个面向大模型与自动化代理的 SolidWorks 控制层。项目通过 MCP Server + Windows Named Pipe + C# COM Bridge 的方式，把 SolidWorks 的桌面 COM API 暴露为稳定、可测试、可扩展的工具接口。

这个仓库的目标不是“再包一层脚本”，而是建立一条清晰的调用链：

1. 大模型或代理通过 MCP Tool 发起 CAD 操作。
2. Node.js MCP Server 负责参数校验、工具注册和协议适配。
3. C# Bridge 负责 Windows Named Pipe 通信、消息路由和 SolidWorks COM 调用。
4. SolidWorks 执行实际的建模、选择、草图、特征和装配操作。

## 项目目标

- 为大模型提供稳定的 SolidWorks 自动化接口。
- 将 CAD 操作拆分为小而明确的工具，便于组合调用。
- 保持桥接层可测试，避免把逻辑直接耦合在 SolidWorks 进程里。
- 用明确的分层结构降低后续扩展成本。
- 让人类开发者和 Vibe coding 大模型都能快速理解项目边界。

## 架构总览

```text
LLM / MCP Client
				|
				v
Node.js MCP Server
	- tool registry
	- zod validation
	- MCP stdio transport
	- named pipe client
				|
				v
Windows Named Pipe
	- length-prefixed JSON
	- request/response correlation
				|
				v
C# SolidWorks Bridge
	- pipe server
	- message handler
	- service layer
	- COM access wrapper
				|
				v
SolidWorks COM API
	- document
	- selection
	- sketch
	- feature
	- assembly
```

## 技术方案

### 1. 分层设计

项目采用双进程分层：

- `mcp-server/` 负责对外协议，即 MCP Server。
- `bridge/` 负责对内执行，即 SolidWorks COM Bridge。

这样设计的原因：

- Node.js 更适合接入 MCP SDK、做 schema 校验和工具编排。
- C# 更适合在 Windows 环境下稳定调用 COM API。
- 两层之间通过 Named Pipe 解耦，便于单独测试和调试。

### 2. 通信协议

Bridge 与 MCP Server 使用长度前缀的 JSON 消息协议：

- 帧格式：`[4-byte little-endian length][UTF-8 JSON body]`
- 消息模型：`PipeRequest` / `PipeResponse`
- 请求通过 `id` 做关联，支持 request-response 模式

协议相关代码位于：

- `bridge/SolidWorksBridge/Models/PipeMessage.cs`
- `bridge/SolidWorksBridge/PipeServer/`
- `mcp-server/src/types/solidworks.ts`
- `mcp-server/src/transport/named-pipe-client.ts`

### 3. 服务层设计

C# Bridge 不是直接把所有逻辑写在 `Program.cs` 里，而是拆成独立服务：

- `DocumentService`：文档连接、打开、关闭、保存、查询
- `SelectionService`：按名称选择、列出可选拓扑实体、按索引精确选择、清空选择
- `SketchService`：进入草图、退出草图、绘制草图实体
- `FeatureService`：拉伸、切除、旋转、圆角、倒角、抽壳、简单孔
- `AssemblyService`：插入零件、配合、列出装配体组件

`AppBootstrapper` 负责依赖装配和 method handler 注册，避免主程序和业务逻辑耦合。

### 4. 可测试性方案

项目从一开始就按“可 mock、可单测、可集成验证”来设计：

- 通过 `ISldWorksApp` 抽象 SolidWorks COM 对象。
- 通过服务接口隔离业务逻辑。
- 用单元测试验证消息路由、参数传递、错误处理。
- 用集成测试直接驱动真实 SolidWorks 验证行为。

这套方案的核心目的是：

- 不把复杂逻辑锁死在真实 SolidWorks 环境里。
- 每新增一个能力，都能先做设计、再写测试、最后做真实集成验证。

## 仓库结构

```text
solidworks-mcp-server/
├─ README.md
├─ bridge/
│  ├─ SolidWorksBridge.sln
│  ├─ SolidWorksBridge/
│  │  ├─ Program.cs
│  │  ├─ AppBootstrapper.cs
│  │  ├─ Models/
│  │  ├─ PipeServer/
│  │  └─ SolidWorks/
│  └─ SolidWorksBridge.Tests/
├─ mcp-server/
│  ├─ package.json
│  ├─ src/
│  │  ├─ index.ts
│  │  ├─ transport/
│  │  ├─ types/
│  │  └─ tools/
│  └─ tests/
└─ scripts/
	 ├─ test-all.bat
	 └─ test-integration.bat
```

## 当前能力范围

### 文档能力

- 连接 SolidWorks
- 断开连接
- 新建文档
- 打开文档
- 关闭文档
- 保存文档
- 列出打开文档
- 获取当前活动文档

### 选择能力

- 按名称和类型选择对象
- 清空选择

### 草图能力

- 进入草图
- 退出草图
- 画线
- 画圆
- 画矩形
- 画圆弧

### 特征能力

- 拉伸实体
- 拉伸切除
- 旋转体
- 圆角
- 倒角
- 抽壳
- 简单孔

### 装配能力

- 插入组件
- Coincident 配合
- Concentric 配合
- Parallel 配合
- Distance 配合
- Angle 配合
- 列出装配体顶层组件

## 安装要求

### 系统要求

- Windows
- 已安装 SolidWorks
- 已安装 .NET 8 SDK
- 已安装 Node.js 18+ 或更高版本

### SolidWorks 依赖要求

Bridge 项目直接引用 SolidWorks Interop DLL，默认路径为：

```text
C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll
C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.swconst.dll
```

如果你的 SolidWorks 安装路径不同，需要同步修改：

- `bridge/SolidWorksBridge/SolidWorksBridge.csproj`
- `bridge/SolidWorksBridge.Tests/SolidWorksBridge.Tests.csproj`

### Node.js 依赖安装

```powershell
cd mcp-server
npm install
```

### .NET 依赖恢复

```powershell
cd bridge
dotnet restore
```

## 本地构建

### 构建 C# Bridge

```powershell
cd bridge
dotnet build SolidWorksBridge.sln
```

### 构建 MCP Server

```powershell
cd mcp-server
npm run build
```

## 本地运行

### 1. 启动 C# Bridge

```powershell
cd bridge\SolidWorksBridge
dotnet run
```

Bridge 启动后会在 STA 线程上监听名为 `SolidWorksMcpBridge` 的 Named Pipe。

### 2. 启动 MCP Server

```powershell
cd mcp-server
npm run build
npm start
```

MCP Server 使用 stdio transport，对外提供 MCP tools；对内通过 Named Pipe 连接 C# Bridge。

## 按 MCP 流程部署项目

如果你的目标是把这个项目作为一个本地 MCP Server 提供给大模型使用，推荐按下面的流程部署。

### 第 1 步：本地部署构建产物

在仓库根目录执行：

```powershell
scripts\deploy-local.bat
```

这个脚本会完成三件事：

1. 以 `Release` 模式构建 C# Bridge。
2. 安装 `mcp-server/` 的 Node.js 依赖。
3. 构建 MCP Server 的 `dist/` 输出。

### 第 2 步：启动 MCP Server

MCP Server 本身是 stdio 模式，本地入口为：

```powershell
cd mcp-server
node dist/index.js
```

从当前版本开始，MCP Server 在启动时会自动检查 `SolidWorksMcpBridge` Named Pipe：

1. 如果 bridge 已经在运行，MCP 会直接复用它。
2. 如果 bridge 尚未运行，MCP 会自动在后台拉起 bridge，再继续对外提供 tools。

一般情况下，你不需要手工启动这一步，因为 VS Code 会通过 `mcp.json` 自动启动它。

### 手工启动 Bridge 的用途

如果你要单独调试 bridge，仍然可以手工执行：

```powershell
scripts\start-bridge.bat
```

这个脚本现在的语义是“确保 bridge 已运行并等待 pipe 就绪”，适合用于诊断或单独联调。

### 部署后的目录约定

部署完成后，关键入口如下：

- C# Bridge 启动脚本：`scripts\start-bridge.bat`
- MCP Server 配置：`.vscode\mcp.json`
- MCP Server 运行入口：`mcp-server\dist\index.js`

### MCP 部署注意事项

1. VS Code 会直接启动 Node.js MCP Server，而 MCP Server 会在自身启动阶段自动确保 C# Bridge 可用。
2. 如果 bridge 可执行文件尚未构建，自动启动会失败，所以仍然应先执行 `scripts\deploy-local.bat`。
3. `scripts\start-bridge.bat` 仍然保留，用于单独排查 bridge 是否能正常拉起。
4. 当前方案面向本机开发和本机 CAD 自动化，不是远程 HTTP MCP Server。

## 在 VS Code 中接入这个 MCP

### 方式 1：直接使用仓库内置配置

仓库已经提供了可直接使用的配置文件：

- `.vscode/mcp.json`
- `.vscode/tasks.json`

其中：

- `.vscode/mcp.json` 用来告诉 VS Code 如何启动这个 MCP Server。
- `.vscode/tasks.json` 提供了部署和启动 Bridge 的任务。

### VS Code 接入步骤

1. 在 VS Code 打开这个仓库根目录。
2. 先执行任务 `Deploy SolidWorks MCP`。
3. 打开命令面板，运行 `MCP: List Servers`，确认 `solidworks-mcp-server` 已被识别。
4. 第一次启动时，VS Code 会要求你确认是否信任这个 MCP Server。
5. 信任后，Chat / Agent 就能看到这个服务器暴露的 tools。
6. 如果你只想单独验证 bridge 是否能起来，再额外执行任务 `Start SolidWorks Bridge`。

### `.vscode/mcp.json` 说明

当前仓库内置的 VS Code MCP 配置等价于：

```json
{
	"servers": {
		"solidworks-mcp-server": {
			"type": "stdio",
			"command": "C:/Program Files/nodejs/node.exe",
			"args": [
				"${workspaceFolder}/mcp-server/dist/index.js"
			],
			"cwd": "${workspaceFolder}/mcp-server"
		}
	}
}
```

这表示：

- VS Code 会把它当成本地 stdio MCP Server 启动。
- 启动命令是显式的 `node.exe mcp-server/dist/index.js`，避免 VS Code 进程里出现 `spawn node ENOENT`。
- 服务工作目录是 `mcp-server/`。
- MCP Server 进程启动后，会自动检查并拉起 C# Bridge。

### 方式 2：手工添加到用户级 MCP 配置

如果你不想把配置放在工作区，也可以在 VS Code 中运行：

```text
MCP: Open User Configuration
```

然后把同样的 server 配置加到用户级 `mcp.json` 中。这样它会在你的 VS Code 用户配置里全局可用。

### 在 VS Code 里如何验证接入成功

接入成功后，你应该能看到以下现象：

1. `MCP: List Servers` 中存在 `solidworks-mcp-server`。
2. 服务器状态为已启用、可启动。
3. Chat 中可以配置该服务器的 tools。
4. 在向 Copilot / Agent 提问时，能看到它尝试调用诸如 `sw_connect`、`sw_new_document` 之类的工具。

### 在 VS Code 中的推荐使用方式

推荐按下面顺序使用：

1. 先执行任务 `Deploy SolidWorks MCP`，确保本地构建产物完整。
2. 再让 VS Code 自动启动 `solidworks-mcp-server`。
3. 在 Chat 里先做小范围验证，例如：

```text
连接 SolidWorks，并告诉我当前活动文档是什么。
```

4. 验证成功后，再执行草图、特征、装配等复杂操作。

### 常见接入问题

#### 1. VS Code 里能看到 MCP Server，但工具调用失败

通常说明 Node.js MCP Server 已启动，但 bridge 自动拉起失败，或者 Named Pipe 未连通。

先检查：

- 是否已经先执行 `scripts\deploy-local.bat`
- SolidWorks 是否可连接
- 必要时再单独执行 `scripts\start-bridge.bat`，确认 bridge 能否独立监听 `SolidWorksMcpBridge`

#### 2. VS Code 里看不到这个 MCP Server

先检查：

- `.vscode/mcp.json` 是否存在且格式正确
- 当前打开的是不是仓库根目录
- VS Code 是否支持 MCP Server 功能并已启用相关能力

#### 2.1 VS Code 日志里出现 `spawn node ENOENT`

这说明 VS Code 的 MCP 扩展宿主没有找到 `node` 可执行文件，通常是因为它没有继承到你的系统 PATH。

当前仓库已经把 `.vscode/mcp.json` 配置为显式 Node 路径：

```json
"command": "C:/Program Files/nodejs/node.exe"
```

如果你的 Node.js 不在这个位置，请把 `.vscode/mcp.json` 里的 `command` 改成你本机实际的 `node.exe` 路径。

#### 3. VS Code 提示需要信任服务器

这是正常流程。由于本地 stdio MCP Server 可以执行本机命令，VS Code 会要求你显式信任。

#### 4. 修改 `mcp.json` 后没有立即生效

可以在 VS Code 中执行：

- `MCP: List Servers`
- 或手动重启 VS Code / 重启 MCP Server

必要时检查 MCP 输出日志。

## MCP 服务使用说明

这一节是按照 MCP 服务面向大模型使用的标准表达方式整理的，目标是让模型在不知道仓库内部实现细节的情况下，也能正确、安全地调用本服务。

### 服务标识

```json
{
	"name": "solidworks-mcp-server",
	"version": "0.1.0",
	"transport": "stdio",
	"runtime": "node",
	"backend": "C# SolidWorks Bridge over Windows Named Pipe"
}
```

### 服务职责

这个 MCP 服务用于驱动本机 SolidWorks 完成 CAD 自动化操作，当前覆盖五类能力：

- 文档管理
- 选择管理
- 草图绘制
- 三维特征创建
- 装配体操作

### 运行前提

大模型在调用这个服务前，必须默认满足以下前提：

1. 服务运行在 Windows 环境。
2. 本机已安装 SolidWorks。
3. C# Bridge 已启动，且 Named Pipe `SolidWorksMcpBridge` 可连接。
4. MCP Server 已通过 stdio 启动并完成 tool 注册。
5. 如执行集成级操作，SolidWorks 当前环境允许打开、新建和编辑模型。

### 返回值约定

当前 MCP Server 的 tool 调用结果会被包装为 `content[].text` 中的 JSON 文本。

也就是说，大模型应把返回内容当成 JSON 字符串解析，而不是假设它天然已经是结构化对象。

### 工具清单

#### 文档工具

- `sw_connect`
	作用：连接正在运行的 SolidWorks 实例。
	输入：无。
	典型返回：`{"connected": true}`

- `sw_disconnect`
	作用：断开与 SolidWorks 的连接。
	输入：无。
	典型返回：`{"connected": false}`

- `sw_new_document`
	作用：新建文档。
	输入：`type` 为 `Part | Assembly | Drawing`，可选 `templatePath`。
	典型返回：`{"path":"...","title":"...","type":1|2|3}`

- `sw_open_document`
	作用：打开已有 SolidWorks 文件。
	输入：`path`

- `sw_close_document`
	作用：关闭已打开文档。
	输入：`path`

- `sw_save_document`
	作用：保存文档。
	输入：`path`

- `sw_list_documents`
	作用：列出所有当前打开文档。
	输入：无。

- `sw_get_active_document`
	作用：获取当前活动文档。
	输入：无。

#### 选择工具

- `sw_select_by_name`
	作用：按名称和类型选择实体。
	输入：`name`, `selType`
	常见 `selType`：`PLANE`, `EDGE`, `FACE`, `VERTEX`

- `sw_list_entities`
	作用：列出当前活动零件或装配体里可直接参与后续特征/配合的拓扑实体。
	输入：可选 `entityType` 为 `Face | Edge | Vertex`，可选 `componentName` 用于装配体过滤。
	典型返回：`[{"index":0,"entityType":"Edge","componentName":null,"box":[...]}]`

- `sw_select_entity`
	作用：按 `sw_list_entities` 返回的索引精确选择实体。
	输入：`entityType`, `index`，可选 `append`, `mark`, `componentName`
	说明：高级建模应优先使用此工具选择边/面/点，而不是假设这些实体有稳定名称。

- `sw_clear_selection`
	作用：清空当前选择。
	输入：无。

#### 草图工具

- `sw_insert_sketch`
	作用：在当前已选平面或面上进入草图。
	输入：无。

- `sw_finish_sketch`
	作用：退出当前草图。
	输入：无。

- `sw_add_line`
	作用：添加草图线段。
	输入：`x1`, `y1`, `x2`, `y2`

- `sw_add_circle`
	作用：添加草图圆。
	输入：`cx`, `cy`, `radius`

- `sw_add_rectangle`
	作用：添加草图矩形。
	输入：`x1`, `y1`, `x2`, `y2`

- `sw_add_arc`
	作用：添加草图圆弧。
	输入：`cx`, `cy`, `x1`, `y1`, `x2`, `y2`, `direction`
	约束：`direction` 只能为 `1` 或 `-1`

#### 特征工具

- `sw_extrude`
	作用：把当前草图拉伸为实体。
	输入：`depth`，可选 `endCondition`, `flipDirection`
	约束：调用时草图应仍处于编辑状态。

- `sw_extrude_cut`
	作用：把当前草图拉伸切除。
	输入：`depth`，可选 `endCondition`, `flipDirection`
	约束：调用时草图应仍处于编辑状态。

- `sw_revolve`
	作用：旋转当前草图。
	输入：`angleDegrees`，可选 `isCut`

- `sw_fillet`
	作用：对选中的边执行圆角。
	输入：`radius`

- `sw_chamfer`
	作用：对选中的边执行倒角。
	输入：`distance`

- `sw_shell`
	作用：对实体执行抽壳。
	输入：`thickness`

- `sw_simple_hole`
	作用：创建简单孔。
	输入：`diameter`, `depth`

#### 装配工具

- `sw_insert_component`
	作用：在当前活动装配体中插入零件或子装配。
	输入：`filePath`，可选 `x`, `y`, `z`

- `sw_add_mate_coincident`
	作用：添加 Coincident 配合。
	输入：可选 `align`

- `sw_add_mate_concentric`
	作用：添加 Concentric 配合。
	输入：可选 `align`

- `sw_add_mate_parallel`
	作用：添加 Parallel 配合。
	输入：可选 `align`

- `sw_add_mate_distance`
	作用：添加 Distance 配合。
	输入：`distance`，可选 `align`

- `sw_add_mate_angle`
	作用：添加 Angle 配合。
	输入：`angleDegrees`，可选 `align`

- `sw_list_components`
	作用：列出当前活动装配体的顶层组件。
	输入：无。

### 参数规则

大模型在构造参数时，需要遵守以下规则：

1. 所有长度和坐标统一使用米。
2. 角度参数统一使用度，除非工具说明明确另有规定。
3. 枚举值使用字符串字面量，不要自行发明缩写。
4. `endCondition` 仅使用 `Blind`、`ThroughAll`、`MidPlane`。
5. `align` 仅使用 `None`、`AntiAligned`、`Closest`。
6. 不要传额外字段，避免被 schema 校验拒绝。

### 调用顺序规则

大模型最容易出错的是操作顺序。这个服务不是任意工具都能自由拼接，必须遵循 SolidWorks 的上下文要求。

#### 零件建模最小顺序

1. `sw_connect`
2. `sw_new_document` with `type=Part`
3. `sw_select_by_name` 选择草图平面
4. `sw_insert_sketch`
5. `sw_add_line` / `sw_add_circle` / `sw_add_rectangle` / `sw_add_arc`
6. 直接调用 `sw_extrude` 或 `sw_extrude_cut`

注意：对于当前实现，`sw_extrude` 和 `sw_extrude_cut` 应在草图仍处于编辑状态时调用，不应默认先执行 `sw_finish_sketch`。

#### 装配最小顺序

1. `sw_connect`
2. `sw_new_document` with `type=Assembly`
3. `sw_insert_component`
4. 先用 `sw_list_entities` 确认目标实体，再用 `sw_select_entity` 逐个选中待配合实体
5. 调用对应的 `sw_add_mate_*`

### 推荐的模型行为

当大模型使用本服务时，推荐遵循以下行为策略：

1. 每次执行复杂 CAD 操作前，先确认当前活动文档类型是否正确。
2. 在进入草图前，先明确选择了正确的平面或面。
3. 在做特征前，先确认草图已经绘制完成且上下文仍有效。
4. 在做圆角、倒角、孔、装配配合前，先用拓扑查询确认边/面/点的索引，再做精确选择。
5. 在做装配配合前，先确认已经选中了兼容实体。
6. 遇到失败时，优先重新获取活动文档和当前上下文，而不是盲目重复同一调用。

### 建议错误恢复策略

如果 tool 调用失败，大模型应按下面顺序恢复：

1. 读取错误文本，提取失败原因。
2. 判断是否为连接问题，如果是，先重新 `sw_connect`。
3. 判断是否为文档上下文问题，如果是，重新获取或新建正确文档。
4. 判断是否为选择问题，如果是，重新选择目标实体。
5. 判断是否为草图状态问题，如果是，重新建立草图上下文。

### 典型调用示例

#### 示例 1：创建零件并拉伸矩形

```text
sw_connect
sw_new_document({"type":"Part"})
sw_select_by_name({"name":"前视基准面","selType":"PLANE"})
sw_insert_sketch({})
sw_add_rectangle({"x1":-0.05,"y1":-0.03,"x2":0.05,"y2":0.03})
sw_extrude({"depth":0.02})
```

#### 示例 2：创建装配并插入组件

```text
sw_connect
sw_new_document({"type":"Assembly"})
sw_insert_component({"filePath":"C:\\Models\\part1.sldprt","x":0,"y":0,"z":0})
sw_list_components({})
```

### 给大模型的硬性约束

1. 不要假设 SolidWorks 使用英文界面。
2. 不要假设基准面名称一定是 `Front Plane`、`Top Plane`、`Right Plane`。
3. 不要把毫米直接传给工具，除非先换算成米。
4. 不要在没有选择对象的情况下调用依赖选择的工具。
5. 不要在没有草图上下文的情况下调用草图或特征工具。
6. 不要把 MCP 层返回的文本结果当成未解析对象直接使用。

## 测试

### 运行全部单元测试

```powershell
scripts\test-all.bat
```

这个脚本会执行：

- Node.js Vitest 测试
- C# Bridge 非集成测试

### 运行 C# 集成测试

```powershell
scripts\test-integration.bat
```

说明：

- 集成测试要求本机安装并可启动 SolidWorks。
- 测试会直接操作真实 SolidWorks。
- 测试依赖当前机器的 SolidWorks 环境、模板和 COM 可用性。

### 手动运行命令

Node.js：

```powershell
cd mcp-server
npm test
```

C# 单元测试：

```powershell
cd bridge
dotnet test SolidWorksBridge.sln --filter "Category!=Integration"
```

C# 集成测试：

```powershell
cd bridge
dotnet test SolidWorksBridge.sln --filter "Category=Integration"
```

## 开发与扩展方式

新增一个能力时，推荐遵循下面的顺序：

1. 在 C# Bridge 设计服务接口和返回模型。
2. 先写单元测试，确保参数传递和错误处理清晰。
3. 实现真实 COM 调用。
4. 写集成测试，在真实 SolidWorks 中验证行为。
5. 在 Node.js `src/tools/` 中新增对应工具封装。
6. 在 `src/index.ts` 注册 MCP Tool。
7. 补充对应的 Vitest 测试。
8. 最后更新 README。

这个顺序的目的是先锁定接口和行为，再接入真实 CAD 环境，避免一开始就陷入 COM 调试泥潭。

## 贡献指南

欢迎提交功能、修复和改进，但请遵循以下规则：

1. 修改前先明确能力边界，不要把多个不相关需求混在一个提交里。
2. 所有新功能至少补充对应单元测试。
3. 涉及真实 SolidWorks 行为的功能，优先补充集成测试。
4. 尽量保持 MCP 层、Pipe 协议层、Bridge 服务层职责清晰。
5. 不要在没有必要的情况下改动公共协议字段名。
6. 不要把仅适合本机环境的路径、模板、配置写死到通用逻辑里。

推荐提交流程：

1. Fork 或建立功能分支。
2. 完成最小必要改动。
3. 运行单元测试。
4. 如涉及 SolidWorks 行为变更，运行集成测试。
5. 提交 Merge Request / Pull Request，并写清楚变更背景、影响范围和验证方式。

## Bug 提交指南

提交 bug 时，请尽量包含以下信息，否则问题很难复现：

### 基础环境

- Windows 版本
- SolidWorks 版本
- .NET SDK 版本
- Node.js 版本
- 是否中文界面或其他本地化界面

### 复现信息

- 复现步骤
- 预期结果
- 实际结果
- 最小可复现输入
- 是否稳定复现

### 日志与错误

- Bridge 控制台输出
- MCP Server 控制台输出
- SolidWorks 是否弹出对话框或报错
- 相关截图或录屏

### 如果问题与 CAD 本体有关

请额外说明：

- 当前打开的是零件、装配体还是工程图
- 当前选中的对象类型
- 使用的模板或模型文件
- 是否为中文基准面名称环境

## 给 Vibe coding / 大模型的项目理解指南

这一节是写给会直接阅读仓库并生成代码的大模型、代理和自动化编程系统的。

### 先理解什么

如果你是一个接手这个项目的大模型，先按这个顺序理解：

1. 读 `README.md`，先建立边界感。
2. 读 `bridge/SolidWorksBridge/Program.cs`，理解进程入口和 Pipe 名称。
3. 读 `bridge/SolidWorksBridge/AppBootstrapper.cs`，理解所有 method 到 service 的映射。
4. 读 `bridge/SolidWorksBridge/SolidWorks/`，理解真实业务能力边界。
5. 读 `mcp-server/src/tools/`，理解 MCP Tool 与桥接方法名的对应关系。
6. 读 `mcp-server/src/index.ts`，理解 Tool 注册和输入校验入口。
7. 最后读测试，理解项目真实约束，而不是只看实现猜行为。

### 这个项目最重要的几个事实

大模型必须先知道这些事实，否则很容易改错：

1. 这是 Windows + SolidWorks + COM 项目，不是纯后端服务。
2. 真正执行 CAD 操作的是 C# Bridge，不是 Node.js。
3. Node.js 的职责主要是 MCP Tool 暴露、参数校验和转发。
4. 新能力应该先在 C# Bridge 中落地，再暴露到 Node.js MCP Tool。
5. 测试是这个项目的主要行为文档，尤其是集成测试。

### 不要误判的几个关键约束

1. 草图相关操作和特征相关操作有明确时序，不是任意顺序都能成功。
2. 拉伸和拉伸切除要求在草图仍处于编辑状态时执行，不能默认先 `FinishSketch`。
3. SolidWorks 中文界面下，标准基准面名称不是英文，例如 `前视基准面`。
4. 坐标和尺寸统一使用米作为单位。
5. COM 互操作签名有时和直觉不一致，改 API 调用前必须查真实签名。
6. 看到 `Feature`、`Component2` 这类类型时，不要先假设它们是类；Interop 中很多是接口投影。

### 大模型改代码时的建议策略

1. 先找现有服务和测试中最接近的模式，不要新发明一套风格。
2. 不要直接在 `Program.cs` 里堆业务逻辑。
3. 不要绕过 `AppBootstrapper` 手工硬编码新方法。
4. 不要只改 Node.js 工具而不补 C# Bridge 实现。
5. 不要只改 C# Bridge 而忘了同步 MCP Tool 注册。
6. 不要在没有测试的情况下扩展真实 SolidWorks 操作。

### 大模型最适合做什么

- 新增一个已有模式下的 CAD 服务
- 补测试
- 修复参数映射错误
- 更新 MCP Tool schema
- 对齐 Bridge 与 MCP 方法命名
- 增加 README 和开发说明

### 大模型最容易犯什么错

- 误用 SolidWorks COM API 签名
- 忽略本地化名称差异
- 把草图和特征操作顺序写反
- 在 Node 层伪造业务逻辑，导致 C# 与 MCP 语义脱节
- 修改一个层次却忘记同步另一个层次

### 大模型接手时的最小检查清单

在提交改动前，至少确认：

1. MCP tool 名称、C# method 名称、测试命名是否一致。
2. 新增参数是否在 Node.js Zod schema 和 C# DTO 两端都定义了。
3. 是否已有对应单元测试。
4. 如果涉及真实 SolidWorks 行为，是否有集成测试。
5. 是否破坏了现有 Named Pipe 协议字段。

## 设计原则

- 先分层，再扩展。
- 先测试，再接真实 SolidWorks。
- 先最小能力闭环，再继续下一个服务。
- 明确输入、输出和失败路径。
- 保持工具粒度小且可组合。

## 常见问题

### 为什么不用纯 Node.js 直接调 SolidWorks？

因为项目运行在 Windows 且依赖 COM，C# 在这条链路上的稳定性、类型能力和调试体验都更合适。

### 为什么还要单独做 MCP Server？

因为 MCP 侧关注的是 tool schema、协议接入和大模型使用体验，这部分用 Node.js 和官方 MCP SDK 更自然。

### 为什么强调先测试再扩展？

SolidWorks COM 行为存在环境依赖、时序依赖和本地化依赖。如果不先把预期行为写进测试，后续维护成本会迅速失控。

## 许可证

当前仓库未在本 README 中单独声明额外许可证约束。若需要对外分发或开源，请在仓库根目录补充正式 License 文件并同步说明。

## Suggestions for a good README

Every project is different, so consider which of these sections apply to yours. The sections used in the template are suggestions for most open source projects. Also keep in mind that while a README can be too long and detailed, too long is better than too short. If you think your README is too long, consider utilizing another form of documentation rather than cutting out information.

## Name
Choose a self-explaining name for your project.

## Description
Let people know what your project can do specifically. Provide context and add a link to any reference visitors might be unfamiliar with. A list of Features or a Background subsection can also be added here. If there are alternatives to your project, this is a good place to list differentiating factors.

## Badges
On some READMEs, you may see small images that convey metadata, such as whether or not all the tests are passing for the project. You can use Shields to add some to your README. Many services also have instructions for adding a badge.

## Visuals
Depending on what you are making, it can be a good idea to include screenshots or even a video (you'll frequently see GIFs rather than actual videos). Tools like ttygif can help, but check out Asciinema for a more sophisticated method.

## Installation
Within a particular ecosystem, there may be a common way of installing things, such as using Yarn, NuGet, or Homebrew. However, consider the possibility that whoever is reading your README is a novice and would like more guidance. Listing specific steps helps remove ambiguity and gets people to using your project as quickly as possible. If it only runs in a specific context like a particular programming language version or operating system or has dependencies that have to be installed manually, also add a Requirements subsection.

## Usage
Use examples liberally, and show the expected output if you can. It's helpful to have inline the smallest example of usage that you can demonstrate, while providing links to more sophisticated examples if they are too long to reasonably include in the README.

## Support
Tell people where they can go to for help. It can be any combination of an issue tracker, a chat room, an email address, etc.

## Roadmap
If you have ideas for releases in the future, it is a good idea to list them in the README.

## Contributing
State if you are open to contributions and what your requirements are for accepting them.

For people who want to make changes to your project, it's helpful to have some documentation on how to get started. Perhaps there is a script that they should run or some environment variables that they need to set. Make these steps explicit. These instructions could also be useful to your future self.

You can also document commands to lint the code or run tests. These steps help to ensure high code quality and reduce the likelihood that the changes inadvertently break something. Having instructions for running tests is especially helpful if it requires external setup, such as starting a Selenium server for testing in a browser.

## Authors and acknowledgment
Show your appreciation to those who have contributed to the project.

## License
For open source projects, say how it is licensed.

## Project status
If you have run out of energy or time for your project, put a note at the top of the README saying that development has slowed down or stopped completely. Someone may choose to fork your project or volunteer to step in as a maintainer or owner, allowing your project to keep going. You can also make an explicit request for maintainers.
