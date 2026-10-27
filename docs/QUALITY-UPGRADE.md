# RIFTLINE — 表现升级与复查

2026-10-26。场景：[Riftline.unity](../Assets/Riftline/Scenes/Riftline.unity)。

这一轮修复了角色重定向、动作播放、转弯相机跟随和装束预览，并重做天空、道路材质、灯光、场景比例与界面表现。验收范围为桌面 Unity Editor；商业上线仍需要设备与商店发布验证。

## 直接查看

- [正常速度游玩录屏](quality/editor-playthrough.mp4)：60 秒、1600×790 的实际 Unity Game 窗口画面，包含教程动作、两次转弯与中文菜单，静音。双语页面另见界面图集。
- [整体前后对比](quality/before-after.png)。
- [角色动作](quality/motion.png)、[骨架诊断](quality/rig-diagnosis.png)。
- [三个区域](quality/regions.png)、[双语界面](quality/interface.png)、[八套装束](quality/outfits.png)。
- [键鼠操作](CONTROLS.md)。

## 已修改

**角色。** 修正生成角色 Humanoid 参考姿态与蒙皮绑定姿态的对应关系，保留原始蒙皮变换。跑步使用 Unity AI Generator 编辑出的 `RunStationary` 循环。动作播放器明确管理动作时间、过渡、落地和缓冲跳跃；暂停与恢复倒计时冻结当前骨骼姿态，避免动作时钟继续推进。

**相机和 XR。** 场景保留一个 XR Origin，由新增 NavigationRig 管理路线转向。平滑、分段和固定三种模式分别执行对应行为，避免把转弯旋转写入被追踪相机的局部姿态。修正校准、回中、重新开始及暂停后恢复时的朝向处理。正常速度教程的旧记录为路线朝向 90°、相机世界朝向 0°；新记录为二者均 90°，相机局部朝向仍为 0°。

**环境。** 接入三个 Generator 天空立方体及区域反射、环境光与六秒过渡。启用 Skybox、HDR、4× MSAA、ACES 和克制的 Bloom；接入生成道路底色与法线。调整地标比例、补光和危险边线，提升跑者、道路与背景的分离度。移除已经通过的危险物件，避免它们滚入近裁切面遮挡画面。

**界面。** 调整 HUD 距离、位置与字号，移除桌面视角中的手腕碎片，统一深色面板和细亮边。装束保留生成角色各材质槽的纹理。发现并修复了连续重建装束页时角色预览消失的问题：`Transform.Find` 命中了等待延迟销毁的旧容器；现在直接使用新建容器引用。

## 验证与证据

| 项目 | 本轮结果 | 原始证据 |
|---|---|---|
| 动作与导航 | 33 项通过，包含缓冲跳跃、下降保持、真实骨骼暂停及三模式左右转向／回中／重置 | [presentation-validation.txt](quality/presentation-validation.txt) |
| 不变的 Core | 79 组、504 个独立样本、10000 种子、20000 条完整序列、420 个高差样本；耗时 266.015 秒 | [core-validation.json](quality/core-validation.json) |
| 实际转弯／暂停 | 三模式、3 次已结算运行、100 项检查；0 运行错误、0 遗漏相机帧、1 条 SDK 警告 | [turn-pause-validation.json](quality/turn-pause-validation.json) |
| 最终双语菜单 | 47 项检查、10 页截图；点击／滚轮／返回和重复装束页重建通过；0 错误、0 警告、0 遗漏帧 | [menu-validation-final.json](quality/menu-validation-final.json) |
| 最终失败／重试 | 自然碰撞失败、角色回放、Enter 重试、暂停／恢复、安全退出；109 项检查、2 次结算；0 运行错误、0 遗漏帧、8 条 SDK／导入警告 | [failure-retry-validation-final.json](quality/failure-retry-validation-final.json) |
| 三个区域 | 每个区域正常速度运行 20 秒，通过正常命令操作，未开启自动转弯 | [运行记录](quality/regional-outcome.txt)、[输入记录](quality/regional-input.csv) |
| 八套装束 | 重复打开页面并切换语言；修复前全部缺少可见预览，修复后 8/8 保留可见 SkinnedMeshRenderer | [修复前](quality/outfit-preview-before.txt)、[修复后](quality/outfit-preview-after.txt) |
| 最终正常速度教程 | 通过实际 Keyboard 状态事件提交 7 个动作，运行至 36 秒，再查看结算和双语页面；`timeScale=1` | [结果](quality/normal-time-outcome.txt)、[输入](quality/normal-time-commands.txt)、[姿态](quality/normal-time-poses.csv) |

装束与区域复查使用从此前真实完成的内容流程复制出的独立存档，没有注入解锁或货币。[原始存档来源](quality/regional-save-source.txt)。

本轮菜单验证曾在首个点击失败。独立输入诊断确认射线可以命中按钮，较晚发送的点击成功；增加首个输入前的初始化等待后通过。失败记录分别保留在 [初次验证](quality/menu-validation.json) 与 [保持焦点后的复现](quality/menu-stable-focus-failed.json)。装束预览新增回归先失败再修复，保留[失败记录](quality/outfit-preview-regression-failed.json)，没有改写失败结果。

此前 37 次完整内容结算仍作为此前的覆盖证据保留在 [EDITOR-VALIDATION.md](EDITOR-VALIDATION.md)。本轮重新执行的是上表中的受影响流程，并未声称再次跑完全部 18 个契约。

## 上线前仍需验证

本轮没有 APK 或真机运行证据。双目显示、实际手柄、佩戴舒适性、持续帧率、热与内存需在目标设备上验证，不能从 Editor 结果推断稳定 72 Hz 或商店认证。

当前 SDK 警告包括重复的 `PICO Hands` 描述符、包内 meta 解析及缺失的 SpatialAudio 插件／不可变目录提示，完整文本保留在验证 JSON。最后的失败／重试会话记录了 8 条；它们不属于游戏运行异常，也没有被隐藏。

生成资源、实际输出和未成功生成的 PBR 通道记录见 [quality-assets.json](quality-assets.json) 与 [DEPENDENCIES.md](DEPENDENCIES.md)。

收尾已保存场景并退出 Play Mode，清除验证输入与临时存档钩子，恢复进入 Play Mode 的编辑器选项和生成代理，停止本轮临时隧道。[场景状态检查](quality/final-scene-state.txt)。
