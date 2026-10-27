# Commercial Opening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Independent source units may be delegated; Unity control and shared-file integration have one owner.

**Goal:** Give Riftline an immediately understandable pursuit/archive/extraction opening, stronger physical feedback and a structured industrial environment.

**Architecture:** Add pure presentation state and two focused world presenters around the existing deterministic simulation. World exposes read-only route poses; audio/UI consume real simulation events. Keep gameplay timing, health, progression and XR navigation semantics intact.

**Tech Stack:** Unity 6000.6.7f1, C#, existing generated prefabs/animations/audio, URP PBR/emission, world-space uGUI, Unity Pipeline.

## Global constraints

- No Git repository; work in the authorized shared project.
- Existing art/audio is from Unity AI Generator; procedural structural geometry is permitted. Preserve generated textures and LODs.
- No HMD pose/FOV/shake changes.
- Temporary test drivers under `/tmp`; final evidence under `docs/commercial-opening/`.
- Chinese and English, font scale 0.8–1.4, reduced-flash/particle options.
- Keep current 60-second contract timings and all Core damage/settlement behavior.
- Existing regular save must remain untouched.

## Task 1 — Pure opening state and red/green evidence

Create `Assets/Riftline/Scripts/Core/RiftlineOpeningPresentation.cs`.
Test in `/tmp/riftline-commercial-tests/`, linking actual `Scripts/Core/*.cs` and the existing Unity stubs.

Interfaces:

```csharp
public enum OpeningObjective { RecoverArchive, ReachExtraction, Extracted }
public struct OpeningFrame
{
    public bool Active;
    public OpeningObjective Objective;
    public float Threat, Progress, ComboPulse, DamagePulse, PhasePulse, LandingPulse;
}
public sealed class RiftlineOpeningPresentation
{
    public void Reset();
    public void RecordFeedback(FeedbackKind kind, float elapsed);
    public OpeningFrame Evaluate(RunSimulation run);
}
```

- [x] Write/run a baseline assertion that the new model is absent. Save its expected failure.
- [x] Add meaningful assertions: three-life threat < two-life threat < one-life threat; phase reduces visual threat; archive feedback persists after old events are pruned; pause/resume leave effect ages unchanged; retry and replacement clear old pulses/objective; repeated evaluation does not restart landing; evaluation never changes run state.
- [x] Implement using simulation elapsed and health. Detect a positive-height to grounded transition only while Running. Treat Menu and terminal panels as inactive; retain `Extracted` only for a genuine distance-complete contract.
- [x] Run focused tests to green. Keep their output outside Assets.

## Task 2 — Reusable dock architecture

Create `Assets/Riftline/Scripts/Runtime/RiftlineRailArchitecture.cs`.
Expose this read-only query in `RiftlineWorld.cs`:

```csharp
public Pose TrackPoseAt(float distance)
{
    Vector3 point = PresentedSample(distance, out float yaw);
    return new Pose(outside.TransformPoint(point), outside.rotation * Quaternion.Euler(0, yaw, 0));
}
```

Presenter interface:

```csharp
public void Initialize(RiftlineGame owner, RiftlineWorld world);
public void Present(RunSimulation run);
public void ResetRun();
```

- [x] Capture baseline first-region renderer/triangle/material counts and its sparse rail view.
- [x] Build one shared frame/ledge mesh from boxes with hard normals, plus one shared emission mesh. Cache 8–10 cells, use two shared materials, and position them through route poses.
- [x] Place pillars/ledges beyond ±4.2 m and crossbeams above 4 m. Keep central deck, hazards and gaps readable. No colliders.
- [x] Use regional material colors; update properties only when region changes. Freeze geometry during pause/countdown and reset cell assignment on retry.
- [x] Verify meshes are supported, bounds finite, added active geometry/renderers within the stated budget and no per-frame creation.

## Task 3 — Pursuer, extraction and physical response

Create `Assets/Riftline/Scripts/Runtime/RiftlineOpeningDirector.cs`.
Integrate in `RiftlineWorld.cs` after runner/route positioning; expose the current `OpeningFrame` for HUD consumption if needed.

Interface:

```csharp
public void Initialize(RiftlineGame owner, RiftlineWorld world);
public void Present(RunSimulation run, float dt);
public void ResetRun();
public OpeningFrame Frame { get; }
```

- [x] Record scene assertions that a visible pursuer and extraction arch are absent before implementation.
- [x] Instantiate the generated Drone prefab once, fit uniformly, preserve textures/LODs and disable its non-gameplay colliders. Keep its center beyond the playable lane boundary; animate only from simulation time.
- [x] Subscribe to the current run feedback, detach on replacement/destruction, reset presentation on retry. Bind actual-health threat to drone position/light/trails; use bounded combo/phase/damage pulses.
- [x] Build a reusable extraction arch that appears during the final 15 seconds at the actual final route pose. It must stay clear of terminal panels.
- [x] Add a short ground-contact ring/transient on a real landing. Update `RiftlineAudio.cs` with a bounded generated-impact segment, music intensity and paused gameplay voices. Respect settings and simulation-time cutoffs.
- [x] Run actual pause/damage/landing/retry checks and inspect screenshots. Verify new effects never move the camera rig or mask action cues.

## Task 4 — Mission framing and results

Modify `RiftlineUI.cs`, `RiftlineRunHud.cs`, `RiftlineUILocalization.cs`. Keep large-file additions in a focused partial UI file when useful.

- [x] Home identifies the mission and threat in two short localized lines; primary action remains immediate launch.
- [x] Route/goal copy changes from recovering archive to reaching extraction only after actual archive feedback. Use the current cue/card footprint.
- [x] Actual victory highlights Next as primary and names the next unlocked contract. Failure preserves accurate advice and states retry restores full life/progression.
- [x] Verify both languages, font 1.4, actual mouse wheel/back/button raycasts and Enter selection semantics.

## Task 5 — Final integration, review and evidence

- [x] Run the pure presentation suite and the existing Core suite. Current command: `dotnet run --project /tmp/riftline-engagement-tests/Engagement.csproj -- all`.
- [x] Use a normal-time disposable-save keyboard/mouse driver. Safe first-contract timing remains A 0.8, D 3.84, Space 8.72, S 11.76, D 16.08, A 21.84, D 25, E 29.72, A 31.08, Space 35.72, A 37.9, D 40, Space 44.72, S 47.76, D 52.08. A deliberate initial-hit variant uses A 3.18 and D 4 instead of the first two actions.
- [x] Assert real health loss, five-chip payoff, phase, archive, extraction and first-clear/next selection. Include pause during protection, resumed clocks, repeated retries and regional/render budget views.
- [x] Invoke requesting-code-review for a read-only functional review with exact scope; reviewer must not control Unity or write shared files.
- [x] Capture a clean Game window with Stats hidden. Restore its original preference afterward. Use actual app audio, inspect key exported frames, and fully decode video/audio.
- [x] Save scene, stop Play, clear temporary devices/hooks, restore Editor options. Write `docs/COMMERCIAL-OPENING-UPGRADE.md` and `docs/commercial-opening/` evidence/comparison/video.

## Tool contracts learned in the previous round

- Working Pipeline names: `unity command editor status`, `unity command script run --file ... --entry ...`, `unity command editor.playmode start/stop`. Old underscored aliases are rejected.
- AutoRefresh is off; `/tmp/riftline-quality-refresh.py` invokes Assets/Refresh privately on port 7800. Never print the descriptor's eval token.
- Native `/tmp/riftline_mcp.py Unity_RunCommand` code is `CommandScript : IRunCommand`; avoid reflection/interop there and fully qualify `UnityEngine.UI.Image`. Bootstrap/live/stop code must be separate because mentioning EditorSettings/AssetDatabase can force Play exit.
- Pipeline script-run supports ordinary external static C# methods and is faster than native rewriting. Keep drivers outside Assets.
- Let initial Game rendering settle ≥25 frames. Call `Canvas.ForceUpdateCanvases()` before computing mouse coordinates and verify raycast hits the intended button.
- `EditorWindow.Focus()` alone may not activate macOS Unity. Use `osascript` to activate Unity; resume unexpected pauses only when TrackingReady, at a bounded interval, with the intended UI selection.
- Existing `/tmp/riftline-engagement-recorder` uses ScreenCaptureKit, prohibited activation policy and first-audio timestamp anchoring. Recorder sessions must not overlap. Trim startup/tooling idle by inspecting actual frames, crop top 109/bottom 23 of a 1728×986 window and preserve speed/audio.

Plan reviewed for scope, missing types, lifecycle and stable interfaces. Proceed under the user's existing authorization; do not pause for another permission cycle.
