using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Riftline
{
    public sealed partial class RiftlineUI
    {
        private readonly Image[] lifeSegments = new Image[RunSimulation.MaxHealth];
        private readonly Image[] damageEdges = new Image[4];
        private Text lifeLabel;
        private Text lifeStatus;
        private Text routeLabel;
        private Text streakLabel;
        private Text phaseBinding;
        private Image progressFill;
        private Image cuePanel;
        private RawImage cueIcon;
        private Image feedbackPanel;
        private Text feedbackLabel;
        private float pickupAt = -10f;
        private float payoffAt = -10f;
        private float lastFeedbackClock;
        private GameCommand cueAction;
        private static readonly Color HudSurface = new Color(.15f, .23f, .34f, .91f);

        private OpeningFrame CurrentOpeningFrame()
        {
            RiftlineWorld world = game == null ? null : game.GetComponent<RiftlineWorld>();
            return world == null ? default(OpeningFrame) : world.Opening;
        }

        private RectTransform HudCard(string name, Vector2 minimum, Vector2 maximum)
        {
            Image card = ui.Panel(name, hudPanel, HudSurface,
                minimum, maximum, Vector2.zero, Vector2.zero);
            card.raycastTarget = false;
            return card.rectTransform;
        }

        private RectTransform HudRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = ui.Rect(name, parent, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, Vector2.zero);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private void BuildSurvivalHud()
        {
            hudPanel = HudRect("Run HUD", canvasRect, new Vector2(0, 650), new Vector2(1800, 170));
            hudPanel.localPosition += Vector3.back * .06f;
            RectTransform life = HudCard("Life Card", Vector2.zero, new Vector2(.24f, 1));
            lifeLabel = ui.FixedLabel("Life Count", life, string.Empty, 32, TextAnchor.MiddleLeft, RiftlineUIElements.White,
                new Vector2(.07f, .58f), new Vector2(.93f, .92f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            for (int i = 0; i < lifeSegments.Length; i++)
            {
                Image slot = ui.Panel("Life Slot " + i, life, new Color(.24f, .33f, .44f, 1f),
                    new Vector2(.07f + i * .29f, .35f), new Vector2(.33f + i * .29f, .51f),
                    Vector2.zero, Vector2.zero);
                slot.raycastTarget = false;
                lifeSegments[i] = ui.Panel("Life Fill", slot.transform, RiftlineUIElements.Red,
                    Vector2.zero, Vector2.one, new Vector2(1, 1), new Vector2(-1, -1));
                lifeSegments[i].raycastTarget = false;
            }
            lifeStatus = ui.FixedLabel("Life Status", life, string.Empty, 20, TextAnchor.MiddleLeft, RiftlineUIElements.Muted,
                new Vector2(.07f, .08f), new Vector2(.96f, .3f), Vector2.zero, Vector2.zero);

            RectTransform route = HudCard("Route Card", new Vector2(.25f, 0), new Vector2(.75f, 1));
            routeLabel = ui.FixedLabel("Region", route, string.Empty, 22, TextAnchor.MiddleLeft, RiftlineUIElements.Muted,
                new Vector2(.04f, .72f), new Vector2(.6f, .96f), Vector2.zero, Vector2.zero);
            hudPrimary = ui.FixedLabel("Distance And Score", route, string.Empty, 40, TextAnchor.MiddleLeft, RiftlineUIElements.White,
                new Vector2(.04f, .4f), new Vector2(.96f, .76f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            hudSecondary = ui.FixedLabel("Objective", route, string.Empty, 20, TextAnchor.MiddleLeft, RiftlineUIElements.Muted,
                new Vector2(.04f, .04f), new Vector2(.5f, .3f), Vector2.zero, Vector2.zero);
            streakLabel = ui.FixedLabel("Chip Chain", route, string.Empty, 22, TextAnchor.MiddleRight, RiftlineUIElements.Amber,
                new Vector2(.51f, .04f), new Vector2(.96f, .3f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            Image progress = ui.Bar("Route Progress", route, new Color(.24f, .33f, .44f), RiftlineUIElements.Amber, out progressFill);
            progress.raycastTarget = false;
            progress.rectTransform.anchorMin = new Vector2(.04f, .32f);
            progress.rectTransform.anchorMax = new Vector2(.96f, .39f);
            progress.rectTransform.offsetMin = progress.rectTransform.offsetMax = Vector2.zero;

            RectTransform phase = HudCard("Phase Card", new Vector2(.76f, 0), Vector2.one);
            phaseLabel = ui.FixedLabel("Phase Label", phase, string.Empty, 28, TextAnchor.MiddleLeft, RiftlineUIElements.Cyan,
                new Vector2(.07f, .6f), new Vector2(.94f, .95f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            Image meter = ui.Bar("Phase Meter", phase, new Color(.24f, .33f, .44f), RiftlineUIElements.Cyan, out phaseFill);
            meter.raycastTarget = false;
            meter.rectTransform.anchorMin = new Vector2(.07f, .36f);
            meter.rectTransform.anchorMax = new Vector2(.93f, .5f);
            meter.rectTransform.offsetMin = meter.rectTransform.offsetMax = Vector2.zero;
            phaseBinding = ui.FixedLabel("Phase Binding", phase, string.Empty, 20, TextAnchor.MiddleLeft, RiftlineUIElements.Muted,
                new Vector2(.07f, .07f), new Vector2(.96f, .31f), Vector2.zero, Vector2.zero);

            RectTransform cue = HudRect("Context Cue", canvasRect, new Vector2(-440, 415), new Vector2(800, 94));
            cuePanel = cue.gameObject.AddComponent<RiftlineUISurface>();
            ((RiftlineUISurface)cuePanel).Configure(18, 1.5f, new Color(.66f, .82f, .96f, .28f), true);
            cuePanel.color = HudSurface;
            cuePanel.raycastTarget = false;
            ui.Panel("Cue Accent", cue, RiftlineUIElements.Amber, new Vector2(0, .2f), new Vector2(0, .8f),
                new Vector2(12, 0), new Vector2(16, 0)).raycastTarget = false;
            RectTransform symbol = HudRect("Generated Action", cue, new Vector2(-330, 0), new Vector2(52, 52));
            cueIcon = symbol.gameObject.AddComponent<RawImage>();
            cueIcon.raycastTarget = false;
            hudTurn = ui.FixedLabel("Context Action", cue, string.Empty, 32, TextAnchor.MiddleLeft, RiftlineUIElements.White,
                new Vector2(.16f, .32f), new Vector2(.97f, .9f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            hudControls = ui.FixedLabel("Context Binding", cue, string.Empty, 20, TextAnchor.MiddleLeft, RiftlineUIElements.Muted,
                new Vector2(.16f, .08f), new Vector2(.97f, .36f), Vector2.zero, Vector2.zero);
            hudCountdown = ui.Label("Countdown", HudRect("Countdown Host", canvasRect, new Vector2(0, 120),
                new Vector2(500, 220)), string.Empty, 108, TextAnchor.MiddleCenter, RiftlineUIElements.Amber, FontStyle.Bold);

            RectTransform payoff = HudRect("Run Feedback", canvasRect, new Vector2(600, 415), new Vector2(600, 94));
            feedbackPanel = payoff.gameObject.AddComponent<RiftlineUISurface>();
            ((RiftlineUISurface)feedbackPanel).Configure(18, 1.5f, new Color(.66f, .82f, .96f, .28f), true);
            feedbackPanel.raycastTarget = false;
            feedbackLabel = ui.Label("Feedback", payoff, string.Empty, 26, TextAnchor.MiddleCenter,
                RiftlineUIElements.Amber, FontStyle.Bold);
            RectTransform edgeHost = HudRect("Damage Frame", canvasRect, new Vector2(0, 250), new Vector2(2320, 1280));
            Vector2[] minima = { Vector2.zero, new Vector2(.985f, 0), new Vector2(0, .985f), Vector2.zero };
            Vector2[] maxima = { new Vector2(.015f, 1), Vector2.one, Vector2.one, new Vector2(1, .015f) };
            for (int i = 0; i < damageEdges.Length; i++)
            {
                damageEdges[i] = ui.Panel("Damage Edge " + i, edgeHost, Color.clear, minima[i], maxima[i],
                    Vector2.zero, Vector2.zero);
                damageEdges[i].raycastTarget = false;
            }

            Image subtitle = ui.Panel("Event Subtitles", canvasRect, HudSurface,
                new Vector2(.14f, .07f), new Vector2(.86f, .14f), Vector2.zero, Vector2.zero);
            subtitle.raycastTarget = false;
            subtitleLabel = ui.Label("Cue", subtitle.transform, string.Empty, 23, TextAnchor.MiddleCenter, RiftlineUIElements.White);
            Image rest = ui.Panel("Rest Notification", canvasRect, HudSurface,
                new Vector2(.12f, .88f), new Vector2(.88f, .98f), Vector2.zero, Vector2.zero);
            rest.raycastTarget = false;
            restLabel = ui.Label("Reminder", rest.transform, string.Empty, 23, TextAnchor.MiddleCenter, RiftlineUIElements.Amber);
            subtitle.gameObject.SetActive(false);
            rest.gameObject.SetActive(false);
            hudPanel.gameObject.SetActive(false);
            hudCountdown.gameObject.SetActive(false);
            cue.gameObject.SetActive(false);
            payoff.gameObject.SetActive(false);
        }

        private void RefreshSurvivalHud()
        {
            RunSimulation run = game.Run;
            if (run == null || lifeLabel == null) return;
            lifeLabel.text = text.Get("health") + "   " + run.Health + " / " + RunSimulation.MaxHealth;
            lifeStatus.text = run.InvulnerableRemaining > 0
                ? text.Get("protected") + "  " + run.InvulnerableRemaining.ToString("0.0") + " s"
                : text.Get("lifeRule");
            lifeLabel.color = run.Health == 1 ? RiftlineUIElements.Red : RiftlineUIElements.White;
            routeLabel.text = text.Get("region" + run.RegionId) + "   ·   ×" + run.Multiplier.ToString("0.00");
            hudPrimary.text = run.Distance.ToString("N0") + " <size=24>m</size>     " +
                "<size=22>" + text.Get("score") + "</size> " + run.Score.ToString("N0");
            float length = run.ActiveContract == null ? 1f : run.ActiveContract.Length;
            OpeningFrame opening = CurrentOpeningFrame();
            hudSecondary.text = OpeningObjectiveCaption(opening, run.Endless);
            if (!run.Endless && Settings.FontScale <= 1.15f)
                hudSecondary.text += "  ·  " + Mathf.Max(0, length - run.Distance).ToString("N0") + " m";
            streakLabel.text = text.Get("chips") + " " + run.Chips + "   /   " + text.Get("streak") + " " + run.ChipStreak;
            ui.SetBarValue(progressFill, run.Endless ? 0f :
                opening.Active ? opening.Progress : run.Distance / Mathf.Max(1f, length));
            ui.SetBarValue(phaseFill, run.PhaseEnergy);
            phaseLabel.text = run.PhaseEnergy >= 1f ? text.Get("phaseReadyShort") :
                text.Get("phase") + "  " + Mathf.RoundToInt(run.PhaseEnergy * 100f) + "%";
            phaseBinding.text = run.PhaseEnergy >= 1f ? ActionBinding(GameCommand.Phase) :
                text.Localized("连收 / 擦身充能", "CHAINS / CLOSE CALLS");
            hudCountdown.gameObject.SetActive(game.Phase == RunPhase.Countdown);
            hudCountdown.text = Mathf.Clamp(Mathf.CeilToInt(run.CountdownRemaining), 1, 3).ToString();
            RefreshContextCue();
        }

        private string CommitHint()
        {
#if UNITY_EDITOR
            return text.Localized("A / D 或摇杆，提前锁定出口", "A / D OR STICK TO COMMIT");
#else
            return text.Localized("朝出口捏合短移，提前锁定", "PINCH TOWARD THE EXIT TO COMMIT");
#endif
        }

        private void RefreshContextCue()
        {
            if (cuePanel == null || game.Run == null) return;
            RunSimulation run = game.Run;
            Vector2 cueSize = cuePanel.rectTransform.sizeDelta;
            cueSize.y = 94f * Mathf.Max(1f, Settings.FontScale);
            cuePanel.rectTransform.sizeDelta = cueSize;
            bool active = game.Phase == RunPhase.Running;
            hudTurn.gameObject.SetActive(active);
            cuePanel.gameObject.SetActive(active);
            if (!active) return;
            cuePanel.color = HudSurface;
            TrackEvent turn = FindUpcomingTurn(run);
            string caption;
            cueAction = GameCommand.None;
            if (turn != null)
            {
                cueAction = turn.Kind == ObstacleKind.TurnLeft ? GameCommand.Left : GameCommand.Right;
                caption = turn.Kind == ObstacleKind.Fork ? text.Get("forkCue") :
                    text.Get(turn.Kind == ObstacleKind.TurnLeft ? "turnLeft" : "turnRight");
                hudControls.text = turn.Resolved ? text.Get(turn.TurnCommitted || turn.Kind == ObstacleKind.Fork ? "turnLocked" : "turnWrong")
                    : CommitHint();
            }
            else
            {
                TrackEvent hazard = run.Events.FirstOrDefault(item => !item.Passed && !item.HitRecorded
                    && run.IsEventActive(item) && TrackGenerator.IsHazard(item.Kind) && item.Lane == run.Lane
                    && item.Distance >= run.Distance && item.Distance - run.Distance <= run.Speed * 2.2f);
                if (hazard != null)
                {
                    cueAction = hazard.RequiredAction;
                    if (cueAction == GameCommand.None)
                    {
                        if (hazard.Kind == ObstacleKind.Gap) cueAction = GameCommand.Jump;
                        else cueAction = hazard.SafeLane < run.Lane ? GameCommand.Left : GameCommand.Right;
                    }
                    caption = text.Action(cueAction);
                    hudControls.text = ActionBinding(cueAction);
                }
                else
                {
                    OpeningFrame opening = CurrentOpeningFrame();
                    caption = OpeningObjectiveCaption(opening, run.Endless);
                    hudControls.text = OpeningObjectiveDetail(opening, run.Endless);
                    float pulse = Mathf.Max(opening.ComboPulse, opening.DamagePulse, opening.PhasePulse, opening.LandingPulse);
                    float pressure = Mathf.Clamp01(opening.Threat * .35f + pulse * .2f);
                    cuePanel.color = Color.Lerp(HudSurface,
                        new Color(.38f, .19f, .24f, .94f), pressure);
                    cuePanel.gameObject.SetActive(opening.Active);
                }
            }
            hudTurn.text = caption;
            Texture2D atlas = game.Assets == null ? null : game.Assets.ActionIcons;
            Rect crop;
            bool showIcon = cueAction != GameCommand.None && atlas != null && iconCrops.TryGetValue(cueAction, out crop);
            cueIcon.gameObject.SetActive(showIcon);
            if (showIcon)
            {
                cueIcon.texture = atlas;
                cueIcon.uvRect = iconCrops[cueAction];
            }
        }

        private string OpeningObjectiveCaption(OpeningFrame opening, bool endless)
        {
            if (endless) return text.Get("objectiveEndless");
            switch (opening.Objective)
            {
                case OpeningObjective.ReachExtraction: return text.Get("objectiveReachExtraction");
                case OpeningObjective.Extracted: return text.Get("objectiveExtracted");
                case OpeningObjective.RecoverArchive:
                default: return text.Get("objectiveRecoverArchive");
            }
        }

        private string OpeningObjectiveDetail(OpeningFrame opening, bool endless)
        {
            if (endless) return text.Get("objectiveEndlessDetail");
            return opening.Objective == OpeningObjective.RecoverArchive
                ? text.Get("objectiveRecoverDetail")
                : text.Get("objectiveExtractionDetail");
        }

        private void OnRunFeedback(FeedbackKind kind)
        {
            if (feedbackPanel == null || game.Run == null) return;
            RunSimulation run = game.Run;
            if (kind == FeedbackKind.Chip) pickupAt = run.Elapsed;
            if (kind == FeedbackKind.Damage || kind == FeedbackKind.Combo || kind == FeedbackKind.NearMiss ||
                kind == FeedbackKind.PhaseReady || kind == FeedbackKind.Phase || kind == FeedbackKind.Archive)
            {
                payoffAt = run.Elapsed;
                feedbackLabel.color = kind == FeedbackKind.Damage ? RiftlineUIElements.Red :
                    kind == FeedbackKind.Phase || kind == FeedbackKind.PhaseReady || kind == FeedbackKind.Archive
                        ? RiftlineUIElements.Cyan : RiftlineUIElements.Amber;
                feedbackLabel.text = kind == FeedbackKind.Damage ? text.Get("damaged") :
                    kind == FeedbackKind.Combo ? run.LastStreakReward + "  " + text.Get("streakReward") :
                    kind == FeedbackKind.NearMiss ? text.Get("subtitleNearMiss") :
                    kind == FeedbackKind.PhaseReady ? text.Get("phaseReadyShort") :
                    kind == FeedbackKind.Archive ? text.Get(run.Endless ? "archiveEndless" : "archiveGoalSwitch") :
                    text.Get("subtitlePhase");
            }
            if (kind == FeedbackKind.Damage || kind == FeedbackKind.Chip || kind == FeedbackKind.Combo ||
                kind == FeedbackKind.Archive)
                RefreshSurvivalHud();
        }

        private void RefreshRunFeedback()
        {
            if (game.Run == null || feedbackPanel == null) return;
            RunSimulation run = game.Run;
            if (run.Elapsed < lastFeedbackClock)
            {
                pickupAt = payoffAt = -10f;
            }
            lastFeedbackClock = run.Elapsed;
            bool active = game.Phase == RunPhase.Running || game.Phase == RunPhase.Countdown;
            float hitAge = run.Elapsed - run.LastDamageTime;
            float hitFade = active ? Mathf.Clamp01(1f - hitAge / .65f) : 0f;
            for (int i = 0; i < lifeSegments.Length; i++)
            {
                Color fill = i < run.Health ? RiftlineUIElements.Red : Color.clear;
                if (i == run.Health && run.DamageCount > 0 && hitAge < .4f)
                    fill = new Color(1, .48f, .5f, Mathf.Clamp01(1 - hitAge / .4f));
                lifeSegments[i].color = fill;
            }
            foreach (Image edge in damageEdges)
                edge.color = new Color(1f, .15f, .22f, hitFade * (Settings.ReducedFlash ? .22f : .55f));
            float pickupAge = run.Elapsed - pickupAt;
            streakLabel.color = pickupAge < .2f ? RiftlineUIElements.White : RiftlineUIElements.Amber;
            float payoffAge = run.Elapsed - payoffAt;
            feedbackPanel.gameObject.SetActive(active && payoffAge >= 0 && payoffAge < 1.25f);
            if (feedbackPanel.gameObject.activeSelf)
            {
                float fade = Mathf.Clamp01((1.25f - payoffAge) / .25f);
                Color surface = HudSurface;
                surface.a *= fade;
                feedbackPanel.color = surface;
                Color ink = feedbackLabel.color;
                ink.a = fade;
                feedbackLabel.color = ink;
            }
        }
    }
}
