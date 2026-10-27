using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace Riftline
{
    public sealed class RiftlineInput : MonoBehaviour
    {
        RiftlineGame game;
        InputAction stick, jump, slide, phaseLeft, phaseRight, phaseKeyboard, pause, comfort;
        InputAction uiStickLeft, uiStickRight;
        bool stickArmed = true, triggerArmed = true;
        float recenterHeld;
        RiftlineUIPointer uiPointer;
        readonly Transform[] controllerRoots = new Transform[2];
        GameObject controllerSource;
        Transform wrist;
        Image wristFill, wristBackground;
        Text wristText;
        float restNoticeUntil;

        public void Initialize(RiftlineGame owner)
        {
            game = owner;
            stick = new InputAction("Riftline Steer", InputActionType.Value);
            stick.AddBinding("<XRController>{LeftHand}/primary2DAxis");
            stick.AddBinding("<XRController>{RightHand}/primary2DAxis");
            stick.AddCompositeBinding("2DVector").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            stick.AddCompositeBinding("2DVector").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            jump = Button("Jump", "<Keyboard>/space", "<XRController>{RightHand}/primaryButton", "<XRController>{LeftHand}/primaryButton");
            slide = Button("Slide", "<Keyboard>/s", "<Keyboard>/downArrow", "<Keyboard>/leftCtrl", "<Keyboard>/rightCtrl",
                "<XRController>{RightHand}/secondaryButton", "<XRController>{LeftHand}/secondaryButton");
            pause = Button("Pause", "<Keyboard>/escape", "<XRController>{LeftHand}/menuButton");
            comfort = Button("Comfort", "<Keyboard>/f2", "<XRController>{LeftHand}/primary2DAxisClick", "<XRController>{RightHand}/primary2DAxisClick");
            phaseLeft = new InputAction("Left Phase", InputActionType.Value, "<XRController>{LeftHand}/trigger");
            phaseRight = new InputAction("Right Phase", InputActionType.Value, "<XRController>{RightHand}/trigger");
            phaseKeyboard = Button("Editor Phase", "<Keyboard>/leftShift", "<Keyboard>/rightShift", "<Keyboard>/e");
            uiStickLeft = new InputAction("Left UI Scroll", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis");
            uiStickRight = new InputAction("Right UI Scroll", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
            foreach (var action in All()) action.Enable();
            uiPointer = game.GetComponent<RiftlineUIPointer>() ??
                game.gameObject.AddComponent<RiftlineUIPointer>();
            uiPointer.Initialize(game);
            BuildWristMeter();
            game.RestReminderRaised += OnRestReminder;
        }

        static InputAction Button(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (string binding in bindings) action.AddBinding(binding);
            return action;
        }

        IEnumerable<InputAction> All()
        {
            yield return stick; yield return jump; yield return slide; yield return phaseLeft;
            yield return phaseRight; yield return pause; yield return comfort;
            yield return phaseKeyboard; yield return uiStickLeft; yield return uiStickRight;
        }

        public void ResetEdges()
        {
            stickArmed = Mathf.Abs(stick.ReadValue<Vector2>().x) < .25f;
            triggerArmed = Mathf.Max(phaseLeft.ReadValue<float>(), phaseRight.ReadValue<float>()) < .25f;
            recenterHeld = 0;
            CancelUiPointer();
        }

        public void Poll()
        {
            if (pause.WasPressedThisFrame()) { CancelUiPointer(); game.SendCommand(GameCommand.Pause); return; }
            if (comfort.WasPressedThisFrame()) { game.CycleComfort(); return; }
            bool running = game.Phase == RunPhase.Running;
            bool uiPhase = game.Phase == RunPhase.Menu || game.Phase == RunPhase.Paused ||
                           game.Phase == RunPhase.Failed || game.Phase == RunPhase.Completed;
            if (!running)
            {
                if (jump.IsPressed() && (game.Phase == RunPhase.Menu || game.Phase == RunPhase.Paused))
                {
                    recenterHeld += Time.unscaledDeltaTime;
                    if (recenterHeld >= 1f)
                    {
                        CancelUiPointer();
                        game.Recenter();
                        recenterHeld = -100f;
                    }
                }
                else recenterHeld = 0;
                if (uiPhase) PollUiRay(); else CancelUiPointer();
                if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    var ui = game.GetComponentInChildren<RiftlineUI>();
                    if (ui != null) ui.TryKeyboardShortcut();
                }
                return;
            }
            CancelUiPointer();
            float axis = stick.ReadValue<Vector2>().x;
            if (Mathf.Abs(axis) < .25f) stickArmed = true;
            bool steerPressed = stickArmed && Mathf.Abs(axis) > .6f;
            // Consume the physical edge even when a higher-priority action wins this frame.
            if (steerPressed) stickArmed = false;
            float leftTrigger = phaseLeft.ReadValue<float>();
            float rightTrigger = phaseRight.ReadValue<float>();
            float trigger = game.Save.Settings.TriggerHand == 1 ? leftTrigger :
                game.Save.Settings.TriggerHand == 2 ? rightTrigger : Mathf.Max(leftTrigger, rightTrigger);
            if (trigger < .25f) triggerArmed = true;
            bool keyboardPhase = phaseKeyboard.WasPressedThisFrame();
            if (keyboardPhase || (triggerArmed && trigger > .65f))
            {
                if (!keyboardPhase) triggerArmed = false;
                game.SendCommand(GameCommand.Phase);
                return;
            }
            bool context = game.Run.HasDirectionContext;
            if (steerPressed && context)
            {
                stickArmed = false;
                game.SendCommand(axis < 0 ? GameCommand.Left : GameCommand.Right);
                return;
            }
            bool jumpPressed = jump.WasPressedThisFrame();
            bool slidePressed = slide.WasPressedThisFrame();
            if (game.Save.Settings.SwapJumpSlide) (jumpPressed, slidePressed) = (slidePressed, jumpPressed);
            if (jumpPressed) { game.SendCommand(GameCommand.Jump); return; }
            if (slidePressed) { game.SendCommand(GameCommand.Slide); return; }
            if (steerPressed)
            {
                stickArmed = false;
                game.SendCommand(axis < 0 ? GameCommand.Left : GameCommand.Right);
            }
        }

        void PollUiRay()
        {
            XRNode hand = game.Save.Settings.LeftHanded ? XRNode.LeftHand : XRNode.RightHand;
            if (!game.TryGetControllerPose(hand, out Pose pose)) { CancelUiPointer(); return; }
            float trigger = hand == XRNode.LeftHand ? phaseLeft.ReadValue<float>() : phaseRight.ReadValue<float>();
            bool held = trigger > .65f || jump.IsPressed();
            float y = (hand == XRNode.LeftHand ? uiStickLeft : uiStickRight).ReadValue<Vector2>().y;
            uiPointer.UpdatePointer(pose, held, y);
        }

        void CancelUiPointer() => uiPointer?.Cancel();

        void LateUpdate()
        {
            if (game == null) return;
            UpdateControllerVisuals();
            UpdateWristMeter();
        }

        void UpdateControllerVisuals()
        {
            GameObject source = game.Assets ? game.Assets.ControllerPrefab : null;
            if (controllerSource != source)
            {
                controllerSource = source;
                for (int i = 0; i < controllerRoots.Length; i++)
                {
                    if (controllerRoots[i]) Destroy(controllerRoots[i].gameObject);
                    controllerRoots[i] = null;
                    if (!source) continue;
                    var root = new GameObject(i == 0 ? "Left Controller Visual" : "Right Controller Visual");
                    root.transform.SetParent(game.transform, false);
                    controllerRoots[i] = root.transform;
                    Instantiate(game.Assets.ControllerPrefab, root.transform);
                }
            }
            var nodes = new[] { XRNode.LeftHand, XRNode.RightHand };
            for (int i = 0; i < controllerRoots.Length; i++)
            {
                if (!controllerRoots[i]) continue;
                bool valid = game.TryGetControllerPose(nodes[i], out Pose pose);
                controllerRoots[i].gameObject.SetActive(valid);
                if (valid) controllerRoots[i].SetPositionAndRotation(pose.position, pose.rotation);
            }
        }

        void BuildWristMeter()
        {
            var root = new GameObject("Main Hand Phase Meter", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(game.transform, false);
            wrist = root.transform;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = game.ViewCamera;
            canvas.sortingOrder = 30;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220, 76);
            rect.localScale = Vector3.one * .001f;
            wristBackground = MeterImage("Phase Background", wrist, new Color(.01f, .03f, .07f, .95f), Vector2.zero, Vector2.one);
            MeterImage("Phase Track", wrist, new Color(.08f, .15f, .2f, 1), new Vector2(.06f, .16f), new Vector2(.94f, .4f));
            var track = wrist.GetChild(wrist.childCount - 1);
            wristFill = MeterImage("Phase Charge", track, new Color(.12f, 1, 1), Vector2.zero, Vector2.one);
            var label = new GameObject("Phase Readout", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(wrist, false);
            wristText = label.GetComponent<Text>();
            wristText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            wristText.fontSize = 23;
            wristText.alignment = TextAnchor.MiddleCenter;
            wristText.raycastTarget = false;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(.04f, .46f);
            labelRect.anchorMax = new Vector2(.96f, .96f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        }

        static Image MeterImage(string name, Transform parent, Color color, Vector2 minimum, Vector2 maximum)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var image = root.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.anchorMin = minimum;
            image.rectTransform.anchorMax = maximum;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            return image;
        }

        void UpdateWristMeter()
        {
            bool left = game.Save.Settings.LeftHanded;
            XRNode hand = left ? XRNode.LeftHand : XRNode.RightHand;
            bool tracked = game.TryGetControllerPose(hand, out Pose pose);
            if (!tracked)
            {
                wrist.gameObject.SetActive(false);
                return;
            }
            Vector3 position = tracked ? pose.position + pose.rotation * new Vector3(0, .085f, .03f) : pose.position;
            Vector3 forward = position - game.ViewCamera.transform.position;
            Quaternion rotation = forward.sqrMagnitude > .0001f
                ? Quaternion.LookRotation(forward, game.ViewCamera.transform.up) : pose.rotation;
            wrist.SetPositionAndRotation(position, rotation);
            bool reminder = Time.unscaledTime < restNoticeUntil;
            wrist.gameObject.SetActive(game.Phase != RunPhase.Menu || reminder);
            float energy = Mathf.Clamp01(game.Run.PhaseEnergy);
            wristFill.rectTransform.anchorMax = new Vector2(energy, 1);
            bool contrast = game.Save.Settings.HighContrast;
            wristFill.color = contrast ? new Color(.3f, 1, 1) : new Color(.06f, .7f, .82f);
            wristText.color = contrast ? Color.white : new Color(.65f, .85f, .9f);
            wristText.fontSize = Mathf.RoundToInt(23 * Mathf.Clamp(game.Save.Settings.FontScale, .8f, 1.4f));
            wristBackground.color = new Color(.01f, .03f, .07f, contrast ? 1 : .85f);
            string phase = game.Save.Settings.Language == "zh" ? "\u76f8\u4f4d" : "PHASE";
            wristText.text = reminder ? game.Save.Settings.Language == "zh" ? "\u5efa\u8bae\u4f11\u606f 20 MIN" : "REST 20 MIN" :
                phase + "  " + (game.Run.PhaseRemaining > 0
                ? game.Run.PhaseRemaining.ToString("0.0") + "s" : Mathf.RoundToInt(energy * 100) + "%");
        }

        void OnRestReminder() => restNoticeUntil = Time.unscaledTime + 8f;

        public void Haptic(FeedbackKind feedback)
        {
            if (!game.Save.Settings.Haptics) return;
            float duration = feedback == FeedbackKind.Fail || feedback == FeedbackKind.Damage ? .09f : feedback == FeedbackKind.Phase ? .13f :
                feedback == FeedbackKind.Jump ? .07f : feedback == FeedbackKind.Slide ? .05f : .025f;
            float amplitude = (feedback == FeedbackKind.Fail || feedback == FeedbackKind.Damage ? .8f : feedback == FeedbackKind.Phase ? .65f : .3f) * game.Save.Settings.HapticIntensity;
            foreach (XRNode node in new[] { XRNode.LeftHand, XRNode.RightHand })
            {
                var device = InputDevices.GetDeviceAtXRNode(node);
                if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
                    device.SendHapticImpulse(0, amplitude, duration);
            }
        }

        void OnDestroy()
        {
            foreach (var action in All()) action?.Dispose();
            if (game != null) game.RestReminderRaised -= OnRestReminder;
            CancelUiPointer();
            if (wrist) Destroy(wrist.gameObject);
            foreach (var root in controllerRoots) if (root) Destroy(root.gameObject);
        }
    }
}
