using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

namespace Riftline
{
    public sealed class RiftlineHandOnboarding : MonoBehaviour
    {
        enum Step
        {
            Inactive,
            ChooseHand,
            Left,
            Right,
            Jump,
            Slide,
            Phase,
            Pause
        }

        readonly List<float> directionDistances = new List<float>(4);
        RiftlineGame game;
        RiftlineHandInput input;
        RectTransform panel;
        Text title;
        Text instruction;
        Text progress;
        Step step;

        public string CurrentStep => step.ToString();
        public bool Active => step != Step.Inactive;

        public void Initialize(RiftlineGame owner, RiftlineHandInput handInput, Canvas canvas)
        {
            game = owner;
            input = handInput;
            BuildVisuals(canvas);
            input.RawPinchStarted += OnRawPinch;
            input.GestureConfirmed += OnGesture;
#if !UNITY_EDITOR
            if (!game.Save.Settings.HandTutorialComplete) Begin();
#endif
        }

        public void Begin()
        {
            if (game == null || input == null || panel == null) return;
            directionDistances.Clear();
            step = Step.ChooseHand;
            input.TrainingActive = true;
            input.ResetEdges();
            panel.gameObject.SetActive(true);
            Refresh();
        }

        void OnRawPinch(Handedness handedness)
        {
            if (step != Step.ChooseHand) return;
            input.SelectDominantHand(handedness);
            step = Step.Left;
            Refresh();
        }

        void OnGesture(GameCommand command, float displacement)
        {
            if (!Active || step == Step.ChooseHand) return;
            if (command != ExpectedCommand()) return;
            if (command == GameCommand.Left || command == GameCommand.Right ||
                command == GameCommand.Jump || command == GameCommand.Slide)
                directionDistances.Add(displacement);

            if (step == Step.Pause)
            {
                Complete();
                return;
            }
            step++;
            input.ResetEdges();
            Refresh();
        }

        GameCommand ExpectedCommand()
        {
            switch (step)
            {
                case Step.Left: return GameCommand.Left;
                case Step.Right: return GameCommand.Right;
                case Step.Jump: return GameCommand.Jump;
                case Step.Slide: return GameCommand.Slide;
                case Step.Phase: return GameCommand.Phase;
                case Step.Pause: return GameCommand.Pause;
                default: return GameCommand.None;
            }
        }

        void Complete()
        {
            directionDistances.Sort();
            float median = directionDistances.Count == 0 ? .035f :
                directionDistances.Count % 2 == 0
                    ? (directionDistances[directionDistances.Count / 2 - 1] +
                       directionDistances[directionDistances.Count / 2]) * .5f
                    : directionDistances[directionDistances.Count / 2];
            game.Save.Settings.HandDirectionDistance = Mathf.Clamp(median * .75f, .025f, .06f);
            game.Save.Settings.HandTutorialComplete = true;
            input.TrainingActive = false;
            input.ApplySettings();
            game.SaveSettings();
            step = Step.Inactive;
            panel.gameObject.SetActive(false);
        }

        void Refresh()
        {
            bool zh = game.Save.Settings.Language != "en";
            title.text = zh ? "手势校准" : "HAND CALIBRATION";
            instruction.text = Instruction(zh);
            progress.text = (int)step + " / 7";
        }

        string Instruction(bool zh)
        {
            switch (step)
            {
                case Step.ChooseHand: return zh ? "用你偏好的主手捏合" : "Pinch with your preferred hand";
                case Step.Left: return zh ? "捏合并向左短移" : "Pinch and move left";
                case Step.Right: return zh ? "捏合并向右短移" : "Pinch and move right";
                case Step.Jump: return zh ? "捏合并向上短移" : "Pinch and move up";
                case Step.Slide: return zh ? "捏合并向下短移" : "Pinch and move down";
                case Step.Phase: return zh ? "张开主手，然后握拳" : "Open your hand, then make a fist";
                case Step.Pause: return zh ? "抬起副手，掌心向上" : "Raise your support hand, palm up";
                default: return string.Empty;
            }
        }

        void BuildVisuals(Canvas canvas)
        {
            var root = new GameObject("Hand Onboarding", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            panel = root.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(900f, 380f);
            panel.anchoredPosition = Vector2.zero;
            var background = root.GetComponent<Image>();
            background.color = new Color(.025f, .055f, .09f, .98f);
            background.raycastTarget = true;
            title = Label("Title", panel, 36, new Vector2(.08f, .7f), new Vector2(.92f, .92f),
                FontStyle.Bold);
            instruction = Label("Instruction", panel, 30, new Vector2(.08f, .28f), new Vector2(.92f, .68f),
                FontStyle.Normal);
            progress = Label("Progress", panel, 22, new Vector2(.08f, .08f), new Vector2(.92f, .24f),
                FontStyle.Normal);
            panel.gameObject.SetActive(false);
        }

        static Text Label(string labelName, Transform parent, int size, Vector2 min, Vector2 max,
            FontStyle style)
        {
            var child = new GameObject(labelName, typeof(RectTransform), typeof(Text));
            child.transform.SetParent(parent, false);
            var text = child.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = min;
            text.rectTransform.anchorMax = max;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            return text;
        }

        void OnDestroy()
        {
            if (input != null)
            {
                input.RawPinchStarted -= OnRawPinch;
                input.GestureConfirmed -= OnGesture;
                input.TrainingActive = false;
            }
        }
    }
}
