# Riftline Pure Hand Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace PICO runtime controller input with seated-first hand gestures that emit the existing discrete `GameCommand` values and remain testable without an XR device.

**Architecture:** A deterministic recognizer consumes device-neutral hand samples and owns all timing, hysteresis, latching, and tracking-loss rules. A runtime adapter samples `XRHandSubsystem` through `XRHandTrackingEvents`, evaluates production-owned XR Hands Open/Fist/Palm assets, transforms poses into the HMD view plane, and forwards confirmed commands through `RiftlineGame.SendCommand()`. Feedback, UI pointing, and onboarding consume recognizer state without modifying `RunSimulation`.

**Tech Stack:** Unity 6000.6.7f1, C#, PICO XR SDK 6.1.1, XR Hands 1.7.3, XR Interaction Toolkit 3.6.1, Input System 1.20.0, uGUI.

## Global Constraints

- PICO builds are mandatory pure hand tracking; no controller fallback.
- Editor-only keyboard and mouse input may remain for development and automated acceptance.
- Frequent actions are dominant-hand Pinch plus camera-plane movement; Phase is dominant-hand Open-to-Fist; Pause is non-dominant Palm pose.
- Do not change `RunSimulation`, obstacle timing, scoring, collision, progression, or the existing 0.2-second input buffer.
- Gameplay and UI Pinch semantics are mutually exclusive.
- Tracking reacquisition never infers, replays, or completes a previous gesture.
- Initial thresholds must match the approved design and remain tunable.
- The workspace has no `.git`; replace commit steps with explicit file and validation checkpoints.

---

## File Map

**Create**

- `Assets/Riftline/Scripts/Core/RiftlineHandGestureRecognizer.cs`: deterministic gesture state machine and device-neutral samples/results.
- `Assets/Riftline/Scripts/Runtime/RiftlineHandControlSettings.cs`: production ScriptableObject containing thresholds and XR Hands gesture assets.
- `Assets/Riftline/Scripts/Runtime/RiftlineHandInput.cs`: XR Hands adapter, run/menu mode owner, command bridge, and tracking guard.
- `Assets/Riftline/Scripts/Runtime/RiftlineHandFeedback.cs`: hand anchor, direction, hold, cancellation, and tracking visuals.
- `Assets/Riftline/Scripts/Runtime/RiftlineUIPointer.cs`: shared uGUI pointer implementation driven by either Editor controller simulation or a hand aim pose.
- `Assets/Riftline/Scripts/UI/RiftlineHandOnboarding.cs`: first-run dominant-hand selection, gesture rehearsal, and calibration flow.
- `Assets/Riftline/HandTracking/Resources/RiftlineHandControlSettings.asset`: runtime settings asset.
- `Assets/Riftline/HandTracking/Gestures/Open Hand Shape.asset`: production copy of the XR Hands sample shape.
- `Assets/Riftline/HandTracking/Gestures/Fist Hand Shape.asset`: production copy of the XR Hands sample shape.
- `Assets/Riftline/HandTracking/Gestures/Open Palm Up.asset`: production copy of the XR Hands sample pose.

**Modify**

- `Assets/Riftline/Scripts/Runtime/RiftlineGame.cs`: create and reset hand input; keep legacy input Editor-only; expose input feedback and hand readiness.
- `Assets/Riftline/Scripts/Runtime/RiftlineInput.cs`: remove runtime XR-controller ownership and delegate pointer mechanics to `RiftlineUIPointer`.
- `Assets/Riftline/Scripts/Core/RiftlineTypes.cs`: persist onboarding completion and calibrated displacement.
- `Assets/Riftline/Scripts/UI/RiftlineUI.cs`: hand-oriented control copy, hand readiness gate, and onboarding host.
- `Assets/Riftline/Scripts/UI/RiftlineUILocalization.cs`: localized hand prompts and tracking messages.
- `Assets/Riftline/Editor/RiftlineCoreValidation.cs`: deterministic hand-recognizer regression suite.
- `Assets/Riftline/Editor/RiftlinePlayValidation.cs`: verify Editor fallback remains isolated from hand runtime behavior.
- `Assets/Riftline/Editor/RiftlineSceneBuilder.cs`: create/copy hand assets and ensure the production settings asset is populated.
- `docs/PICO-HAND-CONTROLS.md`: implementation and physical-device validation notes.

## Task 1: Deterministic Gesture Kernel

**Files:**

- Create: `Assets/Riftline/Scripts/Core/RiftlineHandGestureRecognizer.cs`
- Modify: `Assets/Riftline/Editor/RiftlineCoreValidation.cs`

**Interfaces:**

- Consumes: `GameCommand` from `RiftlineTypes.cs`.
- Produces:
  - `HandGestureThresholds`
  - `HandGestureSample`
  - `HandGestureState`
  - `HandGestureSignal`
  - `RiftlineHandGestureRecognizer.Reset(float now)`
  - `RiftlineHandGestureRecognizer.Update(HandGestureSample sample)`

- [ ] **Step 1: Add failing deterministic validation cases**

Add `ValidateHandGestures(details)` to `RiftlineCoreValidation.RunAll()` before the long seed loops. Cover:

```csharp
var thresholds = HandGestureThresholds.Default;
var recognizer = new RiftlineHandGestureRecognizer(thresholds);
recognizer.Reset(0f);

HandGestureSignal armed = recognizer.Update(Sample(.10f, pinch: .80f));
Require(armed.State == HandGestureState.Armed, "Pinch did not arm");

HandGestureSignal fired = recognizer.Update(Sample(.18f, pinch: .90f, viewX: .061f));
Require(fired.Command == GameCommand.Right && fired.State == HandGestureState.Latched,
    "Right displacement did not fire exactly once");

HandGestureSignal duplicate = recognizer.Update(Sample(.20f, pinch: .90f, viewX: .09f));
Require(duplicate.Command == GameCommand.None, "Latched Pinch fired twice");
```

Also assert all four directions, `0.75/0.35` hysteresis, 1.35 dominant-axis rejection, 350 ms expiry, 100 ms candidate cancellation, 180 ms fist hold, 450 ms pause hold, 300 ms sustained tracking-loss auto-pause, clean reacquisition, and left-handed mirroring.

- [ ] **Step 2: Run validation and confirm the new type is missing**

Run the existing Editor automation `Validate` action.

Expected: Unity compilation fails because `RiftlineHandGestureRecognizer` and related types do not exist.

- [ ] **Step 3: Implement the state machine**

Use a single allocation-free update result:

```csharp
public struct HandGestureSample
{
    public float Time;
    public bool GameplayEnabled;
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
```

Directional behavior:

```csharp
if (state == HandGestureState.Neutral && sample.Pinch >= thresholds.PinchPress)
{
    anchor = sample.PinchViewPosition;
    armedAt = sample.Time;
    state = HandGestureState.Armed;
}

Vector2 delta = sample.PinchViewPosition - anchor;
if (delta.magnitude >= thresholds.DirectionDistance &&
    sample.Time - armedAt <= thresholds.DirectionWindow)
{
    float major = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
    float minor = Mathf.Min(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
    if (major >= minor * thresholds.DominantAxisRatio)
    {
        signal.Command = DirectionCommand(delta);
        state = HandGestureState.Latched;
    }
}
```

Track Open/Fist and support Pause holds only from a valid neutral state. Cancel on tracking loss; never generate a command on the reacquisition frame.

- [ ] **Step 4: Run core validation**

Run `RiftlineCoreValidation.RunAll()`.

Expected: all hand gesture checks pass and the existing core witness/sequence counts remain unchanged.

- [ ] **Step 5: Record checkpoint**

Record the validation output and modified file list in `/tmp/riftline-hand-controls/task-1.txt`.

## Task 2: Production Assets and XR Hands Sampling

**Files:**

- Create: `Assets/Riftline/Scripts/Runtime/RiftlineHandControlSettings.cs`
- Create: `Assets/Riftline/Scripts/Runtime/RiftlineHandInput.cs`
- Modify: `Assets/Riftline/Editor/RiftlineSceneBuilder.cs`
- Create assets under: `Assets/Riftline/HandTracking/`

**Interfaces:**

- Consumes: `RiftlineHandGestureRecognizer`, `XRHandTrackingEvents`, `XRHandShape`, `XRHandPose`.
- Produces:
  - `RiftlineHandInput.Initialize(RiftlineGame owner)`
  - `RiftlineHandInput.ResetEdges()`
  - `RiftlineHandInput.IsDominantHandReady`
  - `RiftlineHandInput.VisualSignal`
  - `RiftlineHandInput.OnJointsUpdated(XRHandJointsUpdatedEventArgs args)`

- [ ] **Step 1: Add asset and configuration validation**

Extend `RiftlineCoreValidation` or an Editor-only validation helper to require:

```csharp
RiftlineHandControlSettings settings =
    Resources.Load<RiftlineHandControlSettings>("RiftlineHandControlSettings");
Require(settings != null, "hand control settings asset missing");
Require(settings.OpenShape != null && settings.FistShape != null && settings.PausePose != null,
    "production hand gesture assets missing");
Require(Mathf.Approximately(settings.Thresholds.DirectionDistance, .06f),
    "default hand displacement changed");
```

- [ ] **Step 2: Create production-owned XR Hands assets**

In `RiftlineSceneBuilder.EnsureHandControlAssets()`:

1. Create `Assets/Riftline/HandTracking/Gestures` and `Resources`.
2. Copy the sample Open, Fist, and Open Palm Up assets when production copies are absent.
3. Create or update `RiftlineHandControlSettings.asset`.
4. Assign copied assets and approved threshold values.
5. Save assets idempotently.

- [ ] **Step 3: Implement XR sampling**

Create left and right `XRHandTrackingEvents` components with `UpdateTypes.Dynamic`. For each joints update:

```csharp
XRCommonHandGestures common = args.hand.handedness == Handedness.Left
    ? args.subsystem.leftHandCommonGestures
    : args.subsystem.rightHandCommonGestures;

bool pinchReady = common.TryGetPinchValue(out float pinch);
bool poseReady = common.TryGetPinchPose(out Pose pinchPose);
bool graspReady = common.TryGetGraspValue(out float grasp);
bool open = settings.OpenShape.CheckConditions(args);
bool fist = settings.FistShape.CheckConditions(args);
bool pausePose = settings.PausePose.CheckConditions(args);
```

Transform tracking-space positions through `game.TrackingSpace`, then project the dominant Pinch delta onto `game.ViewCamera.transform.right` and `.up`. Cache only copied scalar/pose values; never retain `XRHandJointsUpdatedEventArgs`.

- [ ] **Step 4: Guard subsystem and provider gaps**

When common Pinch pose is unavailable, average valid ThumbTip and IndexTip poses. When Pinch strength, Open/Fist assets, or required joints are unavailable, mark the sample invalid and cancel rather than guessing.

- [ ] **Step 5: Run compile and asset validation**

Run scene builder, wait for import/domain reload, then run core validation.

Expected: production settings load from `Resources`, copied assets are outside `Assets/Samples`, and no compile errors occur.

- [ ] **Step 6: Record checkpoint**

Write the resolved package versions, asset GUIDs, and validation result to `/tmp/riftline-hand-controls/task-2.txt`.

## Task 3: Game Command Integration and Tracking Guard

**Files:**

- Modify: `Assets/Riftline/Scripts/Runtime/RiftlineGame.cs`
- Modify: `Assets/Riftline/Scripts/Runtime/RiftlineInput.cs`
- Modify: `Assets/Riftline/Scripts/Core/RiftlineTypes.cs`

**Interfaces:**

- Consumes: `RiftlineHandInput`.
- Produces:
  - `RiftlineGame.HandInputReady`
  - `RiftlineGame.PlayInputFeedback(FeedbackKind kind)`
  - persisted `GameSettings.HandTutorialComplete`
  - persisted `GameSettings.HandDirectionDistance`

- [ ] **Step 1: Add integration assertions**

Add Editor assertions that:

- PICO/runtime code creates `RiftlineHandInput`.
- `RiftlineHandInput` is the only runtime hand component that calls `SendCommand`.
- `RunSimulation` contains no XR Hands references.
- tracking-loss auto-pause does not call `Run.Fail`.

- [ ] **Step 2: Wire hand input into `RiftlineGame`**

Initialize hand input after `world.Initialize(this)` and before UI creation:

```csharp
handInput = gameObject.AddComponent<RiftlineHandInput>();
handInput.Initialize(this);

#if UNITY_EDITOR
input = gameObject.AddComponent<RiftlineInput>();
input.Initialize(this);
#endif
```

Call `handInput.ResetEdges()` from `StartRun`, `ResumeRun`, and `ShowHome`. The runtime adapter sends confirmed actions only through `SendCommand`.

- [ ] **Step 3: Enforce pure-hand runtime ownership**

Keep keyboard/mouse simulation under `UNITY_EDITOR`. Remove XR controller bindings, controller model creation, trigger selection, runtime haptics, and controller pose UI ownership from non-Editor PICO execution.

- [ ] **Step 4: Add readiness and auto-pause**

Before starting or resuming a run, require HMD tracking plus dominant-hand Neutral readiness. On 300 ms dominant-hand loss:

```csharp
if (signal.RequestAutoPause && (game.Phase == RunPhase.Running || game.Phase == RunPhase.Countdown))
{
    game.StatusMessage = LocalizedRaiseHandMessage();
    game.PauseRun();
}
```

After reacquisition, require 300 ms Open stability and explicit resume.

- [ ] **Step 5: Run core and Editor smoke validation**

Expected:

- Core counts unchanged.
- Editor keyboard acceptance still starts, pauses, resumes, and completes a run.
- With no XRHandSubsystem, hand input remains inert and produces no commands.

- [ ] **Step 6: Record checkpoint**

Write command ownership and smoke results to `/tmp/riftline-hand-controls/task-3.txt`.

## Task 4: Hand Feedback and uGUI Pointer

**Files:**

- Create: `Assets/Riftline/Scripts/Runtime/RiftlineHandFeedback.cs`
- Create: `Assets/Riftline/Scripts/Runtime/RiftlineUIPointer.cs`
- Modify: `Assets/Riftline/Scripts/Runtime/RiftlineHandInput.cs`
- Modify: `Assets/Riftline/Scripts/Runtime/RiftlineInput.cs`
- Modify: `Assets/Riftline/Scripts/Runtime/RiftlineGame.cs`

**Interfaces:**

- Consumes: `HandGestureSignal`, hand aim pose, Pinch strength, current `RunPhase`.
- Produces:
  - `RiftlineHandFeedback.Present(HandGestureSignal signal, Pose pinchPose)`
  - `RiftlineUIPointer.UpdatePointer(Pose pose, bool selectHeld, float scroll)`
  - `RiftlineUIPointer.Cancel()`

- [ ] **Step 1: Extract shared uGUI pointer mechanics**

Move `PointerEventData`, raycast, hover, press, drag, release, and scroll behavior from `RiftlineInput` into `RiftlineUIPointer`. Preserve the existing leaf-raycast scrolling behavior.

- [ ] **Step 2: Feed hand UI input only outside Running**

Use `XRCommonHandGestures.TryGetAimPose()` for the ray and Pinch hysteresis for selection. During `Running` and `Countdown`, call `Cancel()` and route Pinch exclusively to gameplay. During Menu, Paused, Failed, and Completed, disable gameplay recognition and enable hand UI selection.

- [ ] **Step 3: Build low-occlusion feedback**

Create an unlit LineRenderer cross at the Pinch anchor:

- Four 6 cm direction arms in the camera plane.
- Candidate arm brightens by normalized displacement.
- Fired arm pulses for 80 ms.
- Phase hold draws a compact ring with progress from 0 to 1.
- Tracking cancellation fades the visual and plays one restrained UI reject sound.

Do not render opaque full-hand meshes.

- [ ] **Step 4: Add audio feedback bridge**

Expose an internal `RiftlineGame.PlayInputFeedback(FeedbackKind)` that delegates to the existing audio system. Confirmed gameplay still uses existing simulation feedback; arm/cancel/menu confirmation uses `FeedbackKind.Ui`.

- [ ] **Step 5: Validate UI mode exclusion**

In Play Mode, inject simulated hand signals and assert one Pinch cannot both emit a `GameCommand` and produce a UI click. Verify hover, click, drag, and scroll still work on the existing world-space Canvas.

- [ ] **Step 6: Record checkpoint**

Save mode-exclusion evidence and screenshots under `/tmp/riftline-hand-controls/task-4/`.

## Task 5: First-Run Onboarding and Hand-Oriented UI

**Files:**

- Create: `Assets/Riftline/Scripts/UI/RiftlineHandOnboarding.cs`
- Modify: `Assets/Riftline/Scripts/UI/RiftlineUI.cs`
- Modify: `Assets/Riftline/Scripts/UI/RiftlineUILocalization.cs`
- Modify: `Assets/Riftline/Scripts/Core/RiftlineTypes.cs`

**Interfaces:**

- Consumes: recognizer semantic events and `GameSettings`.
- Produces:
  - persisted handedness
  - persisted `HandTutorialComplete`
  - calibrated `HandDirectionDistance` clamped to `0.045f..0.075f`

- [ ] **Step 1: Add save normalization checks**

Verify old saves default to `HandTutorialComplete == false` and `HandDirectionDistance == .06f`, while valid calibrated values survive clone/save/load.

- [ ] **Step 2: Implement onboarding sequence**

Use a modal world-space overlay before the first contract:

1. Pinch with the preferred hand to choose handedness.
2. Perform Left, Right, Jump, and Slide once each.
3. Hold Fist to trigger Phase.
4. Present the non-dominant Palm pose to open/confirm the menu step.

Training consumes recognizer output without forwarding commands to `RunSimulation`.

- [ ] **Step 3: Calibrate displacement**

Record peak comfortable displacement for the four directional rehearsals. Use the median, multiply by `0.75f`, and clamp to `0.045f..0.075f`. Save only after every step succeeds.

- [ ] **Step 4: Replace controller-facing copy**

Update control prompts and action bindings:

- Left/Right: `PINCH + LEFT/RIGHT`
- Jump/Slide: `PINCH + UP/DOWN`
- Phase: `FIST`
- Pause: `PALM MENU`

Remove TriggerHand and Haptics controls from the PICO hand settings surface. Keep `LeftHanded` and add `Recalibrate Hands`.

- [ ] **Step 5: Validate onboarding persistence**

Run onboarding with a temporary save, reload, and verify it does not repeat. Invoke recalibration and verify it resets only hand onboarding/calibration fields.

- [ ] **Step 6: Record checkpoint**

Save onboarding evidence and localized screenshots under `/tmp/riftline-hand-controls/task-5/`.

## Task 6: Scene, PICO, and Regression Verification

**Files:**

- Modify: `Assets/Riftline/Editor/RiftlineSceneBuilder.cs`
- Modify: `Assets/Riftline/Editor/RiftlinePlayValidation.cs`
- Create: `docs/PICO-HAND-CONTROLS.md`
- Modify serialized assets only through Unity import/build tooling.

**Interfaces:**

- Consumes: all prior tasks.
- Produces: updated scene, verification report, Android APK, and physical-device checklist.

- [ ] **Step 1: Rebuild scene idempotently**

Run `RiftlineSceneBuilder.Build()` twice. Compare scene object/component counts and confirm no duplicate hand input, tracking events, EventSystem, pointer, feedback, or settings assets.

- [ ] **Step 2: Run fresh compilation and core validation**

Expected:

- zero compile errors;
- all new hand regression groups pass;
- existing 20,000 complete sequences and independent witnesses remain unchanged.

- [ ] **Step 3: Run focused Editor acceptance**

Run menu input, pause/resume, failure retry, and one completed contract with the Editor fallback. Confirm no controller dependency was introduced into Core or hand recognition.

- [ ] **Step 4: Run PICO simulator validation**

Verify:

- XR session starts;
- `XRHandSubsystem` is discovered or reports a clear unavailable status;
- no command is emitted while untracked;
- UI and gameplay modes remain mutually exclusive;
- HMD tracking and UI visibility fixes remain intact.

- [ ] **Step 5: Build Android APK**

Build a development APK after a clean compile. Validate the APK as a ZIP and retain the full build log. Do not claim hand tracking acceptance from the build alone.

- [ ] **Step 6: Physical PICO acceptance**

On a physical PICO device, record:

- at least 50 attempts per command after onboarding;
- recognition success at or above 98%;
- fewer than one unintended command per 10 minutes;
- P95 threshold-to-command latency at or below 120 ms;
- no command replay after tracking loss;
- auto-pause after sustained dominant-hand loss;
- no simultaneous gameplay/UI Pinch;
- 30-minute seated fatigue score at or below 2/5.

- [ ] **Step 7: Document evidence and limitations**

Write commands, package versions, APK path, device/OS, measured rates, logs, and any remaining hardware-only limitations to `docs/PICO-HAND-CONTROLS.md`.

- [ ] **Step 8: Final checkpoint**

List all created and modified files plus verification outputs in `/tmp/riftline-hand-controls/final.txt`. Because the workspace has no Git repository, do not claim a commit or clean working tree.

