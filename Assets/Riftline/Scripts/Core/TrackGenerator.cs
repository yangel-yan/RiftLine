using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftline
{
    internal sealed class TrackGenerator
    {
        private readonly GameCatalog catalog;
        private readonly ContractDefinition contract;
        private readonly bool endless;
        private readonly int seed;
        private int nextEventId;
        private int row;
        private double nextTime = 3d;
        private bool archiveForkGenerated;
        private int lastTemplate = -1;
        private int templateRepeats;
        private TrackEvent groundProfile;

        public TrackGenerator(GameCatalog catalog, ContractDefinition contract, int seed, bool endless)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.contract = contract;
            this.endless = endless;
            this.seed = (seed & int.MaxValue) % 6;
        }

        public void GenerateUntil(float targetDistance, List<TrackEvent> destination)
        {
            if (RunSimulation.DistanceAtTime((float)nextTime) > targetDistance) return;
            double limit = endless ? double.MaxValue : RunSimulation.TimeAtDistance(contract.Length);
            while (nextTime < limit - .05d && RunSimulation.DistanceAtTime((float)nextTime) <= targetDistance)
            {
                int region = endless ? ((int)(nextTime / 120d)) % 3 : contract.Region;
                int budget = endless ? CombinationBudgetAt((float)nextTime) : Math.Min(2, 1 + contract.Region);
                if (!endless && contract.Id == 0 && nextTime <= 108d)
                    AddTutorialRow(destination, region);
                else if (!endless && contract.Id == 1 && nextTime <= 78d && (row % 12 == 0 || row % 12 == 1))
                    AddPhaseLesson(destination, region);
                else
                    AddRecipe(destination, region, budget, limit);
                row++;
                nextTime += endless ? CadenceAt((float)nextTime) : 3d;
            }
            destination.Sort((a, b) =>
            {
                int order = a.Distance.CompareTo(b.Distance);
                return order == 0 ? a.Id.CompareTo(b.Id) : order;
            });
        }

        internal static int CombinationBudgetAt(float time)
        {
            return time < 437.5f ? 1 : time < 557.5f ? 2 : 3;
        }

        internal static float CadenceAt(float time)
        {
            return time < 437.5f ? 3f : time < 557.5f ? 2.6f : 2.2f;
        }

        private int RouteLane { get { return (seed % 3 + row / 12) % 3; } }
        private int Variation { get { return seed + row / 12; } }

        private void AddRecipe(List<TrackEvent> events, int region, int budget, double duration)
        {
            int cell = row % 12;
            bool boss = !endless && contract.Boss && nextTime >= duration - 45d;
            int bossTemplate = 34 + region;
            if (boss && cell != 4 && cell != 8 && cell != 7 && cell != 9)
            {
                ObstacleKind[] kinds = region == 0
                    ? new[] { ObstacleKind.MovingGate, ObstacleKind.Crate, ObstacleKind.LowBarrier }
                    : region == 1
                        ? new[] { ObstacleKind.Drone, ObstacleKind.HighBeam, ObstacleKind.MovingGate }
                        : new[] { ObstacleKind.Gap, ObstacleKind.Collapse, ObstacleKind.PhaseWall };
                // Interleave the boss set-piece with open rows; never repeat a visual template three times.
                if (cell % 3 == 0) Chip(events, nextTime, RouteLane, region, 1);
                else HazardRow(events, nextTime, kinds[cell % 3], RouteLane, region, budget, bossTemplate, GameCommand.None, true);
                return;
            }
            switch (cell)
            {
                case 0:
                case 3:
                case 7:
                case 10:
                    TrackTemplateDefinition straight = Select(TrackTemplateCategory.Straight, null,
                        Variation * 4 + (cell == 0 ? 0 : cell == 3 ? 1 : cell == 7 ? 2 : 3), region == 0 ? 1 : 2);
                    EmitTemplate(events, nextTime, straight, RouteLane, region, budget);
                    break;
                case 5:
                    if (region > 0 && nextTime + 4.5d < duration)
                        AddHeightRecipe(events, region, budget);
                    else Chip(events, nextTime, RouteLane, region, 1);
                    break;
                case 1:
                    EmitTemplate(events, nextTime, Select(TrackTemplateCategory.Obstacle,
                        region == 2 ? ObstacleKind.Gap : ObstacleKind.Crate, Variation, budget > 1 ? 3 : 2),
                        RouteLane, region, budget);
                    break;
                case 2:
                    EmitTemplate(events, nextTime, Select(TrackTemplateCategory.Obstacle,
                        ObstacleKind.LowBarrier, Variation, 2), RouteLane, region, budget);
                    break;
                case 4:
                    int direction = region == 0 ? 1 : ((seed + row / 12) % 2 == 0 ? -1 : 1);
                    TrackTemplateDefinition turn = Select(TrackTemplateCategory.Turn,
                        direction < 0 ? ObstacleKind.TurnLeft : ObstacleKind.TurnRight, Variation / 2, 2);
                    Add(events, nextTime, 1, 1, turn.Kind, turn.Id, region).Branch = turn.Direction;
                    break;
                case 6:
                    EmitTemplate(events, nextTime, Select(TrackTemplateCategory.Obstacle,
                        ObstacleKind.HighBeam, Variation, 2), RouteLane, region, budget);
                    break;
                case 8:
                    AddFork(events, region, budget);
                    break;
                case 9:
                    // Branch payload already occupies this row. No unrelated hazards may intrude on its pickup path.
                    break;
                case 11:
                    ObstacleKind kind = region == 0 ? ObstacleKind.MovingGate
                        : region == 1 ? ObstacleKind.Drone : (budget > 1 && Variation % 2 == 0
                            ? ObstacleKind.Collapse : ObstacleKind.PhaseWall);
                    EmitTemplate(events, nextTime, Select(TrackTemplateCategory.Obstacle,
                        kind, Variation / (region == 2 ? 2 : 1), budget > 1 ? 3 : 2), RouteLane, region, budget);
                    if (endless && budget == 3)
                    {
                        // A second action after recovery raises combination pressure, not speed or visual motion.
                        EmitTemplate(events, nextTime + .95d, catalog.Templates[23], RouteLane, region, budget);
                    }
                    break;
            }
        }

        private void AddFork(List<TrackEvent> events, int region, int budget)
        {
            TrackTemplateDefinition template = Select(TrackTemplateCategory.Fork, null, Variation, 2);
            TrackEvent fork = Add(events, nextTime, 1, 1, template.Kind, template.Id, region);
            fork.Branch = -1;
            bool archive = !endless && !archiveForkGenerated;
            archiveForkGenerated |= archive;
            for (int route = -1; route <= 1; route += 2)
            {
                int first = events.Count;
                bool risky = route == (template.Direction == 0 ? -1 : template.Direction);
                if (risky)
                {
                    HazardRow(events, nextTime + 3d, ObstacleKind.LowBarrier, 1, region, budget, 23, GameCommand.Jump);
                }
                else
                {
                    HazardRow(events, nextTime + 3d, region == 2 ? ObstacleKind.PhaseWall : ObstacleKind.Crate,
                        1, region, 1, region == 2 ? 28 : 22);
                }
                // Keep the long-run left-route income while making right-reward forks real local choices.
                int chips = template.Direction == 0 ? (endless ? 3 : 2)
                    : template.Direction < 0 ? (route < 0 ? (endless ? 6 : 4) : 1)
                    : route > 0 ? 4 : 3;
                for (int i = 1; i < chips; i++)
                    Chip(events, nextTime + 3.12d + i * .12d, 1, region, 1);
                if (archive && contract.BranchArchiveIds != null)
                {
                    TrackEvent pickup = Add(events, nextTime + 4.5d, 1, 1, ObstacleKind.Archive, 0, region);
                    pickup.ArchiveId = contract.BranchArchiveIds[route < 0 ? 0 : 1];
                }
                for (int i = first; i < events.Count; i++)
                {
                    events[i].ForkId = fork.Id;
                    events[i].RequiredRoute = route;
                }
            }
        }

        private TrackTemplateDefinition Select(TrackTemplateCategory category, ObstacleKind? kind,
            int variation, int difficulty)
        {
            var options = new List<TrackTemplateDefinition>();
            foreach (TrackTemplateDefinition template in catalog.Templates)
                if (template.RandomPool && template.Category == category && template.Difficulty <= difficulty
                    && (!kind.HasValue || template.Kind == kind.Value))
                    options.Add(template);
            if (options.Count == 0) throw new InvalidOperationException("No catalog recipe for " + category + "/" + kind);
            return options[variation % options.Count];
        }

        private void EmitTemplate(List<TrackEvent> events, double time, TrackTemplateDefinition template,
            int safeLane, int region, int budget)
        {
            if (!IsHazard(template.Kind))
            {
                Add(events, time, safeLane, safeLane, template.Kind, template.Id, region);
                return;
            }
            ObstacleKind secondary = template.Id == 30 ? ObstacleKind.HighBeam
                : template.Id == 31 ? ObstacleKind.Gap
                : template.Id == 32 || template.Id == 33 ? ObstacleKind.PhaseWall : template.Kind;
            if (secondary != template.Kind && budget >= 2)
            {
                bool first = true;
                for (int lane = 0; lane < 3; lane++)
                {
                    if (lane == safeLane) continue;
                    TrackEvent hazard = Add(events, time, lane, safeLane,
                        first ? template.Kind : secondary, template.Id, region);
                    hazard.CombinationBudget = budget;
                    hazard.Depth = HazardDepth(hazard.Kind);
                    first = false;
                }
                Chip(events, time + .12d, safeLane, region, template.Id);
                return;
            }
            GameCommand action = template.RequiresJump ? GameCommand.Jump
                : template.RequiresSlide ? GameCommand.Slide : GameCommand.None;
            HazardRow(events, time, template.Kind, safeLane, region, budget, template.Id, action);
        }

        private void AddHeightRecipe(List<TrackEvent> events, int region, int budget)
        {
            int[] sequence = region == 2 ? new[] { 17, 18, 19, 20, 21, 18 } : new[] { 17, 18, 19, 20, 17, 18 };
            TrackTemplateDefinition template = catalog.Templates[sequence[Variation % sequence.Length]];
            float distance = RunSimulation.DistanceAtTime((float)nextTime);
            float from = groundProfile == null ? 0f : groundProfile.GroundHeightAtDistance(distance);
            bool rise = template.Kind == ObstacleKind.Rise || template.RequiresJump;
            if (rise && from > .01f || !rise && from < -.01f)
            {
                rise = !rise;
                template = Select(TrackTemplateCategory.Height, rise ? ObstacleKind.Rise : ObstacleKind.Fall, Variation, 2);
            }
            if (!template.RandomPool) { Chip(events, nextTime, RouteLane, region, 1); return; }
            TrackEvent height = Add(events, nextTime, RouteLane, RouteLane,
                rise ? ObstacleKind.Rise : ObstacleKind.Fall, template.Id, region);
            height.GroundHeight = from;
            height.GroundTargetHeight = from + (rise ? 1.2f : -1.2f);
            height.GroundTransitionStartDistance = distance;
            height.GroundTransitionSeconds = 4.5f;
            groundProfile = height;
            if (IsHazard(template.Kind))
            {
                double delay = endless ? Math.Min(1.5d, CadenceAt((float)nextTime) - 1.5d) : 1.5d;
                EmitTemplate(events, nextTime + delay, template, RouteLane, region, budget);
            }
            else Chip(events, nextTime + .12d, RouteLane, region, template.Id);
        }

        private static float HazardDepth(ObstacleKind kind)
        {
            return kind == ObstacleKind.Gap ? 1.1f : kind == ObstacleKind.Collapse ? 1.2f : .6f;
        }

        private void AddTutorialRow(List<TrackEvent> events, int region)
        {
            int cell = row % 12;
            if (row == 0)
            {
                for (int i = 0; i < 3; i++) Chip(events, 1.2d + i * .4d, 0, region, 1);
            }
            int lesson = -1;
            GameCommand action = GameCommand.None;
            if (cell == 0 || cell == 1 || cell == 7)
            {
                lesson = cell == 0 ? 0 : cell == 1 ? 1 : 5;
                action = cell == 1 ? GameCommand.Right : GameCommand.Left;
                int lane = cell == 1 ? 1 : 0;
                HazardRow(events, nextTime, cell == 7 ? ObstacleKind.MovingGate : ObstacleKind.Crate,
                    lane, region, 1, cell == 7 ? 25 : 22);
            }
            else if (cell == 2 || cell == 3)
            {
                lesson = cell == 2 ? 2 : 3;
                action = cell == 2 ? GameCommand.Jump : GameCommand.Slide;
                HazardRow(events, nextTime, cell == 2 ? ObstacleKind.LowBarrier : ObstacleKind.HighBeam,
                    1, region, 1, cell == 2 ? 23 : 24, action);
            }
            else if (cell == 5)
            {
                lesson = 4;
                TrackEvent turn = Add(events, nextTime, 1, 1, ObstacleKind.TurnRight, 11, region);
                turn.Branch = 1;
                turn.TutorialLesson = lesson;
                turn.RequiredAction = GameCommand.Right;
                return;
            }
            else if (row == 8)
            {
                TrackEvent charge = Add(events, nextTime, 1, 1, ObstacleKind.Chip, 1, region);
                charge.PhaseCharge = true;
                return;
            }
            else if (row == 9)
            {
                AddPhaseLessonWall(events, region);
                return;
            }
            else if (row == 10)
            {
                AddFork(events, region, 1);
                return;
            }
            else
            {
                // Reset to center before the next repeated left-lane lesson so an early miss remains recoverable.
                Chip(events, nextTime, cell == 11 ? 1 : cell < 2 || cell >= 7 ? 0 : 1, region, cell % 2);
                return;
            }
            // Lessons use ordinary visible life loss; successful actions still record mastery.
            foreach (TrackEvent item in events)
            {
                if (Math.Abs(RunSimulation.TimeAtDistance(item.Distance) - nextTime) < .001d && IsHazard(item.Kind))
                {
                    item.TutorialLesson = lesson;
                    item.RequiredAction = action;
                }
            }
        }

        private void AddPhaseLesson(List<TrackEvent> events, int region)
        {
            if (row % 12 == 0)
            {
                TrackEvent charge = Add(events, nextTime, 1, 1, ObstacleKind.Chip, 1, region);
                charge.PhaseCharge = true;
            }
            else
            {
                AddPhaseLessonWall(events, region);
            }
        }

        private void AddPhaseLessonWall(List<TrackEvent> events, int region)
        {
            HazardRow(events, nextTime, ObstacleKind.PhaseWall, 1, region, 1, 7, GameCommand.Phase);
            foreach (TrackEvent item in events)
                if (Math.Abs(RunSimulation.TimeAtDistance(item.Distance) - nextTime) < .001d && IsHazard(item.Kind))
                {
                    item.TutorialLesson = 6;
                    item.RequiredAction = GameCommand.Phase;
                }
        }

        private void Chip(List<TrackEvent> events, double time, int lane, int region, int template)
        {
            Add(events, time, lane, lane, ObstacleKind.Chip, template, region);
        }

        private void HazardRow(List<TrackEvent> events, double time, ObstacleKind kind, int safeLane,
            int region, int budget, int template, GameCommand action = GameCommand.None, bool boss = false)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                if (lane == safeLane && action == GameCommand.None) continue;
                TrackEvent hazard = Add(events, time, lane, safeLane, kind, template, region);
                hazard.RequiredAction = action;
                hazard.CombinationBudget = budget;
                hazard.Depth = HazardDepth(kind);
                hazard.BossRecipe = boss;
            }
            bool opening = !endless && contract.Id < 2 && time <= 30d;
            if (opening)
            {
                for (int i = 0; i < 3; i++)
                    Chip(events, time - 1.2d + i * .4d, safeLane, region, i % 2);
            }
            Chip(events, time + .12d, safeLane, region, template);
            if (opening)
            {
                Chip(events, time + .42d, safeLane, region, 0);
                Chip(events, time + .72d, safeLane, region, 1);
            }
        }

        private TrackEvent Add(List<TrackEvent> events, double time, int lane, int safeLane,
            ObstacleKind kind, int template, int region)
        {
            // Track template repetition is counted per row, not per lane instance.
            if (events.Count == 0 || Math.Abs(events[events.Count - 1].Distance - RunSimulation.DistanceAtTime((float)time)) > .001f)
            {
                templateRepeats = template == lastTemplate ? templateRepeats + 1 : 1;
                if (templateRepeats > 2 && kind == ObstacleKind.Chip) template = template == 0 ? 1 : 0;
                lastTemplate = template;
            }
            float speed = Math.Min(10f, 6.5f + .008f * (float)time);
            float warning = Math.Max(2f, catalog.Templates[template].Spacing / speed);
            var item = new TrackEvent
            {
                Id = nextEventId++,
                Distance = RunSimulation.DistanceAtTime((float)time),
                Lane = lane,
                SafeLane = safeLane,
                Kind = kind,
                TemplateId = template,
                Region = region,
                Width = kind == ObstacleKind.Gap ? 1.8f : 1.2f,
                WarningAngle = Mathf.Atan2((lane - 1) * 2.6f, speed * warning + 5.5f) * Mathf.Rad2Deg,
                ObserverClearance = 5.5f,
                WarningSeconds = warning,
                GroundHeight = groundProfile == null ? 0f : groundProfile.GroundHeight,
                GroundTargetHeight = groundProfile == null ? 0f : groundProfile.GroundTargetHeight,
                GroundTransitionStartDistance = groundProfile == null ? 0f : groundProfile.GroundTransitionStartDistance,
                GroundTransitionSeconds = groundProfile == null ? 0f : groundProfile.GroundTransitionSeconds
            };
            events.Add(item);
            return item;
        }

        internal static bool IsHazard(ObstacleKind kind)
        {
            return kind <= ObstacleKind.Collapse || kind == ObstacleKind.Boss;
        }

        public static bool Validate(GameCatalog catalog, int seed, out string error)
        {
            // The Editor runs an independent geometry validator and executable witnesses in addition to this API.
            for (int mode = 0; mode < 2; mode++)
            {
                ContractDefinition contract = catalog.Contracts[(seed & int.MaxValue) % catalog.Contracts.Length];
                var events = new List<TrackEvent>();
                new TrackGenerator(catalog, contract, seed, mode == 1).GenerateUntil(
                    mode == 1 ? RunSimulation.DistanceAtTime(600f) : contract.Length, events);
                double lastTurn = -100d;
                foreach (TrackEvent item in events)
                {
                    double time = RunSimulation.TimeAtDistance(item.Distance);
                    if (item.Lane < 0 || item.Lane > 2 || Math.Abs(item.WarningAngle) > 35f
                        || item.ObserverClearance < 1.5f || item.WarningSeconds < 2f)
                    { error = "invalid lane, warning or near-field clearance"; return false; }
                    if (item.Kind != ObstacleKind.TurnLeft && item.Kind != ObstacleKind.TurnRight) continue;
                    if (time - lastTurn < 3d - .001d)
                    { error = "turn separation under three seconds"; return false; }
                    lastTurn = time;
                }
            }
            error = null;
            return true;
        }
    }
}
