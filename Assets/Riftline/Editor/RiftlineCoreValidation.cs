#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Riftline
{
    public static class RiftlineCoreValidation
    {
        [Serializable]
        public sealed class Witness
        {
            public int ContractId;
            public int Seed;
            public bool Endless;
            public int Route;
            public float Duration;
            public float Distance;
            public int Score;
            public int Chips;
            public int NearMisses;
            public int Health;
            public int DamageCount;
            public int Stars;
            public int PhaseUses;
            public int Turns;
            public float BossSeconds;
            public string TerminalPhase;
            public string FailureReason;
            public bool TutorialComplete;
            public int[] Archives;
            public string[] Commands;
            public int[] HeightTemplates;
            public float MinimumGroundHeight;
            public float MaximumGroundHeight;
            public float FinalGroundHeight;
        }

        [Serializable]
        private sealed class Report
        {
            public bool Success;
            public double DurationSeconds;
            public int Seeds;
            public int CompleteSequences;
            public int ExecutedWitnesses;
            public int HeightWitnesses;
            public int[] TemplateExecutions;
            public string EvidenceLayer = "Core simulation and field serialization; not Unity Play Mode or PICO";
            public string[] Details;
            public Witness[] Witnesses;
        }

        [Serializable]
        private sealed class SlotProbe
        {
            public string Payload = null;
        }

        public static string RunAll(string reportPath = "/tmp/riftline-content-staged/Tests/validation.json")
        {
            var clock = Stopwatch.StartNew();
            var details = new List<string>();
            var witnesses = new List<Witness>();
            var report = new Report();
            try
            {
                GameCatalog catalog = GameCatalog.CreateDefault();
                ValidateCatalog(catalog, details);
                report.TemplateExecutions = ValidateContent(catalog, details);
                ValidateRegressions(details);
                ValidateHandGestures(details);
                ValidateHandControlAssets(details);
                ValidateHandRuntimeAdapter(details);
                var verified = new Dictionary<string, string>();
                for (int id = 0; id < 18; id++)
                for (int mode = 0; mode < 2; mode++)
                for (int variant = 0; variant < 6; variant++)
                {
                    ContractDefinition contract = catalog.Contracts[id];
                    List<TrackEvent> events = Generate(catalog, contract, variant, mode == 1);
                    ValidateGeometry(events, contract, mode == 1);
                    for (int route = -1; route <= 1; route += 2)
                    {
                        Witness evidence;
                        ExecuteWitness(catalog, contract, variant, mode == 1, route, out evidence);
                        witnesses.Add(evidence);
                    }
                    verified.Add(id + ":" + mode + ":" + variant, Fingerprint(events));
                }
                Check(witnesses.Count == 432, "432 executed independent recipe-variant witnesses", details);
                var archiveIds = new HashSet<int>();
                var production = new List<Witness>();
                foreach (ContractDefinition contract in catalog.Contracts)
                for (int mode = 0; mode < 2; mode++)
                for (int route = -1; route <= 1; route += 2)
                {
                    Witness evidence;
                    ExecuteWitness(catalog, contract, contract.Seed, mode == 1, route, out evidence);
                    witnesses.Add(evidence);
                    production.Add(evidence);
                    foreach (int id in evidence.Archives) archiveIds.Add(id);
                }
                Check(archiveIds.SetEquals(Enumerable.Range(0, 30)), "72 production passes acquire all 30 branch archives", details);
                Check(catalog.Contracts.All(c => c.Id == 0
                    ? production.First(w => w.ContractId == c.Id && !w.Endless && w.Route < 0).Chips
                        == production.First(w => w.ContractId == c.Id && !w.Endless && w.Route > 0).Chips
                    : production.First(w => w.ContractId == c.Id && !w.Endless && w.Route < 0).Chips
                        > production.First(w => w.ContractId == c.Id && !w.Endless && w.Route > 0).Chips),
                    "short introduction has a balanced first fork; longer left routes retain aggregate chip advantage", details);
                report.HeightWitnesses = witnesses.Count(w => w.HeightTemplates.Length > 0);
                Check(witnesses.All(w => !(w.Endless || catalog.Contracts[w.ContractId].Region > 0)
                    || w.HeightTemplates.Length > 0 && w.MaximumGroundHeight - w.MinimumGroundHeight > .5f),
                    report.HeightWitnesses + " surviving independent witnesses traverse actual nonzero road elevations", details);
                for (int seed = 0; seed < 10000; seed++)
                for (int mode = 0; mode < 2; mode++)
                {
                    ContractDefinition contract = catalog.Contracts[seed % 18];
                    List<TrackEvent> events = Generate(catalog, contract, seed, mode == 1);
                    // Exact full-stream equivalence transfers the executed witness, not a declared safe-lane assertion.
                    string key = contract.Id + ":" + mode + ":" + seed % 6;
                    Require(Fingerprint(events) == verified[key], "unwitnessed sequence seed=" + seed + " mode=" + mode);
                    ValidateGeometry(events, contract, mode == 1);
                    report.CompleteSequences++;
                }
                report.Seeds = 10000;
                Check(report.CompleteSequences == 20000, "10000 complete seeds in each mode, each matched to both executed routes", details);
                ValidateProgression(catalog, details);
                ValidateValidatorRejection(catalog, details);
                report.Success = true;
                return "RIFTLINE core validation passed: " + details.Count + " groups, " + witnesses.Count
                    + " independent witnesses, " + report.CompleteSequences + " complete sequences";
            }
            catch (Exception error)
            {
                details.Add("FAIL: " + error);
                throw;
            }
            finally
            {
                report.DurationSeconds = clock.Elapsed.TotalSeconds;
                report.ExecutedWitnesses = witnesses.Count;
                report.Details = details.ToArray();
                report.Witnesses = witnesses.ToArray();
                string directory = Path.GetDirectoryName(reportPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            }
        }

        // Does not use SafeLane, RequiredAction, AutoTurn, or the production autopilot.
        private sealed class GeometryController
        {
            private readonly int route;
            private int actionEvent = -1;
            private float nextLaneCommand;
            public GeometryController(int route) { this.route = route; }

            public GameCommand Decide(RunSimulation run)
            {
                TrackEvent context = run.Events.FirstOrDefault(e => !e.Passed && run.IsEventActive(e)
                    && IsContext(e.Kind) && e.Distance >= run.Distance && e.Distance - run.Distance <= run.Speed * 2f);
                if (context != null)
                {
                    if (context.Resolved) return GameCommand.None;
                    int direction = context.Kind == ObstacleKind.Fork ? route
                        : context.Kind == ObstacleKind.TurnLeft ? -1 : 1;
                    return direction < 0 ? GameCommand.Left : GameCommand.Right;
                }
                TrackEvent next = run.Events.FirstOrDefault(e => !e.Passed && run.IsEventActive(e)
                    && RunSimulation.TimeAtDistance(e.Distance) >= run.Elapsed - .001d);
                if (next == null) return GameCommand.None;
                double ahead = RunSimulation.TimeAtDistance(next.Distance) - run.Elapsed;
                if (ahead > 2.2d) return GameCommand.None;
                List<TrackEvent> hazards = run.Events.Where(e => !e.Passed && run.IsEventActive(e)
                    && IsHazard(e.Kind) && Math.Abs(e.Distance - next.Distance) < .003f).ToList();
                int lane = next.Lane;
                GameCommand action = GameCommand.None;
                if (hazards.Count > 0)
                {
                    int[] free = Enumerable.Range(0, 3).Where(candidate => !hazards.Any(h =>
                        Math.Abs((candidate - h.Lane) * 2.6f) <= Math.Max(.55f, h.Width / 2f + .3f))).ToArray();
                    if (free.Length > 0) lane = free.OrderBy(candidate => Math.Abs(candidate - run.Lane)).First();
                    else
                    {
                        TrackEvent pickup = run.Events.FirstOrDefault(e => !e.Passed && run.IsEventActive(e)
                            && e.Kind == ObstacleKind.Chip && e.Distance > next.Distance
                            && RunSimulation.TimeAtDistance(e.Distance) - RunSimulation.TimeAtDistance(next.Distance) < .4d);
                        lane = pickup != null ? pickup.Lane : run.Lane;
                        ObstacleKind kind = hazards.First(h => h.Lane == lane).Kind;
                        if (kind == ObstacleKind.LowBarrier || kind == ObstacleKind.Crate || kind == ObstacleKind.Gap)
                            action = GameCommand.Jump;
                        else if (kind == ObstacleKind.HighBeam || kind == ObstacleKind.Drone)
                            action = GameCommand.Slide;
                        else if (kind == ObstacleKind.PhaseWall || kind == ObstacleKind.Boss)
                            action = GameCommand.Phase;
                        else throw new InvalidOperationException("no geometric avoidance for " + kind);
                    }
                }
                if (lane != run.Lane && run.Elapsed >= nextLaneCommand)
                {
                    nextLaneCommand = run.Elapsed + .34f;
                    return lane < run.Lane ? GameCommand.Left : GameCommand.Right;
                }
                if (action == GameCommand.None || next.Id == actionEvent || ahead > .3d || ahead <= .1d)
                    return GameCommand.None;
                if (action == GameCommand.Phase && run.PhaseEnergy < 1f) return GameCommand.None;
                actionEvent = next.Id;
                return action;
            }
        }

        public static RunSimulation ExecuteWitness(GameCatalog catalog, ContractDefinition contract, int seed,
            bool endless, int route, out Witness evidence, GameProgress progress = null, float endTime = 600f,
            float beginCommandsAt = 0f)
        {
            var run = new RunSimulation(catalog);
            var controller = new GeometryController(route);
            var archives = new List<int>();
            var commands = new List<string>();
            var heightIds = new HashSet<int>();
            var heightTemplates = new List<int>();
            float minimumGround = 0f, maximumGround = 0f;
            float bossStart = -1f;
            run.ArchiveCollected += e =>
            {
                archives.Add(e.ArchiveId);
                if (progress != null) progress.CollectArchive(e.ArchiveId);
            };
            run.Start(contract, seed, endless);
            run.Tick(3f);
            for (int frame = 0; frame < 10000 && run.Phase == RunPhase.Running; frame++)
            {
                GameCommand command = run.Elapsed < beginCommandsAt ? GameCommand.None : controller.Decide(run);
                if (command != GameCommand.None)
                {
                    commands.Add(run.Elapsed.ToString("F3", CultureInfo.InvariantCulture) + ":" + command);
                    run.Handle(command);
                }
                if (run.BossActive && bossStart < 0f) bossStart = run.Elapsed;
                run.Tick(.08f);
                minimumGround = Math.Min(minimumGround, run.GroundHeight);
                maximumGround = Math.Max(maximumGround, run.GroundHeight);
                foreach (TrackEvent item in run.Events)
                    if (item.Entered && (item.Kind == ObstacleKind.Rise || item.Kind == ObstacleKind.Fall)
                        && heightIds.Add(item.Id))
                        heightTemplates.Add(item.TemplateId);
                if (endless && run.Elapsed >= endTime) run.SafeEnd();
            }
            RunResult result = run.CreateResult();
            evidence = new Witness
            {
                ContractId = contract.Id, Seed = seed, Endless = endless, Route = route, Duration = run.Elapsed,
                Distance = run.Distance, Score = run.Score, Chips = run.Chips, NearMisses = run.NearMisses,
                Health = run.Health, DamageCount = run.DamageCount,
                Stars = endless ? 0 : GameProgress.CalculateStars(contract, result), PhaseUses = run.PhaseUses,
                Turns = run.TurnsCompleted, BossSeconds = bossStart < 0f ? 0f : run.Elapsed - bossStart,
                TerminalPhase = run.Phase.ToString(), FailureReason = run.FailureReason, TutorialComplete = run.TutorialComplete,
                Archives = archives.ToArray(), Commands = commands.ToArray(), HeightTemplates = heightTemplates.ToArray(),
                MinimumGroundHeight = minimumGround, MaximumGroundHeight = maximumGround, FinalGroundHeight = run.GroundHeight
            };
            if (run.Phase != RunPhase.Completed || !result.Victory || !endless && evidence.Stars != 3)
                File.WriteAllText("/tmp/riftline-content-staged/Tests/failed-witness.json", JsonUtility.ToJson(evidence, true));
            Require(run.Phase == RunPhase.Completed && result.Victory && !run.UsedAutoTurn,
                "witness failed id=" + contract.Id + " seed=" + seed + " endless=" + endless + " route=" + route
                + " t=" + run.Elapsed + " reason=" + run.FailureReason + " trace=" + string.Join(",", commands.TakeLast(12)));
            Require(beginCommandsAt > 0f || run.DamageCount == 0,
                "independent safe-route witness touched a hazard id=" + contract.Id + " seed=" + seed);
            Require(endless || evidence.Stars == 3, "three-star witness failed id=" + contract.Id);
            Require(endless || Math.Abs(run.Elapsed - contract.DurationSeconds) < .002f, "contract duration mismatch");
            Require(endless || !contract.Boss || Math.Abs(evidence.BossSeconds - 45f) < .1f, "boss not last 45 seconds");
            Require(!endless || endTime < 600f || run.Speed == 10f && run.CombinationBudget == 3, "endless speed/budget cap");
            return run;
        }

        private static List<TrackEvent> Generate(GameCatalog catalog, ContractDefinition contract, int seed, bool endless)
        {
            var events = new List<TrackEvent>();
            new TrackGenerator(catalog, contract, seed, endless).GenerateUntil(
                endless ? RunSimulation.DistanceAtTime(600f) : contract.Length, events);
            return events;
        }

        private static string Fingerprint(List<TrackEvent> events)
        {
            var text = new StringBuilder();
            foreach (TrackEvent e in events)
            {
                text.Append(e.Id).Append(':').Append(e.Distance.ToString("R", CultureInfo.InvariantCulture))
                    .Append(':').Append(e.Kind).Append(':').Append(e.Lane).Append(':').Append(e.Width.ToString("R", CultureInfo.InvariantCulture))
                    .Append(':').Append(e.Depth.ToString("R", CultureInfo.InvariantCulture)).Append(':').Append(e.Branch)
                    .Append(':').Append(e.ForkId).Append(':').Append(e.RequiredRoute).Append(':').Append(e.ArchiveId)
                    .Append(':').Append(e.PhaseCharge).Append(':').Append(e.TutorialLesson).Append(':').Append(e.RequiredAction)
                    .Append(':').Append(e.SafeLane).Append(':').Append(e.TemplateId).Append(':').Append(e.Region)
                    .Append(':').Append(e.CombinationBudget).Append(':').Append(e.BossRecipe)
                    .Append(':').Append(e.WarningAngle.ToString("R", CultureInfo.InvariantCulture))
                    .Append(':').Append(e.ObserverClearance.ToString("R", CultureInfo.InvariantCulture))
                    .Append(':').Append(e.WarningSeconds.ToString("R", CultureInfo.InvariantCulture))
                    .Append(':').Append(e.GroundHeight.ToString("R", CultureInfo.InvariantCulture))
                    .Append(':').Append(e.GroundTargetHeight.ToString("R", CultureInfo.InvariantCulture))
                    .Append(':').Append(e.GroundTransitionStartDistance.ToString("R", CultureInfo.InvariantCulture))
                    .Append(':').Append(e.GroundTransitionSeconds.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            }
            using (SHA256 sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }

        private static void ValidateGeometry(List<TrackEvent> events, ContractDefinition contract, bool endless)
        {
            double duration = endless ? 600d : contract.DurationSeconds;
            Require(events.Count > 0 && events.Select(e => e.Id).Distinct().Count() == events.Count, "empty/duplicate event stream");
            if (endless)
                Require(events.Any(e => e.Kind == ObstacleKind.LowBarrier
                    && RunSimulation.TimeAtDistance(e.Distance) > 557.5d && events.Any(h => IsHazard(h.Kind)
                        && h.Distance < e.Distance && RunSimulation.TimeAtDistance(e.Distance) - RunSimulation.TimeAtDistance(h.Distance) < 1d)),
                    "no actual expert double-action recipe after speed cap");
            foreach (TrackEvent e in events)
            {
                double t = RunSimulation.TimeAtDistance(e.Distance);
                double previewDepth = e.ObserverClearance + e.Distance - RunSimulation.DistanceAtTime((float)Math.Max(0d, t - 2d));
                double lateral = (e.Lane - 1) * 2.6d;
                double ground = e.GroundHeightAtDistance(e.Distance);
                double angle = Math.Abs(Math.Atan2(Math.Sqrt(lateral * lateral + ground * ground), previewDepth) * 180d / Math.PI);
                Require(e.Lane >= 0 && e.Lane < 3 && angle <= 35d && Math.Abs(e.WarningAngle) <= 35f, "warning geometry");
                Require(!float.IsNaN(e.GroundHeight) && !float.IsInfinity(e.GroundHeight)
                    && !float.IsNaN(e.GroundTargetHeight) && !float.IsInfinity(e.GroundTargetHeight)
                    && Math.Abs(e.GroundHeight) <= 1.20001f && Math.Abs(e.GroundTargetHeight) <= 1.20001f,
                    "invalid road elevation");
                if (e.Kind == ObstacleKind.Rise || e.Kind == ObstacleKind.Fall)
                {
                    Require(e.GroundTransitionSeconds >= 3f && Math.Abs(e.GroundTransitionStartDistance - e.Distance) < .001f
                        && (e.Kind == ObstacleKind.Rise ? e.GroundTargetHeight > e.GroundHeight
                            : e.GroundTargetHeight < e.GroundHeight), "empty/abrupt/wrong-direction road transition");
                    Require(endless || t + e.GroundTransitionSeconds <= duration, "height transition beyond contract end");
                }
                Require(e.WarningSeconds >= 2f && e.ObserverClearance >= 1.5f && t <= duration + .01d, "warning/clearance/end bound");
                int expectedRegion = endless ? (int)(t / 120d + .00001d) % 3 : contract.Region;
                Require(e.Region == expectedRegion, "wrong regional recipe at " + t);
                int expectedBudget = endless ? t < 437.5d ? 1 : t < 557.5d ? 2 : 3 : Math.Min(2, 1 + contract.Region);
                // Fork payload retains the budget selected at its entrance, before the next threshold.
                Require(e.CombinationBudget <= expectedBudget && e.CombinationBudget <= 3, "combination budget exceeded");
                Require(!e.BossRecipe || !endless && contract.Boss && t >= duration - 45.001d, "boss in ordinary pool");
                Require(e.ForkId < 0 || events.Any(f => f.Id == e.ForkId && f.Kind == ObstacleKind.Fork
                    && f.Distance < e.Distance && (e.RequiredRoute == -1 || e.RequiredRoute == 1)), "orphan branch event");
            }
            for (int route = -1; route <= 1; route += 2)
            {
                TrackEvent[] selected = events.Where(e => e.ForkId < 0 || e.RequiredRoute == route).ToArray();
                TrackEvent[] turns = selected.Where(e => e.Kind == ObstacleKind.TurnLeft || e.Kind == ObstacleKind.TurnRight).ToArray();
                for (int i = 0; i < turns.Length; i++)
                {
                    double t = RunSimulation.TimeAtDistance(turns[i].Distance);
                    Require(i == 0 || t - RunSimulation.TimeAtDistance(turns[i - 1].Distance) >= 2.999d, "turn separation");
                    Require(!selected.Any(e => e.Distance > turns[i].Distance
                        && RunSimulation.TimeAtDistance(e.Distance) < t + 1.099d && (IsHazard(e.Kind) || IsContext(e.Kind))),
                        "post-turn safety");
                }
                foreach (TrackEvent fork in selected.Where(e => e.Kind == ObstacleKind.Fork))
                {
                    double t = RunSimulation.TimeAtDistance(fork.Distance);
                    Require(!selected.Any(e => e.Distance < fork.Distance && RunSimulation.TimeAtDistance(e.Distance) > t - 2d
                        && IsHazard(e.Kind) && !Enumerable.Range(0, 3).Any(lane => !selected.Any(h =>
                            IsHazard(h.Kind) && Math.Abs(h.Distance - e.Distance) < .003f && h.Lane == lane))),
                        "forced action before fork");
                    TrackEvent[] payload = selected.Where(e => e.ForkId == fork.Id).ToArray();
                    Require(payload.Any(e => IsHazard(e.Kind)) && payload.Any(e => e.Kind == ObstacleKind.Chip), "empty branch");
                }
                foreach (TrackEvent archive in selected.Where(e => e.Kind == ObstacleKind.Archive))
                {
                    double t = RunSimulation.TimeAtDistance(archive.Distance);
                    Require(archive.ArchiveId >= 0 && archive.ArchiveId < 30 && archive.Lane == 1, "invalid archive");
                    Require(!selected.Any(h => IsHazard(h.Kind) && RunSimulation.TimeAtDistance(h.Distance) < t + .7d
                        && RunSimulation.TimeAtDistance(h.Distance + h.Depth) > t - .7d), "archive path not reserved");
                }
                int lastTemplate = -1, repeats = 0;
                double recipeTime = -10d;
                foreach (TrackEvent e in selected)
                {
                    double t = RunSimulation.TimeAtDistance(e.Distance);
                    // Authored opening crystal trails repeat pickups; variety applies to the obstacle course.
                    if (!endless && contract.Id < 2 && e.Kind == ObstacleKind.Chip && t <= 31d) continue;
                    if (t - recipeTime < .4d) continue; // Lane instances and their trailing pickup are one visual recipe.
                    recipeTime = t;
                    repeats = e.TemplateId == lastTemplate ? repeats + 1 : 1;
                    lastTemplate = e.TemplateId;
                    Require(repeats <= 2, "visual template repeated three times at " + t);
                }
            }
        }

        private static int[] ValidateContent(GameCatalog catalog, List<string> details)
        {
            var executions = new int[37];
            var composites = new HashSet<int>();
            var heightTemplates = new HashSet<int>();
            ObstacleKind[][] secondaryKinds =
            {
                new[] { ObstacleKind.Crate, ObstacleKind.HighBeam },
                new[] { ObstacleKind.MovingGate, ObstacleKind.Gap },
                new[] { ObstacleKind.Drone, ObstacleKind.PhaseWall },
                new[] { ObstacleKind.Collapse, ObstacleKind.PhaseWall }
            };
            foreach (ContractDefinition contract in catalog.Contracts)
            for (int mode = 0; mode < 2; mode++)
            for (int seed = 0; seed < 6; seed++)
            {
                List<TrackEvent> events = Generate(catalog, contract, seed, mode == 1);
                foreach (TrackEvent item in events)
                {
                    TrackTemplateDefinition template = catalog.Templates.Single(t => t.Id == item.TemplateId);
                    if (item.Kind == template.Kind || template.Category == TrackTemplateCategory.Boss && item.BossRecipe)
                        executions[template.Id]++;
                    if (item.Kind == ObstacleKind.Fork)
                    {
                        int left = events.Count(e => e.ForkId == item.Id && e.RequiredRoute == -1 && e.Kind == ObstacleKind.Chip);
                        int right = events.Count(e => e.ForkId == item.Id && e.RequiredRoute == 1 && e.Kind == ObstacleKind.Chip);
                        Require(template.Direction == 0 ? left == right : template.Direction < 0 ? left > right : right > left,
                            "fork label does not select its actual local reward route");
                    }
                    if (item.Kind != ObstacleKind.Rise && item.Kind != ObstacleKind.Fall) continue;
                    double time = RunSimulation.TimeAtDistance(item.Distance);
                    double duration = item.GroundTransitionSeconds;
                    foreach (double fraction in new[] { 0d, .001d, .5d, .999d, 1d })
                    {
                        double expected = item.GroundHeight + (item.GroundTargetHeight - item.GroundHeight)
                            * fraction * fraction * (3d - 2d * fraction);
                        double actual = item.GroundHeightAtDistance(RunSimulation.DistanceAtTime((float)(time + duration * fraction)));
                        Require(Math.Abs(actual - expected) < .00002d, "road sampler disagrees with independent smooth profile");
                    }
                    Require(!events.Any(e => (e.Kind == ObstacleKind.Rise || e.Kind == ObstacleKind.Fall)
                        && e.Distance > item.Distance && RunSimulation.TimeAtDistance(e.Distance) < time + duration),
                        "overlapping road transitions");
                    if (IsHazard(template.Kind))
                    {
                        TrackEvent[] hazards = events.Where(e => e.TemplateId == template.Id && e.Kind == template.Kind
                            && e.Distance >= item.Distance && RunSimulation.TimeAtDistance(e.Distance) < time + duration).ToArray();
                        Require(hazards.Length == 3 && hazards.All(e => e.RequiredAction ==
                            (template.RequiresJump ? GameCommand.Jump : GameCommand.Slide)),
                            "height recipe lacks actual three-lane clearance geometry");
                        if (template.RequiresJump)
                        {
                            double jumpTime = RunSimulation.TimeAtDistance(hazards[0].Distance);
                            Require(!events.Any(e => e.RequiredAction == GameCommand.Slide
                                && RunSimulation.TimeAtDistance(e.Distance) > jumpTime
                                && RunSimulation.TimeAtDistance(e.Distance) < jumpTime + 1.1d),
                                "height jump has no slide recovery");
                        }
                    }
                    heightTemplates.Add(template.Id);
                }
                foreach (IGrouping<float, TrackEvent> recipe in events.Where(e => e.TemplateId >= 30 && e.TemplateId <= 33
                    && IsHazard(e.Kind)).GroupBy(e => e.Distance))
                {
                    int id = recipe.First().TemplateId;
                    Require(secondaryKinds[id - 30].All(kind => recipe.Any(e => e.Kind == kind))
                        && recipe.Select(e => e.Lane).Distinct().Count() == 2 && recipe.All(e => e.CombinationBudget >= 2),
                        "advanced catalog recipe is not real mixed geometry with an open lane");
                    composites.Add(id);
                }
            }
            Check(executions.All(n => n > 0), "37/37 catalog recipes have semantic event coverage, not just template labels", details);
            Check(composites.SetEquals(new[] { 30, 31, 32, 33 }), "all four advanced obstacle recipes contain real mixed hazards", details);
            Check(heightTemplates.SetEquals(new[] { 17, 18, 19, 20, 21 }),
                "five real height recipes: smooth bounded persistent profiles and recovered jump/slide geometry", details);
            Check(true, "balanced, left-reward and right-reward forks change actual local pickup routes", details);
            return executions;
        }

        private static void ValidateCatalog(GameCatalog catalog, List<string> details)
        {
            Check(catalog.Contracts.Length == 18 && catalog.Archives.Length == 30 && catalog.Outfits.Length == 8, "catalog counts", details);
            int[] counts = { 10, 4, 3, 5, 12, 3 };
            Check(Enumerable.Range(0, 6).All(i => catalog.Templates.Count(t => (int)t.Category == i) == counts[i]),
                "37 template category counts", details);
            Check(catalog.Contracts.Sum(c => c.FirstClearReward) == 3600 && catalog.Outfits.Skip(1).Sum(o => o.Cost) == 6700,
                "unchanged 3600/6700 economy", details);
            Check(catalog.Contracts.All(c => c.DurationSeconds >= (c.Id == 0 ? 60f : 180f) && c.DurationSeconds <= 420f
                && Math.Abs(RunSimulation.TimeAtDistance(c.Length) - c.DurationSeconds) < .001d),
                "analytic 60-second opening and 180..420-second contract lengths", details);
            Check(catalog.Contracts.Where(c => c.Boss).Select(c => c.Id).SequenceEqual(new[] { 5, 11, 17 }), "three bosses", details);
            Check(catalog.Contracts.All(c => !string.IsNullOrWhiteSpace(c.NameZh) && !string.IsNullOrWhiteSpace(c.NameEn)
                && !string.IsNullOrWhiteSpace(c.DescriptionZh) && !string.IsNullOrWhiteSpace(c.DescriptionEn))
                && catalog.Archives.All(a => !string.IsNullOrWhiteSpace(a.TextZh) && !string.IsNullOrWhiteSpace(a.TextEn)),
                "bilingual content", details);
            Check(Math.Abs(RunSimulation.DistanceAtTime(480f) - 4034.375f) < .001f
                && Math.Abs(RunSimulation.TimeAtDistance(4034.375f) - 480d) < .001d, "capped analytic distance/inverse", details);
        }

        public static string RunRegressions()
        {
            var details = new List<string>();
            ValidateRegressions(details);
            return string.Join("\n", details);
        }

        private static void ValidateRegressions(List<string> details)
        {
            foreach (ObstacleKind kind in new[] { ObstacleKind.TurnLeft, ObstacleKind.Fork })
            foreach (bool correct in new[] { true, false })
            {
                var run = Fixture();
                var item = new TrackEvent { Id = 900, Kind = kind, Distance = 8f, Lane = 1, Branch = -1 };
                run.Events.Add(item);
                GameCommand first = correct ? GameCommand.Left : GameCommand.Right;
                run.Handle(first);
                run.AutoTurn = true;
                run.Handle(correct ? GameCommand.Right : GameCommand.Left);
                run.Tick(1.3f);
                Check(item.SelectedBranch == (correct ? -1 : 1) && run.Lane == 1
                    && (kind == ObstacleKind.Fork || (run.Phase == RunPhase.Running) == correct),
                    kind + " immutable " + first + " then opposite/auto", details);
            }
            foreach (float time in new[] { .795f, .8f, .805f })
            foreach (bool partitioned in new[] { false, true })
            {
                var run = Fixture();
                run.AddPhaseEnergy(1f);
                run.Handle(GameCommand.Phase);
                run.AddPhaseEnergy(1f);
                run.Handle(GameCommand.Phase);
                run.Events.Add(Hazard(time, ObstacleKind.PhaseWall));
                Advance(run, .82f, partitioned);
                Check((run.Health == RunSimulation.MaxHealth) == (time < .8f)
                    && run.PhaseUses == 1 && run.PhaseEnergy == 0f && run.Multiplier >= 1f,
                    "phase expiry t=" + time + " partition=" + partitioned, details);
            }
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.LowBarrier, ObstacleKind.HighBeam,
                ObstacleKind.Drone, ObstacleKind.PhaseWall, ObstacleKind.Gap, ObstacleKind.Collapse, ObstacleKind.MovingGate })
            {
                var run = Fixture();
                run.AddPhaseEnergy(1f);
                run.Handle(GameCommand.Phase);
                run.Events.Add(Hazard(.4f, kind, .6f));
                run.Tick(.65f);
                bool phaseable = kind != ObstacleKind.Gap && kind != ObstacleKind.Collapse && kind != ObstacleKind.MovingGate;
                Check((run.Health == RunSimulation.MaxHealth) == phaseable, "phase semantic " + kind, details);
            }
            foreach (bool partitioned in new[] { false, true })
            {
                var run = Fixture();
                run.AddPhaseEnergy(1f);
                run.Handle(GameCommand.Phase);
                run.Events.Add(Hazard(.7f, ObstacleKind.PhaseWall, 2f));
                Advance(run, 1f, partitioned);
                Check(run.Health == 2 && Math.Abs(run.LastDamageTime - .8f) < .002f,
                    "deep wall remains occupied at expiry partition=" + partitioned, details);
                run = Fixture();
                run.Handle(GameCommand.Jump);
                run.Events.Add(Hazard(.7f, ObstacleKind.Gap, 2f));
                Advance(run, 1.1f, partitioned);
                Check(run.Phase == RunPhase.Failed, "gap clearance required through landing partition=" + partitioned, details);
                run = Fixture();
                run.Handle(GameCommand.Slide);
                run.Events.Add(Hazard(.7f, ObstacleKind.HighBeam, 1f));
                Advance(run, .9f, partitioned);
                Check(run.Health == 2 && Math.Abs(run.LastDamageTime - .75f) < .002f,
                    "slide expiry inside beam partition=" + partitioned, details);
            }
            foreach (float fraction in new[] { 0f, .25f, .5f, .75f, 1f })
            {
                var run = Fixture();
                run.Handle(GameCommand.Left);
                float t = Math.Max(.0001f, .32f * fraction);
                run.Events.Add(Hazard(t, ObstacleKind.Collapse));
                run.Tick(.4f);
                Check((run.Phase == RunPhase.Failed) == (fraction < .5f), "physical lane fraction=" + fraction, details);
            }
            foreach (float time in new[] { .745f, .75f, .755f })
            foreach (bool partitioned in new[] { false, true })
            {
                var run = Fixture();
                run.Handle(GameCommand.Slide);
                run.Events.Add(Hazard(time, ObstacleKind.HighBeam));
                Advance(run, .77f, partitioned);
                Check((run.Health == RunSimulation.MaxHealth) == (time < .75f),
                    "slide crossing expiry t=" + time + " partition=" + partitioned, details);
            }
            {
                var states = new List<RunSimulation>();
                foreach (bool partitioned in new[] { false, true })
                {
                    var run = Fixture();
                    run.Handle(GameCommand.Jump);
                    run.Handle(GameCommand.Left);
                    var obstacle = Hazard(.25f, ObstacleKind.LowBarrier, .6f);
                    obstacle.Lane = 0;
                    run.Events.Add(obstacle);
                    run.Events.Add(new TrackEvent { Id = 9901, Kind = ObstacleKind.Chip,
                        Distance = RunSimulation.DistanceAtTime(.4f), Lane = 0 });
                    Advance(run, .6f, partitioned);
                    states.Add(run);
                }
                Check(states.All(s => s.Phase == RunPhase.Running && s.Chips == 1)
                    && Math.Abs(states[0].Height - states[1].Height) < .00001f
                    && Math.Abs(states[0].LaneX - states[1].LaneX) < .00001f
                    && states[0].Score == states[1].Score && states[0].NearMisses == states[1].NearMisses,
                    "combined jump/lane/occupied hazard/pickup/score is dt-partition invariant", details);
            }
            {
                var run = Fixture();
                run.Handle(GameCommand.Jump);
                run.Tick(.7f);
                run.Handle(GameCommand.Jump);
                run.Tick(.3f);
                Check(run.Height > .6f, "buffered jump survives exact landing", details);
                run = Fixture();
                run.Handle(GameCommand.Jump);
                run.Tick(.2f);
                run.Handle(GameCommand.Slide);
                run.Tick(.15f);
                Check(run.Height == 0f && run.LastHint == "fast_fall", "airborne slide fast-falls without teleport", details);
                run = Fixture();
                run.Handle(GameCommand.Left);
                run.Handle(GameCommand.Jump);
                run.AddPhaseEnergy(1f);
                run.Handle(GameCommand.Phase);
                run.Tick(.1f);
                float elapsed = run.Elapsed, distance = run.Distance, height = run.Height, x = run.LaneX, phase = run.PhaseRemaining;
                int score = run.Score;
                run.Pause();
                run.Tick(100f);
                run.Handle(GameCommand.Right);
                run.Resume();
                run.Tick(2f);
                Check(run.Elapsed == elapsed && run.Distance == distance && run.Height == height && run.LaneX == x
                    && run.PhaseRemaining == phase && run.Score == score && run.Phase == RunPhase.Countdown,
                    "pause and resume countdown freeze every timer/position/score", details);
                run.Tick(1f);
                Check(run.Phase == RunPhase.Running && run.Elapsed == elapsed, "resume needs three non-advancing seconds", details);
            }
            ValidateSaveRecovery(details);
            GameCatalog catalog = GameCatalog.CreateDefault();
            Witness evidence;
            RunSimulation recovered = ExecuteWitness(catalog, catalog.Contracts[0], 1101, false, -1, out evidence, beginCommandsAt: 4f);
            Check(recovered.TutorialComplete && recovered.TutorialRecoveryCount > 0, "missed tutorial lessons recover via repeated real commands", details);
            recovered = ExecuteWitness(catalog, catalog.Contracts[1], 1102, false, -1, out evidence, beginCommandsAt: 7f);
            Check(recovered.TutorialComplete && recovered.PhaseUses > 0 && recovered.TutorialRecoveryCount > 0,
                "missed fixed-charge phase lesson recovers via actual phase command", details);
            var idle = new RunSimulation(catalog);
            idle.Start(catalog.Contracts[0], 1101, false);
            idle.Tick(3f);
            idle.Tick(13f);
            Check(idle.Phase == RunPhase.Failed && idle.Health == 0 && idle.DamageCount == 3
                && !idle.TutorialComplete && idle.TutorialRecoveryCount > 0 && !idle.CreateResult().Victory,
                "idle tutorial loses visible lives and fails on its third collision", details);
            ValidateSurvival(details);
        }

        private static void ValidateHandGestures(List<string> details)
        {
            var recognizer = new RiftlineHandGestureRecognizer(HandGestureThresholds.Default);
            recognizer.Reset(0f);
            Require(!recognizer.IsReady, "recognizer was ready before Neutral preparation");
            PrimeNeutral(recognizer);
            Check(recognizer.IsReady, "recognizer reports ready after stable Neutral Open", details);
            HandGestureSignal armed = recognizer.Update(HandSample(.1f, .8f));
            Require(armed.State == HandGestureState.Armed, "Pinch did not arm");
            HandGestureSignal fired = recognizer.Update(HandSample(.18f, .9f, .061f));
            Check(fired.Command == GameCommand.Right && fired.State == HandGestureState.Latched,
                "Pinch right displacement emits one command", details);
            HandGestureSignal duplicate = recognizer.Update(HandSample(.2f, .9f, .09f));
            Require(duplicate.Command == GameCommand.None, "Latched Pinch fired twice");
            Check(RecognizeDirection(new Vector2(-.061f, 0f)) == GameCommand.Left &&
                  RecognizeDirection(new Vector2(0f, .061f)) == GameCommand.Jump &&
                  RecognizeDirection(new Vector2(0f, -.061f)) == GameCommand.Slide,
                "camera-plane axes map to left jump and slide", details);
            recognizer = new RiftlineHandGestureRecognizer(HandGestureThresholds.Default);
            recognizer.Reset(0f);
            PrimeNeutral(recognizer);
            recognizer.Update(HandSample(.1f, .8f));
            HandGestureSignal hysteresis = recognizer.Update(HandSample(.14f, .5f, .02f));
            HandGestureSignal diagonal = recognizer.Update(HandSample(.16f, .9f, .05f, .05f));
            Check(hysteresis.State == HandGestureState.Armed && diagonal.Command == GameCommand.None,
                "Pinch hysteresis and axis dominance reject unstable input", details);
            HandGestureSignal expired = recognizer.Update(HandSample(.7f, .9f, .03f));
            Check(expired.State == HandGestureState.Neutral && expired.CandidateCancelled,
                "direction candidate expires after the direction window", details);
            recognizer = new RiftlineHandGestureRecognizer(HandGestureThresholds.Default);
            recognizer.Reset(0f);
            PrimeNeutral(recognizer);
            recognizer.Update(HandSample(.1f, .8f));
            recognizer.Update(HandSample(.15f, .2f));
            HandGestureSignal earlyRearm = recognizer.Update(HandSample(.2f, .8f));
            recognizer.Update(HandSample(.21f, .2f));
            HandGestureSignal cooledRearm = recognizer.Update(HandSample(.24f, .8f));
            Check(earlyRearm.State == HandGestureState.Neutral && cooledRearm.State == HandGestureState.Armed,
                "release cooldown blocks immediate rearm", details);
            recognizer = new RiftlineHandGestureRecognizer(HandGestureThresholds.Default);
            recognizer.Reset(0f);
            HandGestureSignal unprepared = recognizer.Update(HandSample(.1f, .8f));
            Check(unprepared.State == HandGestureState.Neutral && unprepared.Command == GameCommand.None,
                "Pinch requires a stable low-pinch neutral period", details);
            recognizer = new RiftlineHandGestureRecognizer(HandGestureThresholds.Default);
            recognizer.Reset(0f);
            recognizer.Update(HandSample(.01f, .05f, open: false));
            recognizer.Update(HandSample(.09f, .05f, open: false));
            Check(recognizer.IsReady,
                "relaxed half-closed hand prepares neutral without a fully open pose", details);
            Check(RecognizeDirection(new Vector2(.036f, 0f)) == GameCommand.Right &&
                  RecognizeDirection(new Vector2(0f, -.036f)) == GameCommand.Slide,
                "shorter camera-plane travel still resolves a lane command", details);
            recognizer = new RiftlineHandGestureRecognizer(HandGestureThresholds.Default);
            recognizer.Reset(0f);
            PrimeNeutral(recognizer);
            HandGestureSignal phaseStarted = recognizer.Update(HandSample(.1f, 0f, fist: true, open: false));
            HandGestureSignal phaseWaiting = recognizer.Update(HandSample(.27f, 0f, fist: true, open: false));
            HandGestureSignal phaseFired = recognizer.Update(HandSample(.29f, 0f, fist: true, open: false));
            HandGestureSignal phaseDuplicate = recognizer.Update(HandSample(.4f, 0f, fist: true, open: false));
            Check(phaseStarted.State == HandGestureState.PhaseHolding &&
                  phaseWaiting.Command == GameCommand.None && phaseWaiting.HoldProgress > .9f &&
                  phaseFired.Command == GameCommand.Phase && phaseFired.State == HandGestureState.Latched &&
                  phaseDuplicate.Command == GameCommand.None,
                "Open-to-Fist hold emits one Phase command", details);
            recognizer = new RiftlineHandGestureRecognizer(HandGestureThresholds.Default);
            recognizer.Reset(0f);
            PrimeNeutral(recognizer);
            HandGestureSignal pauseStarted = recognizer.Update(HandSample(.1f, 0f, pausePose: true));
            HandGestureSignal pauseWaiting = recognizer.Update(HandSample(.54f, 0f, pausePose: true));
            HandGestureSignal pauseFired = recognizer.Update(HandSample(.56f, 0f, pausePose: true));
            HandGestureSignal pauseDuplicate = recognizer.Update(HandSample(.7f, 0f, pausePose: true));
            Check(pauseStarted.State == HandGestureState.PauseHolding &&
                  pauseWaiting.Command == GameCommand.None && pauseWaiting.HoldProgress > .95f &&
                  pauseFired.Command == GameCommand.Pause && pauseFired.State == HandGestureState.Latched &&
                  pauseDuplicate.Command == GameCommand.None,
                "support Palm hold emits one Pause command", details);
            recognizer = new RiftlineHandGestureRecognizer(HandGestureThresholds.Default);
            recognizer.Reset(0f);
            PrimeNeutral(recognizer);
            recognizer.Update(HandSample(.1f, .8f));
            HandGestureSignal shortLoss = recognizer.Update(HandSample(.15f, .9f,
                dominantTracked: false));
            HandGestureSignal cancelledLoss = recognizer.Update(HandSample(.26f, .9f,
                dominantTracked: false));
            HandGestureSignal autoPause = recognizer.Update(HandSample(.46f, .9f,
                dominantTracked: false));
            Check(shortLoss.State == HandGestureState.Armed && !shortLoss.CandidateCancelled &&
                  cancelledLoss.State == HandGestureState.Neutral && cancelledLoss.CandidateCancelled &&
                  autoPause.RequestAutoPause,
                "tracking loss cancels at 100 ms and auto-pauses at 300 ms", details);
            HandGestureSignal unsafeReacquire = recognizer.Update(HandSample(.47f, .9f, .1f));
            recognizer.Update(HandSample(.5f, 0f));
            recognizer.Update(HandSample(.8f, 0f));
            HandGestureSignal recovered = recognizer.Update(HandSample(.81f, .8f));
            Check(unsafeReacquire.Command == GameCommand.None &&
                  unsafeReacquire.State == HandGestureState.Neutral &&
                  recovered.State == HandGestureState.Armed,
                "tracking reacquisition requires 300 ms Open and never replays input", details);
            recognizer = new RiftlineHandGestureRecognizer(HandGestureThresholds.Default);
            recognizer.Reset(0f);
            PrimeNeutral(recognizer);
            recognizer.Update(HandSample(.1f, .8f));
            recognizer.Update(HandSample(.15f, .9f, dominantTracked: false));
            recognizer.Update(HandSample(.2f, .9f));
            HandGestureSignal secondShortLoss = recognizer.Update(HandSample(.3f, .9f,
                dominantTracked: false));
            Check(secondShortLoss.State == HandGestureState.Armed && !secondShortLoss.CandidateCancelled,
                "separate short tracking losses do not accumulate", details);
            recognizer = new RiftlineHandGestureRecognizer(HandGestureThresholds.Default);
            recognizer.Reset(0f);
            recognizer.Update(HandSample(.01f, 0f, commands: false));
            recognizer.Update(HandSample(.09f, 0f, commands: false));
            Check(recognizer.IsReady, "menu observation prepares Neutral without gameplay commands", details);
        }

        private static void ValidateHandControlAssets(List<string> details)
        {
            var defaults = new GameSettings();
            Check(!defaults.HandTutorialComplete &&
                  Mathf.Approximately(defaults.HandDirectionDistance, .035f),
                "new saves default to pending hand onboarding at 3.5 cm", details);
            GameSettings cloned = new SaveData { Settings = defaults }.Clone().Settings;
            Check(!cloned.HandTutorialComplete &&
                  Mathf.Approximately(cloned.HandDirectionDistance, .035f),
                "hand onboarding settings survive save cloning", details);
            string legacyPath = Path.Combine("/tmp/riftline-hand-controls",
                "legacy-hand-settings-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new GameSaveStore(legacyPath);
                var legacy = new SaveData();
                legacy.Settings.HandDirectionDistance = 0f;
                store.Save(legacy);
                Check(Mathf.Approximately(store.Load().Settings.HandDirectionDistance, .035f),
                    "legacy hand calibration normalizes to the relaxed baseline", details);
            }
            finally
            {
                if (Directory.Exists(legacyPath)) Directory.Delete(legacyPath, true);
            }
            RiftlineHandControlSettings settings =
                Resources.Load<RiftlineHandControlSettings>("RiftlineHandControlSettings");
            Check(settings != null, "production hand control settings load from Resources", details);
            Check(settings.OpenShape != null && settings.FistShape != null && settings.PausePose != null,
                "production Open Fist and Palm gesture assets are assigned", details);
            string settingsPath = AssetDatabase.GetAssetPath(settings);
            Check(File.ReadAllText(settingsPath).Contains("DirectionDistance"),
                "production hand thresholds are serialized in the asset", details);
            HandGestureThresholds thresholds = settings.Thresholds;
            Check(Mathf.Approximately(thresholds.DirectionDistance, .035f) &&
                  Mathf.Approximately(thresholds.PinchPress, .7f) &&
                  Mathf.Approximately(thresholds.PinchRelease, .25f) &&
                  Mathf.Approximately(thresholds.PhaseHold, .18f) &&
                  Mathf.Approximately(thresholds.PauseHold, .45f),
                "production hand thresholds match the official pinch hysteresis", details);
        }

        private static void ValidateHandRuntimeAdapter(List<string> details)
        {
            Check(typeof(RiftlineGame).GetProperty("HandInputReady") != null,
                "RiftlineGame exposes hand input readiness", details);
            Check(typeof(RiftlineUIPointer).GetMethod("UpdatePointer") != null &&
                  typeof(RiftlineUIPointer).GetMethod("Cancel") != null,
                "shared UI pointer exposes update and cancel ownership", details);
            Check(typeof(RiftlineHandFeedback).GetMethod("Present") != null,
                "hand feedback exposes semantic gesture presentation", details);
            Pose head = new Pose(Vector3.up, Quaternion.identity);
            Pose eye = new Pose(Vector3.right, Quaternion.Euler(0f, 90f, 0f));
            Check(Vector3.Distance(RiftlineHandInput.SelectGazePose(true, eye, head).position, Vector3.right) < .0001f,
                "valid eye gaze drives the menu ray", details);
            Check(Vector3.Distance(RiftlineHandInput.SelectGazePose(false, eye, head).position, Vector3.up) < .0001f,
                "missing eye tracking falls back to head gaze instead of dropping the ray", details);
            Check(Vector3.Angle(RiftlineHandInput.SelectGazePose(true, eye, head).rotation * Vector3.forward,
                    Quaternion.Euler(0f, 90f, 0f) * Vector3.forward) < .5f,
                "eye gaze keeps its own direction and is not bent by pinch", details);
            Check(typeof(RiftlineHandOnboarding).GetMethod("Begin") != null,
                "hand onboarding exposes an explicit start entry", details);
            Check(RiftlineHandInput.SelectGestureHand(false, RiftlineHandInput.GestureHand.None,
                    false, 0f, false, true, .8f, false, .7f) == RiftlineHandInput.GestureHand.Right,
                "right hand pinch can drive gestures without a fixed dominant hand", details);
            Check(RiftlineHandInput.SelectGestureHand(false, RiftlineHandInput.GestureHand.None,
                    true, .8f, false, false, 0f, false, .7f) == RiftlineHandInput.GestureHand.Left,
                "left hand pinch can drive gestures without a fixed dominant hand", details);
            Check(RiftlineHandInput.SelectGestureHand(false, RiftlineHandInput.GestureHand.None,
                    true, 0f, true, true, 0f, false, .7f) == RiftlineHandInput.GestureHand.Left,
                "either hand fist can drive phase", details);
            Check(RiftlineHandInput.SelectGestureHand(true, RiftlineHandInput.GestureHand.Right,
                    true, .9f, false, true, .8f, false, .7f) == RiftlineHandInput.GestureHand.Right,
                "an in-progress gesture keeps its hand instead of switching mid-motion", details);
            var viewObject = new GameObject("Hand Projection Validation");
            try
            {
                viewObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                Vector2 projected = RiftlineHandInput.ProjectToView(
                    new Vector3(3f, 2f, -1f), viewObject.transform);
                Check(Vector2.Distance(projected, new Vector2(1f, 2f)) < .0001f,
                    "hand Pinch projects onto the HMD camera plane", details);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
            }
        }

        private static GameCommand RecognizeDirection(Vector2 displacement)
        {
            var recognizer = new RiftlineHandGestureRecognizer(HandGestureThresholds.Default);
            recognizer.Reset(0f);
            PrimeNeutral(recognizer);
            recognizer.Update(HandSample(.1f, .8f));
            return recognizer.Update(HandSample(.18f, .9f, displacement.x, displacement.y)).Command;
        }

        private static void PrimeNeutral(RiftlineHandGestureRecognizer recognizer)
        {
            recognizer.Update(HandSample(.01f, 0f));
            recognizer.Update(HandSample(.09f, 0f));
        }

        private static HandGestureSample HandSample(float time, float pinch, float viewX = 0f, float viewY = 0f,
            bool fist = false, bool? open = null, bool pausePose = false,
            bool dominantTracked = true, bool supportTracked = true, bool gameplay = true, bool commands = true)
        {
            return new HandGestureSample
            {
                Time = time,
                GameplayEnabled = gameplay,
                CommandsEnabled = commands,
                DominantTracked = dominantTracked,
                SupportTracked = supportTracked,
                DominantOpen = open ?? pinch < .35f,
                DominantFist = fist,
                SupportPausePose = pausePose,
                Pinch = pinch,
                PinchViewPosition = new Vector2(viewX, viewY)
            };
        }

        public static string RunSurvivalRegressions()
        {
            var details = new List<string>();
            ValidateSurvival(details);
            return string.Join("\n", details);
        }

        private static void ValidateSurvival(List<string> details)
        {
            foreach (bool partitioned in new[] { false, true })
            {
                var run = Fixture();
                int damageFeedback = 0;
                run.Feedback += kind => { if (kind == FeedbackKind.Damage) damageFeedback++; };
                var first = Hazard(.5f, ObstacleKind.Crate, 20f);
                run.Events.Add(first);
                Advance(run, 2f, partitioned);
                Check(run.Phase == RunPhase.Running && run.Health == 2 && run.DamageCount == 1
                    && damageFeedback == 1 && first.HitRecorded && !first.NearMissAwarded
                    && Math.Abs(run.LastDamageTime - .5f) < .00001f,
                    "one occupied crate removes exactly one life and never awards a near miss partition=" + partitioned, details);
                run.Events.Add(Hazard(2.1f, ObstacleKind.HighBeam));
                run.Events.Add(Hazard(3.5f, ObstacleKind.LowBarrier));
                Advance(run, 2f, partitioned);
                Check(run.Phase == RunPhase.Failed && run.Health == 0 && run.DamageCount == 3 && damageFeedback == 3,
                    "third independent collision fails with exactly three damage signals partition=" + partitioned, details);

                run = Fixture();
                run.Events.Add(Hazard(.5f, ObstacleKind.Crate));
                var protectedHit = Hazard(1f, ObstacleKind.HighBeam, 20f);
                protectedHit.Id++;
                run.Events.Add(protectedHit);
                var expiryHit = Hazard(1.75f, ObstacleKind.LowBarrier);
                expiryHit.Id += 2;
                run.Events.Add(expiryHit);
                Advance(run, 2f, partitioned);
                Check(run.Health == 1 && run.DamageCount == 2 && protectedHit.HitRecorded
                    && Math.Abs(run.LastDamageTime - 1.75f) < .00001f,
                    "protection suppresses overlap and expires at exact analytic time partition=" + partitioned, details);
            }
            foreach (ObstacleKind kind in new[] { ObstacleKind.Gap, ObstacleKind.Collapse })
            {
                var run = Fixture();
                run.Events.Add(Hazard(.1f, ObstacleKind.Crate));
                run.Events.Add(Hazard(.2f, kind));
                run.Tick(.3f);
                Check(run.Phase == RunPhase.Failed && run.Health == 0,
                    "terminal terrain bypasses remaining lives and protection: " + kind, details);
            }
            foreach (bool wrongDirection in new[] { false, true })
            {
                var run = Fixture();
                run.Events.Add(new TrackEvent { Id = 9910, Kind = ObstacleKind.TurnRight, Branch = 1, Lane = 1,
                    Distance = RunSimulation.DistanceAtTime(.5f), TutorialLesson = 4, RequiredAction = GameCommand.Right });
                if (wrongDirection) run.Handle(GameCommand.Left);
                run.Tick(.6f);
                Check(run.Phase == RunPhase.Failed && run.Heading == 0 &&
                    run.FailureReason == (wrongDirection ? "wrong_turn" : "missed_turn"),
                    "tutorial turn remains terminal when " + (wrongDirection ? "wrong" : "missed"), details);
            }
            {
                var run = Fixture();
                run.Events.Add(Hazard(.1f, ObstacleKind.Crate));
                run.Tick(.2f);
                float protection = run.InvulnerableRemaining;
                run.Pause();
                run.Tick(200f);
                run.Resume();
                run.Tick(2f);
                Check(run.Health == 2 && run.InvulnerableRemaining == protection && run.Elapsed == .2f,
                    "pause and resume countdown freeze life protection", details);
                run.Start(GameCatalog.CreateDefault().Contracts[0], 1101, false);
                Check(run.Health == 3 && run.DamageCount == 0 && run.ChipStreak == 0
                    && run.InvulnerableRemaining == 0 && run.CountdownRemaining == 1f,
                    "retry resets all survival state and opens after one second", details);
            }
            {
                var run = Fixture();
                int combos = 0;
                run.Feedback += kind => { if (kind == FeedbackKind.Combo) combos++; };
                for (int i = 0; i < 20; i++)
                    run.Events.Add(new TrackEvent { Id = i, Kind = ObstacleKind.Chip, Lane = 1,
                        Distance = RunSimulation.DistanceAtTime(.1f + i * .08f) });
                run.Tick(1.7f);
                Check(run.Chips == 20 && run.ChipStreak == 20 && run.BestChipStreak == 20
                    && combos == 3 && run.LastStreakReward == 20 && run.Multiplier > 1f && run.PhaseEnergy > .5f,
                    "5/10/20 collection chains produce three real charge/multiplier rewards", details);
                run.Events.Add(new TrackEvent { Id = 100, Kind = ObstacleKind.Chip, Lane = 0,
                    Distance = RunSimulation.DistanceAtTime(1.8f) });
                run.Tick(.2f);
                Check(run.ChipStreak == 0 && run.BestChipStreak == 20, "missed chip breaks current chain and preserves best", details);
                run.Events.Add(Hazard(2f, ObstacleKind.Crate));
                run.Tick(.2f);
                Check(run.Multiplier == 1f && run.ChipStreak == 0 && run.Health == 2, "damage resets multiplier and chain", details);
            }
            {
                var catalog = GameCatalog.CreateDefault();
                var run = new RunSimulation(catalog);
                run.Start(catalog.Contracts[0], 1101, false);
                Check(catalog.Contracts[0].DurationSeconds == 60f
                    && run.Events.Any(e => e.Kind == ObstacleKind.Chip && RunSimulation.TimeAtDistance(e.Distance) < 1.21d)
                    && run.Events.Count(e => e.Kind == ObstacleKind.Chip && RunSimulation.TimeAtDistance(e.Distance) < 10d) >= 15,
                    "first minute has immediate collectible trails", details);
                run.Tick(1f);
                run.Tick(34f);
                // Pure generation check uses an independent diagnostic generator, not a survival witness.
                var events = new List<TrackEvent>();
                new TrackGenerator(catalog, catalog.Contracts[0], 1101, false).GenerateUntil(catalog.Contracts[0].Length, events);
                Check(events.Any(e => e.PhaseCharge && RunSimulation.TimeAtDistance(e.Distance) < 28d)
                    && events.Any(e => e.Kind == ObstacleKind.PhaseWall && RunSimulation.TimeAtDistance(e.Distance) < 31d)
                    && events.Any(e => e.Kind == ObstacleKind.Archive),
                    "opening teaches phase and offers an actual archive branch before completion", details);
            }
        }

        private static void ValidateSaveRecovery(List<string> details)
        {
            string path = Path.Combine("/tmp/riftline-content-staged/Tests", "save-regression-" + Guid.NewGuid().ToString("N"));
            try
            {
                File.WriteAllText(path, "blocked storage");
                var catalog = GameCatalog.CreateDefault();
                var store = new GameSaveStore(path);
                var progress = new GameProgress(catalog, new SaveData(), store);
                Witness evidence;
                RunSimulation run = ExecuteWitness(catalog, catalog.Contracts[0], 1101, false, -1, out evidence, progress);
                Require(progress.HasPendingSave && progress.Save.Archives[0], "actual archive pickup not pending");
                RunResult first;
                Check(!progress.TrySettleRun(run, out first) && first.PersistencePending && progress.Save.Revision == 0,
                    "failed actual settlement stays pending without revision advance", details);
                int balance = progress.Save.Chips;
                RunResult retry;
                progress.TrySettleRun(run, out retry);
                Check(ReferenceEquals(first, retry) && progress.Save.Chips == balance, "pending retry never pays twice", details);
                File.Delete(path);
                Check(progress.TrySettleRun(run, out retry) && !first.PersistencePending && progress.Save.Revision == 1,
                    "restored storage atomically saves archive/reward/settlement once", details);
                progress = new GameProgress(catalog, store.Load(), store);
                Check(progress.TrySettleRun(run, out retry) && retry.AlreadySettled && progress.Save.Chips == balance,
                    "fresh progress instance rejects durable duplicate settlement", details);
                foreach (int id in new[] { 1, 2 })
                {
                    run = ExecuteWitness(catalog, catalog.Contracts[id], catalog.Contracts[id].Seed, false, -1, out evidence, progress);
                    Require(progress.TrySettleRun(run, out retry), "earned purchase setup did not settle");
                }
                balance = progress.Save.Chips;
                int revision = progress.Save.Revision;
                string hidden = path + "-saved";
                Directory.Move(path, hidden);
                File.WriteAllText(path, "blocked purchase");
                try
                {
                    Require(balance >= catalog.Outfits[1].Cost, "fixture lacks earned chips");
                    Check(!progress.BuyOutfit(1) && progress.HasPendingSave && progress.Save.Revision == revision,
                        "failed outfit purchase exposes pending state", details);
                    int afterCharge = progress.Save.Chips;
                    Require(!progress.BuyOutfit(1) && progress.Save.Chips == afterCharge, "purchase retry charged twice");
                    File.Delete(path);
                    Directory.Move(hidden, path);
                    Check(progress.BuyOutfit(1) && store.Load().OwnedOutfits[1] && store.Load().Chips == afterCharge,
                        "purchase retry persists exactly one charge", details);
                }
                finally
                {
                    if (File.Exists(path)) File.Delete(path);
                    if (Directory.Exists(hidden) && !Directory.Exists(path)) Directory.Move(hidden, path);
                }
                SaveData save = store.Load();
                int[] expected = { 111, 222, 333 };
                foreach (int chips in expected) { save.Chips = chips; store.Save(save); }
                string[] slots = Directory.GetFiles(path, "save-*.json");
                Require(slots.Length == 3, "wrong rotating slot count");
                string[] corruptSlots = new[] { 333, 222 }.Select(chips => slots.First(s => JsonUtility.FromJson<SaveData>(
                    JsonUtility.FromJson<SlotProbe>(File.ReadAllText(s)).Payload).Chips == chips)).ToArray();
                foreach (string slot in corruptSlots) File.WriteAllText(slot, "{corrupt");
                Check(store.Load().Chips == 111, "two corrupted newest slots fall back to remaining valid revision", details);
                save = new SaveData { ContractStars = null, OwnedOutfits = null, Archives = null, Settings = null };
                store.Save(save);
                Check(store.Load().ContractStars.Length == 18 && store.Load().Archives.Length == 30
                    && store.Load().OwnedOutfits[0] && save.ContractStars == null, "legacy null arrays normalize without mutating source", details);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
        }

        private static void ValidateProgression(GameCatalog catalog, List<string> details)
        {
            string path = Path.Combine("/tmp/riftline-content-staged/Tests", "progression-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new GameSaveStore(path);
                var progress = new GameProgress(catalog, store.Load(), store);
                int rewards = 0, pickups = 0;
                foreach (ContractDefinition contract in catalog.Contracts)
                {
                    Require(progress.IsContractUnlocked(contract.Id), "contract not unlocked by real preceding clears");
                    Witness evidence;
                    RunSimulation run = ExecuteWitness(catalog, contract, contract.Seed, false, -1, out evidence, progress);
                    RunResult result;
                    Require(progress.TrySettleRun(run, out result) && result.Stars == 3, "clear not durable/three-star");
                    rewards += result.FirstClearReward;
                    pickups += result.ChipsEarned;
                    progress = new GameProgress(catalog, store.Load(), store);
                    if (contract.Id == 3 || contract.Id == 9)
                        Require(!progress.IsRegionUnlocked(contract.Region + 1), "four clears unlocked next region");
                    if (contract.Id == 4 || contract.Id == 10)
                        Require(progress.IsRegionUnlocked(contract.Region + 1), "five clears did not unlock next region");
                    if (contract.Id == 4) Require(!progress.IsEndlessUnlocked, "endless unlocked before contract 5");
                    if (contract.Id == 5) Require(progress.IsEndlessUnlocked, "endless not unlocked by contract 5");
                    int balance = progress.Save.Chips;
                    Require(progress.TrySettleRun(run, out result) && result.AlreadySettled && progress.Save.Chips == balance,
                        "duplicate after restart paid again");
                }
                Check(rewards == 3600 && progress.Save.ContractStars.Sum() == 54 && progress.Save.Chips == 3600 + pickups,
                    "real production progression: 54 stars, 3600 first-clear reward, earned pickups, reloaded gates", details);
                foreach (ContractDefinition contract in catalog.Contracts)
                {
                    Witness evidence;
                    RunSimulation run = ExecuteWitness(catalog, contract, contract.Seed, false, 1, out evidence, progress);
                    RunResult result;
                    Require(progress.TrySettleRun(run, out result) && result.FirstClearReward == 0, "replay granted first clear");
                }
                Check(store.Load().Archives.All(a => a), "30/30 archives acquired on real surviving left/right replays", details);
                int standardEarnings = 0;
                for (int seed = 0; seed < 29; seed++)
                {
                    Witness evidence;
                    RunSimulation run = ExecuteWitness(catalog, catalog.Contracts[5], seed, true, -1, out evidence, progress, 300f);
                    RunResult result;
                    Require(progress.TrySettleRun(run, out result), "endless settlement pending");
                    standardEarnings += result.ChipsEarned;
                }
                Check(standardEarnings >= 3100 && standardEarnings <= 3300,
                    "29 real five-minute endless runs earn " + standardEarnings + " chips for remaining 3100 budget", details);
                int before = progress.Save.Chips;
                foreach (OutfitDefinition outfit in catalog.Outfits.Skip(1))
                {
                    Require(progress.BuyOutfit(outfit.Id), "earned balance cannot buy outfit " + outfit.Id);
                    int after = progress.Save.Chips;
                    Require(progress.BuyOutfit(outfit.Id) && progress.Save.Chips == after, "owned outfit charged again");
                    progress.EquipOutfit(outfit.Id);
                    progress = new GameProgress(catalog, store.Load(), store);
                    Require(progress.Save.EquippedOutfit == outfit.Id && progress.Save.OwnedOutfits[outfit.Id], "equip did not survive reload");
                }
                Check(progress.Save.Chips == before - 6700 && progress.Save.Chips >= 0 && progress.Save.OwnedOutfits.All(o => o),
                    "all seven paid outfits purchased/equipped from earned chips, 6700 total, no duplicate charge", details);
                foreach (int region in Enumerable.Range(0, 3))
                for (int ending = 0; ending < 2; ending++)
                {
                    var lossStore = new GameSaveStore(Path.Combine(path, "archive-loss-" + region + "-" + ending));
                    var lossProgress = new GameProgress(catalog, lossStore.Load(), lossStore);
                    var run = new RunSimulation(catalog);
                    var controller = new GeometryController(1);
                    int acquired = -1;
                    run.ArchiveCollected += e => { acquired = e.ArchiveId; lossProgress.CollectArchive(acquired); };
                    ContractDefinition contract = catalog.Contracts[region * 6 + 2];
                    run.Start(contract, contract.Seed, false);
                    run.Tick(3f);
                    for (int frame = 0; frame < 5000 && acquired < 0 && run.Phase == RunPhase.Running; frame++)
                    {
                        GameCommand command = controller.Decide(run);
                        if (command != GameCommand.None) run.Handle(command);
                        run.Tick(.08f);
                    }
                    Require(acquired >= 0, "no archive before early ending");
                    if (ending == 0) run.Fail("intentional"); else { run.Pause(); run.SafeEnd(); }
                    Require(lossStore.Load().Archives[acquired], "archive lost before run settlement");
                    RunResult result;
                    Require(lossProgress.TrySettleRun(run, out result) && result.ChipsEarned == run.Chips
                        && lossStore.Load().Chips == run.Chips, "early ending did not preserve earned pickups");
                }
                Check(true, "every region: real branch archive survives failure and paused safe exit before contract clear", details);
                foreach (bool assisted in new[] { true, false })
                {
                    var run = Fixture(true);
                    run.AutoTurn = assisted;
                    run.AutoTurn = false;
                    run.Tick(.1f);
                    run.SafeEnd();
                    var record = new GameProgress(catalog, new SaveData(), new GameSaveStore(Path.Combine(path, "record-" + assisted)));
                    RunResult result;
                    Require(record.TrySettleRun(run, out result)
                        && (record.Save.EndlessBestScore > 0) != assisted, "sticky assistance record eligibility");
                }
                Check(true, "assistance ever enabled excludes record even before first turn", details);
            }
            finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
        }

        private static void ValidateValidatorRejection(GameCatalog catalog, List<string> details)
        {
            var contract = catalog.Contracts[2];
            List<TrackEvent> events = Generate(catalog, contract, 2, false);
            TrackEvent turn = events.First(e => e.Kind == ObstacleKind.TurnRight);
            events.Add(new TrackEvent { Id = 99999, Kind = ObstacleKind.Collapse,
                Distance = RunSimulation.DistanceAtTime((float)RunSimulation.TimeAtDistance(turn.Distance) + .5f),
                Lane = 1, Region = 0, ObserverClearance = 5.5f });
            bool rejected = false;
            try { ValidateGeometry(events.OrderBy(e => e.Distance).ToList(), contract, false); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "independent validator rejects injected post-turn safety violation", details);
            events = Generate(catalog, contract, 2, false);
            events[0].ObserverClearance = 1f;
            rejected = false;
            try { ValidateGeometry(events, contract, false); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "independent validator rejects injected near-field violation", details);
            events = Generate(catalog, contract, 2, false);
            string original = Fingerprint(events);
            events[0].WarningSeconds = 1f;
            Check(Fingerprint(events) != original, "exact witnessed stream fingerprint includes warning metadata", details);
            events = Generate(catalog, catalog.Contracts[8], 2, false);
            TrackEvent height = events.First(e => e.Kind == ObstacleKind.Rise || e.Kind == ObstacleKind.Fall);
            foreach (string name in new[] { "GroundHeight", "GroundTargetHeight", "GroundTransitionStartDistance", "GroundTransitionSeconds" })
            {
                var field = typeof(TrackEvent).GetField(name);
                float saved = (float)field.GetValue(height);
                original = Fingerprint(events);
                field.SetValue(height, saved + .125f);
                Require(Fingerprint(events) != original, "fingerprint omitted " + name);
                field.SetValue(height, saved);
            }
            Check(true, "full-stream witness fingerprint includes all four road-height fields", details);
            height.GroundTargetHeight = 100f;
            rejected = false;
            try { ValidateGeometry(events, catalog.Contracts[8], false); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "independent geometry validator rejects injected unsafe road height", details);
        }

        private static RunSimulation Fixture(bool endless = false)
        {
            var catalog = GameCatalog.CreateDefault();
            var run = new RunSimulation(catalog);
            run.Start(catalog.Contracts[2], 2, endless);
            run.Tick(3f);
            run.Events.Clear(); // Diagnostic collision fixture only. Never used by an acceptance witness.
            return run;
        }

        private static TrackEvent Hazard(float time, ObstacleKind kind, float depth = 0f)
        {
            return new TrackEvent { Id = 9900, Kind = kind, Distance = RunSimulation.DistanceAtTime(time), Lane = 1, Depth = depth };
        }

        private static void Advance(RunSimulation run, float duration, bool partitioned)
        {
            if (!partitioned) { run.Tick(duration); return; }
            for (int i = 0; i < 41; i++) run.Tick(duration / 41f);
        }

        private static bool IsHazard(ObstacleKind kind)
        {
            return kind <= ObstacleKind.Collapse || kind == ObstacleKind.Boss;
        }

        private static bool IsContext(ObstacleKind kind)
        {
            return kind == ObstacleKind.TurnLeft || kind == ObstacleKind.TurnRight || kind == ObstacleKind.Fork;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Check(bool condition, string message, List<string> details)
        {
            Require(condition, message);
            details.Add("PASS: " + message);
        }

        [MenuItem("RIFTLINE/Validate Core")]
        private static void RunFromMenu()
        {
            UnityEngine.Debug.Log(RunAll());
        }
    }
}
#endif
