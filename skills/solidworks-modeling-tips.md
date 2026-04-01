# SolidWorks 建模技巧（Skill Notes）

> 目的：把常用的 SOLIDWORKS 建模“稳定性/可维护性/效率”技巧整理成可复用清单；内容为基于公开教程的归纳总结（非逐字摘录）。

## 1) 草图与设计意图（Design Intent）

- **尽量让草图“少而稳”**：能用多个简单草图/特征表达，就不要在一个草图里塞太多几何。
- **尽可能 Fully Define（完全定义）**：用尺寸 + 关系让关键几何稳定。
- **避免“链式依赖”**：不要让 A 驱动 B、B 再驱动 C…（daisy chaining）。更稳的做法是让 A 直接驱动 B 和 C。

## 2) 镜像：Mirror Entities vs Dynamic Mirror

- **Mirror Entities**：适合草图完成后再镜像，依赖“镜像中心线 + 选择要镜像的实体”。
- **Dynamic Mirror**：适合边画边镜像；先选中心线/模型边进入动态镜像模式，再开始画。
- **用镜像保持对称一致**：对称结构优先用镜像/中面来保证可修改性。

## 3) 参考几何：Reference Planes 的常见用法

- **Offset Plane（偏置平面）**：从面/基准面偏置一段距离，用于“在曲面附近做草图/切口”或做局部特征。
- **Angle Plane（角度平面）**：需要一个面/基准面 + 一个旋转轴（模型边或草图线）。
- **Mid Plane（中面）**：两面之间生成中面；常用于建立对称镜像基准。
- **Cylindrical Surface Plane（圆柱相关平面）**：用于圆柱面上的切割/定位（可能需要额外选择来确定方向）。

## 4) 圆角/倒角的组织方式（稳定性与失败率）

- **尽量把大范围圆角放在后面**：主形体（拉伸/放样/旋转）先稳定，再加圆角细节。
- **需要调整/修复圆角时用 FilletXpert**：
  - `Add` 批量加圆角且不退出 PropertyManager
  - `Change` 统一改半径、删除某些圆角
  - `Corner` 处理三圆角交汇点，或复制角落条件

## 5) 外部引用（External References）与循环引用（Circular References）

- **Top-down 适合前期快速设计**；设计定型后，**建议用尺寸/关系替代外部引用**，让零件“自洽”。
- **不要盲目 Break References**：很多教程建议更安全的方式是“替换草图平面/替换关系 + 重新完全定义”。
- **避免循环引用的实践要点**：
  - 避免 daisy chaining
  - 外部引用尽量挂在“关键零部件”，并保证这些关键件自身不要再依赖外部引用
  - 避免跨层级（顶层组件 ↔ 子装配内组件）建立关系
  - 避免给“已经有外部引用”的特征再添加新的外部引用
  - 谨慎对待装配级特征（孔向导/阵列/装配切除等）的外部引用

## 6) 断引用/丢文件时的排查思路

- **Find References**：优先用 `File > Find References...` 看缺的是哪一个、期望路径是什么。
- **不要一上来就保存**：对“修复引用”类操作，很多情况下先 `Open` 时用 `References...` 重新指向，确认无误再保存。
- **替换规则**：零件只能替换为零件、子装配只能替换为子装配。

## 7) MCP 自动化建模（本仓库使用经验补充）

- **每次选择前先清 Selection**：减少 InsertSketch 失败/宿主不对的问题。
- **草图必须优先挂到实体面上**：先选中真实实体上的平面面，再 `InsertSketch`；不要“凭空”建草图后再赌它落在哪个平面上。
- **读树或删除前先查编辑态**：先 `GetEditState`；如果 `IsEditing = true`，先 `FinishSketch`，再去 `ListFeatureTree`、`DeleteFeatureByName`、`DeleteUnusedSketches`。
- **把“退出草图编辑”当成清理步骤的一部分**：树读取、孤立草图清理、按名字删特征，都应该发生在非编辑态，而不是边编辑边处理。
- **面上做切除要检查方向**：如果是从顶面往实体内部做浅切，通常需要把切除方向翻进实体内部，否则容易直接失败。
- **修干涉优先改非功能面**：像皮带滑轮支架这类零件，优先减掉顶面余量或做让位，不要随便改滑轮孔位和孔径。
- **`select_by_name` 选基准面时优先用 `PLANE` 兜底**：某些环境 `swSelDATUMPLANES` 可能不稳定。
- **拉伸/切除/旋转前确保闭合轮廓**：开放轮廓会导致特征失败（本项目桥接层已加预检，会直接报错）。
- **FinishSketch 后再 Extrude 也要能预检**：本项目已支持从顶层 ProfileFeature 解析草图来预检。

## 8) Hub / COM 恢复流程（本仓库已验证）

- **症状识别**：如果 `get_active_document`、`list_documents`、`list_components` 之前还能用，后来同时开始报 `0x800706BA`，优先怀疑 Hub 缓存了一个已经失活的 `ISldWorks` COM 会话。
- **日志判据**：如果日志里一边报 `RPC server unavailable`，一边又说 `Connect reused the existing SolidWorks session`，说明不是“没连上”，而是“错误复用了死连接”。
- **稳定验证顺序**：修复或重启 Hub 后，先按 `connect -> get_active_document -> list_documents -> list_components` 这条顺序验；这条链路全通，再去做建模。
- **重建前先停旧 Hub**：如果 `SolidWorksBridge.dll` 被 `SolidWorksMcpApp` 锁住，先结束旧的托盘进程再 `dotnet build`，否则代码改对了也落不到运行中的 Hub 上。
- **当前桥接层策略**：`Connect()` / `EnsureConnected()` 现在会先探测连接活性；遇到已知断链 HRESULT 会丢弃旧 COM 包装并重新附着或新建实例。

## 9) SolidWorks UI 交互理念与特征树要点

- **CommandManager 是“按上下文切换命令”的入口**：它会跟随文档类型和当前工作阶段切换标签页；操作时先想清楚自己是在草图、特征、装配还是视图上下文。
- **FeatureManager 不是纯展示树，而是“对象定位器”**：图形区和树是联动的；在图形区难以点准时，优先去树里按名字、层级、父子关系定位。
- **PropertyManager 是“当前命令的参数面板”**：它适合改当前特征/配合/草图命令的参数，不适合拿来理解整体模型结构；理解结构还是回到 FeatureManager。
- **左侧树决定理解成本**：重命名关键特征、草图、基准面、组件后，后续改模、排干涉、查父子关系都会快很多。
- **父子关系和重建顺序很关键**：FeatureManager 支持看 Parent/Child，也能拖动特征重排重建顺序；改顺序前先判断设计意图，避免把稳定模型拖坏。
- **装配场景优先看层级，不要只看画面**：大型装配里视觉上“看见了某零件”不等于“当前激活的就是那个零件”；先确认活动文档、组件名、组件路径，再下刀。
- **需要同时看树和参数时，用分屏/飞出树思路**：官方 UI 允许 FeatureManager 与 PropertyManager 共存；对应到 MCP 工作流，就是“先枚举/确认对象，再执行命令，再回到结果验证”。
- **树里的标准项目值得先看一眼**：基准面、Origin、Bodies、Equations、Sensors、Annotations 这些目录经常比直接点模型更快暴露问题来源。

## 参考来源（供继续深挖）

- https://www.goengineer.com/blog/mirror-2d-sketches-in-solidworks-mirror-entities-and-dynamic-mirror-entites
- https://www.goengineer.com/blog/creating-reference-planes-in-solidworks
- https://www.goengineer.com/blog/solidworks-filletxpert-tool-tutorial
- https://www.goengineer.com/blog/removing-external-references-solidworks-files
- https://www.goengineer.com/blog/managing-external-references-solidworks-assemblies
- https://www.goengineer.com/blog/solidworks-circular-references
- https://www.goengineer.com/blog/repair-broken-references-in-solidworks
- https://help.solidworks.com/2024/english/SolidWorks/sldworks/c_commandmanager.htm
- https://help.solidworks.com/2024/english/SolidWorks/sldworks/c_featuremanager_design_tree_overview.htm
