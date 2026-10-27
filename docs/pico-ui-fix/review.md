# Riftline PICO XR UI Fix: Release Read-Only Review

## Verdict

**Ready for physical PICO device validation.**

No Critical or Important findings were identified. The first-pose stabilization,
shared one-shot gate, explicit Recenter behavior, scene pose bindings, and shader
retention are consistent with the stated fix. One existing Minor build-footprint
risk remains from retaining complete URP shaders.

This was a read-only review of source, scene YAML, GraphicsSettings, installed
PICO XR 6.1.1 reference code/sample prefab, and Input System 1.20.0
`TrackedPoseDriver`. No Unity commands, builds, or runtime regressions were run.

## Findings

### Critical

None.

### Important

None.

### Minor

#### Full URP shaders remain retained

- Location: `ProjectSettings/GraphicsSettings.asset:29-39`;
  `Assets/Riftline/Editor/RiftlineSceneBuilder.cs:26-31,240-270`
- All three retained shaders are required by the six runtime `Shader.Find`
  sites under `Assets/Riftline/Scripts`, and their serialized GUIDs resolve to
  URP Unlit, Lit, and Particles/Unlit. Retaining each complete shader through
  Always Included Shaders can still increase Android build time and APK size,
  especially for URP Lit.
- This is not a rendering correctness blocker. Record shader compilation time
  and APK size during release validation; use serialized materials or a
  deliberately captured variant collection later if the cost is material.

## Pose Stabilization Verification

- `RiftlineHeadTracking` requires and enables `TrackedPoseDriver` independently
  of legacy `XRSettings.isDeviceActive`
  (`RiftlineHeadTracking.cs:6-31`).
- The scene driver is enabled, uses `UpdateAndBeforeRender`, honors tracking
  state, and binds `<XRHMD>/centerEyePosition`,
  `<XRHMD>/centerEyeRotation`, and `<XRHMD>/trackingState`
  (`Riftline.unity:158-247`). These controls exist on PICO's `PXR_HMD` layout
  and match the installed PICO XR reference prefab.
- A first pose is recognized only after the Camera local transform differs from
  its startup pose by more than 1 mm or 0.1 degrees. Stability then requires
  movement within 2 mm and 0.2 degrees for 0.15 seconds
  (`RiftlineHeadTracking.cs:9-12,40-65`).
- The 0.75-second maximum is measured from the first non-default applied pose,
  not from `Awake`, so it cannot fire on an untouched startup transform. It
  prevents natural head motion from postponing alignment indefinitely
  (`RiftlineHeadTracking.cs:47-68`).
- An exact identity pose performs no automatic alignment. This is correct for
  the current scene because `RiftlineGame.Awake()` initially recenters the
  presentation against the identity Camera and places the UI on presentation
  +Z (`RiftlineGame.cs:67-80`; `RiftlineWorld.cs:303-318`).
- `poseObserved` permanently stops the detector after its first attempt, while
  `calibratedXr` is the shared game-level gate. Explicit Recenter also closes
  that gate, preventing a later duplicate startup alignment
  (`RiftlineHeadTracking.cs:35,70-71`; `RiftlineGame.cs:250-263`).

## Phase and Recenter Semantics

- Automatic alignment cannot execute during `RunPhase.Running`: both the
  detector and both game-level alignment methods independently reject it
  (`RiftlineHeadTracking.cs:35-38`; `RiftlineGame.cs:250-260`).
- While Running, stabilization state is left pending rather than consumed. On
  pause or another non-Running phase, it can align once using the current Camera
  pose. The 0.75-second cap may make that alignment immediate after a long run;
  this remains outside Running and uses the latest transform.
- Public `Recenter()` remains repeatable, rejects Running, refreshes tracking,
  and requires `TrackingReady` (`RiftlineGame.cs:242-248`). Automatic startup
  alignment deliberately bypasses the legacy tracking-ready gate and is driven
  by an applied Camera pose.
- `RiftlineNavigationRig.Calibrate()` preserves the XR tracking origin's world
  transform while adjusting the presentation frame
  (`RiftlineNavigationRig.cs:19-30`). `RiftlineWorld.Recenter()` moves the
  presentation/UI once; it does not head-lock the UI.

## Scene, Shader, and Instrumentation Checks

- The reviewed Riftline scene contains one XR Origin, one Main Camera, one
  AudioListener, one enabled `PXR_Manager`, and correctly wired game,
  navigation, tracking-space, and Camera references
  (`Riftline.unity:130-313,498-675,931-979`).
- The three Always Included shader GUIDs resolve to the exact three names used
  by all runtime `Shader.Find` calls under `Assets/Riftline/Scripts`.
  `EnsureRuntimeShaders()` remains idempotent and is called by both scene build
  and presentation refresh.
- No `[DEBUG-pico-ui]` markers or temporary pose/viewport/frame diagnostics
  remain in project runtime or editor source. Existing general validation and
  operational logs are unrelated to this fix.

## Remaining Physical-Device Validation

1. Cold-launch while still, while turning continuously, and while changing
   headset height; verify alignment occurs once after settling or by 0.75 s.
2. Cold-launch at runtime identity heading and confirm the startup UI remains
   visible without an automatic movement.
3. Start a run before stabilization completes; confirm no presentation movement
   during Running and a single alignment only after entering a non-Running
   phase.
4. Exercise explicit Recenter in Menu and Paused states, verify it remains
   repeatable, and confirm it is ignored during Running and while tracking is
   not ready.
5. Test tracking loss/recovery and Android suspend/resume before and after the
   one-shot alignment.
6. Confirm no shader-null startup exception, then record APK size, shader build
   time, and frame behavior on the target PICO model/PICO OS version.
