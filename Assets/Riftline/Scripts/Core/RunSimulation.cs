using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Riftline
{
    public sealed class RunSimulation
    {
        private const float LaneSpacing = 2.6f;
        private const float LaneChangeDuration = .32f;
        private const float PhaseDuration = .8f;
        private const float InputBufferDuration = .2f;
        public const int MaxHealth = 3;
        private const double DamageProtectionSeconds = 1.25d;

        private readonly GameCatalog catalog;
        private TrackGenerator generator;
        private float countdownRemaining;
        private float laneStartX;
        private float laneTargetX;
        private float laneTransition;
        private float verticalVelocity;
        private float slideRemaining;
        private double scoreValue;
        private double preciseElapsed;
        private double phaseEnds;
        private double slideEnds;
        private double laneStarted;
        private double jumpStarted;
        private double jumpHeight;
        private double jumpVelocity;
        private double jumpBufferEnds;
        private double invulnerableEnds;
        private bool airborne;
        private bool autoTurn;
        private int tutorialMask;
        private readonly Dictionary<int, int> routes = new Dictionary<int, int>();
        private readonly Dictionary<GameCommand, double> actionTimes = new Dictionary<GameCommand, double>();
        private bool victory;
        private bool usedAutoTurn;
        private string settlementId;

        public RunPhase Phase { get; private set; } = RunPhase.Menu;
        public ContractDefinition ActiveContract { get; private set; }
        public bool Endless { get; private set; }
        public float Elapsed { get; private set; }
        public float Distance { get; private set; }
        public float Speed { get; private set; } = 6.5f;
        public float LaneX { get; private set; }
        public float Height { get; private set; }
        public float GroundHeight { get; private set; }
        public TrackEvent GroundProfile { get; private set; }
        public float PhaseRemaining { get; private set; }
        public float PhaseEnergy { get; private set; }
        public float Multiplier { get; private set; } = 1f;
        public float LastTurnDistance { get; private set; } = -1000f;
        public int Lane { get; private set; } = 1;
        public int Score { get; private set; }
        public int Chips { get; private set; }
        public int NearMisses { get; private set; }
        public int RegionId { get; private set; }
        public int TurnsCompleted { get; private set; }
        public int Heading { get; private set; }
        public int ChosenRoute { get; private set; }
        public bool Sliding { get { return slideRemaining > 0f && Height <= .001f; } }
        public string FailureReason { get; private set; }
        public string LastHint { get; private set; }
        public bool AutoTurn
        {
            get { return autoTurn; }
            set { autoTurn = value; if (value) usedAutoTurn = true; }
        }
        public bool UsedAutoTurn { get { return usedAutoTurn; } }
        public int PhaseUses { get; private set; }
        public int TutorialRecoveryCount { get; private set; }
        public int Health { get; private set; } = MaxHealth;
        public int DamageCount { get; private set; }
        public int ChipStreak { get; private set; }
        public int BestChipStreak { get; private set; }
        public int LastStreakReward { get; private set; }
        public int LastDamageEventId { get; private set; } = -1;
        public string LastDamageReason { get; private set; } = string.Empty;
        public float LastDamageTime { get; private set; } = -10f;
        public float InvulnerableRemaining => (float)Math.Max(0d, invulnerableEnds - preciseElapsed);
        public float CountdownRemaining => countdownRemaining;
        public int TutorialLessonsCompleted { get { return Enumerable.Range(0, 7).Count(i => (tutorialMask & (1 << i)) != 0); } }
        public bool TutorialComplete
        {
            get { return Endless || ActiveContract == null || (ActiveContract.Id == 0 ? (tutorialMask & 63) == 63
                : ActiveContract.Id != 1 || (tutorialMask & 64) != 0); }
        }
        public bool BossActive { get { return !Endless && ActiveContract != null && ActiveContract.Boss
            && preciseElapsed >= TimeAtDistance(ActiveContract.Length) - 45d; } }
        public int CombinationBudget { get { return Endless ? TrackGenerator.CombinationBudgetAt(Elapsed) : Math.Min(2, 1 + RegionId); } }
        public List<TrackEvent> Events { get; } = new List<TrackEvent>();
        public List<ReplayEntry> Replay { get; } = new List<ReplayEntry>();

        public event Action<TrackEvent> ArchiveCollected;
        public event Action<FeedbackKind> Feedback;
        public event Action<RunPhase> StateChanged;

        public RunSimulation(GameCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public void Start(ContractDefinition contract, int seed, bool endless)
        {
            ActiveContract = (contract ?? throw new ArgumentNullException(nameof(contract))).Clone();
            Endless = endless;
            Elapsed = 0f;
            preciseElapsed = 0d;
            phaseEnds = slideEnds = laneStarted = jumpStarted = jumpBufferEnds = 0d;
            airborne = false;
            jumpHeight = jumpVelocity = 0d;
            tutorialMask = 0;
            TutorialRecoveryCount = 0;
            Health = MaxHealth;
            DamageCount = ChipStreak = BestChipStreak = LastStreakReward = 0;
            LastDamageEventId = -1;
            LastDamageReason = string.Empty;
            LastDamageTime = -10f;
            invulnerableEnds = 0d;
            PhaseUses = 0;
            routes.Clear();
            actionTimes.Clear();
            Distance = 0f;
            Speed = 6.5f;
            Lane = 1;
            LaneX = 0f;
            laneStartX = 0f;
            laneTargetX = 0f;
            laneTransition = LaneChangeDuration;
            Height = 0f;
            GroundHeight = 0f;
            GroundProfile = null;
            verticalVelocity = 0f;
            slideRemaining = 0f;
            PhaseRemaining = 0f;
            PhaseEnergy = 0f;
            Multiplier = 1f;
            LastTurnDistance = -1000f;
            Score = 0;
            scoreValue = 0f;
            Chips = 0;
            NearMisses = 0;
            RegionId = contract.Region;
            TurnsCompleted = 0;
            Heading = 0;
            ChosenRoute = 0;
            FailureReason = string.Empty;
            LastHint = string.Empty;
            victory = false;
            usedAutoTurn = AutoTurn;
            settlementId = Guid.NewGuid().ToString("N");
            Events.Clear();
            Replay.Clear();
            generator = new TrackGenerator(catalog, contract, seed, endless);
            generator.GenerateUntil(180f, Events);
            countdownRemaining = 1f;
            SetPhase(RunPhase.Countdown);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            if (Phase == RunPhase.Countdown)
            {
                countdownRemaining = Mathf.Max(0f, countdownRemaining - dt);
                if (countdownRemaining <= 0f)
                {
                    SetPhase(RunPhase.Running);
                }
                return;
            }
            if (Phase != RunPhase.Running)
            {
                return;
            }

            double target = preciseElapsed + Math.Min(dt, 1800f);
            if (!Endless) target = Math.Min(target, TimeAtDistance(ActiveContract.Length));
            while (preciseElapsed < target - 1e-10d && Phase == RunPhase.Running)
            {
                generator.GenerateUntil(DistanceAtTimePrecise(preciseElapsed + 3d) + 180f, Events);
                if (AutoTurn) CommitAutomaticTurn();
                double boundary = NextBoundary(target);
                StepRunning(boundary);
            }
        }

        public void Handle(GameCommand command)
        {
            RecordCommand(command);
            if (command == GameCommand.Pause)
            {
                Pause();
                return;
            }
            if (Phase != RunPhase.Running)
            {
                return;
            }

            switch (command)
            {
                case GameCommand.Left:
                    HandleDirection(-1);
                    break;
                case GameCommand.Right:
                    HandleDirection(1);
                    break;
                case GameCommand.Jump:
                    HandleJump();
                    break;
                case GameCommand.Slide:
                    HandleSlide();
                    break;
                case GameCommand.Phase:
                    HandlePhase();
                    break;
                case GameCommand.Comfort:
                    EmitFeedback(FeedbackKind.Ui);
                    break;
            }
        }

        public void Pause()
        {
            if (Phase == RunPhase.Running || Phase == RunPhase.Countdown)
            {
                SetPhase(RunPhase.Paused);
            }
        }

        public void Resume()
        {
            if (Phase != RunPhase.Paused)
            {
                return;
            }
            countdownRemaining = 3f;
            SetPhase(RunPhase.Countdown);
        }

        public void SafeEnd()
        {
            if (Phase == RunPhase.Failed || Phase == RunPhase.Completed || Phase == RunPhase.Menu)
            {
                return;
            }

            victory = Endless || (ActiveContract != null && Distance >= ActiveContract.Length);
            FailureReason = victory ? string.Empty : "safe_exit";
            SetPhase(RunPhase.Completed);
            EmitFeedback(FeedbackKind.Complete);
        }

        public void Fail(string reason)
        {
            if (Phase == RunPhase.Failed || Phase == RunPhase.Completed || Phase == RunPhase.Menu)
            {
                return;
            }

            victory = false;
            FailureReason = string.IsNullOrWhiteSpace(reason) ? "collision" : reason;
            CollectionUtility.AddBounded(Replay, new ReplayEntry
            {
                Elapsed = Elapsed,
                Distance = Distance,
                EventId = NearestEventId(),
                Outcome = "failed:" + FailureReason
            }, 64);
            SetPhase(RunPhase.Failed);
            EmitFeedback(FeedbackKind.Fail);
        }

        public void AddPhaseEnergy(float amount)
        {
            if (amount <= 0f || PhaseEnergy >= 1f || PhaseRemaining > 0f)
            {
                return;
            }

            float previous = PhaseEnergy;
            PhaseEnergy = Mathf.Clamp01(PhaseEnergy + amount);
            if (previous < 1f && PhaseEnergy >= 1f)
            {
                EmitFeedback(FeedbackKind.PhaseReady);
            }
        }

        public static float DistanceAtTime(float elapsed)
        {
            return DistanceAtTimePrecise(elapsed);
        }

        public static double TimeAtDistance(float distance)
        {
            double d = Math.Max(0d, distance);
            const double capDistance = 3609.375d;
            return d <= capDistance ? 2d * d / (6.5d + Math.Sqrt(42.25d + .016d * d))
                : 437.5d + (d - capDistance) / 10d;
        }

        public bool IsEventActive(TrackEvent item)
        {
            if (item.ForkId < 0) return true;
            int route;
            return !routes.TryGetValue(item.ForkId, out route) || route == item.RequiredRoute;
        }

        public bool HasDirectionContext
        {
            get { return FindContextEvent(item => item.Kind == ObstacleKind.Fork
                || item.Kind == ObstacleKind.TurnLeft || item.Kind == ObstacleKind.TurnRight) != null; }
        }

        private static float DistanceAtTimePrecise(double elapsed)
        {
            double time = Math.Max(0d, elapsed);
            const double capTime = 437.5d;
            if (time <= capTime)
            {
                return (float)(6.5d * time + .004d * time * time);
            }

            double capDistance = 6.5d * capTime + .004d * capTime * capTime;
            return (float)(capDistance + 10d * (time - capTime));
        }

        public static bool ValidateGeneratedSequence(GameCatalog catalog, int seed, out string error)
        {
            return TrackGenerator.Validate(catalog, seed, out error);
        }

        internal RunResult CreateResult()
        {
            return new RunResult
            {
                Victory = victory,
                Endless = Endless,
                Score = Score,
                ChipsEarned = Chips,
                FirstClearReward = 0,
                Stars = 0,
                ContractId = ActiveContract != null ? ActiveContract.Id : -1,
                NearMisses = NearMisses,
                Distance = Distance,
                FailureReason = FailureReason,
                SettlementId = settlementId,
                DurationSeconds = Elapsed
            };
        }

        private void StepRunning(double time)
        {
            float previousDistance = Distance;
            float dt = (float)(time - preciseElapsed);
            preciseElapsed = time;
            Elapsed = (float)preciseElapsed;
            Distance = DistanceAtTimePrecise(preciseElapsed);
            Speed = Mathf.Min(10f, 6.5f + .008f * Elapsed);

            UpdateLane(dt);
            UpdateVerticalMotion(dt);
            slideRemaining = (float)Math.Max(0d, slideEnds - preciseElapsed);
            PhaseRemaining = (float)Math.Max(0d, phaseEnds - preciseElapsed);
            scoreValue += (Distance - previousDistance) * 10f * Multiplier;
            ProcessCrossedEvents(previousDistance, Distance);
            GroundHeight = GroundProfile == null ? 0f : GroundProfile.GroundHeightAtDistance(Distance);
            Score = Math.Max(0, (int)Math.Floor(scoreValue));
            if (Phase != RunPhase.Running)
            {
                return;
            }

            if (Endless)
            {
                UpdateEndlessRegion();
            }
            else if (preciseElapsed >= TimeAtDistance(ActiveContract.Length) - 1e-7d)
            {
                if (!TutorialComplete) { Fail("tutorial_incomplete"); return; }
                victory = true;
                FailureReason = string.Empty;
                SetPhase(RunPhase.Completed);
                EmitFeedback(FeedbackKind.Complete);
                return;
            }

            Events.RemoveAll(item => item.Passed && item.Distance + item.Depth < Distance - 30f);
        }

        private void UpdateLane(float dt)
        {
            if (laneTransition >= LaneChangeDuration)
            {
                LaneX = laneTargetX;
                return;
            }

            laneTransition = (float)Math.Min(LaneChangeDuration, preciseElapsed - laneStarted);
            float t = laneTransition / LaneChangeDuration;
            LaneX = Mathf.Lerp(laneStartX, laneTargetX, t * t * (3f - 2f * t));
        }

        private void UpdateVerticalMotion(float dt)
        {
            if (!airborne) { Height = 0f; verticalVelocity = 0f; return; }
            double age = preciseElapsed - jumpStarted;
            Height = (float)Math.Max(0d, jumpHeight + jumpVelocity * age - 7d * age * age);
            verticalVelocity = (float)(jumpVelocity - 14d * age);
            if (Height > 1e-6f) return;
            Height = 0f;
            verticalVelocity = 0f;
            airborne = false;
            if (jumpBufferEnds > preciseElapsed + 1e-7d) BeginJump();
        }

        private void ProcessCrossedEvents(float previousDistance, float currentDistance)
        {
            for (int index = 0; index < Events.Count; index++)
            {
                TrackEvent trackEvent = Events[index];
                double crossing = TimeAtDistance(trackEvent.Distance);
                if (trackEvent.Passed || crossing > preciseElapsed + 1e-7d)
                {
                    continue;
                }
                if (!IsEventActive(trackEvent)) { trackEvent.Passed = true; continue; }
                bool hazard = TrackGenerator.IsHazard(trackEvent.Kind);
                bool leaving = trackEvent.Depth > 0f && preciseElapsed >= TimeAtDistance(trackEvent.Distance + trackEvent.Depth) - 1e-7d;
                if (hazard && leaving)
                {
                    AwardNearMissIfClose(trackEvent);
                    trackEvent.Passed = true;
                }
                else
                {
                    trackEvent.Entered = true;
                    ResolveEvent(trackEvent);
                    trackEvent.Passed = !hazard || trackEvent.Depth <= 0f;
                }
                if (Phase != RunPhase.Running)
                {
                    return;
                }
            }
        }

        private double NextBoundary(double limit)
        {
            double next = limit;
            ConsiderBoundary(ref next, phaseEnds);
            ConsiderBoundary(ref next, slideEnds);
            ConsiderBoundary(ref next, jumpBufferEnds);
            ConsiderBoundary(ref next, invulnerableEnds);
            if (GroundProfile != null)
                ConsiderBoundary(ref next, TimeAtDistance(GroundProfile.GroundTransitionStartDistance)
                    + GroundProfile.GroundTransitionSeconds);
            if (laneTransition < LaneChangeDuration) ConsiderBoundary(ref next, laneStarted + LaneChangeDuration);
            if (airborne)
            {
                foreach (double height in new[] { 0d, .35d, .65d })
                {
                    double discriminant = jumpVelocity * jumpVelocity + 28d * (jumpHeight - height);
                    if (discriminant < 0d) continue;
                    double root = Math.Sqrt(discriminant);
                    ConsiderBoundary(ref next, jumpStarted + (jumpVelocity - root) / 14d);
                    ConsiderBoundary(ref next, jumpStarted + (jumpVelocity + root) / 14d);
                }
            }
            foreach (TrackEvent item in Events)
            {
                if (item.Passed || !IsEventActive(item)) continue;
                ConsiderBoundary(ref next, TimeAtDistance(item.Distance));
                if (item.Depth > 0f) ConsiderBoundary(ref next, TimeAtDistance(item.Distance + item.Depth));
                if (!TrackGenerator.IsHazard(item.Kind) || laneTransition >= LaneChangeDuration) continue;
                double half = Math.Max(.55d, item.Width * .5d + .3d);
                foreach (double edge in new[] { LaneToX(item.Lane) - half, LaneToX(item.Lane) + half })
                {
                    double fraction = (edge - laneStartX) / (laneTargetX - laneStartX);
                    if (fraction <= 0d || fraction >= 1d) continue;
                    double low = 0d, high = 1d;
                    for (int i = 0; i < 36; i++)
                    {
                        double mid = (low + high) * .5d;
                        if (mid * mid * (3d - 2d * mid) < fraction) low = mid; else high = mid;
                    }
                    ConsiderBoundary(ref next, laneStarted + (low + high) * .5d * LaneChangeDuration);
                }
            }
            return next;
        }

        private void ConsiderBoundary(ref double next, double candidate)
        {
            if (candidate > preciseElapsed + 1e-7d && candidate < next) next = candidate;
        }

        private void ResolveEvent(TrackEvent trackEvent)
        {
            switch (trackEvent.Kind)
            {
                case ObstacleKind.Chip:
                    ResolveChip(trackEvent);
                    return;
                case ObstacleKind.Archive:
                    ResolveArchive(trackEvent);
                    return;
                case ObstacleKind.TurnLeft:
                case ObstacleKind.TurnRight:
                    ResolveTurn(trackEvent);
                    return;
                case ObstacleKind.Fork:
                    if (!trackEvent.Resolved)
                    {
                        ChosenRoute = -1;
                        trackEvent.SelectedBranch = -1;
                        trackEvent.Resolved = true;
                    }
                    routes[trackEvent.Id] = trackEvent.SelectedBranch;
                    return;
                case ObstacleKind.Rise:
                case ObstacleKind.Fall:
                    GroundProfile = trackEvent;
                    trackEvent.Resolved = true;
                    return;
            }

            ResolveHazard(trackEvent);
        }

        private void ResolveChip(TrackEvent trackEvent)
        {
            if (!IsInEventLane(trackEvent))
            {
                ChipStreak = 0;
                return;
            }

            trackEvent.Resolved = true;
            Chips++;
            ChipStreak++;
            BestChipStreak = Math.Max(BestChipStreak, ChipStreak);
            scoreValue += 100f * Multiplier;
            if (ChipStreak == 5 || ChipStreak == 10 || ChipStreak == 20)
            {
                LastStreakReward = ChipStreak;
                Multiplier = Mathf.Min(4f, Multiplier + .25f);
                AddPhaseEnergy(.2f);
                EmitFeedback(FeedbackKind.Combo);
            }
            if (trackEvent.PhaseCharge)
            {
                AddPhaseEnergy(1f);
                LastHint = "phase_ready";
            }
            EmitFeedback(FeedbackKind.Chip);
        }

        private void ResolveArchive(TrackEvent trackEvent)
        {
            if (!IsInEventLane(trackEvent))
            {
                return;
            }

            trackEvent.Resolved = true;
            ArchiveCollected?.Invoke(trackEvent);
            EmitFeedback(FeedbackKind.Archive);
        }

        private void ResolveTurn(TrackEvent trackEvent)
        {
            if (!trackEvent.TurnCommitted)
            {
                if (trackEvent.TutorialLesson >= 0)
                {
                    TutorialRecoveryCount++;
                    LastHint = "tutorial_retry_" + trackEvent.TutorialLesson;
                }
                Fail(trackEvent.Resolved ? "wrong_turn" : "missed_turn");
                return;
            }

            Heading += trackEvent.Branch * 90;
            TurnsCompleted++;
            LastTurnDistance = trackEvent.Distance;
            trackEvent.Resolved = true;
            LastHint = trackEvent.Branch < 0 ? "turn_left" : "turn_right";
            LearnTutorial(trackEvent);
            EmitFeedback(FeedbackKind.Turn);
        }

        private void ResolveHazard(TrackEvent trackEvent)
        {
            if (trackEvent.HitRecorded)
            {
                return;
            }
            float testedX = LaneX;
            float testedHeight = Height;
            if (trackEvent.Depth > 0f)
            {
                // Occupied intervals are half-open. Sample their right-hand state at an exact analytic boundary.
                testedX = LanePositionAt(preciseElapsed + 1e-6d);
                if (airborne)
                {
                    double age = preciseElapsed + 1e-6d - jumpStarted;
                    testedHeight = (float)Math.Max(0d, jumpHeight + jumpVelocity * age - 7d * age * age);
                }
            }
            if (!IsInEventLane(trackEvent, testedX))
            {
                LearnTutorial(trackEvent);
                if (trackEvent.Depth <= 0f) AwardNearMissIfClose(trackEvent);
                return;
            }

            bool avoided;
            switch (trackEvent.Kind)
            {
                case ObstacleKind.Gap:
                    avoided = testedHeight >= .35f;
                    break;
                case ObstacleKind.Crate:
                case ObstacleKind.LowBarrier:
                    avoided = PhaseIsActive || testedHeight >= .65f;
                    break;
                case ObstacleKind.HighBeam:
                case ObstacleKind.Drone:
                    avoided = PhaseIsActive || Sliding;
                    break;
                case ObstacleKind.PhaseWall:
                case ObstacleKind.Boss:
                    avoided = PhaseIsActive;
                    break;
                case ObstacleKind.MovingGate:
                case ObstacleKind.Collapse:
                    avoided = false;
                    break;
                default:
                    avoided = true;
                    break;
            }

            if (!avoided)
            {
                if (trackEvent.TutorialLesson >= 0)
                {
                    TutorialRecoveryCount++;
                    LastHint = "tutorial_retry_" + trackEvent.TutorialLesson;
                }
                ApplyCollision(trackEvent);
                return;
            }

            LearnTutorial(trackEvent);
            if (trackEvent.Depth <= 0f) AwardNearMiss(trackEvent);
        }

        private void ApplyCollision(TrackEvent item)
        {
            item.HitRecorded = true;
            item.Resolved = true;
            string reason = FailureFor(item.Kind);
            if (item.Kind == ObstacleKind.Gap || item.Kind == ObstacleKind.Collapse)
            {
                Health = 0;
                Fail(reason);
                return;
            }
            if (invulnerableEnds > preciseElapsed + 1e-7d)
            {
                return;
            }
            Health = Math.Max(0, Health - 1);
            DamageCount++;
            LastDamageTime = Elapsed;
            LastDamageReason = reason;
            LastDamageEventId = item.Id;
            invulnerableEnds = preciseElapsed + DamageProtectionSeconds;
            Multiplier = 1f;
            ChipStreak = 0;
            CollectionUtility.AddBounded(Replay, new ReplayEntry
            {
                Elapsed = Elapsed, Distance = Distance, EventId = item.Id,
                Outcome = "damage:" + reason + ":health=" + Health
            }, 64);
            EmitFeedback(FeedbackKind.Damage);
            if (Health <= 0)
            {
                Fail(reason);
            }
        }

        private void AwardNearMissIfClose(TrackEvent trackEvent)
        {
            if (trackEvent.HitRecorded) return;
            float obstacleX = LaneToX(trackEvent.Lane);
            // A neighboring lane at 2.6m is safe avoidance, not a near miss.
            float dangerEdge = trackEvent.Width * .5f + .3f;
            if (Mathf.Abs(LaneX - obstacleX) <= dangerEdge + .4f
                || (IsInEventLane(trackEvent) && (Height > .001f || Sliding)))
            {
                AwardNearMiss(trackEvent);
            }
        }

        private void AwardNearMiss(TrackEvent trackEvent)
        {
            if (trackEvent.NearMissAwarded)
            {
                return;
            }

            trackEvent.Resolved = true;
            trackEvent.NearMissAwarded = true;
            NearMisses++;
            scoreValue += 150f * Multiplier;
            Multiplier = Mathf.Min(4f, Multiplier + .25f);
            if (!PhaseIsActive)
            {
                AddPhaseEnergy(trackEvent.Kind == ObstacleKind.Drone || trackEvent.Kind == ObstacleKind.MovingGate ? .3f : .2f);
            }
            EmitFeedback(FeedbackKind.NearMiss);
        }

        private void HandleDirection(int direction)
        {
            TrackEvent turn = FindContextEvent(item =>
                item.Kind == ObstacleKind.TurnLeft || item.Kind == ObstacleKind.TurnRight);
            if (turn != null)
            {
                if (turn.Resolved) return;
                turn.Resolved = true;
                turn.TurnCommitted = direction == turn.Branch;
                turn.SelectedBranch = direction;
                actionTimes[direction < 0 ? GameCommand.Left : GameCommand.Right] = preciseElapsed;
                LastHint = turn.TurnCommitted ? "turn_locked" : "wrong_turn_input";
                return;
            }

            TrackEvent fork = FindContextEvent(item => item.Kind == ObstacleKind.Fork);
            if (fork != null)
            {
                if (fork.Resolved) return;
                fork.Resolved = true;
                ChosenRoute = direction;
                fork.SelectedBranch = direction;
                routes[fork.Id] = direction;
                LastHint = direction < 0 ? "route_left" : "route_right";
                return;
            }

            int targetLane = Mathf.Clamp(Lane + direction, 0, 2);
            if (targetLane == Lane)
            {
                return;
            }

            Lane = targetLane;
            laneStartX = LaneX;
            laneTargetX = LaneToX(targetLane);
            laneTransition = 0f;
            laneStarted = preciseElapsed;
            actionTimes[direction < 0 ? GameCommand.Left : GameCommand.Right] = preciseElapsed;
        }

        private TrackEvent FindContextEvent(Func<TrackEvent, bool> predicate)
        {
            float maximumDistance = Distance + Speed * 2f;
            return Events
                .Where(item => !item.Passed && IsEventActive(item) && item.Distance >= Distance && item.Distance <= maximumDistance && predicate(item))
                .OrderBy(item => item.Distance)
                .FirstOrDefault();
        }

        private void CommitAutomaticTurn()
        {
            TrackEvent turn = FindContextEvent(item =>
                item.Kind == ObstacleKind.TurnLeft || item.Kind == ObstacleKind.TurnRight);
            if (turn == null || turn.Resolved)
            {
                return;
            }

            turn.Resolved = true;
            turn.TurnCommitted = true;
            turn.SelectedBranch = turn.Branch;
            usedAutoTurn = true;
            LastHint = "auto_turn";
        }

        private void HandleJump()
        {
            actionTimes[GameCommand.Jump] = preciseElapsed;
            if (!airborne)
            {
                BeginJump();
            }
            else
            {
                jumpBufferEnds = preciseElapsed + InputBufferDuration;
            }
        }

        private void BeginJump()
        {
            Height = 0f;
            verticalVelocity = 5.8f;
            airborne = true;
            jumpHeight = 0d;
            jumpVelocity = 5.8d;
            jumpStarted = preciseElapsed;
            jumpBufferEnds = 0d;
            slideRemaining = 0f;
            slideEnds = 0d;
            EmitFeedback(FeedbackKind.Jump);
        }

        private void HandleSlide()
        {
            actionTimes[GameCommand.Slide] = preciseElapsed;
            if (Height > .001f)
            {
                verticalVelocity = Mathf.Min(verticalVelocity, -8f);
                jumpHeight = Height;
                jumpVelocity = verticalVelocity;
                jumpStarted = preciseElapsed;
                LastHint = "fast_fall";
                return;
            }

            slideRemaining = .75f;
            slideEnds = preciseElapsed + .75d;
            EmitFeedback(FeedbackKind.Slide);
        }

        private void HandlePhase()
        {
            if (PhaseEnergy < 1f || PhaseRemaining > 0f)
            {
                LastHint = "phase_not_ready";
                return;
            }

            PhaseEnergy = 0f;
            PhaseRemaining = PhaseDuration;
            phaseEnds = preciseElapsed + PhaseDuration;
            PhaseUses++;
            actionTimes[GameCommand.Phase] = preciseElapsed;
            Multiplier = Mathf.Max(1f, Multiplier - .5f);
            EmitFeedback(FeedbackKind.Phase);
        }

        private bool PhaseIsActive { get { return phaseEnds > preciseElapsed + 1e-7d; } }

        private void LearnTutorial(TrackEvent item)
        {
            if (item.TutorialLesson < 0 || (tutorialMask & (1 << item.TutorialLesson)) != 0) return;
            double commanded;
            if (!actionTimes.TryGetValue(item.RequiredAction, out commanded) || preciseElapsed - commanded > 3d) return;
            if (item.RequiredAction == GameCommand.Jump && Height < .65f - 1e-6f) return;
            if (item.RequiredAction == GameCommand.Slide && !Sliding) return;
            if (item.RequiredAction == GameCommand.Phase && !PhaseIsActive) return;
            if ((item.TutorialLesson == 0 || item.TutorialLesson == 1 || item.TutorialLesson == 5)
                && Math.Abs(LaneX - LaneToX(item.SafeLane)) > .1f) return;
            tutorialMask |= 1 << item.TutorialLesson;
            LastHint = "tutorial_learned_" + item.TutorialLesson;
        }

        private void UpdateEndlessRegion()
        {
            int cycle = Mathf.FloorToInt(Elapsed / 120f);
            RegionId = cycle % 3;
            float cycleTime = Elapsed - cycle * 120f;
            if (cycleTime >= 114f)
            {
                LastHint = "region_transition_" + ((RegionId + 1) % 3);
            }
            else if (LastHint.StartsWith("region_transition_", StringComparison.Ordinal))
            {
                LastHint = string.Empty;
            }
        }

        private bool IsInEventLane(TrackEvent trackEvent)
        {
            return IsInEventLane(trackEvent, LaneX);
        }

        private bool IsInEventLane(TrackEvent trackEvent, float x)
        {
            float obstacleX = LaneToX(trackEvent.Lane);
            float collisionHalfWidth = Mathf.Max(.55f, trackEvent.Width * .5f + .3f);
            return Mathf.Abs(x - obstacleX) <= collisionHalfWidth;
        }

        private float LanePositionAt(double time)
        {
            double t = Math.Min(1d, Math.Max(0d, (time - laneStarted) / LaneChangeDuration));
            return (float)(laneStartX + (laneTargetX - laneStartX) * t * t * (3d - 2d * t));
        }

        private void RecordCommand(GameCommand command)
        {
            CollectionUtility.AddBounded(Replay, new ReplayEntry
            {
                Elapsed = Elapsed,
                Distance = Distance,
                Command = command,
                Outcome = Phase == RunPhase.Running ? "accepted" : "ineligible"
            }, 64);
        }

        private int NearestEventId()
        {
            TrackEvent nearest = Events
                .Where(item => !item.Passed)
                .OrderBy(item => Mathf.Abs(item.Distance - Distance))
                .FirstOrDefault();
            return nearest != null ? nearest.Id : -1;
        }

        private static string FailureFor(ObstacleKind kind)
        {
            switch (kind)
            {
                case ObstacleKind.Gap:
                    return "gap";
                case ObstacleKind.HighBeam:
                case ObstacleKind.Drone:
                    return "slide_required";
                case ObstacleKind.LowBarrier:
                    return "jump_required";
                case ObstacleKind.PhaseWall:
                    return "phase_or_lane_required";
                default:
                    return "lane_collision";
            }
        }

        private static float LaneToX(int lane)
        {
            return (lane - 1) * LaneSpacing;
        }

        private void SetPhase(RunPhase value)
        {
            if (Phase == value)
            {
                return;
            }
            Phase = value;
            StateChanged?.Invoke(value);
        }

        private void EmitFeedback(FeedbackKind kind)
        {
            Feedback?.Invoke(kind);
        }
    }
}
