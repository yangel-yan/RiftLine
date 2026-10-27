using System;
using System.Collections.Generic;

namespace Riftline
{
    internal sealed class RiftlineUILocalization
    {
        private static readonly Dictionary<string, string[]> Copy = new Dictionary<string, string[]>
        {
            { "runNow", new[] { "立即开跑", "RUN NOW" } },
            { "homeMissionContract", new[] { "任务 // {0}：档案 → 撤离点", "MISSION // {0}: ARCHIVE → EXTRACTION" } },
            { "homeMissionEndless", new[] { "任务 // 无尽航线：坚持奔跑并刷新纪录", "MISSION // ENDLESS: SURVIVE AND SET A RECORD" } },
            { "homeThreat", new[] { "威胁 // 追兵已锁定你的航线", "THREAT // PURSUERS LOCKED ON YOUR ROUTE" } },
            { "openingPitch", new[] { "追上晶片。擦过危险。穿越裂隙。\n一次漂亮的闪避，就是下一次的动力。", "Chase crystals. Dodge danger. Cross the rift.\nEvery close call earns your next escape." } },
            { "heroCaption", new[] { "三条航道 · 一次完美逃亡", "THREE LANES. ONE PERFECT ESCAPE." } },
            { "startHint", new[] { "Enter 开跑  ·  A / D 换道  ·  空格跳跃", "Enter to run  ·  A / D to dodge  ·  Space to jump" } },
            { "health", new[] { "生命", "LIFE" } },
            { "lifeRule", new[] { "碰撞 −1 · 断层失败", "HIT −1 · GAP = FAIL" } },
            { "streak", new[] { "连收", "CHAIN" } },
            { "streakReward", new[] { "连收！相位充能 +20%", "CHAIN! PHASE CHARGE +20%" } },
            { "damaged", new[] { "受击  −1 生命", "HIT  −1 LIFE" } },
            { "protected", new[] { "短暂无敌", "PROTECTED" } },
            { "phaseReadyShort", new[] { "相位就绪", "PHASE READY" } },
            { "collectCue", new[] { "跟随晶片，连收 5 个获得充能", "Follow crystals. Chain 5 to charge phase." } },
            { "runCue", new[] { "保持节奏，读清下一道障碍", "Find your rhythm. Read the next hazard." } },
            { "subtitleDamage", new[] { "碰撞扣除一格生命 · 短暂无敌", "One life lost · Briefly protected" } },
            { "navigator", new[] { "导航员控制台 // 航线重组中", "NAVIGATOR CONSOLE // ROUTE REFORMING" } },
            { "play", new[] { "选择契约", "SELECT CONTRACT" } },
            { "endless", new[] { "无尽航线", "ENDLESS RUN" } },
            { "locked", new[] { "未解锁", "LOCKED" } },
            { "archives", new[] { "档案", "ARCHIVES" } },
            { "outfits", new[] { "装束", "OUTFITS" } },
            { "settings", new[] { "设置", "SETTINGS" } },
            { "back", new[] { "返回", "BACK" } },
            { "enter", new[] { "进入", "ENTER" } },
            { "boss", new[] { "首领航段", "BOSS ROUTE" } },
            { "chips", new[] { "晶片", "CHIPS" } },
            { "owned", new[] { "已拥有", "OWNED" } },
            { "equipped", new[] { "已装备", "EQUIPPED" } },
            { "equip", new[] { "装备", "EQUIP" } },
            { "buy", new[] { "解锁", "UNLOCK" } },
            { "collected", new[] { "已收集", "COLLECTED" } },
            { "encrypted", new[] { "加密档案 // 在航线上收集", "ENCRYPTED // COLLECT ON ROUTE" } },
            { "resume", new[] { "继续 // 3 秒倒计时", "RESUME // 3 SEC COUNTDOWN" } },
            { "safeEnd", new[] { "安全结束 // 保留晶片", "SAFE END // KEEP CHIPS" } },
            { "recenter", new[] { "重置朝向", "RECENTER" } },
            { "home", new[] { "返回主页", "RETURN HOME" } },
            { "pause", new[] { "航线已暂停", "ROUTE PAUSED" } },
            { "session", new[] { "本次佩戴", "SESSION" } },
            { "rest", new[] { "建议休息并观察远处", "Consider a rest and focus at distance" } },
            { "victory", new[] { "契约完成", "CONTRACT COMPLETE" } },
            { "failure", new[] { "航线中断", "ROUTE INTERRUPTED" } },
            { "safeExit", new[] { "安全撤离", "SAFE EXIT" } },
            { "score", new[] { "分数", "SCORE" } },
            { "distance", new[] { "距离", "DISTANCE" } },
            { "reward", new[] { "首通奖励", "FIRST CLEAR" } },
            { "retry", new[] { "重试", "RETRY" } },
            { "next", new[] { "下一契约", "NEXT CONTRACT" } },
            { "resultContract", new[] { "本次契约 // {0}", "CONTRACT // {0}" } },
            { "resultEndless", new[] { "本次航线 // 无尽航线", "RUN // ENDLESS RUN" } },
            { "resultExtractionConfirmed", new[] { "撤离确认 // 契约目标完成", "EXTRACTION CONFIRMED // CONTRACT OBJECTIVE COMPLETE" } },
            { "resultArchiveConfirmed", new[] { "档案已安全回收", "ARCHIVE SECURED" } },
            { "resultNextTarget", new[] { "下一目标 // {0}", "NEXT TARGET // {0}" } },
            { "retryRestores", new[] { "满血重试 · 晶片与档案保留", "Retry with full life. Chips and archives retained." } },
            { "replay", new[] { "跑者复盘 // 最近 4 秒", "RUNNER REPLAY // LAST 4 SEC" } },
            { "language", new[] { "语言", "Language" } },
            { "alignment", new[] { "航向对齐（下一局生效）", "Alignment (next run)" } },
            { "comfort", new[] { "动态遮罩", "Comfort Mask" } },
            { "seated", new[] { "坐姿模式", "Seated Mode" } },
            { "leftHanded", new[] { "左手模式", "Left-handed" } },
            { "recalibrateHands", new[] { "重新校准手势", "Recalibrate Hands" } },
            { "calibrateNow", new[] { "开始", "START" } },
            { "swapActions", new[] { "交换跳跃/滑铲", "Swap Jump/Slide" } },
            { "triggerHand", new[] { "相位扳机", "Phase Trigger" } },
            { "haptics", new[] { "震动", "Haptics" } },
            { "hapticIntensity", new[] { "震动强度", "Haptic Intensity" } },
            { "music", new[] { "音乐音量", "Music Volume" } },
            { "sfx", new[] { "音效音量", "SFX Volume" } },
            { "contrast", new[] { "高对比危险", "High Contrast" } },
            { "reducedFlash", new[] { "降低闪光", "Reduced Flashes" } },
            { "particles", new[] { "减少粒子", "Reduced Particles" } },
            { "fog", new[] { "减少雾雨", "Reduced Fog" } },
            { "fontScale", new[] { "字号", "Font Scale" } },
            { "runnerDistance", new[] { "跑者距离", "Runner Distance" } },
            { "subtitles", new[] { "字幕", "Subtitles" } },
            { "restReminder", new[] { "休息提醒", "Rest Reminder" } },
            { "autoTurn", new[] { "自动转弯", "Auto Turn" } },
            { "autoTurnNote", new[] { "自动转弯不影响契约奖励，但无尽模式不会写入最高纪录。", "Auto Turn preserves contract rewards, but disables Endless best-score records." } },
            { "on", new[] { "开启", "ON" } },
            { "off", new[] { "关闭", "OFF" } },
            { "smooth", new[] { "平滑", "Smooth" } },
            { "stepped", new[] { "45° × 2", "45° × 2" } },
            { "fixed", new[] { "舱体固定", "Fixed Cabin" } },
            { "light", new[] { "轻", "Light" } },
            { "medium", new[] { "中", "Medium" } },
            { "strong", new[] { "强", "Strong" } },
            { "any", new[] { "任意手", "Either" } },
            { "left", new[] { "左手", "Left" } },
            { "right", new[] { "右手", "Right" } },
            { "contractsRule", new[] { "完成前一区域任意 5 个契约以解锁下一地区", "Clear any 5 contracts in the previous region to unlock the next" } },
            { "target", new[] { "目标", "TARGET" } },
            { "phase", new[] { "相位", "PHASE" } },
            { "controls", new[] { "摇杆/A D/←→：换道  A/X/空格：跳跃  B/Y/S/↓/Ctrl：滑铲\n扳机/左 Shift/E：相位  菜单键/Esc：暂停", "STICK/A D/←→: LANE  A/X/SPACE: JUMP  B/Y/S/↓/CTRL: SLIDE\nTRIGGER/LEFT SHIFT/E: PHASE  MENU/ESC: PAUSE" } },
            { "controlsSwapped", new[] { "摇杆/A D/←→：换道  B/Y/S/↓/Ctrl：跳跃  A/X/空格：滑铲\n扳机/左 Shift/E：相位  菜单键/Esc：暂停", "STICK/A D/←→: LANE  B/Y/S/↓/CTRL: JUMP  A/X/SPACE: SLIDE\nTRIGGER/LEFT SHIFT/E: PHASE  MENU/ESC: PAUSE" } },
            { "handControls", new[] { "任一手捏合并短移：换道 / 跳跃 / 滑铲\n任一手握拳：相位  另一手掌心向上：暂停", "EITHER HAND PINCH + MOVE: LANE / JUMP / SLIDE\nEITHER FIST: PHASE  OTHER PALM: PAUSE" } },
            { "handMenuControls", new[] { "视线对准按钮，捏合确认\n另一手捏合上下拖动：滚动列表", "LOOK AT A BUTTON, PINCH TO CONFIRM\nOTHER HAND PINCH DRAG: SCROLL" } },
            { "quit", new[] { "退出游戏", "QUIT GAME" } },
            { "turnLeft", new[] { "← 左转", "← TURN LEFT" } },
            { "turnRight", new[] { "右转 →", "TURN RIGHT →" } },
            { "region0", new[] { "轨道船坞", "ORBITAL DOCKS" } },
            { "region1", new[] { "霓虹雨城", "NEON RAIN CITY" } },
            { "region2", new[] { "量子遗迹", "QUANTUM RUINS" } },
            { "purchaseFailed", new[] { "晶片不足", "INSUFFICIENT CHIPS" } },
            { "noResult", new[] { "结算数据同步中", "SYNCING RUN RESULTS" } },
            { "jumpAction", new[] { "跳跃", "JUMP" } },
            { "slideAction", new[] { "滑铲", "SLIDE" } },
            { "phaseAction", new[] { "相位", "PHASE" } },
            { "leftAction", new[] { "左移/左转", "LEFT / TURN" } },
            { "rightAction", new[] { "右移/右转", "RIGHT / TURN" } },
            { "pauseAction", new[] { "暂停", "PAUSE" } },
            { "subtitleJump", new[] { "Eve 正在跳跃", "Eve jumping" } },
            { "subtitleSlide", new[] { "Eve 正在滑铲", "Eve sliding" } },
            { "subtitlePhase", new[] { "相位已启动 // 断层仍需跳跃", "Phase active // Gaps still require a jump" } },
            { "subtitlePhaseReady", new[] { "相位已充满", "Phase fully charged" } },
            { "subtitleArchive", new[] { "档案已收集", "Archive recovered" } },
            { "subtitleNearMiss", new[] { "擦身而过 // 相位充能", "Near miss // Phase charging" } },
            { "subtitleFail", new[] { "航线中断 // 航道已冻结", "Route interrupted // Track frozen" } },
            { "subtitleComplete", new[] { "航线结束 // 晶片已保留", "Run finished // Chips retained" } },
            { "turnLocked", new[] { "出口已锁定", "EXIT LOCKED" } },
            { "turnWrong", new[] { "方向不符", "WRONG DIRECTION" } },
            { "wrongTurnInput", new[] { "方向不符 // 请推向亮起的出口", "WRONG DIRECTION // Push toward the lit exit" } },
            { "phaseNotReady", new[] { "相位未就绪 // 请选择安全轨道", "PHASE NOT READY // Choose a safe lane" } },
            { "fastFall", new[] { "快速落地", "Fast landing" } },
            { "routeLeft", new[] { "已选择左分支", "Left branch selected" } },
            { "routeRight", new[] { "已选择右分支", "Right branch selected" } },
            { "autoTurnCue", new[] { "自动转弯已锁定", "Auto turn locked" } },
            { "forkCue", new[] { "← 左分支 / 右分支 →", "← LEFT BRANCH / RIGHT BRANCH →" } },
            { "objectiveRecoverArchive", new[] { "回收档案", "RECOVER ARCHIVE" } },
            { "objectiveReachExtraction", new[] { "前往撤离点", "REACH EXTRACTION" } },
            { "objectiveExtracted", new[] { "撤离完成", "EXTRACTED" } },
            { "objectiveEndless", new[] { "保持奔跑", "KEEP RUNNING" } },
            { "objectiveRecoverDetail", new[] { "找到档案 · 甩开追兵", "FIND THE ARCHIVE · EVADE PURSUERS" } },
            { "objectiveExtractionDetail", new[] { "档案已回收 · 抵达撤离点", "ARCHIVE SECURED · REACH THE EXIT" } },
            { "objectiveEndlessDetail", new[] { "保持生命并刷新最高纪录", "STAY ALIVE · SET A NEW RECORD" } },
            { "archiveGoalSwitch", new[] { "档案已回收 // 前往撤离点", "ARCHIVE SECURED // REACH EXTRACTION" } },
            { "archiveEndless", new[] { "档案已回收 // 保持奔跑", "ARCHIVE SECURED // KEEP RUNNING" } },
            { "transition", new[] { "航线即将进入", "Route approaching" } },
            { "replayUnavailable", new[] { "本局未捕获跑者轨迹", "Runner snapshots unavailable for this run" } },
            { "eveUnavailable", new[] { "等待生成的 Eve 模型", "Generated Eve model not assigned" } },
            { "failure_missed_turn", new[] { "错过转弯出口", "Missed the turn exit" } },
            { "failure_wrong_turn", new[] { "转弯方向错误", "Wrong turn direction" } },
            { "failure_gap", new[] { "坠入轨道断层", "Fell into a track gap" } },
            { "failure_slide_required", new[] { "撞到上方障碍", "Hit an overhead hazard" } },
            { "failure_jump_required", new[] { "撞到低矮障碍", "Hit a low barrier" } },
            { "failure_phase_or_lane_required", new[] { "撞到相位屏障", "Hit a phase barrier" } },
            { "failure_lane_collision", new[] { "所在轨道发生碰撞", "Collision in the current lane" } },
            { "failure_collision", new[] { "跑者与障碍发生碰撞", "Runner collided with a hazard" } },
            { "failure_safe_exit", new[] { "已安全结束航线", "Run ended safely" } }
        };

        public bool IsChinese { get; private set; }

        public RiftlineUILocalization(string language)
        {
            SetLanguage(language);
        }

        public void SetLanguage(string language)
        {
            IsChinese = !string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
        }

        public string Get(string key)
        {
            string[] values;
            return Copy.TryGetValue(key, out values) ? values[IsChinese ? 0 : 1] : key;
        }

        public string Localized(string zh, string en)
        {
            return IsChinese ? zh : en;
        }

        public string HomeMission(string contractName, bool endless)
        {
            return endless ? Get("homeMissionEndless") : string.Format(Get("homeMissionContract"), contractName);
        }

        public string ResultContract(string contractName, bool endless)
        {
            return endless ? Get("resultEndless") : string.Format(Get("resultContract"), contractName);
        }

        public string Region(int region)
        {
            return Get("region" + Math.Max(0, Math.Min(2, region)));
        }

        public string FailureReason(string reason)
        {
            string key = "failure_" + (reason ?? string.Empty).ToLowerInvariant();
            return Copy.ContainsKey(key) ? Get(key) : Get("failure_collision");
        }

        public string Action(GameCommand command)
        {
            switch (command)
            {
                case GameCommand.Jump: return Get("jumpAction");
                case GameCommand.Slide: return Get("slideAction");
                case GameCommand.Phase: return Get("phaseAction");
                case GameCommand.Left: return Get("leftAction");
                case GameCommand.Right: return Get("rightAction");
                case GameCommand.Pause: return Get("pauseAction");
                default: return Localized("奔跑", "RUNNING");
            }
        }

        public string Hazard(ObstacleKind kind)
        {
            switch (kind)
            {
                case ObstacleKind.Gap: return Localized("断层", "GAP");
                case ObstacleKind.HighBeam: return Localized("横梁", "BEAM");
                case ObstacleKind.Drone: return Localized("无人机", "DRONE");
                case ObstacleKind.LowBarrier: return Localized("矮障碍", "BARRIER");
                case ObstacleKind.PhaseWall: return Localized("相位墙", "PHASE WALL");
                case ObstacleKind.MovingGate: return Localized("移动闸门", "MOVING GATE");
                case ObstacleKind.Collapse: return Localized("坍塌", "COLLAPSE");
                case ObstacleKind.Boss: return Get("boss");
                default: return Localized("货箱", "CRATE");
            }
        }

        public string FailureAdvice(string reason)
        {
            string value = (reason ?? string.Empty).ToLowerInvariant();
            if (value.Contains("turn") || value.Contains("转"))
            {
#if UNITY_EDITOR
                return Localized("方向灯亮起后的 2 秒窗口内，将摇杆推向出口。确认“出口已锁定”，无需一直按住。", "Push toward the lit exit during its 2-second cue. Check EXIT LOCKED; holding is not required.");
#else
                return Localized("方向灯亮起后的 2 秒窗口内，朝出口捏合短移。确认“出口已锁定”，无需保持捏合。", "Pinch toward the lit exit within its 2-second cue. Check EXIT LOCKED; holding is not required.");
#endif
            }
            if (value.Contains("gap") || value.Contains("fall") || value.Contains("坠") || value.Contains("断"))
            {
                return Localized("断层无法被相位穿越；提前换道或向上捏合跳跃。", "Gaps cannot be phased. Change lane early or pinch up to jump.");
            }
            if (value.Contains("beam") || value.Contains("drone") || value.Contains("slide") || value.Contains("横梁"))
            {
                return Localized("观察上方橙色警示，在接近前向下捏合滑铲。", "Watch the overhead amber cue and pinch down to slide before contact.");
            }
            if (value.Contains("phase") || value.Contains("wall") || value.Contains("相位"))
            {
#if UNITY_EDITOR
                return Localized("相位槽满时按任一允许的扳机，或提前换道。", "Use an allowed trigger when phase is full, or change lane early.");
#else
                return Localized("相位槽满时主手握拳，或提前换道。", "Make a fist with the dominant hand when phase is full, or change lane early.");
#endif
            }
            if (value.Contains("jump"))
            {
#if UNITY_EDITOR
                return Localized("低障碍需要跳跃、相位或提前换道；确认使用当前跳跃按键。", "Jump, phase, or change lane before a low barrier. Check the current jump binding.");
#else
                return Localized("低障碍需要向上捏合跳跃、相位或提前换道。", "Pinch up to jump, phase, or change lane before a low barrier.");
#endif
            }
            return Localized("更早读取橙色危险轮廓；先确定安全轨道，再执行动作。", "Read the amber hazard silhouette earlier; choose a safe lane before acting.");
        }
    }
}
