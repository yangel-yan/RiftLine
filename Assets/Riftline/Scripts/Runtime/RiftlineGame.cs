using System;
using UnityEngine;
using UnityEngine.XR;

namespace Riftline
{
    [DefaultExecutionOrder(-100)]
    public sealed class RiftlineGame : MonoBehaviour
    {
        public RiftlineAssets Assets;
        public Camera ViewCamera;
        public Transform TrackingSpace;
        public RiftlineNavigationRig NavigationRig;
        public Transform PresentationFrame => NavigationRig ? NavigationRig.transform : transform;
#if UNITY_EDITOR
        public static string EditorSaveDirectory;
        public static bool EditorAcceptanceActive;
#endif
        public GameCatalog Catalog { get; private set; }
        public GameProgress Progress { get; private set; }
        public SaveData Save => Progress.Save;
        public RunSimulation Run { get; private set; }
        public RunResult LastResult { get; private set; }
        public bool SettlementPending => !resultSettled && (Phase == RunPhase.Failed || Phase == RunPhase.Completed);
        public RunPhase Phase => Run?.Phase ?? RunPhase.Menu;
        public float SessionMinutes => tracking.WearerSeconds / 60f;
        public bool RestSuggested => Save.Settings.RestReminder && SessionMinutes >= 20f;
        public bool TrackingReady => tracking.CanRun;
        public bool TrackingSuspended => !tracking.CanRun;
        public bool HandInputReady => handInput && handInput.IsDominantHandReady;
        public string StatusMessage { get; set; } = "";
        public event Action Changed;
        public event Action RestReminderRaised;

        RiftlineInput input;
        RiftlineHandInput handInput;
        RiftlineWorld world;
        RiftlineUI ui;
        RiftlineAudio audioFeedback;
        GameSaveStore store;
        readonly RiftlineTrackingGate tracking = new RiftlineTrackingGate();
        bool focused = true;
        bool applicationPaused;
        bool calibratedXr;
        bool restIssued;
        bool resultSettled;
        float nextPersistenceRetry;

        void Awake()
        {
            Catalog = GameCatalog.CreateDefault();
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("Riftline.Acceptance.Active", false))
            {
                EditorAcceptanceActive = true;
                EditorSaveDirectory = UnityEditor.SessionState.GetString("Riftline.Acceptance.SaveDirectory", "");
                if (string.IsNullOrWhiteSpace(EditorSaveDirectory))
                    throw new InvalidOperationException("Acceptance requires an isolated save directory.");
            }
            store = string.IsNullOrEmpty(EditorSaveDirectory) ? new GameSaveStore() : new GameSaveStore(EditorSaveDirectory);
#else
            store = new GameSaveStore();
#endif
            Progress = new GameProgress(Catalog, store.Load(), store);
            Run = new RunSimulation(Catalog);
            Run.StateChanged += OnPhaseChanged;
            Run.ArchiveCollected += Archive;
            Run.Feedback += Feedback;
            if (!ViewCamera) ViewCamera = Camera.main;
            if (!TrackingSpace && ViewCamera) TrackingSpace = ViewCamera.transform.parent;
            audioFeedback = gameObject.AddComponent<RiftlineAudio>();
            audioFeedback.Initialize(this);
            world = GetComponent<RiftlineWorld>() ?? gameObject.AddComponent<RiftlineWorld>();
            world.Initialize(this);
            handInput = gameObject.AddComponent<RiftlineHandInput>();
            handInput.Initialize(this);
#if UNITY_EDITOR
            input = gameObject.AddComponent<RiftlineInput>();
            input.Initialize(this);
#endif
            var panel = new GameObject("Navigation Interface");
            panel.transform.SetParent(PresentationFrame, false);
            ui = panel.AddComponent<RiftlineUI>();
            ui.Initialize(this);
            if (ViewCamera) world.Recenter(ViewCamera.transform);
            CheckTracking(0);
            Application.targetFrameRate = 72;
        }

        void Update()
        {
            CheckTracking(Time.unscaledDeltaTime);
            input?.Poll();
            Run.Tick(Time.deltaTime);
            world.Present(Time.deltaTime);
            audioFeedback.Present();
            if (Time.unscaledTime >= nextPersistenceRetry)
            {
                nextPersistenceRetry = Time.unscaledTime + 1f;
                if (!resultSettled && (Phase == RunPhase.Failed || Phase == RunPhase.Completed))
                    SettleResult();
                else if (Progress.HasPendingSave && Progress.TrySavePending()) Changed?.Invoke();
            }
        }

        public void StartContract(int id)
        {
            if (id < 0 || id >= Catalog.Contracts.Length || !Progress.IsContractUnlocked(id)) return;
            StartRun(Catalog.Contracts[id], false);
        }

        public void StartRecommendedContract()
        {
            for (int id = 0; id < Catalog.Contracts.Length; id++)
            {
                if (Progress.IsContractUnlocked(id) && Save.ContractStars[id] == 0)
                {
                    StartContract(id);
                    return;
                }
            }
            if (Progress.IsEndlessUnlocked) StartEndless();
            else StartContract(0);
        }

        public void StartEndless()
        {
            if (!Progress.IsEndlessUnlocked) return;
            StartRun(Catalog.Contracts[5], true);
        }

        void StartRun(ContractDefinition contract, bool endless)
        {
            CheckTracking(0);
            if (!TrackingReady) { ShowTrackingStatus(); return; }
#if !UNITY_EDITOR
            if (!HandInputReady) { ShowHandTrackingStatus(); return; }
#endif
            if (NavigationRig) NavigationRig.ResetHeadingPreservingTracking();
            audioFeedback.ResetRun();
            world.ResetRun();
            Recenter();
            LastResult = null;
            StatusMessage = "";
            resultSettled = false;
            Run.AutoTurn = Save.Settings.AutoTurn;
            Run.Start(contract, endless ? unchecked(Environment.TickCount) : contract.Seed, endless);
            input?.ResetEdges();
            handInput?.ResetEdges();
            Changed?.Invoke();
        }

        public void Retry()
        {
            if (Run.ActiveContract != null) StartRun(Run.ActiveContract, Run.Endless);
        }

        public void ShowHome()
        {
            if (Phase == RunPhase.Running || Phase == RunPhase.Paused || Phase == RunPhase.Countdown)
                SafeEndRun();
            if (Progress.HasPendingSave && !Progress.TrySavePending())
            {
                ShowPersistenceStatus();
                return;
            }
            Run.StateChanged -= OnPhaseChanged;
            Run.ArchiveCollected -= Archive;
            Run.Feedback -= Feedback;
            Run = new RunSimulation(Catalog);
            Run.StateChanged += OnPhaseChanged;
            Run.ArchiveCollected += Archive;
            Run.Feedback += Feedback;
            LastResult = null;
            resultSettled = false;
            audioFeedback.ResetRun();
            world.ResetRun();
            Recenter();
            Changed?.Invoke();
        }

        void Archive(TrackEvent item) => Progress.CollectArchive(item.ArchiveId);
        public void PauseRun()
        {
            if (Phase == RunPhase.Running || Phase == RunPhase.Countdown) Run.Pause();
        }

        public void ResumeRun()
        {
            if (Phase != RunPhase.Paused) return;
            CheckTracking(0);
            if (!TrackingReady) { ShowTrackingStatus(); return; }
#if !UNITY_EDITOR
            if (!HandInputReady) { ShowHandTrackingStatus(); return; }
#endif
            Recenter();
            input?.ResetEdges();
            handInput?.ResetEdges();
            Run.Resume();
        }

        public void SafeEndRun()
        {
            if (Phase == RunPhase.Menu || resultSettled) return;
            Run.SafeEnd();
            if (!resultSettled) SettleResult();
            Changed?.Invoke();
        }

        void SettleResult()
        {
            resultSettled = Progress.TrySettleRun(Run, out var result);
            LastResult = result;
            if (!resultSettled) ShowPersistenceStatus();
            Changed?.Invoke();
        }

        void ShowPersistenceStatus()
        {
            StatusMessage = Save.Settings.Language == "zh"
                ? "进度等待保存，正在重试。请保留本局。" : "Progress is waiting to save. Retrying; keep this run open.";
            Changed?.Invoke();
        }

        public void SendCommand(GameCommand command)
        {
            if (command == GameCommand.Comfort) { CycleComfort(); return; }
            if (command == GameCommand.Pause)
            {
                if (Phase == RunPhase.Paused) ResumeRun(); else PauseRun();
                return;
            }
            Run.Handle(command);
        }

        public void CycleComfort()
        {
            Save.Settings.ComfortMask = (Save.Settings.ComfortMask + 1) % 4;
            SaveSettings();
        }

        public void SaveSettings()
        {
            try { store.Save(Save); }
            catch (Exception exception)
            {
                StatusMessage = Save.Settings.Language == "zh" ? "设置未能保存，请检查存储空间。" : "Settings could not be saved. Check storage space.";
                Debug.LogError("[Riftline] Settings save failed: " + exception.Message);
            }
            Run.AutoTurn = Save.Settings.AutoTurn;
            audioFeedback.ApplySettings();
            world.ApplySettings();
            handInput?.ApplySettings();
            Changed?.Invoke();
        }

        public void Recenter()
        {
            if (Phase == RunPhase.Running) return;
            CheckTracking(0);
            if (!TrackingReady) return;
            AlignPresentationToHead();
        }

        internal bool TryInitialAlignPresentationToHead()
        {
            if (calibratedXr || Phase == RunPhase.Running || !ViewCamera) return false;
            AlignPresentationToHead();
            return true;
        }

        internal void AlignPresentationToHead()
        {
            if (Phase == RunPhase.Running || !ViewCamera) return;
            // The tracking origin retains its world pose while the navigation frame is calibrated.
            world.Recenter(ViewCamera.transform);
            calibratedXr = true;
            Changed?.Invoke();
        }

        public bool TryGetControllerPose(XRNode node, out Pose pose)
        {
            pose = default;
            if (!TrackingReady || !XRSettings.isDeviceActive) return false;
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid ||
                !device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked ||
                !device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position) ||
                !device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation)) return false;
            Transform space = TrackingSpace ? TrackingSpace : ViewCamera.transform.parent;
            if (!space) space = transform;
            pose = new Pose(space.TransformPoint(position), space.rotation * rotation);
            return true;
        }

        public Pose ControllerFallbackPose(bool left) => world.ControllerFallbackPose(left);

        public void QuitGame()
        {
            SafeEndRun();
            store.Save(Save);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnPhaseChanged(RunPhase phase) => Changed?.Invoke();

        void Feedback(FeedbackKind feedback)
        {
            audioFeedback.Play(feedback, world != null ? world.RunnerPosition : new Vector3(0, .5f, 5.5f));
            input?.Haptic(feedback);
        }

        internal void PauseForHandTracking()
        {
            if (Phase != RunPhase.Running && Phase != RunPhase.Countdown) return;
            ShowHandTrackingStatus();
            PauseRun();
        }

        internal void PlayInputFeedback(FeedbackKind kind, Vector3 position)
        {
            if (audioFeedback) audioFeedback.Play(kind, position);
        }

        void CheckTracking(float dt)
        {
            var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool valid = device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
            bool worn = device.isValid && (!device.TryGetFeatureValue(CommonUsages.userPresence, out bool presence) || presence);
            bool readyBefore = TrackingReady;
            tracking.Observe(XRSettings.isDeviceActive, device.isValid, valid, worn,
                focused && !applicationPaused, dt);
            if (!TrackingReady && (Phase == RunPhase.Running || Phase == RunPhase.Countdown))
            {
                SetTrackingStatus();
                PauseRun();
            }
            if (TrackingReady != readyBefore) Changed?.Invoke();
            if (RestSuggested && !restIssued)
            {
                restIssued = true;
                StatusMessage = Save.Settings.Language == "zh" ? "已佩戴 20 分钟，建议稍作休息。" : "20 minutes wearing the headset. Consider a short rest.";
                RestReminderRaised?.Invoke();
                Changed?.Invoke();
            }
        }

        void SetTrackingStatus()
        {
            StatusMessage = Save.Settings.Language == "zh" ? "追踪已暂停。请佩戴头显，恢复追踪后再继续。" : "Tracking paused. Wear the headset and restore tracking before continuing.";
        }

        void ShowTrackingStatus()
        {
            SetTrackingStatus();
            Changed?.Invoke();
        }

        void ShowHandTrackingStatus()
        {
            StatusMessage = Save.Settings.Language == "zh"
                ? "手部追踪已暂停。请抬起主手并张开，稳定后再继续。"
                : "Hand tracking paused. Raise and open your dominant hand before resuming.";
            Changed?.Invoke();
        }

        void OnApplicationFocus(bool focus)
        {
            focused = focus;
            CheckTracking(0);
            if (!focus) PauseRun();
        }

        void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            CheckTracking(0);
            if (paused) PauseRun();
        }

        void OnDestroy()
        {
            if (Run != null)
            {
                Run.StateChanged -= OnPhaseChanged;
                Run.ArchiveCollected -= Archive;
                Run.Feedback -= Feedback;
            }
        }
    }
}
