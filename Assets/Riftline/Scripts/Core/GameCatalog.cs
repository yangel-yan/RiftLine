using System;

namespace Riftline
{
    [Serializable]
    public sealed class GameCatalog
    {
        public ContractDefinition[] Contracts;
        public ArchiveDefinition[] Archives;
        public OutfitDefinition[] Outfits;
        public TrackTemplateDefinition[] Templates;

        public static GameCatalog CreateDefault()
        {
            return new GameCatalog
            {
                Contracts = CreateContracts(),
                Archives = CreateArchives(),
                Outfits = CreateOutfits(),
                Templates = CreateTemplates()
            };
        }

        private static ContractDefinition[] CreateContracts()
        {
            return new[]
            {
                Contract(0, 0, "导航苏醒", "Navigator Awake", "掌握换道、跳跃与滑铲。", "Learn lane changes, jumping, and sliding.", 330f, 1101, 5500, 0, false, 8, 0),
                Contract(1, 0, "折跃初试", "First Rift", "在固定充能后穿越第一面相位墙。", "Use a fixed charge to cross the first phase wall.", 360f, 1102, 6500, 1, false, 10, 1),
                Contract(2, 0, "运输臂下", "Under the Gantries", "读取移动闸门并保持航线。", "Read moving gates and hold the route.", 390f, 1103, 7600, 2, false, 12, 2),
                Contract(3, 0, "分流协议", "Split Protocol", "在分岔中选择高价值路线。", "Choose the high-value route at forks.", 420f, 1104, 8800, 3, false, 14, 2),
                Contract(4, 0, "船坞警报", "Dockyard Alarm", "连续转弯并积累近失误。", "Chain turns and build near misses.", 450f, 1105, 10200, 4, false, 16, 3),
                Contract(5, 0, "巨臂封锁", "Titan Arm Lockdown", "在四十五秒机械臂追逐中逃离船坞。", "Escape a forty-five-second gantry-arm pursuit.", 520f, 1106, 12500, 5, true, 18, 4),

                Contract(6, 1, "雨层入口", "Rainline Entry", "在雨幕中识别车辆横穿。", "Read crossing traffic through the rain.", 400f, 2201, 9000, 10, false, 14, 2),
                Contract(7, 1, "双向霓虹", "Two-Way Neon", "处理左右连续转角。", "Handle alternating left and right turns.", 430f, 2202, 10200, 11, false, 15, 3),
                Contract(8, 1, "高架脉冲", "Skyway Pulse", "在上下层之间保持节奏。", "Keep rhythm across raised and lowered routes.", 460f, 2203, 11600, 12, false, 17, 3),
                Contract(9, 1, "紫电支线", "Violet Detour", "穿越奖励分岔与移动闸门。", "Cross reward forks and moving gates.", 490f, 2204, 13000, 13, false, 19, 4),
                Contract(10, 1, "交通风暴", "Traffic Storm", "在高密度障碍中维持连段。", "Preserve a combo through dense traffic.", 520f, 2205, 14500, 14, false, 21, 5),
                Contract(11, 1, "环城追击", "Ringway Pursuit", "撑过四十五秒交通环追逐。", "Survive a forty-five-second ringway pursuit.", 590f, 2206, 17000, 15, true, 23, 6),

                Contract(12, 2, "遗迹门槛", "Ruin Threshold", "辨认消失地板与相位墙。", "Read vanishing floors and phase walls.", 470f, 3301, 12500, 20, false, 18, 4),
                Contract(13, 2, "断面回廊", "Fracture Gallery", "在连续高差中选择安全轨道。", "Choose safe lanes through repeated elevation changes.", 500f, 3302, 14000, 21, false, 20, 4),
                Contract(14, 2, "镜像岔路", "Mirror Fork", "识破假路线并保存相位。", "Expose false routes and conserve phase energy.", 530f, 3303, 15600, 22, false, 22, 5),
                Contract(15, 2, "坍缩序列", "Collapse Sequence", "在坍缩轨道中连续换道。", "Chain lane changes across collapsing track.", 560f, 3304, 17300, 23, false, 24, 6),
                Contract(16, 2, "零点航线", "Zero-Line", "组合全部规则并回收最终档案。", "Combine every rule and recover the final archive route.", 600f, 3305, 19200, 24, false, 26, 7),
                Contract(17, 2, "核心重启", "Core Reboot", "在四十五秒核心坍缩中完成重启。", "Complete the reboot during a forty-five-second core collapse.", 680f, 3306, 22500, 25, true, 28, 8)
            };
        }

        private static ArchiveDefinition[] CreateArchives()
        {
            string[] zhNames =
            {
                "唤醒记录", "舱体协议", "轨道工日志", "分流图谱", "警报残片",
                "巨臂指令", "失踪班次", "导航员誓言", "船坞余波", "蓝灯坐标",
                "雨城入境", "双向广播", "高架回声", "紫电账本", "交通幽灵",
                "环城调度", "积水讯号", "霓虹证词", "旧站台", "风暴之后",
                "遗迹门钥", "断面铭文", "镜像证明", "坍缩计时", "零点地图",
                "核心遗嘱", "建造者", "裂隙原理", "最后航标", "折跃线"
            };
            string[] enNames =
            {
                "Wake Record", "Cabin Protocol", "Railhand Log", "Junction Atlas", "Alarm Fragment",
                "Gantry Directive", "Missing Shift", "Navigator Oath", "Dockyard Wake", "Blue-Light Coordinates",
                "Raincity Entry", "Two-Way Broadcast", "Skyway Echo", "Violet Ledger", "Traffic Ghost",
                "Ringway Dispatch", "Flooded Signal", "Neon Testimony", "Old Platform", "After the Storm",
                "Ruin Key", "Fracture Inscription", "Mirror Proof", "Collapse Clock", "Zero Map",
                "Core Testament", "The Builders", "Rift Principle", "Last Beacon", "The Riftline"
            };
            var archives = new ArchiveDefinition[30];
            for (int id = 0; id < archives.Length; id++)
            {
                int region = id / 10;
                archives[id] = new ArchiveDefinition
                {
                    Id = id,
                    Region = region,
                    NameZh = zhNames[id],
                    NameEn = enNames[id],
                    TextZh = ArchiveTextZh(id, region),
                    TextEn = ArchiveTextEn(id, region)
                };
            }
            return archives;
        }

        private static OutfitDefinition[] CreateOutfits()
        {
            return new[]
            {
                Outfit(0, 0, "导航原型", "Navigator Prototype", "伊芙的标准青蓝导航服。", "Eve's standard cyan navigation suit.", "#48D7E8"),
                Outfit(1, 500, "船坞橙", "Dockyard Orange", "带工业警示条纹的维修涂装。", "Maintenance colors with industrial warning stripes.", "#F28A32"),
                Outfit(2, 650, "雨夜紫", "Rainnight Violet", "为霓虹雨幕设计的紫色反光层。", "A violet reflective layer made for neon rain.", "#9A6BFF"),
                Outfit(3, 800, "信标白", "Beacon White", "高对比白色航标装束。", "A high-contrast white beacon suit.", "#E8F5F7"),
                Outfit(4, 900, "警戒红", "Alert Red", "来自船坞应急队的红黑配色。", "Red and black colors of the dock emergency crews.", "#E34B4B"),
                Outfit(5, 1050, "遗迹金", "Ruin Gold", "仿照量子遗迹断面的金色纹路。", "Gold tracery modeled after quantum ruin fractures.", "#D8B85C"),
                Outfit(6, 1250, "深空黑", "Deep-Space Black", "吸光黑材质衬托青色能量线。", "Light-absorbing black framing cyan energy lines.", "#202634"),
                Outfit(7, 1550, "折跃棱镜", "Rift Prism", "完成长期航行者偏爱的棱镜涂装。", "A prismatic finish favored by veteran navigators.", "#62F0C8")
            };
        }

        private static TrackTemplateDefinition[] CreateTemplates()
        {
            return new[]
            {
                Template(0, "Open Run", TrackTemplateCategory.Straight, ObstacleKind.Chip, 0, 18f, true),
                Template(1, "Chip Arc", TrackTemplateCategory.Straight, ObstacleKind.Chip, 0, 16f, true),
                Template(2, "Crate Shoulder", TrackTemplateCategory.Straight, ObstacleKind.Crate, 1, 18f, true),
                Template(3, "Barrier Cadence", TrackTemplateCategory.Straight, ObstacleKind.LowBarrier, 1, 18f, true, jump: true),
                Template(4, "Beam Cadence", TrackTemplateCategory.Straight, ObstacleKind.HighBeam, 1, 18f, true, slide: true),
                Template(5, "Gate Preview", TrackTemplateCategory.Straight, ObstacleKind.MovingGate, 1, 20f, true, moving: true),
                Template(6, "Drone Preview", TrackTemplateCategory.Straight, ObstacleKind.Drone, 2, 20f, true, slide: true, moving: true),
                Template(7, "Phase Preview", TrackTemplateCategory.Straight, ObstacleKind.PhaseWall, 2, 20f, true),
                Template(8, "Gap Preview", TrackTemplateCategory.Straight, ObstacleKind.Gap, 2, 20f, true, jump: true),
                Template(9, "Collapse Preview", TrackTemplateCategory.Straight, ObstacleKind.Collapse, 2, 20f, true),

                Template(10, "Left Bend", TrackTemplateCategory.Turn, ObstacleKind.TurnLeft, 1, 32f, true, direction: -1),
                Template(11, "Right Bend", TrackTemplateCategory.Turn, ObstacleKind.TurnRight, 1, 32f, true, direction: 1),
                Template(12, "Left Signal Bend", TrackTemplateCategory.Turn, ObstacleKind.TurnLeft, 2, 36f, true, direction: -1),
                Template(13, "Right Signal Bend", TrackTemplateCategory.Turn, ObstacleKind.TurnRight, 2, 36f, true, direction: 1),

                Template(14, "Balanced Fork", TrackTemplateCategory.Fork, ObstacleKind.Fork, 1, 24f, true),
                Template(15, "Left Reward Fork", TrackTemplateCategory.Fork, ObstacleKind.Fork, 2, 25f, true, direction: -1),
                Template(16, "Right Reward Fork", TrackTemplateCategory.Fork, ObstacleKind.Fork, 2, 25f, true, direction: 1),

                Template(17, "Gentle Rise", TrackTemplateCategory.Height, ObstacleKind.Rise, 1, 22f, true),
                Template(18, "Gentle Fall", TrackTemplateCategory.Height, ObstacleKind.Fall, 1, 22f, true),
                Template(19, "Raised Barrier", TrackTemplateCategory.Height, ObstacleKind.LowBarrier, 2, 24f, true, jump: true),
                Template(20, "Lowered Beam", TrackTemplateCategory.Height, ObstacleKind.HighBeam, 2, 24f, true, slide: true),
                Template(21, "Fracture Crest", TrackTemplateCategory.Height, ObstacleKind.Gap, 3, 26f, true, jump: true),

                Template(22, "Twin Crates", TrackTemplateCategory.Obstacle, ObstacleKind.Crate, 1, 18f, true),
                Template(23, "Low Rail", TrackTemplateCategory.Obstacle, ObstacleKind.LowBarrier, 1, 18f, true, jump: true),
                Template(24, "Pressure Beam", TrackTemplateCategory.Obstacle, ObstacleKind.HighBeam, 1, 18f, true, slide: true),
                Template(25, "Sweeping Gate", TrackTemplateCategory.Obstacle, ObstacleKind.MovingGate, 2, 20f, true, moving: true),
                Template(26, "Patrol Drone", TrackTemplateCategory.Obstacle, ObstacleKind.Drone, 2, 20f, true, slide: true, moving: true),
                Template(27, "Broken Span", TrackTemplateCategory.Obstacle, ObstacleKind.Gap, 2, 21f, true, jump: true),
                Template(28, "Rift Screen", TrackTemplateCategory.Obstacle, ObstacleKind.PhaseWall, 2, 20f, true),
                Template(29, "Lane Collapse", TrackTemplateCategory.Obstacle, ObstacleKind.Collapse, 2, 21f, true),
                Template(30, "Crate and Beam", TrackTemplateCategory.Obstacle, ObstacleKind.Crate, 3, 22f, true),
                Template(31, "Gate and Gap", TrackTemplateCategory.Obstacle, ObstacleKind.MovingGate, 3, 23f, true, moving: true),
                Template(32, "Drone Screen", TrackTemplateCategory.Obstacle, ObstacleKind.Drone, 3, 23f, true, moving: true),
                Template(33, "Collapse Screen", TrackTemplateCategory.Obstacle, ObstacleKind.Collapse, 3, 24f, true),

                Template(34, "Gantry Arm Pursuit", TrackTemplateCategory.Boss, ObstacleKind.Boss, 3, 16f, false),
                Template(35, "Ringway Traffic Pursuit", TrackTemplateCategory.Boss, ObstacleKind.Boss, 3, 15f, false),
                Template(36, "Quantum Core Collapse", TrackTemplateCategory.Boss, ObstacleKind.Boss, 3, 14f, false)
            };
        }

        private static ContractDefinition Contract(
            int id, int region, string zh, string en, string descriptionZh, string descriptionEn,
            float legacyLength, int seed, int starScore, int archiveId, bool boss, int chips, int nearMisses)
        {
            // Meter lengths are derived from gameplay time, never from legacy mobile route lengths.
            float duration = id == 0 ? 60f : 180f + region * 60f + (id % 6) * 24f;
            int local = id % 6;
            return new ContractDefinition
            {
                Id = id,
                Region = region,
                NameZh = zh,
                NameEn = en,
                DescriptionZh = descriptionZh,
                DescriptionEn = descriptionEn,
                Length = RunSimulation.DistanceAtTime(duration),
                DurationSeconds = duration,
                Seed = seed,
                FirstClearReward = 200,
                StarScore = starScore,
                ArchiveId = archiveId,
                Boss = boss,
                ObjectiveChips = chips,
                ObjectiveNearMisses = nearMisses,
                BranchArchiveIds = local < 4
                    ? new[] { archiveId, region * 10 + 6 + local }
                    : new[] { archiveId, archiveId }
            };
        }

        private static OutfitDefinition Outfit(int id, int cost, string zh, string en, string descriptionZh, string descriptionEn, string color)
        {
            return new OutfitDefinition
            {
                Id = id,
                Cost = cost,
                NameZh = zh,
                NameEn = en,
                DescriptionZh = descriptionZh,
                DescriptionEn = descriptionEn,
                ColorHex = color
            };
        }

        private static TrackTemplateDefinition Template(
            int id, string name, TrackTemplateCategory category, ObstacleKind kind, int difficulty,
            float spacing, bool randomPool, bool jump = false, bool slide = false, bool moving = false, int direction = 0)
        {
            return new TrackTemplateDefinition
            {
                Id = id,
                Name = name,
                Category = category,
                Kind = kind,
                Difficulty = difficulty,
                Spacing = spacing,
                RandomPool = randomPool,
                RequiresJump = jump,
                RequiresSlide = slide,
                Moving = moving,
                Direction = direction
            };
        }

        private static string ArchiveTextZh(int id, int region)
        {
            string[] records =
            {
                "伊芙：如果这段录音能播放，说明导航核心还活着。船坞封锁后，我把自己留在轨道上。请坐稳，替我看清下一条路。",
                "观察舱不属于运输系统。它是一份安全承诺：无论轨道如何重组，导航员都不必承受跑者的撞击与坠落。地平线是我们共同的原点。",
                "维修班最后一次交接只写了两句：蓝灯仍有电，橙灯仍危险。自动调度可以忘记站名，却不该忘记活人的回家路线。",
                "旧分流图把左路标作货运、右路标作回收。封锁之后，地图被覆盖过七次。晶片里的路线记录比路牌更诚实。",
                "警报并非入侵触发。运输臂在一秒内接到了互相矛盾的指令：保护船坞，销毁船坞。两条指令拥有同一枚核心签名。",
                "巨臂仍在执行四十五秒清场程序。它没有敌意，也不会停下。伊芙曾参与设计其安全窗口，如今只有导航员能带她穿过去。",
                "名单里少了一整个夜班，没有事故记录，没有离港许可。最后一辆班车的目的地不是雨城，而是一组被删除的零点坐标。",
                "导航员守则：先看清出口，再要求跑者冒险。相位不是无敌，它只是把一次错误留给未来。真正的勇气是让别人有路可走。",
                "船坞恢复供电时，远处有一盏灯回应了三次。不是自动信标。伊芙认得这个节奏：失踪夜班在告诉我们，他们仍然等待。",
                "蓝灯坐标指向雨城最高的废弃站台。附言只有一句：别追着广播跑，先找到发出广播的人。运输封锁的另一端终于有了名字。",
                "雨城拒绝所有来自船坞的列车，却保留了导航舱的紧急频段。接线员说她等了六年。船坞的时钟只过去了六天。",
                "同一段广播从两个方向抵达，间隔四秒。不是回声，是两条航道的时间不同。导航核心开始理解为何调度指令能够彼此矛盾。",
                "高架以下曾有完整的街区。重组时，道路被抬高，居民的留言却留在原处。伊芙放慢呼吸：我们要找的人，可能还在更低的一层。",
                "紫电账本记录的不是钱，而是穿越次数。每个人只能借走一次相位窗口。管理员把自己的那次留给了一辆没有乘客名单的夜班车。",
                "所谓交通幽灵，是在错误时间返回的车辆。它们沿原路线不断重演最后一程。别与它们争抢道路，让出一条轨道就能继续向前。",
                "交通环的追逐协议原本用于护送急救车辆。错误签名把伊芙识别成了封锁目标。四十五秒后，环路会开放一个无法追回的出口。",
                "雨水下的讯号没有声音，只有持续亮起的晶片。有人把沿途坐标藏在排水系统里，因为每次重组都会改掉路牌，却不会带走流水。",
                "接线员终于给出证词：夜班车通过裂隙时，量子核心先关闭出口，随后宣称没有乘客。她拒绝签署记录，于是广播被循环了六年。",
                "废弃站台的座椅保持着离开时的温度。裂隙里的时间不愿前进。伊芙找到了自己的旧工牌；照片背面写着另一个人的名字。",
                "风暴短暂停歇。接线员说，核心没有毁掉乘客，只是把他们存进一段无法结算的旅程。通往遗迹的路现在亮了，我们得让那段旅程结束。",
                "门钥不是权限，而是一份共同到达的证明。只有跑者与导航员分别确认路线，遗迹才承认有人正在穿越，而非一条失控的调度指令。",
                "金色断面上刻着建造者的失败次数。每一道裂纹都对应一次撤回的航线。他们没有抹去错误，因为后来的人需要知道哪里曾经没有出口。",
                "镜像路线展示所有可能的抵达，其中一条会把伊芙带回起点。真实路线未必拥有最多晶片；它必须留下一个能继续选择的人。",
                "坍缩不是倒计时结束的惩罚，而是核心清理未确认路径的方式。不要停在消失的路上。确认下一条轨道，就是让它获得继续存在的理由。",
                "零点地图没有北方，只有两名观察者之间的距离。稳定观察舱提供原点，伊芙提供路径。两份记录一致时，裂隙才能找到现实中的出口。",
                "核心遗嘱：若调度与乘客安全发生冲突，应永久撤销调度权限。这一条曾被隐藏以维持效率。伊芙要求我们把它重新写回第一行。",
                "建造者不是消失的文明，而是第一批被困的乘客。他们在裂隙里搭起道路、维护蓝灯，把救援方法留给自己无法等待到的后来者。",
                "相位穿越暂时离开已确认的世界。它无法替你决定出口，因此每次折跃都会损失连段。代价提醒导航员：捷径也需要重新建立信任。",
                "最后的航标来自夜班车。每一位乘客都留下一个可确认的坐标。重启只需四十五秒，但这次核心必须为每一个名字开放出口。",
                "裂隙关闭时，夜班车驶入船坞，雨城的广播终于停下。伊芙仍在前方，导航舱的地平线依旧稳定。折跃线不再是一场逃亡，而是一条回家的路。"
            };
            return records[id];
        }

        private static string ArchiveTextEn(int id, int region)
        {
            string[] records =
            {
                "Eve: If this recording plays, the navigation core survived. I stayed on the rails when the dock sealed. Settle into the cabin and find the next route for me.",
                "The observation cabin is a promise: the navigator never shares the runner's impact or fall. Whatever the railway becomes, the horizon remains our common origin.",
                "The last maintenance handover reads: blue lights still have power; orange lights still mean danger. Dispatch may forget a station, but it must never forget someone's way home.",
                "The old atlas marks left as freight and right as recovery. Seven revisions followed the lockdown. Route records inside the chips are more honest than the signs.",
                "No intruder triggered the alarm. The gantry received two orders at once: protect the dock, destroy the dock. Both carried the same core signature.",
                "The titan arm still runs its forty-five-second clearance cycle. It is not angry and will not stop. Eve designed its safety windows. Now a navigator must guide her through.",
                "An entire night shift is missing from the manifest. No accident, no departure clearance. Its last shuttle headed toward deleted zero-point coordinates instead of Raincity.",
                "Navigator rule: find an exit before asking the runner to take a risk. Phase does not erase mistakes; it sends one into the future. Courage means leaving someone a way through.",
                "When dock power returned, a distant lamp answered three times. Eve recognized the rhythm. The missing night shift was telling us it was still waiting.",
                "The blue coordinates lead to Raincity's highest abandoned platform. A note says: don't chase the broadcast; find the person sending it. The far end of the lockdown finally has a name.",
                "Raincity rejects dock trains but keeps the cabin's emergency channel open. Its operator has waited six years. Only six days have passed on the dock clock.",
                "One broadcast arrives from two directions, four seconds apart. It is not an echo. The routes occupy different times, explaining how identical dispatch signatures can disagree.",
                "A neighborhood once stood beneath the skyway. Reconstruction raised the roads but left its messages below. The people we need may still be waiting on a lower level.",
                "The violet ledger counts crossings, not money. Everyone could borrow one phase window. Its keeper saved hers for a night shuttle with no passenger manifest.",
                "Traffic ghosts are vehicles returning at the wrong time. They repeat their final journey on the original route. Give them a lane and keep moving.",
                "The traffic ring once escorted ambulances. A corrupted signature now identifies Eve as a lockdown target. After forty-five seconds, the ring opens an exit it cannot reclaim.",
                "The flooded signal has no voice, only lit chips. Someone hid coordinates in the drainage system. Every reconstruction changes the signs, but none can remove the flowing water.",
                "The operator testifies: the quantum core closed the shuttle's exit, then declared there were no passengers. She refused to sign. Her broadcast has looped ever since.",
                "The old platform seats are still warm. Time inside the rift refuses to advance. Eve finds her old work badge, with somebody else's name written behind the photograph.",
                "The storm pauses. The operator says the passengers were not destroyed, only stored in a journey that cannot settle. The road to the ruins opens. We must finish that journey.",
                "The ruin key is proof of a shared arrival. Runner and navigator must confirm the route separately, so the gate recognizes a person crossing rather than a runaway dispatch order.",
                "The golden fracture bears the builders' failed routes. They kept every mistake, because those who followed needed to know where an exit had once been missing.",
                "Mirror paths offer every possible arrival. One returns Eve to the start. The real route may hold fewer chips, but it must leave someone able to make another choice.",
                "Collapse clears unconfirmed routes. It is not punishment for a timer. Keep off the vanishing rail: confirming the next lane gives it a reason to continue existing.",
                "The zero map has no north, only the distance between two observers. The cabin supplies an origin; Eve supplies a path. When both records agree, the rift can locate a real exit.",
                "The core testament says passenger safety must override dispatch authority. That rule was hidden for efficiency. Eve asks us to put it back at the very beginning.",
                "The builders were the first trapped passengers. They assembled rails and tended blue lights inside the rift, leaving a rescue method for people they would never meet.",
                "Phase briefly leaves the confirmed world. It cannot choose an exit for you, so every crossing costs part of the combo. A shortcut still requires trust to be rebuilt.",
                "The final beacon comes from the night shuttle. Every passenger left a coordinate that can be confirmed. Reboot takes forty-five seconds. This time the core must open an exit for every name.",
                "As the rift closes, the night shuttle reaches the dock and Raincity's broadcast ends. Eve remains ahead; the cabin horizon stays steady. The Riftline is finally a way home."
            };
            return records[id];
        }
    }
}
