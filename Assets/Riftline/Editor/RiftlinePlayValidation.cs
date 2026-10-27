#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Riftline
{
    // Orchestration is Editor-driven; commands are submitted only after an actual game-camera render.
    [InitializeOnLoad]
    public static class RiftlinePlayValidation
    {
        public const string ReportPath = "/tmp/riftline-play-validation.json";
        const string ActiveKey = "Riftline.Acceptance.Active";
        const string SaveKey = "Riftline.Acceptance.SaveDirectory";
        const string ScenarioKey = "Riftline.Acceptance.Scenario";
        const string ReportKey = "Riftline.Acceptance.Report";
        const string RestoreKey = "Riftline.Acceptance.Restore";
        const double SuiteTimeout = 3600;
        enum Stage { WaitingForPlay, Bind, MenuInput, Pages, Launch, Countdown, Driving, PauseHold,
            ResumeCountdown, Settlement, Results, Purchase, PurchaseCapture, ManualStart, Manual,
            FailureDriving, FailureRetry, FailurePause, FailureResume, CaptureDrain, Finished }

        [Serializable]
        public sealed class Report
        {
            public string Session, Scenario, Status, Stage, Message, StartedUtc, UpdatedUtc;
            public string SaveDirectory, Scene, UnityVersion, EvidenceScope;
            public bool Success, RuntimeExecuted, XrObserved, FocusBypassRequested;
            public int CompletedRuns, Stars, Archives, Chips, OwnedOutfits, FirstClearRewards, FundingRuns;
            public int RenderedFrames, MissedPlayerFrames, RuntimeErrors, Warnings, FramesOver72HzBudget;
            public float GameplaySeconds, MaximumObservedDelta, RequestedTimeScale = 8;
            public double WallSeconds;
            public Budget LiveBudget = new Budget(), PeakBudget = new Budget();
            public List<RunEvidence> Runs = new List<RunEvidence>();
            public List<Check> Checks = new List<Check>();
            public List<ConsoleEntry> Console = new List<ConsoleEntry>();
            public List<Capture> Screenshots = new List<Capture>();
            public List<string> Limitations = new List<string>();
        }

        [Serializable]
        public sealed class RunEvidence
        {
            public string Name, Mode, Status, FailureReason, SettlementId;
            public int ContractId, PreferredRoute, AlignmentMode, Commands, RenderedFrames, Seed;
            public int Score, PickupChips, FirstClearReward, Stars, NearMisses, Turns, PhaseUses;
            public int ChipsBefore, ChipsAfter, TutorialLessons, TutorialRecoveries;
            public int LeftForks, RightForks, FramesOver72HzBudget;
            public int HeightRenderedFrames, BossRenderedFrames;
            public double FrameMillisecondsTotal;
            public bool Victory, UsedAutoTurn, Durable, PauseAtTurnVerified, PauseOnSlopeVerified, SeedObserved;
            public bool BossCaptureRequested, HeightCaptureRequested;
            public float Elapsed, Distance, BossObservedSeconds, MaximumDelta;
            public float MaximumGroundHeight, MinimumGroundHeight;
            public double WallSeconds;
            public List<int> ArchivesAcquired = new List<int>();
            public List<CommandEvidence> Trace = new List<CommandEvidence>();
            public Budget PeakBudget = new Budget();
            public Budget LastBudget = new Budget();
            public List<Budget> BudgetSamples = new List<Budget>();
        }

        [Serializable]
        public sealed class CommandEvidence
        {
            public int Frame, Lane;
            public float Elapsed, Distance, Height, Energy;
            public string Command, Source;
        }

        [Serializable]
        public sealed class Budget
        {
            public int Frame, ActiveRenderers, VisibleRenderers, ActiveCanvasRenderers;
            public int LineRenderers, ParticleSystems, LiveParticles, UnsupportedRenderers, InvalidBounds;
            public long ActiveVertices, ActiveTriangles, VisibleVertices, VisibleTriangles, UiVertices, UiTriangles;
            public float UnscaledFrameMilliseconds, SimulationDelta, RunnerCenterAngle;
            public float FrameBudgetMilliseconds = 1000f / 72;
            public Vector3 RunnerPosition, RunnerBoundsCenter, RunnerBoundsSize, CameraPosition, RunnerViewport;
            public Vector3 ExternalPosition, ExternalEuler;
            public bool RunnerBoundsPresent;
            public string CountMethod = "Conservative per-instance LOD0 vertices/indices; game-camera frustum/layer eligibility, not selected LOD, occlusion or GPU counters. Lines baked; particle geometry excluded.";
        }

        [Serializable]
        public sealed class Check
        {
            public string Name, Detail;
            public bool Passed;
            public int Frame;
        }

        [Serializable]
        public sealed class ConsoleEntry
        {
            public string Type, Message, Stack;
            public int Frame;
        }

        [Serializable]
        public sealed class Capture
        {
            public string Name, Path;
            public int RequestedFrame;
            public int CapturedFrame;
            public string RenderedPage, RenderedLanguage, RenderedPhase;
            public string ExpectedPage, ExpectedLanguage, ExpectedPhase;
            public bool Submitted;
            public bool FileObserved;
            public long Bytes;
        }

        [Serializable]
        sealed class RestoreState
        {
            public float TimeScale, MaximumDelta, FixedDelta;
            public int FrameRate, VSync;
            public bool RunInBackground;
            public double Started;
        }

        sealed class PoseSnapshot
        {
            public Transform Target;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
            public PoseSnapshot(Transform target)
            {
                Target = target; Position = target.localPosition;
                Scale = target.localScale; Rotation = target.localRotation;
            }
            public bool Matches() => Target &&
                Vector3.Distance(Position, Target.localPosition) < .00001f &&
                Quaternion.Angle(Rotation, Target.localRotation) < .001f &&
                Vector3.Distance(Scale, Target.localScale) < .00001f;
        }

        static readonly string[] Pages = { "Home", "Contracts", "Outfits", "Archives", "Settings" };
        static Report report;
        static RestoreState restore;
        static Stage stage;
        static RiftlineGame game;
        static RiftlineUI ui;
        static Transform outside, runner;
        static RunAutopilot pilot;
        static RunEvidence current;
        static Keyboard keyboard;
        static Keyboard previousKeyboard;
        static Mouse mouse, previousMouse;
        static int menuInputStep;
        static float menuScrollBefore;
        static Mesh scratch;
        static FieldInfo saveHook, activeHook;
        static PropertyInfo settlementHook;
        static readonly List<PoseSnapshot> cameraPoses = new List<PoseSnapshot>();
        static List<PoseSnapshot> frozen;
        static bool[] archiveBefore;
        static bool active, finishRequested, cameraChecked, manualRun, alternate, endless, supplement;
        static int contract, fundingRuns, page, lastRendered = -1, stageFrame, manualStep, manualVariant;
        static int queuedFrame, replayBefore, laneBefore, turnsBefore;
        static TrackEvent committedContext;
        static double stageStarted, nextReport, lastRenderAt, runStarted, lastHeartbeat;
        static float frozenElapsed, frozenDistance, holdUntil, targetEndless, resumeStarted;
        static float previousElapsed;
        static string manualExpected, manualLabel;
        static int manualHeldFrames;
        static bool probePending, manualStartObserved;
        static int firstClearTotal;
        static string pendingFinishMessage;

        static RiftlinePlayValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            Application.logMessageReceived += OnLog;
            RenderPipelineManager.endContextRendering += OnRendered;
            Camera.onPostRender += OnCameraRendered;
            if (!SessionState.GetBool(ActiveKey, false)) return;
            try
            {
                report = JsonUtility.FromJson<Report>(SessionState.GetString(ReportKey, ""));
                restore = JsonUtility.FromJson<RestoreState>(SessionState.GetString(RestoreKey, ""));
                RequireHooks();
                if (report != null && report.Stage == Stage.Finished.ToString())
                {
                    RestoreEnvironment();
                    SessionState.SetBool(ActiveKey, false);
                    SetHooks(null, false);
                    return;
                }
                SetHooks(SessionState.GetString(SaveKey, ""), true);
                active = true;
                if (report == null || restore == null) throw new InvalidOperationException("Acceptance session could not be restored.");
                // A reload on entry is expected. A reload during a run cannot reconstruct its controller decisions.
                if (report.RuntimeExecuted)
                    throw new InvalidOperationException("Assembly reload interrupted acceptance; recorded runs are partial, restart from Edit Mode.");
                ChangeStage(Stage.WaitingForPlay);
            }
            catch (Exception error)
            {
                if (report == null) report = NewReport("Unknown");
                report.Message = error.Message;
                report.Status = "Failed";
                report.Success = false;
                WriteReport();
                active = false;
                finishRequested = true;
            }
        }

        public static bool Active => active;
        public static string Status => report == null ? "Not started" : report.Status + ": " + report.Stage + "; " + report.Message;

        [MenuItem("Riftline/Acceptance/Start Full Editor Acceptance")]
        public static void Start() => Start("All");

        public static void Start(string scenario)
        {
            if (active || SessionState.GetBool(ActiveKey, false))
                throw new InvalidOperationException("Acceptance is already active.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start acceptance in Edit Mode so saves are isolated before RiftlineGame.Awake.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for imports and compilation before acceptance.");
            if (!new[] { "All", "TestPhaseZero", "ManualStart", "All18", "Branches", "Endless600", "Outfits", "PauseAtTurn", "ReplayRegression", "FailureRetry" }.Contains(scenario))
                throw new ArgumentException("Unknown scenario: " + scenario);
            RequireHooks();
            current = null;
            game = null;
            ui = null;
            outside = runner = null;
            pilot = null;
            firstClearTotal = fundingRuns = 0;
            lastHeartbeat = nextReport = 0;
            report = NewReport(scenario);
            restore = new RestoreState
            {
                TimeScale = Time.timeScale, MaximumDelta = Time.maximumDeltaTime, FixedDelta = Time.fixedDeltaTime,
                FrameRate = Application.targetFrameRate, VSync = QualitySettings.vSyncCount,
                RunInBackground = Application.runInBackground, Started = EditorApplication.timeSinceStartup
            };
            Directory.CreateDirectory(report.SaveDirectory);
            SessionState.SetString(SaveKey, report.SaveDirectory);
            SessionState.SetString(ScenarioKey, scenario);
            SessionState.SetString(RestoreKey, JsonUtility.ToJson(restore));
            SessionState.SetBool(ActiveKey, true);
            SetHooks(report.SaveDirectory, true);
            active = true;
            finishRequested = false;
            report.Status = "Running";
            ChangeStage(Stage.WaitingForPlay);
            WriteReport();
            if (!active) throw new IOException(report.Message);
            EditorApplication.isPlaying = true;
        }

        // Parent automation can use this from its existing nonblocking Action switch.
        public static string Action(string action)
        {
            if (action == "Status") return Status;
            if (action == "Stop") { Stop(); return Status; }
            if (action == "Start") Start(); else Start(action);
            return "Acceptance requested: " + action + "; poll " + ReportPath;
        }

        [MenuItem("Riftline/Acceptance/Stop")]
        public static void Stop()
        {
            if (active) Finish(false, "Cancelled by operator; partial evidence retained.");
        }

        static Report NewReport(string scenario) => new Report
        {
            Session = Guid.NewGuid().ToString("N"), Scenario = scenario, Status = "Preparing",
            StartedUtc = DateTime.UtcNow.ToString("O"),
            SaveDirectory = Path.Combine("/tmp/riftline-acceptance-staged/saves", Guid.NewGuid().ToString("N")),
            UnityVersion = Application.unityVersion,
            EvidenceScope = "Real Unity Editor Play Mode, actual scene/presentation and runtime input. No APK, headset, comfort or device-performance certification.",
            Limitations = new List<string>
            {
                "Production RunAutopilot reads event metadata; this is integration acceptance, not an independent Core solvability proof.",
                "Geometry is dynamically counted by instance and game-camera frustum eligibility, not occlusion, draw calls, GPU timing or PICO performance.",
                "Particle counts are observed; particle-generated geometry is excluded and explicitly counted as unsupported.",
                "Screenshot requests require nonempty files; visual correctness still requires human inspection.",
                "Queued keyboard events exercise real InputActions. Physical XR stick, controller rays/drag, worn duration and headset comfort are not certified.",
                "No mutations of events, scores, energy, unlocks, lane positions, duration or AutoTurn assistance are permitted."
            }
        };

        static void RequireHooks()
        {
            var type = typeof(RiftlineGame);
            saveHook = type.GetField("EditorSaveDirectory", BindingFlags.Public | BindingFlags.Static);
            activeHook = type.GetField("EditorAcceptanceActive", BindingFlags.Public | BindingFlags.Static);
            settlementHook = type.GetProperty("SettlementPending", BindingFlags.Public | BindingFlags.Instance);
            if (saveHook == null || saveHook.FieldType != typeof(string) ||
                activeHook == null || activeHook.FieldType != typeof(bool) ||
                settlementHook == null || settlementHook.PropertyType != typeof(bool))
                throw new InvalidOperationException("Parent hooks required: static string EditorSaveDirectory, static bool EditorAcceptanceActive, bool SettlementPending. See /tmp/riftline-editor-acceptance.md.");
        }

        static void SetHooks(string directory, bool enabled)
        {
            if (enabled && (string.IsNullOrWhiteSpace(directory) ||
                !Path.GetFullPath(directory).StartsWith(Path.GetFullPath("/tmp/riftline-acceptance-staged/saves/"), StringComparison.Ordinal)))
                throw new InvalidOperationException("Unsafe or empty acceptance save directory.");
            saveHook?.SetValue(null, enabled ? directory : null);
            activeHook?.SetValue(null, enabled);
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && active) ChangeStage(Stage.Bind);
            if (state == PlayModeStateChange.ExitingPlayMode && active)
                Finish(false, "Play Mode exited before acceptance finished.", false);
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ActiveKey, false)) return;
            RestoreEnvironment();
            SessionState.SetBool(ActiveKey, false);
            SetHooks(null, false);
            active = false;
            finishRequested = false;
        }

        static void BeforeReload()
        {
            if (active) WriteReport();
            RemoveVirtualKeyboard();
            if (scratch) UnityEngine.Object.DestroyImmediate(scratch);
        }

        static void Tick()
        {
            if (finishRequested && EditorApplication.isPlaying)
            {
                finishRequested = false;
                EditorApplication.isPlaying = false;
                return;
            }
            if (!active || report == null) return;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - restore.Started > SuiteTimeout) throw new TimeoutException("One-hour suite wall-clock limit exceeded.");
                if (report.RuntimeErrors > 0) throw new InvalidOperationException("Runtime Console error captured; see report.Console.");
                if (EditorApplication.isCompiling) throw new InvalidOperationException("Compilation began during acceptance.");
                if (!EditorApplication.isPlaying)
                {
                    if (now - stageStarted > 60) throw new TimeoutException("Play Mode entry timed out.");
                    return;
                }
                if (EditorApplication.isPaused) throw new InvalidOperationException("Editor Pause interrupts rendered-frame acceptance.");
                if (stage == Stage.WaitingForPlay) ChangeStage(Stage.Bind);
                if (stage == Stage.Bind) BindScene();
                if (game && now - lastRenderAt > 30) throw new TimeoutException("No game-camera render for 30 seconds; keep Game view visible/focused.");
                if (game && stage != Stage.Bind && stage != Stage.WaitingForPlay && now - stageStarted > StageTimeout())
                    throw new TimeoutException("Stage stalled: " + stage + "; phase=" + game.Phase + "; tracking=" + game.TrackingReady);
                if (stage == Stage.Launch) LaunchRun();
                if (stage == Stage.Settlement) WaitForSettlement();
                if (stage == Stage.Purchase) PurchaseOutfits();
                ObserveCaptureFiles();
                if (stage == Stage.CaptureDrain && report.Screenshots.All(value => value.FileObserved))
                    Finish(true, pendingFinishMessage);
                if (now >= nextReport)
                {
                    WriteReport(); nextReport = now + 1;
                }
                if (now - lastHeartbeat >= 15)
                {
                    lastHeartbeat = now;
                    Debug.Log("[Riftline Acceptance] " + Status + "; elapsed=" + (game ? game.Run.Elapsed.ToString("0.0") : "0") +
                        "; stars=" + report.Stars + "; archives=" + report.Archives + "; chips=" + report.Chips);
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        static double StageTimeout()
        {
            if (stage == Stage.Driving) return endless ? targetEndless * 2 + 60 : game.Run.ActiveContract.DurationSeconds * 2 + 60;
            if (stage == Stage.Manual) return 150;
            return 60;
        }

        static void BindScene()
        {
            game = UnityEngine.Object.FindAnyObjectByType<RiftlineGame>();
            if (!game || game.Run == null || game.Progress == null) return;
            Require(game.ViewCamera && game.ViewCamera.enabled, "Actual scene camera exists");
            Require((string)saveHook.GetValue(null) == report.SaveDirectory, "Disposable save hook retained after domain reload");
            // Verify the actual Game's store, not just the hook's value, before making any writes.
            var store = typeof(RiftlineGame).GetField("store", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(game);
            var directory = typeof(GameSaveStore).GetField("directory", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(store) as string;
            Require(directory != null && Path.GetFullPath(directory) == Path.GetFullPath(report.SaveDirectory),
                "Runtime store uses disposable directory", directory ?? "store could not be inspected");
            Require(game.Save.Chips == 0 && game.Save.ContractStars.All(value => value == 0) &&
                game.Save.Archives.All(value => !value) && game.Save.OwnedOutfits.Count(value => value) == 1 &&
                game.Save.SettledRuns.Length == 0, "Fresh runtime progress");
            Require(!game.Save.Settings.AutoTurn && !game.Run.AutoTurn, "No AutoTurn assistance");
            Require(game.Catalog.Contracts.Length == 18 && game.Catalog.Archives.Length == 30 &&
                game.Catalog.Outfits.Sum(value => value.Cost) == 6700, "Catalog acceptance totals");
            ui = game.GetComponentInChildren<RiftlineUI>(true);
            outside = FindDescendant(game.transform, "Reconstructed Rail World");
            runner = FindDescendant(game.transform, "Eve Runner");
            Require(ui && ui.Canvas && outside && runner, "Actual runtime and presentation initialized");
            Require(game.Assets && game.Assets.EvePrefab && game.Assets.PlatformPrefab &&
                game.Assets.CockpitPrefab && game.Assets.CollectiblePrefab &&
                game.Assets.ObstaclePrefabs != null && game.Assets.ObstaclePrefabs.Length >= 8 &&
                game.Assets.ObstaclePrefabs.Take(8).All(asset => asset) &&
                game.Assets.LandmarkPrefabs != null && game.Assets.LandmarkPrefabs.Length >= 3 &&
                game.Assets.LandmarkPrefabs.Take(3).All(asset => asset), "Actual generated scene library populated");
            Require(game.Assets.MenuArtwork && game.Assets.ActionIcons &&
                game.Assets.Animations != null && game.Assets.Animations.Length >= 15 &&
                game.Assets.Animations.Take(15).All(asset => asset) &&
                game.Assets.OutfitMaterials != null && game.Assets.OutfitMaterials.Length >= 8 &&
                game.Assets.OutfitMaterials.Take(8).All(asset => asset), "Actual artwork, 15 animations and outfit materials populated");
            Require(runner.GetComponentsInChildren<Renderer>().Any(value => value is SkinnedMeshRenderer || value.GetComponent<MeshFilter>()),
                "Actual runner mesh instantiated");
            Require(game.Assets.Music && game.Assets.Music.length > 1 &&
                game.Assets.Sounds.Length == Enum.GetValues(typeof(FeedbackKind)).Length &&
                game.Assets.Sounds.All(value => value && value.length > .05f),
                "Generated music and all feedback sounds contain audio");
            var meshes = new List<Mesh>();
            var generatedPrefabs = new List<GameObject> { game.Assets.EvePrefab, game.Assets.CockpitPrefab,
                game.Assets.PlatformPrefab, game.Assets.CollectiblePrefab, game.Assets.ControllerPrefab };
            generatedPrefabs.AddRange(game.Assets.ObstaclePrefabs);
            generatedPrefabs.AddRange(game.Assets.LandmarkPrefabs);
            foreach (var prefab in generatedPrefabs.Where(value => value))
            {
                meshes.AddRange(prefab.GetComponentsInChildren<MeshFilter>(true).Select(value => value.sharedMesh).Where(value => value));
                meshes.AddRange(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(value => value.sharedMesh).Where(value => value));
            }
            Require(meshes.Count > 0 && meshes.All(value => value.lodCount > 1),
                "All generated game meshes have native reduced LODs");
            var deck = outside.Find("Rail Deck 0");
            var deckRenderer = deck.GetComponentInChildren<MeshRenderer>();
            Vector3 measured = deckRenderer.bounds.size;
            Require(Mathf.Abs(measured.x - 7.8f) < .02f && Mathf.Abs(measured.y - .36f) < .02f &&
                Mathf.Abs(measured.z - 6.02f) < .02f, "Actual generated platform dimensions", measured.ToString("F3"));
            Require(Mathf.Abs(deckRenderer.bounds.max.y - deck.position.y) < .01f,
                "Deck top is below runner feet, clear of UI");
            report.RuntimeExecuted = true;
            report.Scene = game.gameObject.scene.path;
            report.XrObserved = XRSettings.isDeviceActive;
            Require(!report.XrObserved, "Editor acceptance is desktop-only, not a device test");
            cameraPoses.Clear();
            Transform camera = game.ViewCamera.transform;
            // Capture camera-local pose independently of simulation/presentation helpers.
            var initialCameraLocalPosition = camera.localPosition;
            for (Transform ancestor = camera; ancestor && !ancestor.GetComponent<RiftlineNavigationRig>(); ancestor = ancestor.parent)
                cameraPoses.Add(new PoseSnapshot(ancestor));
            Require(initialCameraLocalPosition == camera.localPosition, "Independent camera-local snapshot captured");
            scratch = new Mesh { name = "Riftline QA transient geometry counter" };
            Time.timeScale = 8f;
            Time.fixedDeltaTime = .0025f;
            // Unity scales this cap with timeScale; keep each simulated rendered frame <= .08s.
            Time.maximumDeltaTime = .01f;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            FocusGameView();
            contract = fundingRuns = firstClearTotal = page = 0;
            manualVariant = 0;
            alternate = endless = supplement = manualRun = false;
            lastRendered = -1;
            lastRenderAt = EditorApplication.timeSinceStartup;
            if (report.Scenario == "ManualStart") BeginManual();
            else
            {
                previousMouse = Mouse.current;
                mouse = InputSystem.AddDevice<Mouse>("RiftlineAcceptanceMouse");
                mouse.MakeCurrent();
                menuInputStep = 0;
                ChangeStage(Stage.MenuInput);
            }
        }

        static void FocusGameView()
        {
            Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (type != null) EditorWindow.GetWindow(type).Focus();
            report.FocusBypassRequested = false;
        }

        static Transform FindDescendant(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(value => value.name == name);

        static void OnRendered(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (game && cameras != null && cameras.Contains(game.ViewCamera)) RenderedFrame();
        }

        static void OnCameraRendered(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null && game && camera == game.ViewCamera) RenderedFrame();
        }

        static void RenderedFrame()
        {
            if (!active || !game || lastRendered == Time.frameCount) return;
            try
            {
                if (lastRendered >= 0 && Time.frameCount > lastRendered + 1)
                    report.MissedPlayerFrames += Time.frameCount - lastRendered - 1;
                lastRendered = Time.frameCount;
                lastRenderAt = EditorApplication.timeSinceStartup;
                report.RenderedFrames++;
                if (Time.unscaledDeltaTime > 1f / 72) report.FramesOver72HzBudget++;
                report.MaximumObservedDelta = Mathf.Max(report.MaximumObservedDelta, Time.deltaTime);
                report.XrObserved |= XRSettings.isDeviceActive;
                RequireSilent(!report.XrObserved, "XR became active; this acceptance harness is desktop-only.");
                RequireSilent(game.Phase != RunPhase.Running || Time.deltaTime <= .0801f,
                    "Observed running game delta exceeded .08 seconds.");
                RequireSilent(!game.Run.AutoTurn && !game.Run.UsedAutoTurn && !game.Save.Settings.AutoTurn, "AutoTurn contaminated acceptance.");
                if (!report.XrObserved)
                    RequireSilent(cameraPoses.All(value => value.Matches()), "Actual tracked-camera or XR-origin local transform changed.");
                if (current != null)
                {
                    current.RenderedFrames++;
                    current.FrameMillisecondsTotal += Time.unscaledDeltaTime * 1000;
                    if (Time.unscaledDeltaTime > 1f / 72) current.FramesOver72HzBudget++;
                    current.MaximumDelta = Mathf.Max(current.MaximumDelta, Time.deltaTime);
                    if (game.Run.BossActive) current.BossObservedSeconds += Mathf.Max(0, game.Run.Elapsed - previousElapsed);
                    current.MaximumGroundHeight = Mathf.Max(current.MaximumGroundHeight, game.Run.GroundHeight);
                    current.MinimumGroundHeight = Mathf.Min(current.MinimumGroundHeight, game.Run.GroundHeight);
                    VerifyPresentedTerrain();
                    var setPiece = outside.Find("Boss Encounter " + game.Run.RegionId);
                    Plane[] bossFrustum = game.Run.BossActive ? GeometryUtility.CalculateFrustumPlanes(game.ViewCamera) : null;
                    if (game.Run.BossActive && setPiece && setPiece.gameObject.activeInHierarchy &&
                        setPiece.GetComponentsInChildren<Renderer>().Any(value => value.enabled && !value.forceRenderingOff &&
                            (game.ViewCamera.cullingMask & (1 << value.gameObject.layer)) != 0 &&
                            GeometryUtility.TestPlanesAABB(bossFrustum, value.bounds)))
                        current.BossRenderedFrames++;
                    if (current.BossRenderedFrames >= 20 && !current.BossCaptureRequested)
                    {
                        current.BossCaptureRequested = true;
                        RequestCapture(current.Name + "-boss");
                    }
                    previousElapsed = game.Run.Elapsed;
                    SnapshotRun();
                }
                if (report.RenderedFrames % 30 == 0 || report.RenderedFrames == 1) SampleBudget();
                SubmitRenderedCaptures();
                switch (stage)
                {
                    case Stage.MenuInput: VerifyMenuInput(); break;
                    case Stage.Pages: VisitPages(); break;
                    case Stage.Countdown:
                        if (game.Phase == RunPhase.Running) { ChangeStage(Stage.Driving); RequestCapture(current.Name + "-running"); }
                        break;
                    case Stage.Driving: DriveRun(); break;
                    case Stage.PauseHold: VerifyFrozen(false); break;
                    case Stage.ResumeCountdown: VerifyFrozen(true); break;
                    case Stage.Results:
                        if (Time.frameCount - stageFrame >= 3 && EditorApplication.timeSinceStartup - stageStarted >= .2) AfterResults();
                        break;
                    case Stage.PurchaseCapture:
                        if (report.Screenshots.Last().Submitted) BeginManual();
                        break;
                    case Stage.ManualStart: AwaitManualStart(); break;
                    case Stage.Manual: ManualTick(); break;
                    case Stage.FailureDriving: WaitForNaturalFailure(); break;
                    case Stage.FailureRetry: AwaitFailureRetry(); break;
                    case Stage.FailurePause: VerifyRetryPause(); break;
                    case Stage.FailureResume: FinishRetryResume(); break;
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        static void VerifyMenuInput()
        {
            // Let Game-view sizing and initial UI actions settle before the first pointer event.
            if (Time.frameCount - stageFrame < (menuInputStep == 0 ? 20 : 4)) return;
            switch (menuInputStep++)
            {
                case 0: QueueMouseButton("Contracts", true); break;
                case 1: QueueMouseButton("Contracts", false); break;
                case 2:
                    Require(ui.CurrentPage == "Contracts", "Actual mouse click opens Contracts");
                    Require(ui.TryOpenPage("Home"), "Home restored for mouse Settings check");
                    break;
                case 3: QueueMouseButton("Settings", true); break;
                case 4: QueueMouseButton("Settings", false); break;
                case 5:
                    Require(ui.CurrentPage == "Settings", "Actual mouse click opens Settings");
                    var scroll = ui.Panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
                    menuScrollBefore = scroll.verticalNormalizedPosition;
                    Vector3 position = game.ViewCamera.WorldToScreenPoint(scroll.viewport.TransformPoint(scroll.viewport.rect.center));
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = position, scroll = new Vector2(0, -120) });
                    break;
                case 6:
                    var observed = ui.Panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
                    Require(observed.verticalNormalizedPosition < menuScrollBefore - .01f, "Actual mouse wheel scrolls Settings");
                    QueueMouseButton("Back", true);
                    break;
                case 7: QueueMouseButton("Back", false); break;
                default:
                    Require(ui.CurrentPage == "Home", "Actual mouse Back returns Home");
                    ChangeStage(Stage.Pages);
                    return;
            }
            stageFrame = Time.frameCount;
        }

        static void QueueMouseButton(string name, bool pressed)
        {
            var button = ui.Panel.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(value => value.name == name);
            // The button may have been replaced by its click; releases use the previous position.
            Vector2 position = mouse.position.ReadValue();
            if (button)
            {
                var rect = (RectTransform)button.transform;
                position = game.ViewCamera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
            }
            RequireSilent(!pressed || button, "Mouse target button missing: " + name);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = pressed ? (ushort)1 : (ushort)0 });
        }

        static void VisitPages()
        {
            if (Time.frameCount - stageFrame < 3) return;
            if (ui.CurrentPage == "Outfits")
            {
                var planes = GeometryUtility.CalculateFrustumPlanes(game.ViewCamera);
                Require(ui.Panel.GetComponentsInChildren<SkinnedMeshRenderer>().Any(value =>
                    value.enabled && !value.forceRenderingOff && value.bounds.size.y > .01f &&
                    (game.ViewCamera.cullingMask & (1 << value.gameObject.layer)) != 0 &&
                    GeometryUtility.TestPlanesAABB(planes, value.bounds)),
                    "Actual outfit preview survives repeated page rebuild", game.Save.Settings.Language);
            }
            int count = Pages.Length * 2;
            if (page < count)
            {
                string language = page < Pages.Length ? "zh" : "en";
                game.Save.Settings.Language = language;
                game.SaveSettings();
                string name = Pages[page % Pages.Length];
                Require(ui.TryOpenPage(name) && ui.CurrentPage == name, "Page " + language + "/" + name);
                if (name == "Outfits") Require(ui.TryOpenPage(name), "Repeated outfit page navigation", language);
                Require(ui.VisibleButtonCount > 0, "Page has real interactive buttons", name);
                RequestCapture("phase-zero-" + language + "-" + name.ToLowerInvariant());
                page++;
                stageFrame = Time.frameCount;
                return;
            }
            Require(!ui.TryOpenPage("InvalidPage"), "Invalid UI navigation rejected");
            Require(game.Progress.IsRegionUnlocked(0) && !game.Progress.IsRegionUnlocked(1) &&
                !game.Progress.IsRegionUnlocked(2) && !game.Progress.IsEndlessUnlocked, "Clean runtime gates");
            if (report.Scenario == "TestPhaseZero") Finish(true, "Phase-zero scene/UI smoke checks complete.");
            else if (report.Scenario == "FailureRetry") BeginNaturalFailure();
            else
            {
                if (report.Scenario == "ReplayRegression") contract = 5;
                ChangeStage(Stage.Launch);
            }
        }

        static PoseSnapshot replayProbe;

        static void BeginNaturalFailure()
        {
            contract = 1;
            manualRun = true;
            game.ShowHome();
            game.Save.Settings.Language = "en";
            game.SaveSettings();
            previousKeyboard = Keyboard.current;
            keyboard = InputSystem.AddDevice<Keyboard>("RiftlineFailureKeyboard");
            QueueKeys();
            BeginEvidence("natural-collision-failure", "QueuedCollisionKeyboard");
            manualStep = 0;
            Require(game.Progress.IsContractUnlocked(contract), "Failure witness uses an ordinarily unlocked contract");
            game.StartContract(contract);
            ObserveSeed();
            Require(game.Phase == RunPhase.Countdown, "Failure witness enters ordinary countdown");
            ChangeStage(Stage.FailureDriving);
        }

        static void WaitForNaturalFailure()
        {
            if (game.Phase == RunPhase.Countdown) return;
            if (game.Phase == RunPhase.Running)
            {
                if (manualStep == 0)
                {
                    // Select a real barrier lane through queued input, then deliberately do not jump.
                    var barrier = game.Run.Events.First(value => value.Kind == ObstacleKind.LowBarrier &&
                        value.TutorialLesson < 0 && value.Distance > game.Run.Distance);
                    if (barrier.Lane < game.Run.Lane) QueueKeys(Key.A);
                    else if (barrier.Lane > game.Run.Lane) QueueKeys(Key.D);
                    manualStep = 1;
                }
                else if (manualStep == 1)
                {
                    QueueKeys();
                    manualStep = 2;
                }
                return;
            }
            Require(game.Phase == RunPhase.Failed && !string.IsNullOrEmpty(game.Run.FailureReason),
                "Ordinary untouched hazards cause a real collision failure", game.Run.FailureReason);
            manualStep = 0;
            ChangeStage(Stage.Settlement);
        }

        static void VerifyTerminalIndicators()
        {
            // Inspect the rendered hierarchy independently of World pooling/visibility helpers.
            LineRenderer[] foreground = outside.GetComponentsInChildren<LineRenderer>()
                .Where(value => value.enabled && value.gameObject.activeInHierarchy &&
                    (value.name == "Danger Boundary" || value.name == "Exit Direction")).ToArray();
            Require(foreground.Length == 0, "Terminal results hide all live hazard and turn indicators",
                current.Name + "; active=" + foreground.Length + "; names=" +
                string.Join(",", foreground.Select(value => value.name + "/" + value.transform.parent.name)));
        }

        static void AfterFailureResults()
        {
            if (!ui.ResultsVisible) return;
            if (current.Name != "natural-collision-failure")
            {
                VerifyTerminalIndicators();
                Require(current.Durable && !current.Victory && current.FailureReason == "safe_exit",
                    "Retried run safely exits with a durable ordinary result");
                Finish(true, "Actual collision loss, moving generated-runner replay, queued Enter retry, queued pause/resume and safe exit passed.");
                return;
            }
            if (manualStep == 0)
            {
                VerifyTerminalIndicators();
                Require(!current.Victory && current.Durable && !string.IsNullOrEmpty(current.FailureReason),
                    "Natural failure has a durable non-victory settlement");
                var replay = ui.GetComponentsInChildren<Transform>()
                    .FirstOrDefault(value => value.name == "Replay Eve Anchor");
                Require(replay && replay.GetComponentsInChildren<Renderer>().Any(value => value.enabled),
                    "Failure results instantiate the generated runner in the recorded replay");
                replayProbe = new PoseSnapshot(replay);
                holdUntil = Time.unscaledTime + .7f;
                manualStep = 1;
                return;
            }
            if (Time.unscaledTime < holdUntil || !report.Screenshots.Last().Submitted) return;
            Require(!replayProbe.Matches(), "Failure replay actually advances its recorded runner pose");
            BeginEvidence("queued-keyboard-retry", "ManualKeyboard");
            manualStep = 0;
            manualStartObserved = false;
            QueueKeys(Key.Enter);
            ChangeStage(Stage.FailureRetry);
        }

        static void AwaitFailureRetry()
        {
            if (game.Phase == RunPhase.Countdown)
            {
                if (!manualStartObserved)
                {
                    Require(game.Run.ActiveContract.Id == contract && game.Run.Elapsed == 0,
                        "Queued Enter retries the same contract from its ordinary start");
                    ObserveSeed();
                    Require(current.Seed == report.Runs[0].Seed, "Contract retry retains its deterministic seed");
                    QueueKeys();
                    manualStartObserved = true;
                }
                return;
            }
            Require(game.Phase == RunPhase.Running && manualStartObserved,
                "Actual retry countdown reaches Running");
            if (manualStep == 0)
            {
                RequestCapture("retry-running-direction-icons");
                stageFrame = Time.frameCount;
                manualStep = 1;
                return;
            }
            if (Time.frameCount - stageFrame < 4 || !report.Screenshots.Last().Submitted) return;
            frozen = null;
            QueueKeys(Key.Escape);
            ChangeStage(Stage.FailurePause);
        }

        static void VerifyRetryPause()
        {
            Require(game.Phase == RunPhase.Paused, "Queued Escape pauses the actual retried run");
            if (frozen == null)
            {
                QueueKeys();
                FreezeSnapshot();
                RequestCapture("retry-keyboard-pause");
                return;
            }
            RequireSilent(game.Run.Elapsed == frozenElapsed && game.Run.Distance == frozenDistance &&
                frozen.All(value => value.Matches()), "Retried run or outside transforms moved while paused.");
            if (Time.unscaledTime < holdUntil || !report.Screenshots.Last().Submitted) return;
            RecordCheck("Retried run and actual outside transforms remain frozen", true, "");
            QueueKeys(Key.Enter);
            ChangeStage(Stage.FailureResume);
        }

        static void FinishRetryResume()
        {
            if (game.Phase == RunPhase.Countdown)
            {
                QueueKeys();
                RequireSilent(game.Run.Elapsed == frozenElapsed && game.Run.Distance == frozenDistance &&
                    frozen.All(value => value.Matches()), "Retried run moved during resume countdown.");
                return;
            }
            Require(game.Phase == RunPhase.Running, "Queued Enter resumes the retried run");
            QueueKeys();
            game.SafeEndRun();
            Require(game.Phase == RunPhase.Completed, "Retried run accepts an ordinary safe exit");
            ChangeStage(Stage.Settlement);
        }

        static void LaunchRun()
        {
            Require(!game.Progress.HasPendingSave && !SettlementPending(), "No pending persistence before replacing run");
            game.ShowHome();
            game.Save.Settings.AlignmentMode = contract % 3;
            game.Save.Settings.Language = contract % 2 == 0 ? "zh" : "en";
            game.SaveSettings();
            Require(!game.Progress.HasPendingSave, "Run settings saved");
            BeginEvidence(endless ? (supplement ? "funding-" + fundingRuns : "endless-600") :
                (alternate ? "alternate-" : "primary-") + contract.ToString("00"), endless ? "Endless" : "Contract");
            pilot = new RunAutopilot { PreferredRoute = alternate ? 1 : -1 };
            if (endless) game.StartEndless(); else
            {
                Require(game.Progress.IsContractUnlocked(contract), "Contract unlocked through actual progress", contract.ToString());
                game.StartContract(contract);
            }
            Require(game.Phase == RunPhase.Countdown, "Real runtime start entered countdown", game.StatusMessage);
            ObserveSeed();
            ChangeStage(Stage.Countdown);
        }

        static void BeginEvidence(string name, string mode)
        {
            current = new RunEvidence
            {
                Name = name, Mode = mode, Status = "Running", ContractId = endless ? 5 : contract,
                PreferredRoute = alternate ? 1 : -1, AlignmentMode = game.Save.Settings.AlignmentMode,
                ChipsBefore = game.Save.Chips
            };
            archiveBefore = (bool[])game.Save.Archives.Clone();
            report.Runs.Add(current);
            previousElapsed = 0;
            runStarted = EditorApplication.timeSinceStartup;
            frozen = null;
            turnsBefore = 0;
            cameraChecked = false;
        }

        static void DriveRun()
        {
            if (game.Phase == RunPhase.Failed || game.Phase == RunPhase.Completed)
            {
                ChangeStage(Stage.Settlement);
                return;
            }
            RequireSilent(game.Phase == RunPhase.Running, "Unexpected pause/tracking loss during autopilot; deliberate QA resume required.");
            var ground = game.Run.GroundProfile;
            double slopeAge = ground == null ? -1 : game.Run.Elapsed - RunSimulation.TimeAtDistance(ground.GroundTransitionStartDistance);
            if (!endless && !alternate && contract == 8 && frozen == null && slopeAge > .8f && slopeAge < 1.6f)
            {
                game.PauseRun();
                Require(game.Phase == RunPhase.Paused, "Pause on elevated transition enters paused state");
                FreezeSnapshot();
                current.PauseOnSlopeVerified = true;
                RequestCapture(current.Name + "-pause-on-slope");
                ChangeStage(Stage.PauseHold);
                return;
            }
            if (!endless && !alternate && contract < 3 && game.Run.TurnsCompleted > turnsBefore && frozen == null)
            {
                game.PauseRun();
                Require(game.Phase == RunPhase.Paused, "PauseAtTurn enters actual paused state", "alignment=" + current.AlignmentMode);
                FreezeSnapshot();
                RequestCapture(current.Name + "-pause-at-turn");
                ChangeStage(Stage.PauseHold);
                return;
            }
            if (endless && game.Run.Elapsed >= targetEndless)
            {
                game.SafeEndRun();
                ChangeStage(Stage.Settlement);
                return;
            }
            GameCommand command = pilot.NextCommand(game.Run);
            if (command != GameCommand.None)
            {
                TraceCommand(command, "RunAutopilot");
                game.SendCommand(command);
            }
        }

        static void FreezeSnapshot()
        {
            frozen = outside.GetComponentsInChildren<Transform>(true).Select(value => new PoseSnapshot(value)).ToList();
            frozenElapsed = game.Run.Elapsed;
            frozenDistance = game.Run.Distance;
            holdUntil = Time.unscaledTime + .5f;
        }

        static void VerifyFrozen(bool countdown)
        {
            if (countdown && game.Phase == RunPhase.Running)
            {
                RequireSilent(Mathf.Abs(game.Run.Elapsed - frozenElapsed) < .0001f &&
                    Mathf.Abs(game.Run.Distance - frozenDistance) < .0001f, "Simulation advanced on countdown transition.");
                Require(Time.time - resumeStarted >= 2.99f, "Resume countdown lasts three actual scaled seconds");
                current.PauseAtTurnVerified = true;
                RecordCheck("Actual PauseAtTurn freeze", true, "alignment=" + current.AlignmentMode + "; transforms=" + frozen.Count);
                // Running permits presentation motion again. Keep the marker to avoid another pause.
                ChangeStage(Stage.Driving);
                return;
            }
            RequireSilent(frozen != null && frozen.All(value => value.Matches()), "Actual external-world transform moved while paused/counting down.");
            RequireSilent(Mathf.Abs(game.Run.Elapsed - frozenElapsed) < .0001f &&
                Mathf.Abs(game.Run.Distance - frozenDistance) < .0001f, "Simulation moved while paused/counting down.");
            if (!countdown)
            {
                RequireSilent(game.Phase == RunPhase.Paused, "PauseAtTurn was unexpectedly resumed.");
                if (Time.unscaledTime < holdUntil || Time.frameCount - stageFrame < 5) return;
                Require(ui.TryOpenPage("Settings") && ui.TryOpenPage("Pause"), "Pause > Settings > Pause navigation");
                game.ResumeRun();
                Require(game.Phase == RunPhase.Countdown, "PauseAtTurn resumes through actual countdown");
                resumeStarted = Time.time;
                ChangeStage(Stage.ResumeCountdown);
            }
            else RequireSilent(game.Phase == RunPhase.Countdown, "Unexpected phase during resume countdown.");
        }

        static bool SettlementPending() => (bool)settlementHook.GetValue(game);

        static void WaitForSettlement()
        {
            if (SettlementPending() || game.Progress.HasPendingSave || game.LastResult == null ||
                game.LastResult.PersistencePending) return;
            RunResult result = game.LastResult;
            SnapshotRun();
            current.Durable = true;
            current.SettlementId = result.SettlementId;
            current.ChipsAfter = game.Save.Chips;
            current.PickupChips = result.ChipsEarned;
            current.FirstClearReward = result.FirstClearReward;
            current.Stars = result.Stars;
            current.Victory = result.Victory;
            current.Status = "Settled";
            Require(current.ChipsAfter - current.ChipsBefore == result.ChipsEarned + result.FirstClearReward,
                "Actual settlement earnings reconcile", current.Name);
            Require(game.Save.SettledRuns.Count(value => value == result.SettlementId) == 1, "Settlement persisted exactly once", current.Name);
            VerifyReload();
            if (!manualRun)
            {
                if (endless)
                {
                    Require(game.Run.Elapsed >= targetEndless && game.Run.FailureReason.Length == 0,
                        "Endless survived target and safely settled", current.Name);
                    if (!supplement)
                        Require(game.Run.Speed >= 9.99f && game.Run.CombinationBudget >= 3 &&
                            game.Save.EndlessBestScore >= result.Score, "Endless 600s pressure and eligible actual record");
                }
                else
                {
                    Require(result.Victory && result.Stars == 3, "Contract completed with real three-star result",
                        current.Name + ": " + result.FailureReason);
                    int expectedReward = alternate ? 0 : game.Catalog.Contracts[contract].FirstClearReward;
                    Require(result.FirstClearReward == expectedReward, "First-clear/replay reward matches", current.Name);
                    Require(game.Run.Events.Where(value => value.Kind == ObstacleKind.Fork && value.Passed)
                        .All(value => value.SelectedBranch == current.PreferredRoute), "Every actual fork selected the requested branch", current.Name);
                    firstClearTotal += result.FirstClearReward;
                    if (contract == 0) Require(game.Run.TutorialComplete, "Actual tutorial lessons completed");
                    if (contract == 1) Require(game.Run.PhaseUses > 0, "Actual phase lesson executed");
                    if (game.Run.ActiveContract.Boss)
                    {
                        Require(current.BossObservedSeconds >= 44.8f && current.BossObservedSeconds <= 45.2f,
                            "Actual boss window observed for 45 seconds", current.BossObservedSeconds.ToString("0.000"));
                        Require(current.BossRenderedFrames > 100, "Generated boss set piece rendered", current.Name);
                    }
                    if (contract == 8 || contract == 13)
                        Require(current.HeightRenderedFrames > 100 &&
                            current.MaximumGroundHeight - current.MinimumGroundHeight > 1f,
                            "Actual elevated route traversed with independent camera", current.Name);
                    if (contract == 8 && !alternate)
                        Require(current.PauseOnSlopeVerified, "Actual road and runner frozen during slope pause/resume", current.Name);
                }
            }
            report.CompletedRuns++;
            RequestCapture(current.Name + "-results");
            ChangeStage(Stage.Results);
            WriteReport();
        }

        static void VerifyReload()
        {
            SaveData loaded = new GameSaveStore(report.SaveDirectory).Load();
            Require(loaded.Revision == game.Save.Revision && loaded.Chips == game.Save.Chips &&
                loaded.ContractStars.SequenceEqual(game.Save.ContractStars) &&
                loaded.Archives.SequenceEqual(game.Save.Archives) &&
                loaded.OwnedOutfits.SequenceEqual(game.Save.OwnedOutfits) &&
                loaded.EquippedOutfit == game.Save.EquippedOutfit &&
                loaded.SettledRuns.SequenceEqual(game.Save.SettledRuns), "Fresh store reload matches runtime durable progress");
        }

        static void AfterResults()
        {
            if (report.Scenario == "FailureRetry") { AfterFailureResults(); return; }
            Require(ui.ResultsVisible && ui.TryOpenPage("Results"), "Actual results panel becomes visible", current.Name);
            if (report.Scenario == "ReplayRegression")
            {
                VerifyTerminalIndicators();
                Require(report.Screenshots.Last().Submitted, "Terminal results were captured before replacing the run");
                current = null;
                if (endless)
                    Finish(true, "Actual boss victory and 600-second endless terminal results have no live hazard/turn geometry.");
                else
                {
                    Require(game.Progress.IsEndlessUnlocked, "Actual contract 5 victory unlocks the endless visual regression");
                    endless = true; targetEndless = 600;
                    ChangeStage(Stage.Launch);
                }
                return;
            }
            current = null;
            if (manualRun)
            {
                manualVariant++;
                if (manualVariant <= 3) BeginManual();
                else Finish(true, "Requested Editor sequence and real queued-keyboard priority checks completed.");
                return;
            }
            if (endless)
            {
                if (supplement) fundingRuns++;
                ChangeStage(Stage.Purchase);
                return;
            }
            if (!alternate)
            {
                int clears = game.Save.ContractStars.Count(value => value > 0);
                if (contract == 3) Require(!game.Progress.IsRegionUnlocked(1), "Region 1 locked after four actual clears");
                if (contract == 4) Require(game.Progress.IsRegionUnlocked(1), "Region 1 opens after five actual clears");
                if (contract == 5) Require(game.Progress.IsEndlessUnlocked, "Endless opens after actual contract 5 clear");
                if (contract == 9) Require(!game.Progress.IsRegionUnlocked(2), "Region 2 locked after four region-1 clears");
                if (contract == 10) Require(game.Progress.IsRegionUnlocked(2), "Region 2 opens after five actual region-1 clears");
                Require(clears == contract + 1, "Sequential progression contains no injected clears");
            }
            contract++;
            if (report.Scenario == "PauseAtTurn" && contract == 3)
            {
                Require(report.Runs.All(value => value.PauseAtTurnVerified), "All three alignment-mode PauseAtTurn probes");
                Finish(true, "Three actual alignment-mode PauseAtTurn runs completed.");
                return;
            }
            if (contract < 18) { ChangeStage(Stage.Launch); return; }
            Require(game.Save.ContractStars.Sum() == 54 && firstClearTotal == 3600, "All18 actual progression: 54 stars / 3600 first-clear chips");
            if (report.Scenario == "All18") { Finish(true, "All18 fresh-save actual progression completed."); return; }
            if (!alternate)
            {
                alternate = true; contract = 0;
                ChangeStage(Stage.Launch);
                return;
            }
            Require(game.Save.Archives.Count(value => value) == 30, "Both executed contract branches acquired all 30 archives");
            if (report.Scenario == "Branches") { Finish(true, "Both branch passes completed: 30 archives / 54 stars."); return; }
            endless = true; alternate = false; contract = 5; targetEndless = 600;
            ChangeStage(Stage.Launch);
        }

        static void PurchaseOutfits()
        {
            if (report.Scenario == "Endless600") { Finish(true, "Endless survived and durably settled 600 gameplay seconds."); return; }
            game.ShowHome();
            int remainingCost = game.Catalog.Outfits.Where(value => !game.Save.OwnedOutfits[value.Id]).Sum(value => value.Cost);
            if (game.Save.Chips < remainingCost)
            {
                if (fundingRuns >= 29) throw new InvalidOperationException("29 supplemental real 300s endless runs did not fund remaining outfits.");
                supplement = true; targetEndless = 300;
                ChangeStage(Stage.Launch);
                return;
            }
            int spent = 0;
            foreach (OutfitDefinition outfit in game.Catalog.Outfits)
            {
                int balance = game.Save.Chips;
                bool owned = game.Save.OwnedOutfits[outfit.Id];
                Require(game.Progress.BuyOutfit(outfit.Id), "Actual outfit purchase persists", outfit.Id.ToString());
                Require(game.Save.Chips == balance - (owned ? 0 : outfit.Cost) && game.Save.Chips >= 0,
                    "Outfit charged exactly once", outfit.Id.ToString());
                if (!owned) spent += outfit.Cost;
                Require(game.Progress.BuyOutfit(outfit.Id) && game.Save.Chips == balance - (owned ? 0 : outfit.Cost),
                    "Repeated outfit purchase does not charge again", outfit.Id.ToString());
                game.Progress.EquipOutfit(outfit.Id);
                Require(!game.Progress.HasPendingSave && game.Save.EquippedOutfit == outfit.Id, "Actual outfit equip persists", outfit.Id.ToString());
                VerifyReload();
            }
            Require(spent == 6700 && game.Save.OwnedOutfits.All(value => value), "All outfits bought with 6700 actual earned chips");
            game.SaveSettings();
            Require(ui.TryOpenPage("Outfits"), "Purchased outfits page available");
            RequestCapture("all-outfits-earned");
            if (report.Scenario == "Outfits") { Finish(true, "Both branches, endless 600s and all earned outfit purchases completed."); return; }
            ChangeStage(Stage.PurchaseCapture);
        }

        static void BeginManual()
        {
            manualRun = true; endless = alternate = supplement = false; contract = manualVariant == 0 ? 0 : 1;
            Require(!game.Progress.HasPendingSave && !SettlementPending(), "Manual input starts after durable settlement");
            game.ShowHome();
            Time.timeScale = 1f;
            game.Save.Settings.SwapJumpSlide = false;
            game.Save.Settings.TriggerHand = manualVariant == 2 ? 1 : 2;
            game.SaveSettings();
            if (keyboard == null)
            {
                previousKeyboard = Keyboard.current;
                keyboard = InputSystem.AddDevice<Keyboard>("RiftlineAcceptanceKeyboard");
            }
            BeginEvidence("manual-" + manualVariant, "ManualKeyboard");
            current.PreferredRoute = 0;
            manualStep = 0;
            probePending = false;
            manualStartObserved = false;
            manualHeldFrames = 0;
            committedContext = null;
            if (manualVariant == 0)
            {
                QueueKeys(Key.Enter);
                ChangeStage(Stage.ManualStart);
            }
            else
            {
                QueueKeys();
                game.StartContract(1);
                Require(game.Phase == RunPhase.Countdown, "Phase input probe uses real runtime start");
                ChangeStage(Stage.ManualStart);
            }
        }

        static void AwaitManualStart()
        {
            if (game.Phase == RunPhase.Countdown)
            {
                if (!current.SeedObserved) ObserveSeed();
                if (manualVariant == 0 && !manualStartObserved && Time.frameCount > queuedFrame)
                {
                    RecordCheck("Queued Enter starts real runtime", true, "InputSystem keyboard event -> RiftlineInput.Poll");
                    manualStartObserved = true;
                    QueueKeys();
                }
                return;
            }
            if (game.Phase == RunPhase.Running) ChangeStage(Stage.Manual);
        }

        static void QueueKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            queuedFrame = Time.frameCount;
        }

        static void Probe(string label, GameCommand expected, params Key[] keys)
        {
            manualLabel = label; manualExpected = expected.ToString();
            replayBefore = game.Run.Replay.Count;
            laneBefore = game.Run.Lane;
            probePending = true;
            QueueKeys(keys);
        }

        static bool CheckProbe()
        {
            if (Time.frameCount <= queuedFrame) return false;
            var newCommands = game.Run.Replay.Skip(replayBefore).Where(value => value.Command != GameCommand.None).ToArray();
            GameCommand actual;
            if (manualExpected == GameCommand.Pause.ToString())
            {
                // RiftlineGame owns Pause and intentionally does not forward it to Run.Handle/Replay.
                Require(newCommands.Length == 0 && game.Phase == RunPhase.Paused &&
                    game.TrackingReady && keyboard.escapeKey.isPressed, manualLabel, "real phase, tracking and queued Escape state");
                actual = GameCommand.Pause;
            }
            else
            {
                Require(newCommands.Length == 1 && newCommands[0].Command.ToString() == manualExpected,
                    manualLabel, "actual commands=" + string.Join(",", newCommands.Select(value => value.Command.ToString())));
                actual = newCommands[0].Command;
            }
            TraceCommand(actual, "InputSystem.QueueStateEvent");
            if (actual == GameCommand.Jump)
                Require(game.Run.Height > 0 && game.Run.Lane == laneBefore && !game.Run.Sliding, manualLabel + " actual jump/lane");
            if (actual == GameCommand.Slide)
                Require(game.Run.Sliding && game.Run.Height <= .001f && game.Run.Lane == laneBefore, manualLabel + " actual slide/lane");
            if (actual == GameCommand.Phase)
                Require(game.Run.PhaseRemaining > 0 && game.Run.Height <= .001f &&
                    !game.Run.Sliding && game.Run.Lane == laneBefore, manualLabel + " actual phase priority");
            probePending = false;
            manualHeldFrames = 0;
            return true;
        }

        static void ManualTick()
        {
            RequireSilent(game.Phase == RunPhase.Running || game.Phase == RunPhase.Paused || game.Phase == RunPhase.Countdown,
                "Manual input probe run unexpectedly terminated: " + game.Run.FailureReason);
            if (probePending)
            {
                if (!CheckProbe()) return;
                manualStep++;
                holdUntil = Time.unscaledTime + .8f;
                return;
            }
            if (manualVariant > 0) { ManualPhaseTick(); return; }
            switch (manualStep)
            {
                case 0: Probe("Jump outranks slide and ordinary steer", GameCommand.Jump, Key.Space, Key.S, Key.A); break;
                case 1:
                    if (++manualHeldFrames < 4) return;
                    Require(game.Run.Replay.Count == replayBefore + 1 && game.Run.Lane == laneBefore,
                        "Held keyboard has no repeated gameplay edge",
                        "before=" + replayBefore + "; after=" + game.Run.Replay.Count +
                        "; lane=" + laneBefore + "->" + game.Run.Lane +
                        "; new commands=" + string.Join(",", game.Run.Replay.Skip(replayBefore).Select(value => value.Command.ToString())));
                    QueueKeys(); manualStep++; break;
                case 2:
                    if (game.Run.Height > .001f || game.Run.Elapsed < 1.5f) return;
                    Probe("Ctrl slide alias", GameCommand.Slide, Key.LeftCtrl); break;
                case 3:
                    QueueKeys(); manualStep++; break;
                case 4:
                    if (game.Run.Sliding) return;
                    game.Save.Settings.SwapJumpSlide = true; game.SaveSettings();
                    Probe("Swapped Ctrl jumps", GameCommand.Jump, Key.RightCtrl); break;
                case 5: QueueKeys(); manualStep++; break;
                case 6:
                    if (game.Run.Height > .001f) return;
                    Probe("Swapped Space slides", GameCommand.Slide, Key.Space); break;
                case 7: QueueKeys(); manualStep++; break;
                case 8:
                    if (game.Run.Sliding) return;
                    game.Save.Settings.SwapJumpSlide = false; game.SaveSettings();
                    manualStep++; break;
                case 9:
                    committedContext = game.Run.Events.FirstOrDefault(value => !value.Resolved && !value.Passed &&
                        value.Distance >= game.Run.Distance && value.Distance - game.Run.Distance < game.Run.Speed * 1.8f &&
                        (value.Kind == ObstacleKind.TurnLeft || value.Kind == ObstacleKind.TurnRight));
                    if (committedContext == null) return;
                    Probe("Direction context outranks jump", committedContext.Branch < 0 ? GameCommand.Left : GameCommand.Right,
                        committedContext.Branch < 0 ? Key.A : Key.D, Key.Space);
                    break;
                case 10:
                    Require(committedContext.TurnCommitted && committedContext.SelectedBranch == committedContext.Branch &&
                        game.Run.Height <= .001f && game.Run.Lane == laneBefore, "Actual turn commitment suppresses jump/lane");
                    QueueKeys(); manualStep++; break;
                case 11:
                    if (Time.frameCount <= queuedFrame) return;
                    Probe("Later direction edge is consumed by committed context", committedContext.Branch < 0 ? GameCommand.Right : GameCommand.Left,
                        committedContext.Branch < 0 ? Key.D : Key.A);
                    break;
                case 12:
                    Require(committedContext.TurnCommitted && committedContext.SelectedBranch == committedContext.Branch &&
                        game.Run.Lane == laneBefore, "First actual direction commitment stays immutable");
                    QueueKeys(); manualStep++; break;
                case 13:
                    if (!committedContext.Passed) return;
                    Probe("Steer after context crossing", GameCommand.Left, Key.A); break;
                case 14:
                    if (Time.unscaledTime < holdUntil) return;
                    Require(game.Run.Lane == 0 && game.Run.Replay.Count == replayBefore + 1,
                        "Held direction does not repeatedly change lanes");
                    QueueKeys(); manualStep++; break;
                case 15:
                    if (Time.frameCount <= queuedFrame) return;
                    Probe("Keyboard neutral rearms direction", GameCommand.Right, Key.D); break;
                case 16:
                    Require(game.Run.Lane == 1, "Actual rearmed direction returns to center");
                    QueueKeys(); game.SafeEndRun(); ChangeStage(Stage.Settlement); break;
            }
        }

        static void ManualPhaseTick()
        {
            switch (manualStep)
            {
                case 0:
                    if (game.Run.PhaseEnergy < 1f) return;
                    Require(game.Run.Elapsed < 10, "Phase probe charged through real pickup");
                    Probe(manualVariant == 3 ? "Shift phase alias outranks jump/slide/steer" : "E phase ignores TriggerHand and outranks jump/slide/steer",
                        GameCommand.Phase, manualVariant == 3 ? Key.LeftShift : Key.E, Key.Space, Key.S, Key.A);
                    break;
                case 1:
                    Require(game.Run.PhaseUses == 1, "Exactly one phase use from queued edge");
                    QueueKeys(); manualStep++; break;
                case 2:
                    if (manualVariant < 3)
                    {
                        game.SafeEndRun(); ChangeStage(Stage.Settlement); return;
                    }
                    if (Time.frameCount <= queuedFrame) return;
                    Probe("Pause outranks phase/jump/steer", GameCommand.Pause, Key.Escape, Key.E, Key.Space, Key.A);
                    break;
                case 3:
                    Require(game.Phase == RunPhase.Paused && game.Run.PhaseUses == 1, "Actual pause wins input priority");
                    QueueKeys(); FreezeSnapshot(); manualStep++; break;
                case 4:
                    RequireSilent(frozen.All(value => value.Matches()) && game.Run.Elapsed == frozenElapsed,
                        "External transforms moved during queued-input pause.");
                    if (Time.unscaledTime < holdUntil) return;
                    resumeStarted = Time.time;
                    QueueKeys(Key.Enter); manualStep++; break;
                case 5:
                    if (Time.frameCount <= queuedFrame) return;
                    Require(game.Phase == RunPhase.Countdown, "Queued Enter resumes through actual countdown");
                    QueueKeys(); manualStep++; break;
                case 6:
                    if (game.Phase == RunPhase.Running)
                    {
                        Require(game.Run.Elapsed == frozenElapsed && Time.time - resumeStarted >= 2.99f,
                            "Queued resume completes full countdown without simulation advance");
                        RecordCheck("Queued pause/resume real-world freeze", true, "transforms=" + frozen.Count);
                        game.SafeEndRun(); ChangeStage(Stage.Settlement); return;
                    }
                    RequireSilent(frozen.All(value => value.Matches()) && game.Run.Elapsed == frozenElapsed,
                        "External transforms moved during queued-input resume countdown.");
                    break;
            }
        }

        static void TraceCommand(GameCommand command, string source)
        {
            if (current == null) return;
            current.Commands++;
            if (current.Trace.Count >= 5000) throw new InvalidOperationException("Bounded per-run command trace exceeded.");
            current.Trace.Add(new CommandEvidence
            {
                Frame = Time.frameCount, Elapsed = game.Run.Elapsed, Distance = game.Run.Distance,
                Lane = game.Run.Lane, Height = game.Run.Height, Energy = game.Run.PhaseEnergy,
                Command = command.ToString(), Source = source
            });
        }

        static void ObserveSeed()
        {
            var generator = typeof(RunSimulation).GetField("generator", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(game.Run);
            object seed = generator?.GetType().GetField("seed", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(generator);
            if (seed is int actual)
            {
                current.Seed = actual;
                current.SeedObserved = true;
            }
        }

        static void SnapshotRun()
        {
            current.Elapsed = game.Run.Elapsed;
            current.Distance = game.Run.Distance;
            current.Score = game.Run.Score;
            current.PickupChips = game.Run.Chips;
            current.NearMisses = game.Run.NearMisses;
            current.Turns = game.Run.TurnsCompleted;
            current.PhaseUses = game.Run.PhaseUses;
            current.UsedAutoTurn = game.Run.UsedAutoTurn;
            current.FailureReason = game.Run.FailureReason;
            current.TutorialLessons = game.Run.TutorialLessonsCompleted;
            current.TutorialRecoveries = game.Run.TutorialRecoveryCount;
            current.LeftForks = game.Run.Events.Count(value => value.Kind == ObstacleKind.Fork && value.Passed && value.SelectedBranch < 0);
            current.RightForks = game.Run.Events.Count(value => value.Kind == ObstacleKind.Fork && value.Passed && value.SelectedBranch > 0);
            current.WallSeconds = EditorApplication.timeSinceStartup - runStarted;
            current.ArchivesAcquired.Clear();
            for (int id = 0; id < archiveBefore.Length; id++)
                if (!archiveBefore[id] && game.Save.Archives[id]) current.ArchivesAcquired.Add(id);
        }

        static void VerifyPresentedTerrain()
        {
            var run = game.Run;
            TrackEvent profile = run.GroundProfile;
            if (run.Phase != RunPhase.Running || profile == null) return;
            double age = run.Elapsed - RunSimulation.TimeAtDistance(profile.GroundTransitionStartDistance);
            if (age < .15 || age > profile.GroundTransitionSeconds - .15) return;
            float expected = ExpectedGround(run.Distance);
            float clearance = outside.InverseTransformPoint(runner.position).y - expected;
            RequireSilent(Mathf.Abs(clearance - (.04f + run.Height)) < .006f,
                "Actual runner clearance disagrees with the road elevation.");
            float roadStart = Mathf.Floor((run.Distance - 12) / 6) * 6;
            bool visibleSlope = false;
            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(game.ViewCamera);
            foreach (Transform deck in outside)
            {
                if (!deck.name.StartsWith("Rail Deck ", StringComparison.Ordinal)) continue;
                if (!int.TryParse(deck.name.Substring(10), out int index)) continue;
                float distance = Mathf.Max(0, roadStart + index * 6);
                if (Mathf.Abs(distance - run.Distance) > 12) continue;
                float before = ExpectedGround(distance - 3), after = ExpectedGround(distance + 3);
                RequireSilent(Mathf.Abs(deck.localPosition.y - ExpectedGround(distance)) < .006f,
                    "Actual deck height disagrees with the independent route profile.");
                float delta = after - before;
                float expectedForwardY = delta / Mathf.Sqrt(36 + delta * delta);
                RequireSilent(Mathf.Abs((deck.localRotation * Vector3.forward).y - expectedForwardY) < .002f,
                    "Actual deck remains flat or has the wrong slope.");
                if (Mathf.Abs(delta) < .005f) continue;
                var renderers = deck.GetComponentsInChildren<MeshRenderer>();
                visibleSlope |= renderers.Any(value => value.enabled && !value.forceRenderingOff &&
                    (game.ViewCamera.cullingMask & (1 << value.gameObject.layer)) != 0 &&
                    GeometryUtility.TestPlanesAABB(frustum, value.bounds));
            }
            if (!visibleSlope) return;
            current.HeightRenderedFrames++;
            if (current.HeightRenderedFrames >= 20 && !current.HeightCaptureRequested)
            {
                current.HeightCaptureRequested = true;
                RequestCapture(current.Name + "-elevation");
            }
        }

        static float ExpectedGround(float distance)
        {
            TrackEvent profile = game.Run.GroundProfile;
            foreach (var item in game.Run.Events)
                if ((item.Kind == ObstacleKind.Rise || item.Kind == ObstacleKind.Fall) &&
                    item.GroundTransitionStartDistance <= distance &&
                    (profile == null || item.GroundTransitionStartDistance > profile.GroundTransitionStartDistance))
                    profile = item;
            if (profile == null) return 0;
            double age = RunSimulation.TimeAtDistance(distance) - RunSimulation.TimeAtDistance(profile.GroundTransitionStartDistance);
            double fraction = Math.Max(0, Math.Min(1, age / profile.GroundTransitionSeconds));
            return (float)(profile.GroundHeight + (profile.GroundTargetHeight - profile.GroundHeight) *
                fraction * fraction * (3 - 2 * fraction));
        }

        static void SampleBudget()
        {
            var sample = new Budget
            {
                Frame = Time.frameCount, SimulationDelta = Time.deltaTime,
                UnscaledFrameMilliseconds = Time.unscaledDeltaTime * 1000,
                RunnerPosition = runner.position, CameraPosition = game.ViewCamera.transform.position,
                RunnerViewport = game.ViewCamera.WorldToViewportPoint(runner.position),
                ExternalPosition = outside.position, ExternalEuler = outside.eulerAngles
            };
            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(game.ViewCamera);
            foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.forceRenderingOff) continue;
                sample.ActiveRenderers++;
                Bounds bounds = renderer.bounds;
                if (!Finite(bounds.center) || !Finite(bounds.size)) sample.InvalidBounds++;
                bool visible = (game.ViewCamera.cullingMask & (1 << renderer.gameObject.layer)) != 0 &&
                    GeometryUtility.TestPlanesAABB(frustum, bounds);
                if (visible) sample.VisibleRenderers++;
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skin) mesh = skin.sharedMesh;
                else if (renderer is LineRenderer line)
                {
                    sample.LineRenderers++;
                    scratch.Clear(); line.BakeMesh(scratch, game.ViewCamera, true); mesh = scratch;
                }
                else if (renderer is ParticleSystemRenderer)
                {
                    sample.ParticleSystems++;
                    var particles = renderer.GetComponent<ParticleSystem>();
                    if (particles) sample.LiveParticles += particles.particleCount;
                    sample.UnsupportedRenderers++;
                }
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter) mesh = filter.sharedMesh;
                    else sample.UnsupportedRenderers++;
                }
                if (mesh)
                {
                    long vertices = mesh.vertexCount, triangles = Triangles(mesh);
                    sample.ActiveVertices += vertices; sample.ActiveTriangles += triangles;
                    if (visible) { sample.VisibleVertices += vertices; sample.VisibleTriangles += triangles; }
                }
            }
            foreach (CanvasRenderer canvas in UnityEngine.Object.FindObjectsByType<CanvasRenderer>())
            {
                if (!canvas.gameObject.activeInHierarchy || canvas.cull) continue;
                sample.ActiveCanvasRenderers++;
                Mesh mesh = canvas.GetMesh();
                if (!mesh) continue;
                sample.UiVertices += mesh.vertexCount;
                sample.UiTriangles += Triangles(mesh);
            }
            Renderer[] avatar = runner.GetComponentsInChildren<Renderer>().Where(value => value.enabled &&
                !(value is LineRenderer) && !(value is ParticleSystemRenderer)).ToArray();
            if (avatar.Length > 0)
            {
                Bounds bounds = avatar[0].bounds;
                foreach (var renderer in avatar.Skip(1)) bounds.Encapsulate(renderer.bounds);
                sample.RunnerBoundsPresent = true;
                sample.RunnerBoundsCenter = bounds.center;
                sample.RunnerBoundsSize = bounds.size;
                RequireSilent(Finite(bounds.size) && bounds.size.y > .01f, "Actual avatar rendered bounds invalid.");
            }
            Vector3 centered = runner.position - game.PresentationFrame.right * game.Run.LaneX;
            sample.RunnerCenterAngle = Vector3.Angle(game.ViewCamera.transform.forward, centered - game.ViewCamera.transform.position);
            RequireSilent(sample.InvalidBounds == 0, "Non-finite actual renderer bounds observed.");
            report.LiveBudget = sample;
            AccumulatePeak(report.PeakBudget, sample);
            if (current != null)
            {
                AccumulatePeak(current.PeakBudget, sample);
                current.LastBudget = sample;
                if ((current.RenderedFrames % 300 < 30 || current.BudgetSamples.Count == 0) && current.BudgetSamples.Count < 64)
                    current.BudgetSamples.Add(sample);
            }
            if (!cameraChecked && current != null)
            {
                RecordCheck("Independent actual camera / avatar observation", true,
                    "camera-local/ancestor snapshot; viewport=" + sample.RunnerViewport + "; bounds=" + sample.RunnerBoundsSize);
                cameraChecked = true;
            }
        }

        static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        static long Triangles(Mesh mesh)
        {
            long count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                MeshTopology topology = mesh.GetTopology(i);
                if (topology == MeshTopology.Triangles) count += mesh.GetIndexCount(i) / 3;
                else if (topology == MeshTopology.Quads) count += mesh.GetIndexCount(i) / 4 * 2;
            }
            return count;
        }

        static void AccumulatePeak(Budget peak, Budget sample)
        {
            peak.ActiveRenderers = Math.Max(peak.ActiveRenderers, sample.ActiveRenderers);
            peak.VisibleRenderers = Math.Max(peak.VisibleRenderers, sample.VisibleRenderers);
            peak.ActiveCanvasRenderers = Math.Max(peak.ActiveCanvasRenderers, sample.ActiveCanvasRenderers);
            peak.ActiveVertices = Math.Max(peak.ActiveVertices, sample.ActiveVertices);
            peak.ActiveTriangles = Math.Max(peak.ActiveTriangles, sample.ActiveTriangles);
            peak.VisibleVertices = Math.Max(peak.VisibleVertices, sample.VisibleVertices);
            peak.VisibleTriangles = Math.Max(peak.VisibleTriangles, sample.VisibleTriangles);
            peak.UiVertices = Math.Max(peak.UiVertices, sample.UiVertices);
            peak.UiTriangles = Math.Max(peak.UiTriangles, sample.UiTriangles);
            peak.LineRenderers = Math.Max(peak.LineRenderers, sample.LineRenderers);
            peak.ParticleSystems = Math.Max(peak.ParticleSystems, sample.ParticleSystems);
            peak.LiveParticles = Math.Max(peak.LiveParticles, sample.LiveParticles);
            peak.UnsupportedRenderers = Math.Max(peak.UnsupportedRenderers, sample.UnsupportedRenderers);
            peak.UnscaledFrameMilliseconds = Mathf.Max(peak.UnscaledFrameMilliseconds, sample.UnscaledFrameMilliseconds);
            peak.SimulationDelta = Mathf.Max(peak.SimulationDelta, sample.SimulationDelta);
            peak.RunnerCenterAngle = Mathf.Max(peak.RunnerCenterAngle, sample.RunnerCenterAngle);
        }

        static void RequestCapture(string name)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/validation", report.Session));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + ".png");
            string expectedPage = game.Phase == RunPhase.Paused ? "Pause" :
                game.Phase == RunPhase.Completed || game.Phase == RunPhase.Failed ? "Results" : ui.CurrentPage;
            report.Screenshots.Add(new Capture { Name = name, Path = path, RequestedFrame = Time.frameCount,
                ExpectedPage = expectedPage, ExpectedLanguage = game.Save.Settings.Language, ExpectedPhase = game.Phase.ToString() });
        }

        static void SubmitRenderedCaptures()
        {
            foreach (Capture capture in report.Screenshots)
            {
                if (capture.Submitted || Time.frameCount - capture.RequestedFrame < 2) continue;
                RequireSilent(Time.frameCount - capture.RequestedFrame < 240, "Expected screenshot state never rendered: " + capture.Name);
                if (ui.CurrentPage != capture.ExpectedPage || game.Save.Settings.Language != capture.ExpectedLanguage ||
                    game.Phase.ToString() != capture.ExpectedPhase || capture.ExpectedPage == "Results" && !ui.ResultsVisible) continue;
                capture.Submitted = true;
                capture.CapturedFrame = Time.frameCount;
                capture.RenderedPage = ui.CurrentPage;
                capture.RenderedLanguage = game.Save.Settings.Language;
                capture.RenderedPhase = game.Phase.ToString();
                ScreenCapture.CaptureScreenshot(capture.Path);
            }
        }

        static void ObserveCaptureFiles()
        {
            foreach (Capture capture in report.Screenshots.Where(value => !value.FileObserved))
            {
                if (!File.Exists(capture.Path)) continue;
                capture.Bytes = new FileInfo(capture.Path).Length;
                capture.FileObserved = capture.Bytes > 0;
            }
        }

        static void ChangeStage(Stage next)
        {
            stage = next; stageFrame = Time.frameCount;
            stageStarted = EditorApplication.timeSinceStartup;
            if (report != null)
            {
                report.Stage = next.ToString();
                report.Message = current == null ? next.ToString() : current.Name + ": " + next;
            }
        }

        static void Require(bool condition, string name, string detail = "")
        {
            RecordCheck(name, condition, detail);
            if (!condition) throw new InvalidOperationException(name + ": " + detail);
        }

        static void RequireSilent(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void RecordCheck(string name, bool passed, string detail)
        {
            report.Checks.Add(new Check { Name = name, Passed = passed, Detail = detail, Frame = Time.frameCount });
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (!active || report == null) return;
            bool error = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            if (error) report.RuntimeErrors++;
            if (type == LogType.Warning) report.Warnings++;
            if ((error || type == LogType.Warning) && report.Console.Count < 100)
                report.Console.Add(new ConsoleEntry { Type = type.ToString(), Message = message, Stack = stack, Frame = Time.frameCount });
        }

        static void Finish(bool success, string message, bool stopPlay = true)
        {
            if (report == null) return;
            if (current != null && current.Status != "Settled")
            {
                if (game) SnapshotRun();
                current.Status = "Interrupted";
            }
            ObserveCaptureFiles();
            if (success && report.Screenshots.Any(value => !value.FileObserved))
            {
                pendingFinishMessage = message;
                ChangeStage(Stage.CaptureDrain);
                return;
            }
            if (success && report.RuntimeErrors != 0) { success = false; message += " Runtime errors were recorded."; }
            if (success && report.MissedPlayerFrames != 0)
            {
                success = false; message += " Some Player frames had no game-camera render; command cadence was not fully observed.";
            }
            report.Success = success;
            report.Status = success ? "Passed" : "Failed";
            report.Message = message;
            report.Stage = Stage.Finished.ToString();
            if (game && game.Phase == RunPhase.Running) game.PauseRun();
            active = false;
            stage = Stage.Finished;
            RemoveVirtualKeyboard();
            RestoreEnvironment();
            WriteReport();
            finishRequested = stopPlay && EditorApplication.isPlaying;
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(ActiveKey, false);
                SetHooks(null, false);
            }
        }

        static void RemoveVirtualKeyboard()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            previousKeyboard = null;
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            mouse = null;
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            previousMouse = null;
        }

        static void RestoreEnvironment()
        {
            if (restore == null && SessionState.GetString(RestoreKey, "") != "")
                restore = JsonUtility.FromJson<RestoreState>(SessionState.GetString(RestoreKey, ""));
            if (restore == null) return;
            Time.timeScale = restore.TimeScale;
            Time.fixedDeltaTime = restore.FixedDelta > 0 ? restore.FixedDelta : .02f;
            Time.maximumDeltaTime = restore.MaximumDelta;
            Application.targetFrameRate = restore.FrameRate;
            QualitySettings.vSyncCount = restore.VSync;
            Application.runInBackground = restore.RunInBackground;
            if (scratch) UnityEngine.Object.DestroyImmediate(scratch);
            scratch = null;
        }

        static void WriteReport()
        {
            if (report == null) return;
            report.UpdatedUtc = DateTime.UtcNow.ToString("O");
            report.WallSeconds = restore == null ? 0 : EditorApplication.timeSinceStartup - restore.Started;
            report.FirstClearRewards = firstClearTotal;
            report.FundingRuns = fundingRuns;
            report.GameplaySeconds = report.Runs.Sum(value => value.Elapsed);
            if (game && game.Progress != null)
            {
                report.Stars = game.Save.ContractStars.Sum();
                report.Archives = game.Save.Archives.Count(value => value);
                report.Chips = game.Save.Chips;
                report.OwnedOutfits = game.Save.OwnedOutfits.Count(value => value);
            }
            string json = JsonUtility.ToJson(report, true);
            SessionState.SetString(ReportKey, json);
            // Keep readers from observing half a JSON document; all paths are QA-owned.
            try
            {
                string temporary = ReportPath + ".tmp";
                File.WriteAllText(temporary, json);
                if (File.Exists(ReportPath)) File.Replace(temporary, ReportPath, null);
                else File.Move(temporary, ReportPath);
                if (report.SaveDirectory != null && Directory.Exists(report.SaveDirectory))
                    File.WriteAllText(Path.Combine(report.SaveDirectory, "validation-report.json"), json);
            }
            catch (Exception error)
            {
                report.Status = "Failed";
                report.Success = false;
                report.Stage = Stage.Finished.ToString();
                report.Message = "Acceptance report storage failed: " + error.Message;
                active = false;
                SessionState.SetString(ReportKey, JsonUtility.ToJson(report, true));
                RemoveVirtualKeyboard();
                RestoreEnvironment();
                if (game && game.Phase == RunPhase.Running) game.PauseRun();
                finishRequested = EditorApplication.isPlaying;
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetBool(ActiveKey, false);
                    SetHooks(null, false);
                }
                Debug.LogError("[Riftline Acceptance] " + report.Message);
            }
        }
    }
}
#endif
