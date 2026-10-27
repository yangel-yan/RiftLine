using System;
using UnityEngine;
using UnityEngine.XR.Hands;
#if ENABLE_PICO_XR_SDK
using ByteDance.PICO.XR;
#endif

namespace Riftline
{
    public sealed class RiftlineHandInput : MonoBehaviour
    {
        const float StaleTrackingSeconds = .15f;
        const float AnchorSmoothingSeconds = .03f;

        sealed class HandFrame
        {
            public bool Tracked;
            public bool Open;
            public bool Fist;
            public bool PausePose;
            public float Pinch;
            public float UpdatedAt = -1f;
            public Pose PinchPose;
            public Vector3 AnchorWorld;
            public bool AnchorInitialized;
            public bool EverTracked;
        }

        readonly HandFrame left = new HandFrame();
        readonly HandFrame right = new HandFrame();
        RiftlineGame game;
        RiftlineHandControlSettings settings;
        RiftlineHandGestureRecognizer recognizer;
        XRHandTrackingEvents leftEvents;
        XRHandTrackingEvents rightEvents;
        RiftlineUIPointer uiPointer;
        RiftlineHandFeedback feedback;
        bool uiSelectHeld;
        bool leftRawPinched;
        bool rightRawPinched;
        Vector3 supportScrollAnchor;
        bool supportScrollAnchored;
        GestureHand lockedHand = GestureHand.None;

        public event Action<GameCommand, float> GestureConfirmed;
        public event Action<Handedness> RawPinchStarted;
        public bool TrainingActive { get; set; }
        public bool IsDominantHandReady
        {
            get
            {
                return recognizer != null && recognizer.IsReady &&
                       (IsTracked(left) || IsTracked(right));
            }
        }

        public HandGestureSignal VisualSignal { get; private set; }
        public Pose DominantPinchPose => DominantFrame.PinchPose;
        HandFrame PreferredFrame => game != null && game.Save.Settings.LeftHanded ? left : right;
        HandFrame DominantFrame { get; set; }
        HandFrame SupportFrame { get; set; }

        public enum GestureHand
        {
            None = -1,
            Left = 0,
            Right = 1
        }

        public static GestureHand SelectGestureHand(bool gestureActive, GestureHand locked,
            bool leftTracked, float leftPinch, bool leftFist,
            bool rightTracked, float rightPinch, bool rightFist, float press)
        {
            if (gestureActive && locked != GestureHand.None) return locked;
            bool leftActive = leftTracked && (leftPinch >= press || leftFist);
            bool rightActive = rightTracked && (rightPinch >= press || rightFist);
            if (leftActive && (!rightActive || leftPinch >= rightPinch)) return GestureHand.Left;
            if (rightActive) return GestureHand.Right;
            return GestureHand.None;
        }

        public void Initialize(RiftlineGame owner)
        {
            game = owner;
            settings = Resources.Load<RiftlineHandControlSettings>("RiftlineHandControlSettings");
            if (!settings)
            {
                Debug.LogError("[Riftline] Hand control settings are missing from Resources.");
                enabled = false;
                return;
            }

            ApplySettings();
            DominantFrame = PreferredFrame;
            SupportFrame = DominantFrame == left ? right : left;
            uiPointer = game.GetComponent<RiftlineUIPointer>() ??
                game.gameObject.AddComponent<RiftlineUIPointer>();
            uiPointer.Initialize(game);
            feedback = gameObject.AddComponent<RiftlineHandFeedback>();
            feedback.Initialize(game);
            if (settings.PausePose && settings.PausePose.relativeOrientation != null && game.ViewCamera)
                settings.PausePose.relativeOrientation.targetTransform = game.ViewCamera.transform;
            leftEvents = CreateTrackingEvents("Left Hand Tracking Events", Handedness.Left);
            rightEvents = CreateTrackingEvents("Right Hand Tracking Events", Handedness.Right);
        }

        XRHandTrackingEvents CreateTrackingEvents(string objectName, Handedness handedness)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var events = child.AddComponent<XRHandTrackingEvents>();
            events.handedness = handedness;
            events.updateType = XRHandTrackingEvents.UpdateTypes.Dynamic;
            events.jointsUpdated.AddListener(OnJointsUpdated);
            events.trackingChanged.AddListener(tracked => SetTracked(handedness, tracked));
            return events;
        }

        public void ResetEdges()
        {
            recognizer?.Reset(Time.unscaledTime);
            VisualSignal = default;
            lockedHand = GestureHand.None;
            uiSelectHeld = false;
            supportScrollAnchored = false;
            uiPointer?.Cancel();
        }

        public void ApplySettings()
        {
            if (!settings || game == null) return;
            HandGestureThresholds thresholds = settings.Thresholds;
            thresholds.DirectionDistance = Mathf.Clamp(
                game.Save.Settings.HandDirectionDistance, .025f, .06f);
            recognizer = new RiftlineHandGestureRecognizer(thresholds);
            recognizer.Reset(Time.unscaledTime);
        }

        public void SelectDominantHand(Handedness handedness)
        {
            if (game == null || handedness == Handedness.Invalid) return;
            game.Save.Settings.LeftHanded = handedness == Handedness.Left;
            ApplySettings();
        }

        public void OnJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            if (settings == null || args.subsystem == null) return;
            HandFrame frame = args.hand.handedness == Handedness.Left ? left : right;
            XRCommonHandGestures common = args.hand.handedness == Handedness.Left
                ? args.subsystem.leftHandCommonGestures
                : args.subsystem.rightHandCommonGestures;

            bool poseValid = common.TryGetPinchPose(out Pose pinchPose) ||
                TryGetPinchPose(args.hand, out pinchPose);
            bool pinchValid = common.TryGetPinchValue(out float pinch);
            if (!pinchValid && poseValid)
                pinchValid = TryGetPinchValue(args.hand, out pinch);
            if (!poseValid || !pinchValid)
            {
                frame.Tracked = false;
                return;
            }

            frame.Tracked = args.hand.isTracked;
            frame.EverTracked |= frame.Tracked;
            frame.Pinch = Mathf.Clamp01(pinch);
            frame.PinchPose = TrackingToWorld(pinchPose);
            frame.AnchorWorld = SmoothAnchor(frame,
                TrackingToWorld(ResolveAnchor(args.hand, pinchPose)).position);
            frame.Open = settings.OpenShape && settings.OpenShape.CheckConditions(args);
            frame.Fist = settings.FistShape && settings.FistShape.CheckConditions(args);
            frame.PausePose = settings.PausePose && settings.PausePose.CheckConditions(args);
            frame.UpdatedAt = Time.unscaledTime;
        }

        void Update()
        {
            if (game == null || recognizer == null || !game.ViewCamera) return;
            bool gestureActive = VisualSignal.State != HandGestureState.Neutral;
            GestureHand chosen = SelectGestureHand(gestureActive, lockedHand,
                IsTracked(left), left.Pinch, left.Fist,
                IsTracked(right), right.Pinch, right.Fist,
                settings.Thresholds.PinchPress);
            lockedHand = chosen;
            DominantFrame = chosen == GestureHand.Left ? left
                : chosen == GestureHand.Right ? right : PreferredFrame;
            SupportFrame = DominantFrame == left ? right : left;
            HandFrame dominant = DominantFrame;
            HandFrame support = SupportFrame;
            bool gameplay = game.Phase == RunPhase.Running || game.Phase == RunPhase.Countdown;
            UpdateRawPinch(left, Handedness.Left, ref leftRawPinched);
            UpdateRawPinch(right, Handedness.Right, ref rightRawPinched);
            VisualSignal = recognizer.Update(new HandGestureSample
            {
                Time = Time.unscaledTime,
                GameplayEnabled = dominant.EverTracked,
                CommandsEnabled = (gameplay || TrainingActive) && dominant.EverTracked,
                DominantTracked = IsTracked(dominant),
                SupportTracked = IsTracked(support),
                DominantOpen = dominant.Open,
                DominantFist = dominant.Fist,
                SupportPausePose = support.PausePose,
                Pinch = dominant.Pinch,
                PinchViewPosition = ProjectToView(dominant.AnchorWorld, game.ViewCamera.transform)
            });

            if (gameplay || TrainingActive)
            {
                uiSelectHeld = false;
                uiPointer.Cancel();
            }
            else
            {
                // Gaze keeps the cursor visible; either hand may pinch to confirm.
                bool pinched = (IsTracked(left) && left.Pinch >= settings.Thresholds.PinchPress) ||
                    (IsTracked(right) && right.Pinch >= settings.Thresholds.PinchPress);
                bool released = (!IsTracked(left) || left.Pinch <= settings.Thresholds.PinchRelease) &&
                    (!IsTracked(right) || right.Pinch <= settings.Thresholds.PinchRelease);
                if (pinched) uiSelectHeld = true;
                else if (released) uiSelectHeld = false;
                uiPointer.UpdatePointer(GazePose(), uiSelectHeld, SupportScroll(support));
            }
            feedback.Present(VisualSignal, dominant.PinchPose, IsTracked(dominant));

            if (VisualSignal.Command != GameCommand.None)
            {
                GestureConfirmed?.Invoke(VisualSignal.Command, VisualSignal.Displacement.magnitude);
                if (!TrainingActive) game.SendCommand(VisualSignal.Command);
            }
            else if (VisualSignal.RequestAutoPause &&
                     !TrainingActive &&
                     (game.Phase == RunPhase.Running || game.Phase == RunPhase.Countdown))
                game.PauseForHandTracking();
        }

        float SupportScroll(HandFrame support)
        {
            bool pinching = IsTracked(support) && support.Pinch >= settings.Thresholds.PinchPress;
            if (!pinching || support.Pinch <= settings.Thresholds.PinchRelease)
            {
                supportScrollAnchored = false;
                return 0f;
            }
            if (!supportScrollAnchored)
            {
                supportScrollAnchor = support.AnchorWorld;
                supportScrollAnchored = true;
                return 0f;
            }
            // Vertical travel speed of the secondary pinch drags the active scroll page.
            float dt = Mathf.Max(Time.unscaledDeltaTime, .001f);
            float rise = Vector3.Dot(support.AnchorWorld - supportScrollAnchor,
                game.ViewCamera.transform.up) / dt;
            supportScrollAnchor = support.AnchorWorld;
            return Mathf.Clamp(rise * 3f, -1f, 1f);
        }

        void UpdateRawPinch(HandFrame frame, Handedness handedness, ref bool latched)
        {
            if (!IsTracked(frame))
            {
                latched = false;
                return;
            }
            if (!latched && frame.Pinch >= settings.Thresholds.PinchPress)
            {
                latched = true;
                RawPinchStarted?.Invoke(handedness);
            }
            else if (latched && frame.Pinch <= settings.Thresholds.PinchRelease)
            {
                latched = false;
            }
        }

        bool IsTracked(HandFrame frame)
        {
            return frame.Tracked && frame.UpdatedAt >= 0f &&
                   Time.unscaledTime - frame.UpdatedAt <= StaleTrackingSeconds;
        }

        void SetTracked(Handedness handedness, bool tracked)
        {
            HandFrame frame = handedness == Handedness.Left ? left : right;
            frame.Tracked = tracked;
            frame.EverTracked |= tracked;
            if (!tracked)
            {
                frame.Open = frame.Fist = frame.PausePose = false;
                frame.AnchorInitialized = false;
            }
        }

        Pose TrackingToWorld(Pose pose)
        {
            Transform space = game.TrackingSpace ? game.TrackingSpace : transform;
            return new Pose(space.TransformPoint(pose.position), space.rotation * pose.rotation);
        }

        Pose GazePose()
        {
            Transform view = game.ViewCamera.transform;
            Pose head = new Pose(view.position, view.rotation);
#if ENABLE_PICO_XR_SDK
            if (TryGetEyeGaze(out Pose eye)) return SelectGazePose(true, eye, head);
#endif
            return SelectGazePose(false, default, head);
        }

        public static Pose SelectGazePose(bool eyeValid, Pose eye, Pose head) => eyeValid ? eye : head;

#if ENABLE_PICO_XR_SDK
        static bool TryGetEyeGaze(out Pose pose)
        {
            pose = default;
            PXR_Manager manager = PXR_Manager.Instance;
            if (!manager || !manager.eyeTracking) return false;
            if (!PXR_EyeTracking.GetEyeGazeStatus(out bool status) || !status) return false;
            if (!PXR_EyeTracking.GetEyeGazePosition(out Vector3 position) ||
                !PXR_EyeTracking.GetEyeGazeRotation(out Quaternion rotation)) return false;
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z)) return false;
            pose = new Pose(position, rotation);
            return true;
        }
#endif

        static Vector3 SmoothAnchor(HandFrame frame, Vector3 target)
        {
            if (!frame.AnchorInitialized)
            {
                frame.AnchorInitialized = true;
                return target;
            }
            // Low-pass the direction anchor so joint jitter does not read as a lane change.
            float alpha = 1f - Mathf.Exp(-Mathf.Max(Time.unscaledDeltaTime, .001f) / AnchorSmoothingSeconds);
            return Vector3.Lerp(frame.AnchorWorld, target, alpha);
        }

        static Pose ResolveAnchor(XRHand hand, Pose pinchPose)
        {
            if (hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm)) return palm;
            if (hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose wrist)) return wrist;
            return pinchPose;
        }

        static bool TryGetPinchPose(XRHand hand, out Pose pose)
        {
            pose = Pose.identity;
            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumb) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose index)) return false;
            pose.position = Vector3.Lerp(thumb.position, index.position, .5f);
            pose.rotation = index.rotation;
            return true;
        }

        static bool TryGetPinchValue(XRHand hand, out float value)
        {
            value = 0f;
            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumb) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose index)) return false;
            value = Mathf.InverseLerp(.045f, .008f, Vector3.Distance(thumb.position, index.position));
            return true;
        }

        public static Vector2 ProjectToView(Vector3 worldPosition, Transform view)
        {
            return new Vector2(Vector3.Dot(worldPosition, view.right),
                Vector3.Dot(worldPosition, view.up));
        }

        void OnDestroy()
        {
            if (leftEvents) leftEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
            if (rightEvents) rightEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
        }
    }
}
