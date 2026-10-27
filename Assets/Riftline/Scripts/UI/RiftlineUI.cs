using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Riftline
{
    [DisallowMultipleComponent]
    public sealed partial class RiftlineUI : MonoBehaviour
    {
        private enum Page
        {
            Home,
            Contracts,
            Outfits,
            Archives,
            Settings,
            Pause,
            Results
        }

        private const float CanvasScale = .0015f;
        private const float HudInterval = .1f;
        private const float FailureTransition = .15f;
        private const float ReplaySeconds = 4f;

        private struct RunnerFrame
        {
            public float Elapsed;
            public float Distance;
            public Vector3 Center;
            public float LaneX;
            public float Height;
            public int Heading;
            public bool Sliding;
            public bool Phasing;
        }

        private RiftlineGame game;
        private RiftlineUIElements ui;
        private RiftlineUILocalization text;
        private RectTransform canvasRect;
        private RectTransform mainPanel;
        private RectTransform pageHost;
        private RectTransform hudPanel;
        private Text hudPrimary;
        private Text hudSecondary;
        private Text hudCountdown;
        private Text hudTurn;
        private Text hudControls;
        private Text phaseLabel;
        private static readonly GameCommand[] HudActions =
        {
            GameCommand.Left, GameCommand.Right, GameCommand.Jump,
            GameCommand.Slide, GameCommand.Phase, GameCommand.Pause
        };
        private readonly Dictionary<GameCommand, Rect> iconCrops = new Dictionary<GameCommand, Rect>();
        private Text subtitleLabel;
        private Text restLabel;
        private Text pauseSession;
        private Text pauseRest;
        private string subtitleKey;
        private float subtitleUntil;
        private float restUntil;
        private bool restWasSuggested;
        private string observedHint = string.Empty;
        private float failureResultsAt;
        private bool failurePanelShown;
        private RunSimulation subscribedRun;
        private readonly List<RunnerFrame> runnerFrames = new List<RunnerFrame>();
        private readonly Dictionary<int, TrackEvent> replayEvents = new Dictionary<int, TrackEvent>();
        private RunnerFrame[] replayFrames;
        private RectTransform replayStage;
        private Transform replayRunner;
        private GameObject replayEve;
        private Text replayCaption;
        private float replayBegan;
        private Vector3 replayOrigin;
        private int replayHeading;
        private Vector2 replayMinimum;
        private Vector2 replayOffset;
        private float replayScale;
        private readonly Dictionary<string, float> scrollPositions = new Dictionary<string, float>();
        private bool resetScroll = true;
        private Image phaseFill;
        private GameObject createdEventSystem;
        private InputSystemUIInputModule createdInputModule;
        private GameObject outfitPreview;
        private Page page = Page.Home;
        private Page settingsReturnPage = Page.Home;
        private RunPhase observedPhase;
        private bool initialized;
        private bool rebuildRequested;
        private float nextHudUpdate;
        private int selectedRegion;
        private int selectedArchive = -1;
        private int visibleButtonCount;

        public Canvas Canvas { get; private set; }
        public RectTransform Panel { get { return mainPanel; } }
        public string CurrentPage { get { return page.ToString(); } }
        public int VisibleButtonCount { get { return visibleButtonCount; } }
        public bool ResultsVisible { get { return initialized && page == Page.Results && mainPanel.gameObject.activeSelf; } }

        public void Initialize(RiftlineGame owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (initialized)
            {
                if (game == owner)
                {
                    RequestRebuild();
                    return;
                }
                Unsubscribe();
                DestroyOwnedObjects();
            }

            game = owner;
            // Actual Generator atlas: three columns and three rows, with inset card bounds.
            SetActionIconCrop(GameCommand.Jump, new Rect(.11f, .665f, .225f, .215f));
            SetActionIconCrop(GameCommand.Slide, new Rect(.66f, .665f, .225f, .215f));
            SetActionIconCrop(GameCommand.Phase, new Rect(.385f, .39f, .225f, .215f));
            SetActionIconCrop(GameCommand.Left, new Rect(.1455078f, .4472656f, .080078125f, .109375f));
            SetActionIconCrop(GameCommand.Right, new Rect(.7734375f, .4472656f, .080078125f, .109375f));
            SetActionIconCrop(GameCommand.Pause, new Rect(.66f, .125f, .225f, .215f));
            text = new RiftlineUILocalization(Settings.Language);
            ui = new RiftlineUIElements(() => Mathf.Clamp(Settings.FontScale, .8f, 1.4f), () => visibleButtonCount++);
            BuildCanvas();
            EnsureEventSystem();
            RiftlineHandInput handInput = game.GetComponent<RiftlineHandInput>();
            var handOnboarding = game.GetComponent<RiftlineHandOnboarding>() ??
                game.gameObject.AddComponent<RiftlineHandOnboarding>();
            handOnboarding.Initialize(game, handInput, Canvas);
            game.Changed += OnGameChanged;
            observedPhase = game.Phase;
            page = PageForPhase(observedPhase, Page.Home);
            initialized = true;
            failureResultsAt = Time.unscaledTime + FailureTransition;
            BindRun();
            Rebuild();
        }

        public bool TryKeyboardShortcut()
        {
            if (!initialized) return false;
            GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(Canvas.transform) &&
                ExecuteEvents.GetEventHandler<ISubmitHandler>(selected) != null)
                return false;
            if (page == Page.Home && game.Phase == RunPhase.Menu) game.StartRecommendedContract();
            else if (page == Page.Pause && game.Phase == RunPhase.Paused) game.ResumeRun();
            else if (ResultsVisible && (game.Phase == RunPhase.Failed || game.Phase == RunPhase.Completed))
                return TryStartPrimaryResultAction();
            else return false;
            return true;
        }

        public void OpenPage(string pageName)
        {
            TryOpenPage(pageName);
        }

        public bool TryOpenPage(string pageName)
        {
            if (!initialized || string.IsNullOrWhiteSpace(pageName))
            {
                return false;
            }

            Page requested;
            if (!Enum.TryParse(pageName, true, out requested) || !Enum.IsDefined(typeof(Page), requested))
            {
                return false;
            }
            RunPhase phase = game.Phase;
            bool allowed = phase == RunPhase.Menu && requested != Page.Pause && requested != Page.Results ||
                           phase == RunPhase.Paused && (requested == Page.Pause || requested == Page.Settings) ||
                           (phase == RunPhase.Failed || phase == RunPhase.Completed) && requested == Page.Results &&
                           (phase != RunPhase.Failed || Time.unscaledTime >= failureResultsAt);
            if (!allowed)
            {
                return false;
            }

            if (requested == Page.Settings && page != Page.Settings)
            {
                settingsReturnPage = game.Phase == RunPhase.Paused ? Page.Pause : page;
            }
            resetScroll = requested != page;
            page = requested;
            Rebuild();
            return true;
        }

        public bool ScrollPage(float normalizedDelta)
        {
            if (!initialized || !mainPanel.gameObject.activeSelf || float.IsNaN(normalizedDelta) || float.IsInfinity(normalizedDelta))
            {
                return false;
            }
            ScrollRect scroll = pageHost.GetComponentInChildren<ScrollRect>();
            if (scroll == null)
            {
                return false;
            }
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + normalizedDelta);
            return true;
        }

        public bool SetActionIconCrop(GameCommand command, Rect crop)
        {
            if (!HudActions.Contains(command) || crop.width <= 0f || crop.height <= 0f ||
                crop.xMin < 0f || crop.yMin < 0f || crop.xMax > 1f || crop.yMax > 1f ||
                float.IsNaN(crop.x + crop.y + crop.width + crop.height))
            {
                return false;
            }
            iconCrops[command] = crop;
            if (initialized)
            {
                RefreshActionIcons();
            }
            return true;
        }

        private GameSettings Settings
        {
            get
            {
                if (game.Save.Settings == null)
                {
                    game.Save.Settings = new GameSettings();
                }
                return game.Save.Settings;
            }
        }

        private void Update()
        {
            if (!initialized || game == null)
            {
                return;
            }

            BindRun();
            ObservePhase();
            RunPhase phase = game.Phase;
            if (phase == RunPhase.Running)
            {
                CaptureRunnerFrame();
            }
            if (phase == RunPhase.Failed && !failurePanelShown && Time.unscaledTime >= failureResultsAt)
            {
                failurePanelShown = true;
                RequestRebuild();
            }

            if (rebuildRequested)
            {
                rebuildRequested = false;
                Rebuild();
            }

            RefreshNotifications();
            RefreshRunFeedback();
            UpdateReplay();
            if ((phase == RunPhase.Running || phase == RunPhase.Countdown) && Time.unscaledTime >= nextHudUpdate)
            {
                nextHudUpdate = Time.unscaledTime + HudInterval;
                RefreshSurvivalHud();
                RefreshHint();
            }
        }

        private void ObservePhase()
        {
            RunPhase phase = game.Phase;
            if (phase != observedPhase)
            {
                RunPhase previous = observedPhase;
                observedPhase = phase;
                if (phase == RunPhase.Countdown)
                {
                    if (previous == RunPhase.Menu || previous == RunPhase.Failed || previous == RunPhase.Completed)
                    {
                        ResetReplayCapture();
                    }
                }
                if (phase == RunPhase.Failed)
                {
                    CaptureRunnerFrame();
                    failureResultsAt = Time.unscaledTime + FailureTransition;
                    failurePanelShown = false;
                }
                else if (phase == RunPhase.Completed)
                {
                    CaptureRunnerFrame();
                }
                page = PageForPhase(phase, page);
                if (phase == RunPhase.Menu && previous != RunPhase.Menu)
                {
                    page = Page.Home;
                }
                resetScroll = true;
                RequestRebuild();
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            DestroyOwnedObjects();
        }

        private void BuildCanvas()
        {
            GameObject canvasObject = new GameObject("Riftline World UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1280f, 850f);
            canvasRect.localScale = Vector3.one * CanvasScale;
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;

            Canvas = canvasObject.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.WorldSpace;
            Canvas.worldCamera = game.ViewCamera;
            Canvas.sortingOrder = 20;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;

            Image panelImage = ui.Panel("Main Panel", canvasRect, RiftlineUIElements.Navy, Vector2.zero, Vector2.one,
                new Vector2(12f, 12f), new Vector2(-12f, -12f));
            ((RiftlineUISurface)panelImage).Configure(24, 1.5f, new Color(.66f, .82f, .96f, .3f), true);
            mainPanel = panelImage.rectTransform;
            mainPanel.anchorMin = new Vector2(.5f, .5f);
            mainPanel.anchorMax = new Vector2(.5f, .5f);
            mainPanel.anchoredPosition = new Vector2(0, 250);
            mainPanel.sizeDelta = new Vector2(1920f, 1050f);

            Image inner = ui.Panel("Inner Panel", mainPanel, RiftlineUIElements.NavyRaised,
                Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));
            ((RiftlineUISurface)inner).Configure(20, 1, new Color(.66f, .82f, .96f, .18f), false);
            ui.Panel("Top Accent", inner.transform, new Color(.62f, .85f, .95f, .45f),
                new Vector2(.04f, 1), new Vector2(.18f, 1), new Vector2(0, -3), new Vector2(0, -1)).raycastTarget = false;
            pageHost = ui.Rect("Page Host", inner.transform, Vector2.zero, Vector2.one, new Vector2(28f, 28f), new Vector2(-28f, -28f));
            BuildSurvivalHud();
        }

        private void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                createdEventSystem = new GameObject("Riftline Event System", typeof(EventSystem));
                createdEventSystem.transform.SetParent(transform, false);
                eventSystem = createdEventSystem.GetComponent<EventSystem>();
            }

            InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
            {
                module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
                createdInputModule = module;
            }
        }

        private void Rebuild()
        {
            if (!initialized || pageHost == null)
            {
                return;
            }

            rebuildRequested = false;
            text.SetLanguage(Settings.Language);
            SaveScrollPositions();
            visibleButtonCount = 0;
            ClearChildren(pageHost);
            DestroyPreview();
            DestroyReplay();
            pauseSession = null;
            pauseRest = null;

            bool showMain = game.Phase != RunPhase.Running && game.Phase != RunPhase.Countdown &&
                            (game.Phase != RunPhase.Failed || Time.unscaledTime >= failureResultsAt);
            mainPanel.gameObject.SetActive(showMain);
            hudPanel.gameObject.SetActive(!showMain);
            hudCountdown.gameObject.SetActive(game.Phase == RunPhase.Countdown);
            hudTurn.gameObject.SetActive(false);
            if (!showMain)
            {
                ui.RefreshTypography();
                RefreshSurvivalHud();
                return;
            }

            switch (page)
            {
                case Page.Contracts:
                    BuildContracts();
                    break;
                case Page.Outfits:
                    BuildOutfits();
                    break;
                case Page.Archives:
                    BuildArchives();
                    break;
                case Page.Settings:
                    BuildSettings();
                    break;
                case Page.Pause:
                    BuildPause();
                    break;
                case Page.Results:
                    BuildResults();
                    break;
                default:
                    BuildHome();
                    break;
            }
            RestoreScrollPositions();
            resetScroll = false;
            ui.RefreshTypography();
            RefreshActionIcons();
        }

        private void BuildHome()
        {
            Image heroSurface = ui.Panel("Hero", pageHost, RiftlineUIElements.Navy,
                new Vector2(.48f, .18f), new Vector2(.98f, .98f),
                Vector2.zero, Vector2.zero);
            RectTransform hero = heroSurface.rectTransform;
            Mask heroMask = hero.gameObject.AddComponent<Mask>();
            heroMask.showMaskGraphic = false;
            RawImage artwork = ui.Artwork("Menu Artwork", hero, game.Assets != null ? game.Assets.MenuArtwork : null,
                Vector2.zero, Vector2.one);
            if (artwork != null)
                artwork.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            ui.Gradient("Hero Shade", hero, new Color(.1f, .16f, .24f, .04f),
                new Color(.1f, .16f, .24f, .74f), Vector2.zero, new Vector2(1, .35f));
            ui.Panel("Identity Accent", pageHost, RiftlineUIElements.Amber,
                new Vector2(.04f, .908f), new Vector2(.065f, .913f), Vector2.zero, Vector2.zero).raycastTarget = false;
            ui.FixedLabel("Kicker", pageHost, "RUN THE RIFT", 23, TextAnchor.MiddleLeft, RiftlineUIElements.Amber,
                new Vector2(.08f, .885f), new Vector2(.43f, .94f), Vector2.zero, Vector2.zero);
            Text wordmark = ui.FixedLabel("Wordmark", pageHost, "RIFTLINE", 106, TextAnchor.MiddleLeft, RiftlineUIElements.White,
                new Vector2(.04f, .72f), new Vector2(.455f, .88f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            wordmark.resizeTextForBestFit = true;
            wordmark.resizeTextMinSize = 80;
            wordmark.resizeTextMaxSize = Mathf.RoundToInt(106 * Mathf.Clamp(Settings.FontScale, .8f, 1.4f));
            ui.FixedLabel("Chinese Title", pageHost, text.Localized("折跃逃亡", "ONE MORE RUN"), 42,
                TextAnchor.MiddleLeft, RiftlineUIElements.White, new Vector2(.043f, .64f), new Vector2(.46f, .72f),
                Vector2.zero, Vector2.zero);
            bool recommendedEndless;
            ContractDefinition recommended = GetRecommendedRun(out recommendedEndless);
            string recommendedName = recommended == null ? text.Get("endless") :
                text.Localized(recommended.NameZh, recommended.NameEn);
            Image mission = ui.Panel("Mission Brief", pageHost, RiftlineUIElements.Navy,
                new Vector2(.04f, .48f), new Vector2(.455f, .635f), Vector2.zero, Vector2.zero);
            mission.raycastTarget = false;
            Text pitch = ui.FixedLabel("Mission And Threat", mission.transform,
                text.HomeMission(recommendedName, recommendedEndless) + "\n" + text.Get("homeThreat"),
                25, TextAnchor.MiddleLeft, RiftlineUIElements.Muted,
                Vector2.zero, Vector2.one, new Vector2(22, 12), new Vector2(-22, -12));
            pitch.resizeTextForBestFit = true;
            pitch.resizeTextMinSize = 20;
            pitch.resizeTextMaxSize = Mathf.RoundToInt(25 * Mathf.Clamp(Settings.FontScale, .8f, 1.4f));

            RectTransform buttons = ui.Vertical("Navigation", pageHost, 12f);
            buttons.anchorMin = new Vector2(.04f, .18f);
            buttons.anchorMax = new Vector2(.455f, .46f);
            buttons.offsetMin = Vector2.zero;
            buttons.offsetMax = Vector2.zero;
            ui.Button("Run Now", buttons, text.Get("runNow") + "  →", () => game.StartRecommendedContract(), true,
                RiftlineUIElements.Amber, 104f);
            ui.Button("Contracts", buttons, text.Get("play"), () => OpenPage("Contracts"), true,
                RiftlineUIElements.NavyRaised, 74f);
            bool endless = game.Progress.IsEndlessUnlocked;
            if (endless)
                ui.Button("Endless", buttons, text.Get("endless"), () => game.StartEndless(), true,
                    RiftlineUIElements.NavyRaised, 66f);
            ui.FixedLabel("Start Hint", pageHost, StartHintCopy(), 20, TextAnchor.MiddleLeft, RiftlineUIElements.Muted,
                new Vector2(.048f, .125f), new Vector2(.44f, .18f), Vector2.zero, Vector2.zero);
            ui.Panel("Navigation Divider", pageHost, new Color(.68f, .81f, .94f, .2f),
                new Vector2(.04f, .12f), new Vector2(.96f, .12f), Vector2.zero, new Vector2(0, 1)).raycastTarget = false;
            int archiveCount = CountTrue(game.Save.Archives);
            int outfitCount = CountTrue(game.Save.OwnedOutfits);
            RectTransform utility = ui.Horizontal("Utility", pageHost, 12f, 68f);
            utility.anchorMin = new Vector2(.045f, .035f);
            utility.anchorMax = new Vector2(.67f, .105f);
            utility.offsetMin = utility.offsetMax = Vector2.zero;
            ui.Button("Archives", utility, text.Get("archives") + "  " + archiveCount + "/30", () => OpenPage("Archives"), true,
                RiftlineUIElements.NavyRaised, 68f);
            ui.Button("Outfits", utility, text.Get("outfits") + "  " + outfitCount + "/8", () => OpenPage("Outfits"), true,
                RiftlineUIElements.NavyRaised, 68f);
            ui.Button("Settings", utility, text.Get("settings"), () => OpenPage("Settings"), true,
                RiftlineUIElements.NavyRaised, 68f);
            ui.FixedLabel("Hero Caption", pageHost, text.Get("heroCaption"), 25, TextAnchor.LowerRight, RiftlineUIElements.White,
                new Vector2(.54f, .22f), new Vector2(.95f, .34f), Vector2.zero, Vector2.zero);
            ui.FixedLabel("Chips", pageHost, text.Get("chips") + "  " + game.Save.Chips.ToString("N0"), 21,
                TextAnchor.MiddleRight, RiftlineUIElements.Amber, new Vector2(.72f, .125f), new Vector2(.96f, .175f),
                Vector2.zero, Vector2.zero, FontStyle.Bold);
            if (!string.IsNullOrWhiteSpace(game.StatusMessage))
            {
                ui.FixedLabel("Status", pageHost, game.StatusMessage, 17, TextAnchor.MiddleRight, RiftlineUIElements.Muted,
                    new Vector2(.58f, .17f), new Vector2(.96f, .24f), Vector2.zero, Vector2.zero);
            }
            RectTransform systemActions = ui.Horizontal("System Actions", pageHost, 8f);
            systemActions.anchorMin = new Vector2(.71f, .035f);
            systemActions.anchorMax = new Vector2(.96f, .105f);
            systemActions.offsetMin = Vector2.zero;
            systemActions.offsetMax = Vector2.zero;
            ui.Button("Recenter", systemActions, text.Get("recenter"), () => game.Recenter(), true,
                RiftlineUIElements.NavyRaised, 54f);
            ui.Button("Quit", systemActions, text.Get("quit"), () => game.QuitGame(), true,
                RiftlineUIElements.NavyRaised, 54f);
        }

        private ContractDefinition GetRecommendedRun(out bool endless)
        {
            foreach (ContractDefinition contract in game.Catalog.Contracts)
            {
                if (game.Progress.IsContractUnlocked(contract.Id) && SafeArray(game.Save.ContractStars, contract.Id) == 0)
                {
                    endless = false;
                    return contract;
                }
            }
            endless = game.Progress.IsEndlessUnlocked;
            return endless || game.Catalog.Contracts.Length == 0 ? null : game.Catalog.Contracts[0];
        }

        private void BuildContracts()
        {
            BuildHeader(text.Get("play"), text.Get("contractsRule"), Page.Home);
            RectTransform tabs = ui.Horizontal("Region Tabs", pageHost, 8f);
            tabs.anchorMin = new Vector2(.03f, .68f);
            tabs.anchorMax = new Vector2(.97f, .78f);
            tabs.offsetMin = Vector2.zero;
            tabs.offsetMax = Vector2.zero;
            for (int region = 0; region < 3; region++)
            {
                int captured = region;
                bool unlocked = game.Progress.IsRegionUnlocked(region);
                Color color = region == selectedRegion ? RiftlineUIElements.Cyan : RiftlineUIElements.NavyRaised;
                ui.Button("Region " + region, tabs, text.Region(region) + (unlocked ? string.Empty : " // " + text.Get("locked")),
                    () => { selectedRegion = captured; resetScroll = true; Rebuild(); }, true, color, 60f);
            }

            RectTransform scrollArea = ui.Rect("Contract List", pageHost, new Vector2(.03f, .06f), new Vector2(.97f, .66f),
                Vector2.zero, Vector2.zero);
            RectTransform content;
            ui.Scroll("Scroll", scrollArea, out content);
            IEnumerable<ContractDefinition> contracts = game.Catalog.Contracts.Where(item => item.Region == selectedRegion);
            foreach (ContractDefinition contract in contracts)
            {
                BuildContractRow(content, contract);
            }
        }

        private void BuildContractRow(Transform parent, ContractDefinition contract)
        {
            bool unlocked = game.Progress.IsContractUnlocked(contract.Id);
            int stars = SafeArray(game.Save.ContractStars, contract.Id);
            float durationMinutes = contract.Length / 7.5f / 60f;
            string title = (contract.Id + 1).ToString("00") + "  " + text.Localized(contract.NameZh, contract.NameEn);
            if (contract.Boss)
            {
                title += "  //  " + text.Get("boss");
            }

            Image row = ui.Panel("Contract " + contract.Id, parent, contract.Boss
                    ? new Color(RiftlineUIElements.Amber.r, RiftlineUIElements.Amber.g, RiftlineUIElements.Amber.b, .13f)
                    : RiftlineUIElements.NavyRaised,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.SetLayout(row, -1f, 94f);
            ui.FixedLabel("Title", row.transform, title, 22, TextAnchor.MiddleLeft,
                contract.Boss ? RiftlineUIElements.Amber : RiftlineUIElements.White,
                new Vector2(.025f, .47f), new Vector2(.53f, .92f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            string meta = Mathf.Max(1, Mathf.CeilToInt(durationMinutes)) + " min   " + new string('★', stars) +
                          new string('☆', Mathf.Max(0, 3 - stars));
            ui.FixedLabel("Meta", row.transform, meta, 18, TextAnchor.MiddleLeft, RiftlineUIElements.Muted,
                new Vector2(.025f, .08f), new Vector2(.56f, .48f), Vector2.zero, Vector2.zero);
            ui.FixedLabel("Reward", row.transform, "+" + contract.FirstClearReward + " " + text.Get("chips"), 17,
                TextAnchor.MiddleRight, RiftlineUIElements.Amber, new Vector2(.56f, .14f), new Vector2(.76f, .86f),
                Vector2.zero, Vector2.zero);

            Button button = ui.Button("Enter", row.transform, unlocked ? text.Get("enter") : text.Get("locked"),
                () => game.StartContract(contract.Id), unlocked, unlocked ? RiftlineUIElements.CyanDim : RiftlineUIElements.Disabled, 62f);
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(.79f, .16f);
            rect.anchorMax = new Vector2(.975f, .84f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void BuildOutfits()
        {
            string outfitStatus = text.Get("chips") + "  " + game.Save.Chips.ToString("N0");
            if (!string.IsNullOrWhiteSpace(game.StatusMessage))
            {
                outfitStatus += "     " + game.StatusMessage;
            }
            BuildHeader(text.Get("outfits"), outfitStatus, Page.Home);
            RectTransform listArea = ui.Rect("Outfit List", pageHost, new Vector2(.03f, .06f), new Vector2(.63f, .78f),
                Vector2.zero, Vector2.zero);
            RectTransform content;
            ui.Scroll("Scroll", listArea, out content);
            foreach (OutfitDefinition outfit in game.Catalog.Outfits)
            {
                BuildOutfitRow(content, outfit);
            }

            Image stage = ui.Panel("Preview Backdrop", pageHost, new Color(0f, .12f, .16f, .35f),
                new Vector2(.66f, .08f), new Vector2(.97f, .78f), Vector2.zero, Vector2.zero);
            ui.FixedLabel("Preview Label", stage.transform, "EVE // " + text.Get("equipped"), 18, TextAnchor.UpperCenter,
                RiftlineUIElements.Cyan, new Vector2(.05f, .82f), new Vector2(.95f, .96f), Vector2.zero, Vector2.zero,
                FontStyle.Bold);
            CreateOutfitPreview(stage.rectTransform);
        }

        private void BuildOutfitRow(Transform parent, OutfitDefinition outfit)
        {
            bool owned = SafeArray(game.Save.OwnedOutfits, outfit.Id);
            bool equipped = game.Save.EquippedOutfit == outfit.Id;
            Image row = ui.Panel("Outfit " + outfit.Id, parent, equipped
                    ? new Color(RiftlineUIElements.Cyan.r, RiftlineUIElements.Cyan.g, RiftlineUIElements.Cyan.b, .15f)
                    : RiftlineUIElements.NavyRaised,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.SetLayout(row, -1f, 108f);
            ui.FixedLabel("Name", row.transform, text.Localized(outfit.NameZh, outfit.NameEn), 21, TextAnchor.MiddleLeft,
                RiftlineUIElements.White, new Vector2(.03f, .5f), new Vector2(.65f, .92f), Vector2.zero, Vector2.zero,
                FontStyle.Bold);
            string detail = owned ? (equipped ? text.Get("equipped") : text.Get("owned")) : outfit.Cost + " " + text.Get("chips");
            ui.FixedLabel("Detail", row.transform, detail, 17, TextAnchor.MiddleLeft,
                equipped ? RiftlineUIElements.Cyan : RiftlineUIElements.Amber,
                new Vector2(.03f, .1f), new Vector2(.63f, .5f), Vector2.zero, Vector2.zero);

            string action = equipped ? text.Get("equipped") : (owned ? text.Get("equip") : text.Get("buy"));
            Button button = ui.Button("Action", row.transform, action, () => SelectOutfit(outfit.Id), !equipped,
                equipped ? RiftlineUIElements.Disabled : RiftlineUIElements.CyanDim, 62f);
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(.68f, .2f);
            rect.anchorMax = new Vector2(.97f, .8f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SelectOutfit(int id)
        {
            bool owned = SafeArray(game.Save.OwnedOutfits, id);
            if (!owned && !game.Progress.BuyOutfit(id))
            {
                game.StatusMessage = text.Get("purchaseFailed");
                RequestRebuild();
                return;
            }
            game.Progress.EquipOutfit(id);
            game.StatusMessage = text.Get("equipped");
            RequestRebuild();
        }

        private void BuildArchives()
        {
            int collected = CountTrue(game.Save.Archives);
            BuildHeader(text.Get("archives") + "  " + collected + "/30", string.Empty, Page.Home);

            RectTransform gridArea = ui.Rect("Archive Grid", pageHost, new Vector2(.03f, .06f), new Vector2(.54f, .78f),
                Vector2.zero, Vector2.zero);
            RectTransform content;
            ui.Scroll("Scroll", gridArea, out content);
            foreach (ArchiveDefinition archive in game.Catalog.Archives)
            {
                bool owned = SafeArray(game.Save.Archives, archive.Id);
                string caption = (archive.Id + 1).ToString("00") + "  " +
                                 (owned ? text.Localized(archive.NameZh, archive.NameEn) : text.Get("encrypted"));
                int id = archive.Id;
                ui.Button("Archive " + archive.Id, content, caption, () => { selectedArchive = id; Rebuild(); }, owned,
                    selectedArchive == id ? RiftlineUIElements.Cyan : RiftlineUIElements.NavyRaised, 84f);
            }

            Image detail = ui.Panel("Archive Detail", pageHost, RiftlineUIElements.NavyRaised,
                new Vector2(.57f, .06f), new Vector2(.97f, .78f), Vector2.zero, Vector2.zero);
            ArchiveDefinition selected = selectedArchive >= 0 && selectedArchive < game.Catalog.Archives.Length
                ? game.Catalog.Archives[selectedArchive]
                : null;
            bool canRead = selected != null && SafeArray(game.Save.Archives, selected.Id);
            string title = canRead ? text.Localized(selected.NameZh, selected.NameEn) : text.Get("archives");
            string body = canRead ? text.Localized(selected.TextZh, selected.TextEn) : text.Get("encrypted");
            ui.FixedLabel("Title", detail.transform, title, 26, TextAnchor.UpperLeft, RiftlineUIElements.Cyan,
                new Vector2(.07f, .75f), new Vector2(.93f, .94f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            ui.FixedLabel("Body", detail.transform, body, 20, TextAnchor.UpperLeft, canRead ? RiftlineUIElements.White : RiftlineUIElements.Muted,
                new Vector2(.07f, .08f), new Vector2(.93f, .76f), Vector2.zero, Vector2.zero);
        }

        private void BuildSettings()
        {
            string settingsNote = text.Get("autoTurnNote");
            if (!string.IsNullOrWhiteSpace(game.StatusMessage))
            {
                settingsNote += "  " + game.StatusMessage;
            }
            BuildHeader(text.Get("settings"), settingsNote, settingsReturnPage);
            RectTransform settingsArea = ui.Rect("Settings Area", pageHost, new Vector2(.03f, .06f), new Vector2(.97f, .76f),
                Vector2.zero, Vector2.zero);
            RectTransform content;
            ui.Scroll("Scroll", settingsArea, out content);

            AddChoice(content, "language", Settings.Language == "en" ? "English" : "简体中文",
                () => Change(() => Settings.Language = Settings.Language == "en" ? "zh" : "en"));
            AddCycle(content, "alignment", new[] { text.Get("smooth"), text.Get("stepped"), text.Get("fixed") },
                Settings.AlignmentMode, value => Change(() => Settings.AlignmentMode = value));
            AddCycle(content, "comfort", new[] { text.Get("off"), text.Get("light"), text.Get("medium"), text.Get("strong") },
                Settings.ComfortMask, value => Change(() => Settings.ComfortMask = value));
            AddToggle(content, "seated", Settings.Seated, value => Change(() => Settings.Seated = value));
            AddToggle(content, "leftHanded", Settings.LeftHanded, value => Change(() => Settings.LeftHanded = value));
#if UNITY_EDITOR
            AddToggle(content, "swapActions", Settings.SwapJumpSlide, value => Change(() => Settings.SwapJumpSlide = value));
            AddCycle(content, "triggerHand", new[] { text.Get("any"), text.Get("left"), text.Get("right") },
                Settings.TriggerHand, value => Change(() => Settings.TriggerHand = value));
            AddToggle(content, "haptics", Settings.Haptics, value => Change(() => Settings.Haptics = value));
            AddFloat(content, "hapticIntensity", Settings.HapticIntensity, 0f, 1f, .1f, "%",
                value => Change(() => Settings.HapticIntensity = value));
#else
            AddChoice(content, "recalibrateHands", text.Get("calibrateNow"), () =>
            {
                RiftlineHandOnboarding onboarding = game.GetComponent<RiftlineHandOnboarding>();
                if (onboarding) onboarding.Begin();
            });
#endif
            AddFloat(content, "music", Settings.MusicVolume, 0f, 1f, .1f, "%",
                value => Change(() => Settings.MusicVolume = value));
            AddFloat(content, "sfx", Settings.SfxVolume, 0f, 1f, .1f, "%",
                value => Change(() => Settings.SfxVolume = value));
            AddToggle(content, "contrast", Settings.HighContrast, value => Change(() => Settings.HighContrast = value));
            AddToggle(content, "reducedFlash", Settings.ReducedFlash, value => Change(() => Settings.ReducedFlash = value));
            AddToggle(content, "particles", Settings.ReducedParticles, value => Change(() => Settings.ReducedParticles = value));
            AddToggle(content, "fog", Settings.ReducedFog, value => Change(() => Settings.ReducedFog = value));
            AddFloat(content, "fontScale", Settings.FontScale, .8f, 1.4f, .1f, "x",
                value => Change(() => Settings.FontScale = value));
            AddDiscreteFloat(content, "runnerDistance", new[] { 4.5f, 5.5f, 7f }, Settings.RunnerDistance, " m",
                value => Change(() => Settings.RunnerDistance = value));
            AddToggle(content, "subtitles", Settings.Subtitles, value => Change(() => Settings.Subtitles = value));
            AddToggle(content, "restReminder", Settings.RestReminder, value => Change(() => Settings.RestReminder = value));
            AddToggle(content, "autoTurn", Settings.AutoTurn, value => Change(() => Settings.AutoTurn = value));
        }

        private string StartHintCopy()
        {
#if !UNITY_EDITOR
            return text.Get("handControls");
#else
            return text.Get("startHint");
#endif
        }

        private void BuildPause()
        {
            ui.FixedLabel("Title", pageHost, text.Get("pause"), 45, TextAnchor.MiddleCenter, RiftlineUIElements.White,
                new Vector2(.12f, .78f), new Vector2(.88f, .94f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            string session = text.Get("session") + "  " + FormatTime(game.SessionMinutes * 60f);
            pauseSession = ui.FixedLabel("Session", pageHost, session, 23, TextAnchor.MiddleCenter, RiftlineUIElements.Cyan,
                new Vector2(.15f, .7f), new Vector2(.85f, .8f), Vector2.zero, Vector2.zero);
            pauseRest = ui.FixedLabel("Rest", pageHost, text.Get("rest"), 23, TextAnchor.MiddleCenter, RiftlineUIElements.Amber,
                new Vector2(.12f, .61f), new Vector2(.88f, .7f), Vector2.zero, Vector2.zero);
            pauseRest.gameObject.SetActive(game.RestSuggested);

#if !UNITY_EDITOR
            ui.FixedLabel("Menu Hint", pageHost, text.Get("handMenuControls"), 19, TextAnchor.MiddleCenter,
                RiftlineUIElements.Muted, new Vector2(.12f, .53f), new Vector2(.88f, .6f), Vector2.zero, Vector2.zero);
            const float actionsTop = .515f;
#else
            const float actionsTop = .58f;
#endif
            RectTransform buttons = ui.Vertical("Pause Actions", pageHost, 10f);
            buttons.anchorMin = new Vector2(.12f, .1f);
            buttons.anchorMax = new Vector2(.88f, actionsTop);
            buttons.offsetMin = Vector2.zero;
            buttons.offsetMax = Vector2.zero;
            RectTransform runActions = ui.Horizontal("Run Actions", buttons, 10f, 86f);
            ui.Button("Resume", runActions, text.Get("resume"), () => game.ResumeRun(), true, RiftlineUIElements.Cyan, 86f);
            ui.Button("Safe End", runActions, text.Get("safeEnd"), () => game.SafeEndRun(), true, RiftlineUIElements.Amber, 86f);
            RectTransform utility = ui.Horizontal("Utility", buttons, 10f, 76f);
            ui.Button("Recenter", utility, text.Get("recenter"), () => game.Recenter(), true, RiftlineUIElements.NavyRaised, 60f);
            ui.Button("Comfort", utility, text.Get("comfort"), () => game.CycleComfort(), true, RiftlineUIElements.NavyRaised, 60f);
            RectTransform navigation = ui.Horizontal("Navigation", buttons, 10f, 76f);
            ui.Button("Settings", navigation, text.Get("settings"), () => OpenPage("Settings"), true,
                RiftlineUIElements.NavyRaised, 76f);
            ui.Button("Home", navigation, text.Get("home"), SafeReturnHome, true, RiftlineUIElements.NavyRaised, 76f);
        }

        private void SafeReturnHome()
        {
            if (game.Phase == RunPhase.Paused || game.Phase == RunPhase.Running || game.Phase == RunPhase.Countdown)
            {
                game.SafeEndRun();
            }
            game.ShowHome();
        }

        private void BuildResults()
        {
            RunResult result = game.LastResult;
            bool victory = result != null && result.Victory;
            bool safeExit = result != null && !result.Victory && game.Phase == RunPhase.Completed;
            bool genuineVictory = victory && !result.Endless && game.Phase == RunPhase.Completed;
            bool archiveConfirmed = genuineVictory && CurrentOpeningFrame().ArchiveRecovered;
            int nextContractId;
            bool hasNextContract = TryGetNextContract(result, out nextContractId);
            string heading = result == null ? text.Get("noResult") :
                (victory ? text.Get("victory") : (safeExit ? text.Get("safeExit") : text.Get("failure")));
            Color headingColor = victory ? RiftlineUIElements.Cyan : (safeExit ? RiftlineUIElements.White : RiftlineUIElements.Amber);
            ui.FixedLabel("Heading", pageHost, heading, 52, TextAnchor.MiddleCenter, headingColor,
                new Vector2(.12f, .84f), new Vector2(.88f, .95f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            if (result == null)
            {
                BuildResultButtons(null);
                return;
            }

            ContractDefinition completedContract = result.ContractId >= 0 && result.ContractId < game.Catalog.Contracts.Length
                ? game.Catalog.Contracts[result.ContractId]
                : null;
            string completedName = completedContract == null ? text.Get("endless") :
                text.Localized(completedContract.NameZh, completedContract.NameEn);
            ui.FixedLabel("Result Contract", pageHost, text.ResultContract(completedName, result.Endless), 20,
                TextAnchor.MiddleCenter, RiftlineUIElements.Muted, new Vector2(.08f, .76f), new Vector2(.92f, .83f),
                Vector2.zero, Vector2.zero, FontStyle.Bold);
            BuildResultStat("Score", text.Get("score"), result.Score.ToString("N0"),
                .07f, .35f, RiftlineUIElements.White);
            BuildResultStat("Distance", text.Get("distance"), result.Distance.ToString("N0") + " m",
                .36f, .64f, RiftlineUIElements.White);
            BuildResultStat("Chips", text.Get("chips"), "+" + result.ChipsEarned,
                .65f, .93f, RiftlineUIElements.Amber);
            string stars = new string('★', Mathf.Clamp(result.Stars, 0, 3)) + new string('☆', 3 - Mathf.Clamp(result.Stars, 0, 3));
            string rewards = stars + (result.FirstClearReward > 0
                ? "     " + text.Get("reward") + "  +" + result.FirstClearReward
                : string.Empty);
            ui.FixedLabel("Rewards", pageHost, rewards, 22, TextAnchor.MiddleCenter, RiftlineUIElements.Amber,
                new Vector2(.08f, .595f), new Vector2(.92f, .642f), Vector2.zero, Vector2.zero);

            if (genuineVictory)
            {
                string confirmation = text.Get("resultExtractionConfirmed");
                if (archiveConfirmed)
                {
                    confirmation += "\n" + text.Get("resultArchiveConfirmed");
                }
                ui.FixedLabel("Extraction Confirmation", pageHost, confirmation, 22, TextAnchor.UpperLeft,
                    RiftlineUIElements.Cyan, new Vector2(.07f, .43f), new Vector2(.53f, .59f),
                    Vector2.zero, Vector2.zero, FontStyle.Bold);
                if (hasNextContract)
                {
                    ContractDefinition nextContract = game.Catalog.Contracts[nextContractId];
                    string nextName = text.Localized(nextContract.NameZh, nextContract.NameEn);
                    ui.FixedLabel("Next Contract Target", pageHost,
                        string.Format(text.Get("resultNextTarget"), nextName), 20, TextAnchor.MiddleLeft,
                        RiftlineUIElements.White, new Vector2(.07f, .33f), new Vector2(.53f, .42f),
                        Vector2.zero, Vector2.zero, FontStyle.Bold);
                }
            }
            else if (!victory && !safeExit)
            {
                ui.FixedLabel("Failure", pageHost, text.FailureReason(result.FailureReason) + "\n\n" +
                    text.FailureAdvice(result.FailureReason) + "\n\n" + text.Get("retryRestores"), 22, TextAnchor.UpperLeft,
                    RiftlineUIElements.White, new Vector2(.07f, .29f), new Vector2(.53f, .61f), Vector2.zero, Vector2.zero);
            }
            BuildRunnerReplay();
            BuildResultButtons(result);
        }

        private void BuildResultStat(string name, string caption, string value, float left, float right, Color ink)
        {
            Image card = ui.Panel(name + " Stat", pageHost, RiftlineUIElements.Navy,
                new Vector2(left, .655f), new Vector2(right, .755f), Vector2.zero, Vector2.zero);
            card.raycastTarget = false;
            ui.FixedLabel("Caption", card.transform, caption, 19, TextAnchor.MiddleLeft, RiftlineUIElements.Muted,
                new Vector2(.07f, .56f), new Vector2(.93f, .94f), Vector2.zero, Vector2.zero);
            ui.FixedLabel("Value", card.transform, value, 32, TextAnchor.MiddleLeft, ink,
                new Vector2(.07f, .06f), new Vector2(.93f, .6f), Vector2.zero, Vector2.zero, FontStyle.Bold);
        }

        private void BuildResultButtons(RunResult result)
        {
            RectTransform actions = ui.Horizontal("Result Actions", pageHost, 12f);
            actions.anchorMin = new Vector2(.08f, .08f);
            actions.anchorMax = new Vector2(.92f, .24f);
            actions.offsetMin = Vector2.zero;
            actions.offsetMax = Vector2.zero;
            int nextId;
            bool hasNext = TryGetNextContract(result, out nextId);
            if (hasNext)
            {
                ui.Button("Next", actions, text.Get("next"), () => game.StartContract(nextId), true, RiftlineUIElements.Amber, 68f);
                ui.Button("Retry", actions, text.Get("retry"), () => game.Retry(), true, RiftlineUIElements.CyanDim, 68f);
            }
            else
            {
                ui.Button("Retry", actions, text.Get("retry"), () => game.Retry(), result != null, RiftlineUIElements.Amber, 68f);
                ui.Button("Next", actions, text.Get("next"), () => game.StartContract(nextId), false,
                    RiftlineUIElements.Disabled, 68f);
            }
            ui.Button("Home", actions, text.Get("home"), () => game.ShowHome(), true, RiftlineUIElements.NavyRaised, 68f);
        }

        private bool TryGetNextContract(RunResult result, out int nextId)
        {
            nextId = result == null ? -1 : result.ContractId + 1;
            return result != null && result.Victory && !result.Endless &&
                   nextId >= 0 && nextId < game.Catalog.Contracts.Length &&
                   game.Progress.IsContractUnlocked(nextId);
        }

        private bool TryStartPrimaryResultAction()
        {
            RunResult result = game.LastResult;
            int nextId;
            if (TryGetNextContract(result, out nextId))
            {
                game.StartContract(nextId);
                return true;
            }
            if (result == null)
            {
                return false;
            }
            game.Retry();
            return true;
        }

        private TrackEvent FindUpcomingTurn(RunSimulation run)
        {
            if (run.Events == null)
            {
                return null;
            }
            float maximum = run.Speed * 2f;
            return run.Events
                .Where(item => !item.Passed && (item.Kind == ObstacleKind.TurnLeft ||
                    item.Kind == ObstacleKind.TurnRight || item.Kind == ObstacleKind.Fork))
                .Where(item => item.Distance >= run.Distance && item.Distance - run.Distance <= maximum)
                .OrderBy(item => item.Distance)
                .FirstOrDefault();
        }

        private void RefreshActionIcons()
        {
            RefreshContextCue();
        }

        private string ActionBinding(GameCommand command)
        {
#if !UNITY_EDITOR
            switch (command)
            {
                case GameCommand.Left: return "PINCH + LEFT";
                case GameCommand.Right: return "PINCH + RIGHT";
                case GameCommand.Jump: return "PINCH + UP";
                case GameCommand.Slide: return "PINCH + DOWN";
                case GameCommand.Phase: return "FIST";
                case GameCommand.Pause: return "PALM MENU";
                default: return string.Empty;
            }
#else
            switch (command)
            {
                case GameCommand.Left: return text.Localized("摇杆 A/\u2190", "STICK A/\u2190");
                case GameCommand.Right: return text.Localized("摇杆 D/\u2192", "STICK D/\u2192");
                case GameCommand.Jump:
                    return Settings.SwapJumpSlide ? "B/Y S/\u2193/Ctrl" : text.Localized("A/X 空格", "A/X Space");
                case GameCommand.Slide:
                    return Settings.SwapJumpSlide ? text.Localized("A/X 空格", "A/X Space") : "B/Y S/\u2193/Ctrl";
                case GameCommand.Phase:
                    return (Settings.TriggerHand == 1 ? text.Localized("左扳机", "L TRG") :
                        Settings.TriggerHand == 2 ? text.Localized("右扳机", "R TRG") : text.Localized("扳机", "TRG")) + " Shift/E";
                case GameCommand.Pause: return "Menu / Esc";
                default: return string.Empty;
            }
#endif
        }

        private void BindRun()
        {
            if (subscribedRun == game.Run)
            {
                return;
            }
            if (subscribedRun != null)
            {
                subscribedRun.Feedback -= OnFeedback;
            }
            subscribedRun = game.Run;
            ResetReplayCapture();
            if (subscribedRun != null)
            {
                subscribedRun.Feedback += OnFeedback;
            }
        }

        private void OnFeedback(FeedbackKind feedback)
        {
            if (!initialized)
            {
                return;
            }
            CaptureRunnerFrame();
            OnRunFeedback(feedback);
            string key;
            switch (feedback)
            {
                case FeedbackKind.Jump: key = "subtitleJump"; break;
                case FeedbackKind.Slide: key = "subtitleSlide"; break;
                case FeedbackKind.Phase: key = "subtitlePhase"; break;
                case FeedbackKind.PhaseReady: key = "subtitlePhaseReady"; break;
                case FeedbackKind.Damage: key = "subtitleDamage"; break;
                case FeedbackKind.Archive: key = "subtitleArchive"; break;
                case FeedbackKind.NearMiss:
                    if (game.Run.Elapsed < subtitleUntil) return;
                    key = "subtitleNearMiss";
                    break;
                case FeedbackKind.Turn:
                    key = game.Run.LastHint == "turn_left" ? "turnLeft" : "turnRight";
                    break;
                case FeedbackKind.Fail: key = "subtitleFail"; break;
                case FeedbackKind.Complete: key = "subtitleComplete"; break;
                default: return;
            }
            NotifySubtitle(key);
        }

        private void NotifySubtitle(string key)
        {
            subtitleKey = key;
            subtitleUntil = game.Run.Elapsed + 2.2f;
        }

        private void RefreshHint()
        {
            string hint = game.Run.LastHint ?? string.Empty;
            if (hint == observedHint)
            {
                return;
            }
            observedHint = hint;
            switch (hint)
            {
                case "turn_locked": NotifySubtitle("turnLocked"); break;
                case "wrong_turn_input": NotifySubtitle("wrongTurnInput"); break;
                case "phase_not_ready": NotifySubtitle("phaseNotReady"); break;
                case "fast_fall": NotifySubtitle("fastFall"); break;
                case "route_left": NotifySubtitle("routeLeft"); break;
                case "route_right": NotifySubtitle("routeRight"); break;
                case "auto_turn": NotifySubtitle("autoTurnCue"); break;
                default:
                    if (hint.StartsWith("region_transition_", StringComparison.Ordinal))
                    {
                        NotifySubtitle(hint);
                    }
                    break;
            }
        }

        private void RefreshNotifications()
        {
            bool suggested = game.RestSuggested;
            if (suggested && !restWasSuggested)
            {
                restUntil = Time.unscaledTime + 10f;
            }
            restWasSuggested = suggested;
            restLabel.text = text.Get("rest");
            restLabel.transform.parent.gameObject.SetActive(suggested && Time.unscaledTime < restUntil);
            bool showSubtitle = Settings.Subtitles && game.Run.Elapsed < subtitleUntil &&
                                (game.Phase == RunPhase.Running || game.Phase == RunPhase.Countdown ||
                                 game.Phase == RunPhase.Failed && Time.unscaledTime < failureResultsAt);
            subtitleLabel.transform.parent.gameObject.SetActive(showSubtitle);
            if (showSubtitle)
            {
                int region;
                subtitleLabel.text = subtitleKey != null && subtitleKey.StartsWith("region_transition_", StringComparison.Ordinal) &&
                    int.TryParse(subtitleKey.Substring("region_transition_".Length), out region)
                    ? text.Get("transition") + " // " + text.Region(region)
                    : text.Get(subtitleKey ?? string.Empty);
            }
            if (pauseSession != null)
            {
                pauseSession.text = text.Get("session") + "  " + FormatTime(game.SessionMinutes * 60f);
                pauseRest.gameObject.SetActive(suggested);
            }
        }

        private void SaveScrollPositions()
        {
            foreach (ScrollRect scroll in pageHost.GetComponentsInChildren<ScrollRect>())
            {
                scrollPositions[scroll.transform.parent.name] = scroll.verticalNormalizedPosition;
            }
        }

        private void RestoreScrollPositions()
        {
            foreach (ScrollRect scroll in pageHost.GetComponentsInChildren<ScrollRect>())
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                float position;
                scroll.verticalNormalizedPosition = !resetScroll && scrollPositions.TryGetValue(scroll.transform.parent.name, out position)
                    ? position : 1f;
            }
        }

        private void BuildHeader(string title, string subtitle, Page backPage)
        {
            ui.Panel("Header Accent", pageHost, RiftlineUIElements.Amber,
                new Vector2(.03f, .89f), new Vector2(.035f, .95f), Vector2.zero, Vector2.zero).raycastTarget = false;
            ui.FixedLabel("Page Title", pageHost, title, 42, TextAnchor.MiddleLeft, RiftlineUIElements.White,
                new Vector2(.05f, .86f), new Vector2(.73f, .97f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            ui.FixedLabel("Page Subtitle", pageHost, subtitle, 20, TextAnchor.MiddleLeft, RiftlineUIElements.Muted,
                new Vector2(.05f, .79f), new Vector2(.74f, .87f), Vector2.zero, Vector2.zero);
            Button back = ui.Button("Back", pageHost, text.Get("back"), () => OpenPage(backPage.ToString()), true,
                RiftlineUIElements.NavyRaised, 56f);
            RectTransform rect = (RectTransform)back.transform;
            rect.anchorMin = new Vector2(.81f, .87f);
            rect.anchorMax = new Vector2(.97f, .96f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void AddToggle(Transform parent, string key, bool value, Action<bool> changed)
        {
            AddSettingRow(parent, key, text.Get(value ? "on" : "off"),
                () => changed(!value), () => changed(!value));
        }

        private void AddChoice(Transform parent, string key, string value, Action changed)
        {
            AddSettingRow(parent, key, value, changed, changed);
        }

        private void AddCycle(Transform parent, string key, string[] values, int index, Action<int> changed)
        {
            int safe = Mathf.Clamp(index, 0, values.Length - 1);
            AddSettingRow(parent, key, values[safe],
                () => changed((safe - 1 + values.Length) % values.Length),
                () => changed((safe + 1) % values.Length));
        }

        private void AddFloat(Transform parent, string key, float value, float minimum, float maximum, float step,
            string suffix, Action<float> changed)
        {
            float shown = suffix == "%" ? Mathf.Round(value * 100f) : value;
            string display = suffix == "%" ? shown.ToString("0") + suffix : shown.ToString("0.0") + suffix;
            AddSettingRow(parent, key, display,
                () => changed(Mathf.Clamp(value - step, minimum, maximum)),
                () => changed(Mathf.Clamp(value + step, minimum, maximum)));
        }

        private void AddDiscreteFloat(Transform parent, string key, float[] values, float value, string suffix,
            Action<float> changed)
        {
            int index = 0;
            float closest = float.MaxValue;
            for (int i = 0; i < values.Length; i++)
            {
                float distance = Mathf.Abs(values[i] - value);
                if (distance < closest)
                {
                    closest = distance;
                    index = i;
                }
            }
            int previous = Mathf.Max(0, index - 1);
            int next = Mathf.Min(values.Length - 1, index + 1);
            AddSettingRow(parent, key, values[index].ToString("0.0") + suffix,
                () => changed(values[previous]), () => changed(values[next]));
        }

        private void AddSettingRow(Transform parent, string key, string value, Action previous, Action next)
        {
            Image row = ui.Panel("Setting " + key, parent, RiftlineUIElements.NavyRaised,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.SetLayout(row, -1f, 88f);
            ui.FixedLabel("Name", row.transform, text.Get(key), 23, TextAnchor.MiddleLeft, RiftlineUIElements.White,
                new Vector2(.025f, .1f), new Vector2(.48f, .9f), Vector2.zero, Vector2.zero);
            Button left = ui.Button("Previous", row.transform, "‹", () => previous(), true, RiftlineUIElements.CyanDim, 50f);
            RectTransform leftRect = (RectTransform)left.transform;
            leftRect.anchorMin = new Vector2(.49f, .13f);
            leftRect.anchorMax = new Vector2(.59f, .87f);
            leftRect.offsetMin = Vector2.zero;
            leftRect.offsetMax = Vector2.zero;
            ui.FixedLabel("Value", row.transform, value, 23, TextAnchor.MiddleCenter, RiftlineUIElements.Cyan,
                new Vector2(.6f, .12f), new Vector2(.86f, .88f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            Button right = ui.Button("Next", row.transform, "›", () => next(), true, RiftlineUIElements.CyanDim, 50f);
            RectTransform rightRect = (RectTransform)right.transform;
            rightRect.anchorMin = new Vector2(.87f, .13f);
            rightRect.anchorMax = new Vector2(.97f, .87f);
            rightRect.offsetMin = Vector2.zero;
            rightRect.offsetMax = Vector2.zero;
        }

        private void Change(Action mutation)
        {
            mutation();
            Settings.FontScale = Mathf.Clamp(Settings.FontScale, .8f, 1.4f);
            game.SaveSettings();
            RequestRebuild();
        }

        private void CreateOutfitPreview(RectTransform stage)
        {
            // Deferred destruction leaves old same-named backdrops until the end of the frame.
            RectTransform anchor = ui.Rect("Eve Preview Anchor", stage, new Vector2(.5f, .1f), new Vector2(.5f, .1f),
                Vector2.zero, Vector2.zero);
            GameObject eve = CreateEveClone(anchor, stage.rect.height * .66f, stage.rect.width * .84f);
            outfitPreview = anchor.gameObject;
            if (eve == null)
            {
                ui.FixedLabel("Missing Generated Eve", stage, text.Get("eveUnavailable"), 22, TextAnchor.MiddleCenter,
                    RiftlineUIElements.Muted, new Vector2(.08f, .25f), new Vector2(.92f, .65f), Vector2.zero, Vector2.zero);
                return;
            }
            SampleEvePose(eve, 0, 0f);
        }

        private GameObject CreateEveClone(Transform parent, float height, float width)
        {
            if (game.Assets == null || game.Assets.EvePrefab == null)
            {
                return null;
            }
            parent.gameObject.SetActive(false);
            GameObject eve = Instantiate(game.Assets.EvePrefab, parent);
            eve.name = "Generated Eve UI Clone";
            eve.transform.localPosition = Vector3.zero;
            eve.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            eve.transform.localScale = Vector3.one;
            foreach (MonoBehaviour behaviour in eve.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (Camera camera in eve.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (AudioSource source in eve.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
            foreach (Collider collider in eve.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in eve.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            foreach (Animator animator in eve.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                animator.enabled = false;
            }
            foreach (Animation animation in eve.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
            foreach (ParticleSystem particles in eve.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            foreach (Light light in eve.GetComponentsInChildren<Light>(true)) light.enabled = false;

            Renderer[] renderers = eve.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds localBounds = new Bounds();
            foreach (Renderer renderer in renderers)
            {
                Bounds bounds = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 local = parent.InverseTransformPoint(corner);
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(local, Vector3.zero);
                        hasBounds = true;
                    }
                    else localBounds.Encapsulate(local);
                }
            }
            if (hasBounds)
            {
                float scale = Mathf.Min(height / Mathf.Max(.01f, localBounds.size.y), width / Mathf.Max(.01f, localBounds.size.x));
                eve.transform.localScale = Vector3.one * scale;
                eve.transform.localPosition = -localBounds.center * scale + Vector3.up * localBounds.size.y * scale * .5f;
                eve.transform.localPosition += Vector3.back * (localBounds.size.z * scale * .5f + 8f);
            }
            int outfit = Mathf.Clamp(game.Save.EquippedOutfit, 0, game.Catalog.Outfits.Length - 1);
            ColorUtility.TryParseHtmlString(game.Catalog.Outfits[outfit].ColorHex, out Color tint);
            RiftlineAppearance.Apply(eve, outfit, tint);
            parent.gameObject.SetActive(true);
            return eve;
        }

        private void SampleEvePose(GameObject eve, int clipIndex, float elapsed)
        {
            AnimationClip[] clips = game.Assets != null ? game.Assets.Animations : null;
            if (eve == null || clips == null || clipIndex >= clips.Length || clips[clipIndex] == null)
            {
                return;
            }
            Vector3 position = eve.transform.localPosition;
            Quaternion rotation = eve.transform.localRotation;
            Vector3 scale = eve.transform.localScale;
            AnimationClip clip = clips[clipIndex];
            float time = clip.isLooping ? elapsed % Mathf.Max(.01f, clip.length) : Mathf.Min(elapsed, clip.length);
            clip.SampleAnimation(eve, time);
            eve.transform.localPosition = position;
            eve.transform.localRotation = rotation;
            eve.transform.localScale = scale;
        }

        private void ResetReplayCapture()
        {
            runnerFrames.Clear();
            replayEvents.Clear();
            observedHint = string.Empty;
            subtitleKey = null;
            subtitleUntil = 0f;
        }

        private void CaptureRunnerFrame()
        {
            RunSimulation run = game.Run;
            if (run == null || run.Phase == RunPhase.Menu || run.Phase == RunPhase.Countdown || run.Phase == RunPhase.Paused)
            {
                return;
            }
            var frame = new RunnerFrame
            {
                Elapsed = run.Elapsed, Distance = run.Distance, LaneX = run.LaneX, Height = run.Height,
                Heading = run.Heading, Sliding = run.Sliding, Phasing = run.PhaseRemaining > 0f
            };
            if (runnerFrames.Count > 0)
            {
                RunnerFrame previous = runnerFrames[runnerFrames.Count - 1];
                float delta = Mathf.Max(0f, frame.Distance - previous.Distance);
                float incoming = previous.Heading == frame.Heading ? delta :
                    Mathf.Clamp(run.LastTurnDistance - previous.Distance, 0f, delta);
                frame.Center = previous.Center + Quaternion.Euler(0f, previous.Heading, 0f) * Vector3.forward * incoming +
                    Quaternion.Euler(0f, frame.Heading, 0f) * Vector3.forward * (delta - incoming);
                if (frame.Elapsed <= previous.Elapsed)
                {
                    runnerFrames[runnerFrames.Count - 1] = frame;
                }
                else runnerFrames.Add(frame);
            }
            else runnerFrames.Add(frame);

            if (runnerFrames.Count > 640)
            {
                runnerFrames.RemoveRange(0, runnerFrames.Count - 640);
            }
            while (runnerFrames.Count > 2 && runnerFrames[1].Elapsed < frame.Elapsed - ReplaySeconds - 2f)
            {
                runnerFrames.RemoveAt(0);
            }
            foreach (TrackEvent item in run.Events)
            {
                if (item.Kind > ObstacleKind.Collapse && item.Kind != ObstacleKind.Boss || replayEvents.ContainsKey(item.Id))
                {
                    continue;
                }
                replayEvents[item.Id] = new TrackEvent
                {
                    Id = item.Id, Kind = item.Kind, Lane = item.Lane, Distance = item.Distance, Width = item.Width
                };
            }
            if (replayEvents.Count > 256)
            {
                float minimum = runnerFrames[0].Distance - 10f;
                foreach (int id in replayEvents.Where(pair => pair.Value.Distance < minimum).Select(pair => pair.Key).ToArray())
                {
                    replayEvents.Remove(id);
                }
            }
        }

        private void BuildRunnerReplay()
        {
            Image backdrop = ui.Panel("Runner Replay", pageHost, RiftlineUIElements.NavyRaised,
                new Vector2(.56f, .29f), new Vector2(.96f, .58f), Vector2.zero, Vector2.zero);
            ui.FixedLabel("Replay Title", backdrop.transform, text.Get("replay"), 19, TextAnchor.MiddleCenter,
                RiftlineUIElements.Cyan, new Vector2(.02f, .82f), new Vector2(.98f, 1f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            if (runnerFrames.Count < 2)
            {
                ui.FixedLabel("Unavailable", backdrop.transform, text.Get("replayUnavailable"), 22, TextAnchor.MiddleCenter,
                    RiftlineUIElements.Muted, new Vector2(.06f, .15f), new Vector2(.94f, .8f), Vector2.zero, Vector2.zero);
                return;
            }
            float end = runnerFrames[runnerFrames.Count - 1].Elapsed;
            replayFrames = runnerFrames.Where(frame => frame.Elapsed >= end - ReplaySeconds).ToArray();
            replayOrigin = replayFrames[0].Center;
            replayHeading = replayFrames[0].Heading;
            replayMinimum = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 maximum = new Vector2(float.MinValue, float.MinValue);
            foreach (RunnerFrame frame in replayFrames)
            {
                foreach (float side in new[] { -3.9f, 3.9f })
                {
                    Vector2 point = ReplayPlane(frame.Center + Quaternion.Euler(0f, frame.Heading, 0f) * Vector3.right * side);
                    replayMinimum = Vector2.Min(replayMinimum, point);
                    maximum = Vector2.Max(maximum, point);
                }
            }
            replayStage = ui.Rect("Static Recorded Track", backdrop.transform, new Vector2(.04f, .23f), new Vector2(.96f, .81f),
                Vector2.zero, Vector2.zero);
            Vector2 size = replayStage.rect.size;
            Vector2 span = maximum - replayMinimum;
            replayScale = Mathf.Min(Mathf.Max(1f, size.x - 60f) / Mathf.Max(1f, span.x),
                Mathf.Max(1f, size.y - 44f) / Mathf.Max(1f, span.y));
            replayOffset = (size - span * replayScale) * .5f;
            // The widget's route and view stay fixed; only the generated runner clone advances through snapshots.
            for (int i = 0; i < replayFrames.Length - 1; i += 6)
            {
                RunnerFrame from = replayFrames[i];
                RunnerFrame to = replayFrames[Mathf.Min(i + 6, replayFrames.Length - 1)];
                foreach (float side in new[] { -3.9f, -1.3f, 1.3f, 3.9f })
                {
                    Vector3 a = from.Center + Quaternion.Euler(0f, from.Heading, 0f) * Vector3.right * side;
                    Vector3 b = to.Center + Quaternion.Euler(0f, to.Heading, 0f) * Vector3.right * side;
                    ui.Segment(replayStage, ReplayPoint(a), ReplayPoint(b), 2f, RiftlineUIElements.CyanDim);
                }
            }
            TrackEvent[] visibleEvents = replayEvents.Values
                .Where(item => item.Distance >= replayFrames[0].Distance &&
                    item.Distance <= replayFrames[replayFrames.Length - 1].Distance)
                .ToArray();
            List<ObstacleKind> hazardKinds = visibleEvents.Select(item => item.Kind).Distinct().ToList();
            if (hazardKinds.Count > 0)
            {
                Text legend = ui.FixedLabel("Hazard Legend", replayStage,
                    string.Join("\n", hazardKinds.Select(kind => text.Hazard(kind)).ToArray()),
                    14, TextAnchor.MiddleLeft, RiftlineUIElements.Amber,
                    new Vector2(.01f, .06f), new Vector2(.34f, .94f), Vector2.zero, Vector2.zero, FontStyle.Bold);
                legend.raycastTarget = false;
            }
            foreach (TrackEvent item in visibleEvents)
            {
                RunnerFrame frame = FrameAtDistance(item.Distance);
                float lane = (Mathf.Clamp(item.Lane, 0, 2) - 1) * 2.6f;
                if (item.Kind == ObstacleKind.MovingGate) lane += Mathf.Sin(frame.Elapsed * .75f + item.Id) * .32f;
                Vector2 point = ReplayPoint(frame.Center + Quaternion.Euler(0f, frame.Heading, 0f) * Vector3.right * lane);
                Image marker = ui.Panel("Recorded " + item.Kind, replayStage, RiftlineUIElements.Amber,
                    Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                marker.raycastTarget = false;
                marker.rectTransform.anchoredPosition = point;
                marker.rectTransform.sizeDelta = new Vector2(Mathf.Max(10f, item.Width * replayScale), 6f);
            }
            replayRunner = ui.Rect("Replay Eve Anchor", replayStage, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            replayEve = CreateEveClone(replayRunner, 40f, 28f);
            replayCaption = ui.FixedLabel("Recorded State", backdrop.transform, string.Empty, 17, TextAnchor.MiddleCenter,
                RiftlineUIElements.White, new Vector2(.02f, .01f), new Vector2(.98f, .23f), Vector2.zero, Vector2.zero);
            replayBegan = Time.unscaledTime;
        }

        private Vector2 ReplayPlane(Vector3 point)
        {
            Vector3 relative = Quaternion.Euler(0f, -replayHeading, 0f) * (point - replayOrigin);
            return new Vector2(relative.x, relative.z);
        }

        private Vector2 ReplayPoint(Vector3 point)
        {
            return (ReplayPlane(point) - replayMinimum) * replayScale + replayOffset;
        }

        private RunnerFrame FrameAtDistance(float distance)
        {
            for (int i = 1; i < replayFrames.Length; i++)
            {
                if (replayFrames[i].Distance >= distance)
                {
                    float t = Mathf.InverseLerp(replayFrames[i - 1].Distance, replayFrames[i].Distance, distance);
                    return InterpolateFrame(replayFrames[i - 1], replayFrames[i], t);
                }
            }
            return replayFrames[replayFrames.Length - 1];
        }

        private static RunnerFrame InterpolateFrame(RunnerFrame a, RunnerFrame b, float t)
        {
            RunnerFrame frame = t < 1f ? a : b;
            frame.Elapsed = Mathf.Lerp(a.Elapsed, b.Elapsed, t);
            frame.Distance = Mathf.Lerp(a.Distance, b.Distance, t);
            frame.Center = Vector3.Lerp(a.Center, b.Center, t);
            frame.LaneX = Mathf.Lerp(a.LaneX, b.LaneX, t);
            frame.Height = Mathf.Lerp(a.Height, b.Height, t);
            return frame;
        }

        private void UpdateReplay()
        {
            if (replayStage == null || replayFrames == null || replayFrames.Length < 2 || !ResultsVisible)
            {
                return;
            }
            float duration = replayFrames[replayFrames.Length - 1].Elapsed - replayFrames[0].Elapsed;
            float age = (Time.unscaledTime - replayBegan) % Mathf.Max(.01f, duration + 1.2f);
            float elapsed = replayFrames[0].Elapsed + Mathf.Min(age, duration);
            int index = 1;
            while (index < replayFrames.Length - 1 && replayFrames[index].Elapsed < elapsed) index++;
            RunnerFrame frame = InterpolateFrame(replayFrames[index - 1], replayFrames[index],
                Mathf.InverseLerp(replayFrames[index - 1].Elapsed, replayFrames[index].Elapsed, elapsed));
            Vector2 point = ReplayPoint(frame.Center + Quaternion.Euler(0f, frame.Heading, 0f) * Vector3.right * frame.LaneX);
            point.y += frame.Height * (40f / 1.55f);
            ((RectTransform)replayRunner).anchoredPosition = point;
            Vector3 position = replayRunner.localPosition;
            position.z = -12f;
            replayRunner.localPosition = position;
            bool stopped = age >= duration;
            int pose = stopped && game.Phase == RunPhase.Failed ? 6 : frame.Phasing ? 4 : frame.Sliding ? 3 : frame.Height > .08f ? 2 : 1;
            int began = index - 1;
            while (began > 0 && replayFrames[began - 1].Sliding == frame.Sliding &&
                   replayFrames[began - 1].Phasing == frame.Phasing &&
                   (replayFrames[began - 1].Height > .08f) == (frame.Height > .08f)) began--;
            SampleEvePose(replayEve, pose, stopped ? age - duration : frame.Elapsed - replayFrames[began].Elapsed);
            GameCommand action = frame.Phasing ? GameCommand.Phase : frame.Sliding ? GameCommand.Slide :
                frame.Height > .08f ? GameCommand.Jump : GameCommand.None;
            replayCaption.text = replayEve == null ? text.Get("eveUnavailable") :
                frame.Distance.ToString("0.0") + " m  // " + text.Action(action) +
                (stopped && game.Phase == RunPhase.Failed ? "\n" + text.FailureReason(game.Run.FailureReason) : string.Empty);
        }

        private Page PageForPhase(RunPhase phase, Page fallback)
        {
            if (phase == RunPhase.Paused)
            {
                return Page.Pause;
            }
            if (phase == RunPhase.Failed || phase == RunPhase.Completed)
            {
                return Page.Results;
            }
            return fallback == Page.Pause || fallback == Page.Results ? Page.Home : fallback;
        }

        private void OnGameChanged()
        {
            if (!initialized)
            {
                return;
            }
            BindRun();
            ObservePhase();
            RequestRebuild();
        }

        private void RequestRebuild()
        {
            rebuildRequested = true;
        }

        private void Unsubscribe()
        {
            if (game != null)
            {
                game.Changed -= OnGameChanged;
            }
            if (subscribedRun != null)
            {
                subscribedRun.Feedback -= OnFeedback;
                subscribedRun = null;
            }
            initialized = false;
        }

        private void DestroyOwnedObjects()
        {
            DestroyPreview();
            DestroyReplay();
            if (Canvas != null)
            {
                Canvas.gameObject.SetActive(false);
                Destroy(Canvas.gameObject);
            }
            if (createdEventSystem != null)
            {
                createdEventSystem.SetActive(false);
                Destroy(createdEventSystem);
            }
            else if (createdInputModule != null)
            {
                createdInputModule.enabled = false;
                Destroy(createdInputModule);
            }
            if (ui != null)
            {
                ui.Dispose();
            }
            Canvas = null;
            mainPanel = null;
            pageHost = null;
            hudPanel = null;
            createdEventSystem = null;
            createdInputModule = null;
            ui = null;
        }

        private void DestroyPreview()
        {
            if (outfitPreview != null)
            {
                outfitPreview.SetActive(false);
                Destroy(outfitPreview);
                outfitPreview = null;
            }
        }

        private void DestroyReplay()
        {
            if (replayRunner != null)
            {
                replayRunner.gameObject.SetActive(false);
            }
            replayRunner = null;
            replayEve = null;
            replayFrames = null;
            replayStage = null;
            replayCaption = null;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                parent.GetChild(i).gameObject.SetActive(false);
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        private static int CountTrue(bool[] values)
        {
            return values == null ? 0 : values.Count(value => value);
        }

        private static bool SafeArray(bool[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length && values[index];
        }

        private static int SafeArray(int[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : 0;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }
    }
}
