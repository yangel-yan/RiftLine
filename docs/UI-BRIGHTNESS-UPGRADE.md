# Riftline UI 与亮度升级

2026-10-26 · Unity 6000.6.7f1 · Editor 验证

> 后续 PICO XR 设备可见性问题已单独修复并通过 PICO SpaceOS 模拟器启动验证；详见 [PICO XR UI 可见性修复](PICO-UI-VISIBILITY-FIX.md)。本文件中的 Editor 截图不能代表物理头显验收。

针对“整体 UI 还是像个 demo、色调太暗”的反馈，本轮统一了首页、二级页面、HUD 和结算页的视觉样式，并同时调整运行时环境与场景中的光照。

## 查看实际效果

- [首页、实玩与结算前后对比](ui-brightness/before-after.png)
- [完整实玩视频，含游戏声音](ui-brightness/editor-playthrough.mp4)
- [三个区域的跑动视图](ui-brightness/three-regions.png)
- [英文大字号首页](ui-brightness/after-home-en-large.png)
- [英文大字号通关结算](ui-brightness/recording-victory-en-large.png)
- [英文大字号失败与回放](ui-brightness/after-failure-en-large.png)

## 实现

界面采用钢蓝色面板、白色文字、青色细节和金色主要按钮。圆角、纵向明暗、细边框和柔和阴影统一了页面的材质层次。首页重新安排主视觉、任务说明、立即开跑和次级导航；结算页把分数、距离与晶片分为独立卡片。回放障碍名称改为去重图例，并为奖励行与回放面板留出明确间距。

HUD 保留生命值、距离、分数、任务、连收、相位能量和操作绑定，以较浅的半透明面板承载。中英文与字号 0.8–1.4 沿用现有字体体系。

光照同步调整了区域环境光、地面反弹、天空曝光、雾与金属结构色。关键灯、角色补光、暖色轮廓灯沿用原位置、范围与阴影设置。后处理采用 Neutral、曝光 +0.45、对比度 4、饱和度 6、Bloom 0.18。实际启用的 Mobile URP 现在开启 HDR；renderScale 0.8、MSAA 1、shadowDistance 50 保持原值。

新增的 `RefreshPresentationLighting()` 可以更新已有场景表现，不需要重建场景。连续运行两次仍为 3 盏灯、1 个 Volume 和 4 个 profile 子资源。

## 验证

| 检查 | 结果与证据 |
| --- | --- |
| Core 生产模拟 | 95 组、504 个独立验证点、20,000 条完整序列通过，见 [输出](ui-brightness/core-all.json) |
| 页面与输入 | 4,474 项检查通过：实际 CanvasRenderer 网格、索引、有限坐标、滚动遮罩、鼠标点击/滚轮/悬停、页面切换与资源生命周期，见 [结果](ui-brightness/qa-result.json) |
| 字体与语言 | 首页、契约、装束、档案、设置和暂停，中文/英文、0.8/1.4 截图检查；失败与回放在最终间距调整后再次验证 |
| 三个区域 | 实际进入契约 0、6、12 并检查 RegionId 后拍摄；仅在隔离 QA 存档中临时解锁并恢复星级 |
| 正常速度实跑 | 首关 60 秒完成；实际受伤一次、撤离时生命 2/3、分数 18,604、晶片 43；连收、落地、相位、档案与撤离反馈均触发 |
| 结算与下一关 | 真实撤离与档案确认显示正确；大字号奖励行与回放不重叠；鼠标点击 Next 后进入契约 1、生命 3/3，见 [实跑记录](ui-brightness/report.json) |
| 失败重试与减少闪烁 | 最终源码再次正常跑到碰撞死亡，检查中英文 0.8/1.4 回放图例、奖励间距、禁用 Next，并通过实际 Retry 满血重开，见 [检查](ui-brightness/results-final-checks.txt) |
| 编辑器与存档 | 停止 Play、保存场景、恢复 Stats 与正常 Play Mode 选项、清除临时输入与存档钩子；普通存档三文件 SHA-256 未变化，见 [恢复记录](ui-brightness/restoration.json) |

交付视频为 73.92 秒、1600×790，保留正常速度与游戏声音。完整解码通过：2,218 帧画面、7,096,640 个 PCM 音频采样。已检查首页、受伤、相位、撤离结算和下一关的导出帧。见 [视频检查](ui-brightness/video-check.json) 与 [音频检查](ui-brightness/audio-check.json)。

UI 检查驱动超过了 CLI 默认 30 秒响应期限，后台任务随后完成；独立的现场审核确认最后一个满血 Retry 检查完成，且临时输入设备已清除。完整检查记录见 [ui-checks.txt](ui-brightness/ui-checks.txt)。

实跑通过正常 `Time.timeScale=1` 与生产 InputSystem 的临时键盘/鼠标输入完成。亮度对比使用独立的冻结画面：基准与升级画面均为 elapsed 0.8052086、distance 5.236449、lane 1，相机位置 (0, 1.6, 0)、旋转 identity。该受控画面用于像素比较。

结算升级前图来自上一轮已保存的真实通关截图，升级后图来自本轮录屏；两图均为英文 1.4 字号、同一契约、18,604 分、404 米和 43 晶片。

## 亮度测量

使用相同归一化区域、sRGB 解码后的 Rec.709 线性亮度采样。道路区域避开 HUD。

| 道路区域 | 升级前 | 升级后 |
| --- | ---: | ---: |
| 亮度中位数 | 0.006049 | 0.076478 |
| 低于 0.015 的暗像素比例 | 82.43% | 3.10% |
| 高于 0.98 的亮像素比例 | 0% | 0% |

完整区域定义、采样方法与角色/道路测量见 [luminance.json](ui-brightness/luminance.json)。动态天空与场景动画可能存在帧间差异，数值仅说明画面中间调的变化。

## 代码复核与范围

独立最终复核没有 Critical 或 Important 问题，见 [完整报告](ui-brightness/final-review.md)。两项 Minor 已记录：主视觉遮罩包含少量阴影几何，可能扩展数像素的裁切边缘；细线表面生成少量退化三角形，带来有界的额外网格工作。

本轮没有更改游戏规则、扣血时序、存档结构、进度解锁规则、XR 跟踪姿态或相机 FOV。验证范围为 Editor；头显性能、舒适度、玩家留存及商店验收需另行验证。

源码涉及 `RiftlineUISurface`、`RiftlineUIElements`、`RiftlineUI`、`RiftlineRunHud`、`RiftlineEnvironment`、`RiftlineWorld`、`RiftlineRailArchitecture`、`RiftlineSceneBuilder`。序列化场景、天空材质、VolumeProfile 与实际 Mobile URP 设置已保存。

产出文件大小与 SHA-256 见 [manifest.json](ui-brightness/manifest.json)。
