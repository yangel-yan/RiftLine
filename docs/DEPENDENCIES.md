# RIFTLINE — 资源与依赖登记

版本依据 `ProjectSettings/ProjectVersion.txt`、`Packages/manifest.json`、`Packages/packages-lock.json` 与安装包的 `package.json`。Git 包在 lock 文件中保存实际 commit。

| 依赖 | 当前版本 / commit | 用途与来源 |
| --- | --- | --- |
| Unity | 6000.6.7f1 | Editor、原生 Mesh LOD、动画、音频与场景运行 |
| Universal RP | 17.3.0 | 渲染；Unity registry 包内许可证 |
| Input System | 1.20.0 | 键鼠、XR Input Actions；Unity registry 包内许可证 |
| uGUI | 2.0.0 | 世界空间菜单和仪表；Unity registry 包内许可证 |
| XR Management | 4.6.1 | XR 生命周期；Unity registry 包内许可证 |
| XR Core Utilities | 2.6.0 | XR Origin；Unity registry 包内许可证 |
| PICO XR | 6.1.1 / `75699354718f13fc9cb7c0a756bb4508913b7acd` | [PICO-Unity-SDK](https://github.com/Pico-Developer/PICO-Unity-SDK)，包内 `LICENSE.md` 为 PICO 专有版权声明 |
| Unity AI Assistant | 2.21.0-pre.1 | 仅制作阶段生成资源；包内许可证指向 [Unity Terms](https://unity.com/legal) |
| Unity glTFast | 6.20.0 | AI 角色骨骼导出依赖；Apache-2.0，版权为 Unity Technologies 与 glTFast authors |
| PICO MCP Extensions | `07b3a224881a30a786b2c324cb865c880e58b5e0` | [Unity-MCP-Extensions](https://github.com/Pico-Developer/Unity-MCP-Extensions)，Editor 制作工具 |
| TRAE Editor Unity | `ffe086cd28a5da43a5efaa043a3cb1fe4c055cf1` | [TRAE-Editor-Unity](https://github.com/Pico-Developer/TRAE-Editor-Unity)，Editor 制作工具 |

角色、观察舱、控制器、道路、八类障碍、收集物、三个区域地标、材质、菜单图、操作符号、15 个动画、音乐及反馈音效均通过 **Unity AI Generator** 生成。逐项模型、提示词、操作、输出和实际文件验证记录见 [asset-provenance.json](asset-provenance.json)。生成模型的使用条件沿用 Unity Generator 所提供的条款；本登记不替代供应方条款。

骨骼到 Humanoid 的映射、导入配置、LOD、缩放与锚点、道路组装、轮廓提示和 UI 布局为本项目的本地集成。游戏运行不调用 Generator 或在线生成服务。

2026-10-26 的表现升级新增三套 Generator 天空、三套道路材质与对应法线，并通过 Generator 从已有 Run 动作裁剪出原地循环。记录见 [quality-assets.json](quality-assets.json)。金属度和光滑度采用本地标量设置，未把失败的 PBR 批处理登记为成功生成贴图。

界面使用系统动态字体，优先选择 PingFang SC / Noto Sans CJK SC / Microsoft YaHei 等已安装字体，随后使用 Unity 内置字体回退；项目没有复制或分发系统字体文件。此次 Editor 中文画面使用本机可用字体。

依赖的完整版权和许可文本保留在对应 Unity package 中。原始工程已有的 SDK 示例和编辑器工具未作为游戏美术替代资源。
