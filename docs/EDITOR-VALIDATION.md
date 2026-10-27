# RIFTLINE — Unity Editor 验证记录

日期：2026-10-23。环境：macOS、Unity 6000.6.7f1、URP 17.3.0、Input System 1.20.0。依据：[游戏设计文档](https://www.doubao.com/docx/P0b2d85GkoTCcWx9lq8cM210nle)，revision 30。

可游玩场景为 [Riftline.unity](../Assets/Riftline/Scenes/Riftline.unity)。进入 Play Mode，点击 Game 窗口后可用键鼠游玩；完整操作见 [CONTROLS.md](CONTROLS.md)。实现包含 18 个契约、3 个区域、37 个路径配方、无尽、30 个双语档案、8 套纯外观、三份轮转存档、双语菜单和舒适性设置。

2026-10-26 的角色动画、导航跟随、天空、材质与界面升级及本轮独立回归见 [QUALITY-UPGRADE.md](QUALITY-UPGRADE.md)，包含正常速度录屏与前后对比。本页保留此前内容验证的原始范围与结果。

## 证据与结果

| 验证 | 实际结果 | 原始证据 |
| --- | --- | --- |
| Core 内容、碰撞、生成与存档 | 79 组检查通过；504 个独立执行样本；10000 个种子分别生成契约／无尽的 20000 条完整序列；420 个高差执行样本；全部 37 个配方有实际语义覆盖 | [core-validation.json](validation/core-validation.json) |
| 完整 Play Mode 内容流程 | 37 次真实持久化结算：18 条主路线 + 18 条另一支线 + 无尽 600.009 秒；所有契约路线均为三星；54 星、30 个档案、8 套外观；0 运行错误、0 警告、0 未观察到的游戏相机帧 | [play-mode-validation.json](validation/play-mode-validation.json) |
| 修复后的真实键盘输入 | 4 次手动输入流程通过；包括优先级、按住不重复、松开后再触发、按键互换、转弯上下文和安全退出；0 运行错误／警告／遗漏帧 | [manual-input-validation.json](validation/manual-input-validation.json) |
| 真实失败与重试 | 105 项记录通过；普通低栏碰撞触发 `jump_required`；小型回放中的生成角色实际移动；Enter 重试同一契约和 seed 4；Esc 暂停、Enter 倒计时恢复、`safe_exit` 持久化均通过；0 运行错误／警告／遗漏帧 | [failure-retry-validation.json](validation/failure-retry-validation.json) |
| 结算页遮挡回归 | 修复前无尽结算保留 15 条危险／转向线；修复后 300 秒 Boss 胜利和无尽 600.022 秒的 72 项检查全部通过，结算的临时危险／转向线均为 0；0 运行错误／警告／遗漏帧 | [修复前](validation/terminal-visibility-before.json)、[修复后](validation/terminal-visibility-validation.json) |

**完整流程的原始 JSON 保留 `Status: Failed`，没有改写成全套通过。** 它包含 37 次已成功结算的内容运行，以及随后被输入缺陷中断的第 38 次运行。修复后单独重跑了受影响的 4 次真实键盘流程；最终结论依据这几份证据共同判断。

完整内容会话为 `698bc47fd675410aa87fc17fc1723350`。四次键盘回归为 `ebd709fee3ae4d0eaf7f4423b01a271a`。失败／重试回归为 `2b0c283ac98c462ea51771c108633baa`。Core 在实际 Unity Editor 中执行，耗时 189.348 秒；它验证模拟和字段序列化，不代表渲染或硬件性能验证。

结算可见性修复前会话为 `597c4a2663e9467882e82bacd9352c0b`，修复后为 `d61299b5d68d472a929bef964a5587bb`。最后的修改只涉及终局物件可见性、生成图集裁切和验收编排；这两项受影响的 Play Mode 回归已重跑。37 次完整内容和 Core 的记录保留为此前实际运行的证据。

## 实际覆盖

- 普通与固定充能教学、跳跃／滑铲／快速落地、换道、相位及八类障碍；相位到期、滑铲结束和物理换道的边界碰撞。
- 18 个契约的双方分岔、三个 45 秒 Boss 场景、无尽区域过渡、达到 10 m/s 速度上限后的组合压力。
- 平滑、阶梯、固定三种转向方式的真实暂停／倒计时；高差道路的实际平台和跑者位置、坡道暂停冻结；相机与 XR Origin 的本地姿态独立快照。
- 30 个档案的实际取得、立即保存、失败后保留；重复结算不重复发奖、写入失败后重试、两份较新损坏存档回退到有效旧存档。
- 首通奖励共 3600 晶片；完整内容流程实际赚取 7277 晶片，付费外观消费 6700，剩余 577。8 套外观均实际购买／装备，并通过新存档实例重载核对；没有注入货币或解锁。
- 鼠标真实队列事件：主菜单点击、返回、列表滚轮；简体中文／English 的 Home、Contracts、Outfits、Archives、Settings 共十个页面；生成角色预览、结果和暂停页面。
- 宽度与锚点、道路顶部、可见角色／Boss、高差和截图状态均从实际场景检查；截图须在预期页面、语言和游戏状态渲染后提交，并确认非空文件。

完整内容使用 `timeScale=8` 加速，同时控制模拟 Running 帧的实际步长不超过 0.08 秒；手动输入阶段使用普通时间。37 次完整内容运行观察到 147258 个相机渲染帧，0 遗漏帧。测试读取事件元数据后通过正常运行命令操作，不修改事件、生命、得分、能量、轨道位置、时长或解锁数据；全程关闭自动转弯。

Core 的独立执行样本与生产 `RunAutopilot` 分离；Play Mode 自动操作读取生产事件元数据，因此它证明集成流程能实际运行，不能作为独立于生产生成器的可达性证明。

## 收尾修复与画面

按住 `Space + S + A` 时，旧代码先跳跃、下一帧又消费了方向沿，造成 `Jump,Left` 和 `lane 1→0`。现在优先级裁决前就采样／锁存方向沿，被高优先级动作覆盖的方向不会延迟触发。失败证据：[input-regression-before.json](validation/input-regression-before.json)；修复后证据：[manual-input-validation.json](validation/manual-input-validation.json)。

终局冻结之前，危险边框可能停留在近场并挡住结果面板。现在 Menu、Completed、Failed 隐藏临时事件物件；暂停和恢复倒计时保留冻结几何。检查直接读取实际场景中的 `Danger Boundary` 和 `Exit Direction`，不调用 World 的内部可见性判断。

方向图标使用生成图集中的单个箭头 UV 区域，原始 AI 图集没有修改。已查看实际失败、回放、方向图标、暂停、双语菜单和已购外观截图：

- [失败与回放](validation/2b0c283ac98c462ea51771c108633baa/natural-collision-failure-results.png)
- [局内 HUD 与方向图标](validation/2b0c283ac98c462ea51771c108633baa/retry-running-direction-icons.png)
- [暂停菜单](validation/2b0c283ac98c462ea51771c108633baa/retry-keyboard-pause.png)
- [中文设置](validation/2b0c283ac98c462ea51771c108633baa/phase-zero-zh-settings.png)
- [英文契约](validation/2b0c283ac98c462ea51771c108633baa/phase-zero-en-contracts.png)
- [已购外观与生成角色预览](validation/698bc47fd675410aa87fc17fc1723350/all-outfits-earned.png)
- [修复后的 Boss 结算](validation/d61299b5d68d472a929bef964a5587bb/primary-05-results.png)
- [修复后的无尽结算](validation/d61299b5d68d472a929bef964a5587bb/endless-600-results.png)

## 生成资源

所有角色、环境、材质、菜单图、图标、动画、音乐和音效源文件均来自 Unity AI Generator。16 类游戏网格、Eve 的生成骨骼、15 个实际四秒 Humanoid 动画（每个 130 条曲线）、3 个区域与 8 套外观材质、音乐和 11 种反馈声音已关联到场景资源库。所有游戏网格具有 Unity 原生降低细节级别。

[asset-provenance.json](asset-provenance.json) 记录 88 次生成操作，逐项输出均实际存在。保留了最初异步生成返回“进行中”的响应，并用后续实际导入验证状态注明完成。网格缩放、骨骼映射、材质绑定、LOD、图集裁切、道路组装和 UI 布局属于本地集成。运行游戏不依赖在线生成服务。依赖及来源见 [DEPENDENCIES.md](DEPENDENCIES.md)。

## 测量范围

完整内容运行的峰值保守 LOD0 计数为 822771 个激活顶点／552016 个三角面；游戏相机视锥符合项峰值为 761507 个顶点／512706 个三角面。该统计按实例计数、烘焙 LineRenderer，并不代表 GPU 选中 LOD、遮挡剔除后数值、draw calls 或实际双目开销。粒子几何不计入该口径；本次报告中的粒子和不支持的渲染器计数为零。

Editor 全流程有 76404 帧超过 13.89 ms，其中包含加载、菜单和加速验收开销；因此此次未认定稳定 72 fps。按用户要求只验证桌面 Unity Editor，没有 APK、真机安装或 PICO 测试。双目表现、物理手柄输入、佩戴舒适性、热、内存与设备持续帧率未认证。

## 最终环境

最后一次回归完成后，实际 Unity 命令确认场景已保存、处于 Edit Mode、验收存档 hook 已关闭、虚拟键鼠移除、时间／后台运行设置恢复。Unity 进程的 `HTTPS_PROXY`／`NO_PROXY` 已恢复原值；仅本次使用的临时 IPv4 隧道进程已退出，localhost:18764 不再监听。状态：[final-editor-state.json](validation/final-editor-state.json)、[environment-cleanup.json](validation/environment-cleanup.json)。

最终文件审计核对原始报告结论、截图文件与预期状态、88 项生成输出及文档链接，并记录当前游戏脚本的 SHA-256：[final-artifact-audit.json](validation/final-artifact-audit.json)。
