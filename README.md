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
- `SelectionService`：选择对象、清空选择
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
