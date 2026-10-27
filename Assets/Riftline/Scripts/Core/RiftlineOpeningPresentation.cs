using System;

namespace Riftline
{
    public enum OpeningObjective
    {
        RecoverArchive,
        ReachExtraction,
        Extracted
    }

    public struct OpeningFrame
    {
        public bool Active;
        public bool ArchiveRecovered { get; internal set; }
        public OpeningObjective Objective;
        public float Threat;
        public float Progress;
        public float ComboPulse;
        public float DamagePulse;
        public float PhasePulse;
        public float LandingPulse;
    }

    public sealed class RiftlineOpeningPresentation
    {
        private const float ComboPulseDuration = .8f;
        private const float DamagePulseDuration = .7f;
        private const float PhasePulseDuration = .8f;
        private const float LandingPulseDuration = .45f;

        private RunSimulation observedRun;
        private ContractDefinition observedContract;
        private float lastElapsed;
        private float comboTime;
        private float damageTime;
        private float phaseTime;
        private float landingTime;
        private float previousRunningHeight;
        private bool hasRunningHeight;
        private bool archiveRecovered;

        public RiftlineOpeningPresentation()
        {
            Reset();
        }

        public void Reset()
        {
            observedRun = null;
            observedContract = null;
            lastElapsed = -1f;
            comboTime = float.NegativeInfinity;
            damageTime = float.NegativeInfinity;
            phaseTime = float.NegativeInfinity;
            landingTime = float.NegativeInfinity;
            previousRunningHeight = 0f;
            hasRunningHeight = false;
            archiveRecovered = false;
        }

        public void RecordFeedback(FeedbackKind kind, float elapsed)
        {
            switch (kind)
            {
                case FeedbackKind.Archive:
                    archiveRecovered = true;
                    break;
                case FeedbackKind.Combo:
                    comboTime = elapsed;
                    break;
                case FeedbackKind.Damage:
                    damageTime = elapsed;
                    break;
                case FeedbackKind.Phase:
                    phaseTime = elapsed;
                    break;
            }
        }

        public OpeningFrame Evaluate(RunSimulation run)
        {
            if (run == null)
            {
                throw new ArgumentNullException(nameof(run));
            }

            if (IsNewRun(run))
            {
                BeginRun(run);
            }

            ObserveLanding(run);
            lastElapsed = run.Elapsed;

            bool genuineExtraction = IsGenuineExtraction(run);
            return new OpeningFrame
            {
                Active = run.Phase == RunPhase.Countdown
                    || run.Phase == RunPhase.Running
                    || run.Phase == RunPhase.Paused,
                ArchiveRecovered = archiveRecovered,
                Objective = genuineExtraction ? OpeningObjective.Extracted
                    : archiveRecovered ? OpeningObjective.ReachExtraction
                    : OpeningObjective.RecoverArchive,
                Threat = ThreatFor(run),
                Progress = ProgressFor(run),
                ComboPulse = PulseAt(run.Elapsed, comboTime, ComboPulseDuration),
                DamagePulse = PulseAt(run.Elapsed, damageTime, DamagePulseDuration),
                PhasePulse = PulseAt(run.Elapsed, phaseTime, PhasePulseDuration),
                LandingPulse = PulseAt(run.Elapsed, landingTime, LandingPulseDuration)
            };
        }

        private bool IsNewRun(RunSimulation run)
        {
            return !ReferenceEquals(observedRun, run)
                || !ReferenceEquals(observedContract, run.ActiveContract)
                || run.Elapsed < lastElapsed;
        }

        private void BeginRun(RunSimulation run)
        {
            Reset();
            observedRun = run;
            observedContract = run.ActiveContract;
            lastElapsed = run.Elapsed;
        }

        private void ObserveLanding(RunSimulation run)
        {
            if (run.Phase != RunPhase.Running)
            {
                return;
            }

            if (hasRunningHeight && previousRunningHeight > .001f && run.Height <= .001f)
            {
                landingTime = run.Elapsed;
            }

            previousRunningHeight = run.Height;
            hasRunningHeight = true;
        }

        private static bool IsGenuineExtraction(RunSimulation run)
        {
            return run.Phase == RunPhase.Completed
                && !run.Endless
                && run.ActiveContract != null
                && string.IsNullOrEmpty(run.FailureReason)
                && run.Distance >= run.ActiveContract.Length - .001f;
        }

        private static float ThreatFor(RunSimulation run)
        {
            int health = Math.Max(0, Math.Min(RunSimulation.MaxHealth, run.Health));
            float threat = .2f + (RunSimulation.MaxHealth - health) * .35f;
            if (run.PhaseRemaining > 0f)
            {
                threat *= .45f;
            }
            return Clamp01(threat);
        }

        private static float ProgressFor(RunSimulation run)
        {
            if (run.ActiveContract == null || run.ActiveContract.Length <= 0f)
            {
                return 0f;
            }
            return Clamp01(run.Distance / run.ActiveContract.Length);
        }

        private static float PulseAt(float elapsed, float started, float duration)
        {
            float age = Math.Max(0f, elapsed - started);
            return Clamp01(1f - age / duration);
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
