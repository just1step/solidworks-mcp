# SolidWorks 建模技巧（Skill Notes）

> 目的：把常用的 SOLIDWORKS 建模“稳定性/可维护性/效率”技巧整理成可复用清单；内容为基于公开教程的归纳总结（非逐字摘录）。

## 1) 草图与设计意图（Design Intent）

- **尽量让草图“少而稳”**：能用多个简单草图/特征表达，就不要在一个草图里塞太多几何。
- **尽可能 Fully Define（完全定义）**：用尺寸 + 关系让关键几何稳定。
- **避免“链式依赖”**：不要让 A 驱动 B、B 再驱动 C…（daisy chaining）。更稳的做法是让 A 直接驱动 B 和 C。

## 2) 镜像：Mirror Entities vs Dynamic Mirror

- **Mirror Entities**：适合草图完成后再镜像，依赖“镜像中心线 + 选择要镜像的实体”。
- **Dynamic Mirror**：适合边画边镜像；先选中心线/模型边进入动态镜像模式，再开始画。
- **用镜像保持对称一致**：对称结构（例如鸭子左右翅膀）优先用镜像/中面来保证可修改性。

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

## 7) 把这些技巧映射到“鸭子”这种卡通建模

- **喙**：先用简单闭合草图拉伸，再用圆角柔化边缘；尽量保持喙与头部的接触区域连续。
- **眼睛**：优先用切除做“眼窝/高光点”，再小圆角；避免切太深导致穿透。
- **翅膀**：用中面对称（Mid Plane + 镜像）或草图动态镜像保持左右一致；翅膀最好是“薄拉伸 + 大圆角”。

## 8) MCP 自动化建模（本仓库使用经验补充）

- **每次选择前先清 Selection**：减少 InsertSketch 失败/宿主不对的问题。
- **`select_by_name` 选基准面时优先用 `PLANE` 兜底**：某些环境 `swSelDATUMPLANES` 可能不稳定。
- **拉伸/切除/旋转前确保闭合轮廓**：开放轮廓会导致特征失败（本项目桥接层已加预检，会直接报错）。
- **FinishSketch 后再 Extrude 也要能预检**：本项目已支持从顶层 ProfileFeature 解析草图来预检。

## 参考来源（供继续深挖）

- https://www.goengineer.com/blog/mirror-2d-sketches-in-solidworks-mirror-entities-and-dynamic-mirror-entites
- https://www.goengineer.com/blog/creating-reference-planes-in-solidworks
- https://www.goengineer.com/blog/solidworks-filletxpert-tool-tutorial
- https://www.goengineer.com/blog/removing-external-references-solidworks-files
- https://www.goengineer.com/blog/managing-external-references-solidworks-assemblies
- https://www.goengineer.com/blog/solidworks-circular-references
- https://www.goengineer.com/blog/repair-broken-references-in-solidworks
