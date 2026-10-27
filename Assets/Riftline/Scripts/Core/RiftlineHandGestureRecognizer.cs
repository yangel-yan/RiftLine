using UnityEngine;

namespace Riftline
{
    public enum HandGestureState
    {
        Neutral,
        Armed,
        PhaseHolding,
        PauseHolding,
        Latched
    }

    [System.Serializable]
    public struct HandGestureThresholds
    {
        public float PinchPress;
        public float PinchRelease;
        public float DirectionDistance;
        public float DirectionWindow;
        public float DominantAxisRatio;
        public float RearmCooldown;
        public float NeutralStability;
        public float PhaseHold;
        public float PauseHold;
        public float TrackingCancel;
        public float AutoPause;
        public float RecoveryStability;

        public static HandGestureThresholds Default => new HandGestureThresholds
        {
            PinchPress = .7f,
            PinchRelease = .25f,
            DirectionDistance = .035f,
            DirectionWindow = .55f,
            DominantAxisRatio = 1.12f,
            RearmCooldown = .08f,
            NeutralStability = .05f,
            PhaseHold = .18f,
            PauseHold = .45f,
            TrackingCancel = .1f,
            AutoPause = .3f,
            RecoveryStability = .3f
        };
    }

    public struct HandGestureSample
    {
        public float Time;
        public bool GameplayEnabled;
        public bool CommandsEnabled;
        public bool DominantTracked;
        public bool SupportTracked;
        public bool DominantOpen;
        public bool DominantFist;
        public bool SupportPausePose;
        public float Pinch;
        public Vector2 PinchViewPosition;
    }

    public struct HandGestureSignal
    {
        public GameCommand Command;
        public HandGestureState State;
        public Vector2 Anchor;
        public Vector2 Displacement;
        public float HoldProgress;
        public bool CandidateCancelled;
        public bool RequestAutoPause;
    }

    public sealed class RiftlineHandGestureRecognizer
    {
        readonly HandGestureThresholds thresholds;
        HandGestureState state;
        Vector2 anchor;
        float armedAt;
        float rearmAt;
        float neutralOpenAt;
        float phaseAt;
        float pauseAt;
        float trackingLostAt;
        float recoveryOpenAt;
        bool canArm;
        bool latchedByFist;
        bool latchedByPause;
        bool trackingInvalidated;
        bool recoveryRequired;
        bool autoPauseIssued;

        public bool IsReady => state == HandGestureState.Neutral && canArm &&
            !recoveryRequired && trackingLostAt < 0f;

        public RiftlineHandGestureRecognizer(HandGestureThresholds thresholds)
        {
            this.thresholds = thresholds;
        }

        public void Reset(float now)
        {
            state = HandGestureState.Neutral;
            anchor = Vector2.zero;
            armedAt = now;
            rearmAt = now;
            neutralOpenAt = -1f;
            canArm = false;
            latchedByFist = false;
            latchedByPause = false;
            trackingLostAt = -1f;
            recoveryOpenAt = -1f;
            trackingInvalidated = false;
            recoveryRequired = false;
            autoPauseIssued = false;
        }

        public HandGestureSignal Update(HandGestureSample sample)
        {
            GameCommand command = GameCommand.None;
            bool cancelled = false;
            bool requestAutoPause = false;
            float holdProgress = 0f;
            Vector2 displacement = state == HandGestureState.Armed
                ? sample.PinchViewPosition - anchor
                : Vector2.zero;

            if (sample.GameplayEnabled && sample.DominantTracked &&
                trackingLostAt >= 0f && !trackingInvalidated)
            {
                trackingLostAt = -1f;
                autoPauseIssued = false;
            }

            if (!sample.GameplayEnabled)
            {
                state = HandGestureState.Neutral;
                canArm = false;
                neutralOpenAt = -1f;
                trackingLostAt = -1f;
                trackingInvalidated = false;
                recoveryRequired = false;
                recoveryOpenAt = -1f;
            }
            else if (!sample.DominantTracked)
            {
                if (trackingLostAt < 0f) trackingLostAt = sample.Time;
                float lostFor = sample.Time - trackingLostAt;
                if (lostFor >= thresholds.TrackingCancel && !trackingInvalidated)
                {
                    trackingInvalidated = true;
                    state = HandGestureState.Neutral;
                    canArm = false;
                    neutralOpenAt = -1f;
                    cancelled = true;
                }
                if (lostFor >= thresholds.AutoPause && !autoPauseIssued)
                {
                    autoPauseIssued = true;
                    requestAutoPause = true;
                }
            }
            else if (trackingLostAt >= 0f && trackingInvalidated)
            {
                trackingLostAt = -1f;
                trackingInvalidated = false;
                autoPauseIssued = false;
                recoveryRequired = true;
                recoveryOpenAt = -1f;
                state = HandGestureState.Neutral;
                canArm = false;
            }
            else if (recoveryRequired)
            {
                if (sample.Pinch <= thresholds.PinchRelease)
                {
                    if (recoveryOpenAt < 0f) recoveryOpenAt = sample.Time;
                    if (sample.Time - recoveryOpenAt >= thresholds.RecoveryStability)
                    {
                        recoveryRequired = false;
                        canArm = true;
                        neutralOpenAt = sample.Time;
                    }
                }
                else recoveryOpenAt = -1f;
            }
            else if (state == HandGestureState.Neutral)
            {
                if (!canArm)
                {
                    if (sample.Pinch <= thresholds.PinchRelease)
                    {
                        if (neutralOpenAt < 0f) neutralOpenAt = sample.Time;
                        if (sample.Time - neutralOpenAt >= thresholds.NeutralStability)
                            canArm = true;
                    }
                    else neutralOpenAt = -1f;
                }
                if (sample.CommandsEnabled && canArm &&
                    sample.SupportTracked && sample.SupportPausePose)
                {
                    pauseAt = sample.Time;
                    canArm = false;
                    state = HandGestureState.PauseHolding;
                }
                else if (sample.CommandsEnabled && canArm && sample.DominantFist)
                {
                    phaseAt = sample.Time;
                    canArm = false;
                    state = HandGestureState.PhaseHolding;
                }
                else if (sample.CommandsEnabled && canArm &&
                         sample.Time >= rearmAt && sample.Pinch >= thresholds.PinchPress)
                {
                    anchor = sample.PinchViewPosition;
                    armedAt = sample.Time;
                    state = HandGestureState.Armed;
                    displacement = Vector2.zero;
                }
            }
            else if (state == HandGestureState.Armed)
            {
                displacement = sample.PinchViewPosition - anchor;
                if (sample.Pinch <= thresholds.PinchRelease)
                {
                    state = HandGestureState.Neutral;
                    rearmAt = sample.Time + thresholds.RearmCooldown;
                }
                else if (sample.Time - armedAt > thresholds.DirectionWindow)
                {
                    state = HandGestureState.Neutral;
                    canArm = false;
                    neutralOpenAt = -1f;
                    cancelled = true;
                }
                else if (
                         displacement.magnitude >= thresholds.DirectionDistance)
                {
                    float x = Mathf.Abs(displacement.x);
                    float y = Mathf.Abs(displacement.y);
                    float major = Mathf.Max(x, y);
                    float minor = Mathf.Min(x, y);
                    if (major >= minor * thresholds.DominantAxisRatio)
                    {
                        command = x > y
                            ? displacement.x < 0f ? GameCommand.Left : GameCommand.Right
                            : displacement.y < 0f ? GameCommand.Slide : GameCommand.Jump;
                        latchedByFist = false;
                        latchedByPause = false;
                        state = HandGestureState.Latched;
                    }
                }
            }
            else if (state == HandGestureState.PhaseHolding)
            {
                if (!sample.DominantFist)
                {
                    state = HandGestureState.Neutral;
                    neutralOpenAt = -1f;
                    cancelled = true;
                }
                else
                {
                    holdProgress = Mathf.Clamp01((sample.Time - phaseAt) / thresholds.PhaseHold);
                    if (holdProgress >= 1f)
                    {
                        command = GameCommand.Phase;
                        latchedByFist = true;
                        latchedByPause = false;
                        state = HandGestureState.Latched;
                    }
                }
            }
            else if (state == HandGestureState.PauseHolding)
            {
                if (!sample.SupportTracked || !sample.SupportPausePose)
                {
                    state = HandGestureState.Neutral;
                    neutralOpenAt = -1f;
                    cancelled = true;
                }
                else
                {
                    holdProgress = Mathf.Clamp01((sample.Time - pauseAt) / thresholds.PauseHold);
                    if (holdProgress >= 1f)
                    {
                        command = GameCommand.Pause;
                        latchedByFist = false;
                        latchedByPause = true;
                        state = HandGestureState.Latched;
                    }
                }
            }
            else if (state == HandGestureState.Latched && latchedByPause &&
                     !sample.SupportPausePose)
            {
                state = HandGestureState.Neutral;
                canArm = false;
                neutralOpenAt = -1f;
            }
            else if (state == HandGestureState.Latched && latchedByFist &&
                     (sample.DominantOpen || sample.Pinch <= thresholds.PinchRelease))
            {
                state = HandGestureState.Neutral;
                neutralOpenAt = sample.Time;
            }
            else if (state == HandGestureState.Latched && !latchedByFist && !latchedByPause &&
                     sample.Pinch <= thresholds.PinchRelease)
            {
                state = HandGestureState.Neutral;
                rearmAt = sample.Time + thresholds.RearmCooldown;
            }

            return new HandGestureSignal
            {
                Command = command,
                State = state,
                Anchor = anchor,
                Displacement = displacement,
                HoldProgress = holdProgress,
                CandidateCancelled = cancelled,
                RequestAutoPause = requestAutoPause
            };
        }
    }
}
