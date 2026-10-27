# PICO XR 真机 UI 可见性修复

日期：2026-10-26

## 结论

本问题不是单一的 uGUI 样式问题，而是 PICO Android 启动与 XR 初始姿态链路中的三层故障：

1. Android 构建剥离了运行时通过 `Shader.Find` 查找的 URP Shader，`RiftlineGame.Awake()` 在创建 UI 前中断。
2. Main Camera 的 `TrackedPoseDriver` 被旧的 `XRSettings.isDeviceActive` 状态关闭，PICO HMD 姿态无法可靠传入 Camera。
3. PICO/XROrigin 的首帧高度是过渡值；若立即校准，几帧后姿态稳定时 UI 会再次移出视野。

当前 APK 已在 PICO SpaceOS `swan_arm64` 模拟器完成安装和启动验证。模拟器进入 XR Session 5，Riftline 进程保持运行，最终日志中没有 Riftline 异常、Shader 空引用或遗留诊断日志。

当前通过 ADB 连接的是 PICO SpaceOS 模拟器，不是物理头显。因此修复状态是：**已通过 PICO 模拟器，等待物理 PICO 冷启动复测**。

## 修改

### XR Camera

- Main Camera 的 `TrackedPoseDriver` 现在始终启用，不再依赖 `XRSettings.isDeviceActive`。
- 场景和 `RiftlineSceneBuilder` 都将该组件序列化为启用。
- `RiftlineHeadTracking` 显式要求 `TrackedPoseDriver`。

### 初始 UI 校准

- 自动初始校准只有一个共享门控，最多执行一次。
- 只接受已经写入 Main Camera 的非默认 HMD 姿态。
- 等待姿态稳定 0.15 秒；如果用户持续移动，最多等待 0.75 秒后使用最新姿态。
- `RunPhase.Running` 中不会自动校准。
- 菜单/暂停页中的显式“重置朝向”仍可重复执行，并保留 `TrackingReady` 检查。

### Android Shader

构建流程持续维护以下运行时 Shader，避免 Android Shader stripping：

- `Universal Render Pipeline/Unlit`
- `Universal Render Pipeline/Lit`
- `Universal Render Pipeline/Particles/Unlit`

`RiftlineSceneBuilder.Build()` 和 `RefreshPresentationLighting()` 都会幂等地检查并补齐配置。

## 验证

### 红绿回归

- 旧实现：虚拟 PICO HMD 提供 96° yaw 后，Camera 仍为 identity。
- 中间实现：Camera 能跟踪，但 UI 仍停留在场景 +Z，落到 Camera 背后。
- 冷启动测试：确认 tracking readiness 不再在首个 Input System 姿态前抢先消耗校准门控。
- 一次性测试：第一次自动校准返回 true，第二次返回 false，只触发一次 `Changed`。
- 姿态测试：96° yaw 误差 0°；XROrigin 高度稳定后，UI 位于视口 `(0.50, 0.74)`，第 60 帧保持不变。
- [虚拟 HMD UI 截图](pico-ui-fix/virtual-hmd-ui.png)

### PICO SpaceOS 模拟器

- 环境：`swan_arm64`
- PICO XR SDK 运行时：6.1.1
- XR Session：1 → 2 → 3 → 4 → 5
- 最终启动：无 `ArgumentNullException`、`NullReferenceException` 或 Riftline 堆栈
- 诊断构建证据：[模拟器诊断日志](pico-ui-fix/pico-emulator-diagnostic-log.txt)
- 最终无诊断构建：[最终启动日志](pico-ui-fix/pico-emulator-final-log.txt)

### 构建与回归

- Android Development APK 构建成功，IL2CPP、ARM64、Min API 29。
- APK 压缩内容完整，无 ZIP 错误。
- Core：95 组、504 个独立验证点、20,000 条完整序列通过。
- Editor 最终状态：Edit Mode、无编译、场景未脏、1 个 XR Origin/PXR_Manager/Main Camera、姿态驱动启用、无临时输入设备，见 [最终审计](pico-ui-fix/final-audit.json)。
- 独立代码复核：无 Critical 或 Important；唯一 Minor 是 Always Included URP Shader 会增加包体和构建时间。
- [独立复核报告](pico-ui-fix/review.md)

桌面普通存档中最新文件的修改时间为 20:36:19，早于本轮 PICO 修复开始时间；本轮未覆盖或回滚该进度。

## APK

- 文件：[Riftline-PICO-UIFix.apk](../Builds/Riftline-PICO-UIFix.apk)
- Package：`com.pico.xr.template`
- Version：`0.1.0` (`versionCode 1`)
- SHA-256：`29e29986084f612bc609f35989d51bee0541c619931ed2d175b043364dbfbca5`

当前仍使用模板 Package ID。正式上架前应改为产品自己的 Application Identifier。

## 物理 PICO 待测

连接物理头显后执行：

```bash
adb devices -l
adb install -r -d Builds/Riftline-PICO-UIFix.apk
adb logcat -c
adb shell monkey -p com.pico.xr.template -c android.intent.category.LAUNCHER 1
adb logcat "Unity:D" "PxrUnity:D" "AndroidRuntime:E" "*:S"
```

需要检查：

1. 面向非场景 +Z 方向冷启动，首页仍出现在正前方。
2. identity heading 冷启动正常。
3. 摘下/戴回头显、Home/Quick Menu 返回后 UI 仍可见。
4. 菜单和暂停页的“重置朝向”可重复工作。
5. 正在跑动时不会突然重校准画面。

Editor 或模拟器无法替代以上物理头显双眼验证。
