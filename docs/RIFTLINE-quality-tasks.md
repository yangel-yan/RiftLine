# RIFTLINE Commercial Presentation Implementation Plan

**Goal:** Repair the reported motion/camera defects and replace prototype presentation with a coherent, playable commercial-style VR game.

**Architecture:** Keep the deterministic Core unchanged. A navigation rig owns route yaw; a motion player owns clip selection/time; an environment component owns authored skies and lighting. Existing world and UI consumers use the navigation frame.

**Tech Stack:** Unity 6000.6.7f1, URP 17.3, Input System 1.20, XR Core Utilities, PICO XR 6.1.1, Unity AI Generator.

The user authorizes direct execution. Remain in the shared project, which has no Git repository. Store temporary diagnostic commands, rendered sample caches and restoration state under `/tmp`; retain final code, generated game assets and comparison deliverables in the project.

## Task 1 — Diagnose and fix humanoid playback

Files: `RiftlineArtSetup.cs`, `RiftlineRunnerAnimation.cs`, generated Eve Avatar/prefab and generated clips.

- [x] Run actual keyboard baseline and capture camera/pose/frame evidence.
- [x] Sample each essential clip on the actual generated rig at fixed timestamps in a render stage; compare bind matrices with current transforms.
- [x] Record a failing natural-stance/gait contact result before modifying the rig.
- [x] Repair the demonstrated bind/root-motion convention; regenerate only unsuitable motion and use Generator stationarity/loop editing.
- [x] Replace implicit displacement-based clip selection and whole-clip compression with explicit time/transition ownership.
- [x] Re-run the same motion samples and normal-time tutorial, inspect every action contact sheet.

## Task 2 — Correct navigation follow

Files: new `Scripts/Runtime/RiftlineNavigationRig.cs`; `RiftlineSceneBuilder.cs`, `RiftlineGame.cs`, `RiftlineWorld.cs`, `RiftlinePlayValidation.cs`.

- [x] Record old camera world yaw 0 while route heading changes to 90.
- [x] Add serialized navigation frame above the existing XR Origin; anchor cockpit/UI/runner to it.
- [x] Move alignment yaw to the navigation frame; remove inverse yaw from the outside-world rotation.
- [x] Preserve HMD world pose during calibration and reset alignment before start/retry recentering.
- [x] Change acceptance to protect tracked local pose without forbidding intended navigation rotation.
- [x] Verify left/right turns for smooth/stepped/fixed modes, pause/resume and recenter continuity.

## Task 3 — Authored environment and lighting

Files: new environment component and generated cubemaps/materials; `RiftlineAssets.cs`, `RiftlineSceneBuilder.cs`, `RiftlineWorld.cs`.

- [x] Record SolidColor/no-HDR/no-postprocessing baseline.
- [x] Generate three coherent regional skyboxes through Unity AI Generator and record actual outputs.
- [x] Use Skybox clearing, HDR, MSAA and restrained ACES/Bloom; tune key/fill/ambient/reflection response.
- [x] Recompose scenery and lanes, remove distorted overscaling, retain generated surface/detail maps.
- [x] Add readable contact grounding, stable horizon/cockpit and moderate hazard edges.
- [x] Render and inspect all three regions at actual gameplay scale.

## Task 4 — Interface and game feel

Files: `RiftlineUI.cs`, `RiftlineUIElements.cs`, `RiftlineUILocalization.cs`, cosmetic presentation and local feedback.

- [x] Remove clipped desktop controller/wrist fragments and improve actual HUD text size/readability.
- [x] Apply consistent dark industrial panels, hierarchy, spacing and action states to all pages.
- [x] Keep generated character textures in cosmetic previews/gameplay and inspect regional outfits.
- [x] Verify mouse clicks/scroll/back, keyboard retry/pause, bilingual pages, phase and results.
- [x] Reproduce and fix preview deletion on repeated outfit-page rebuilds; retain failing and passing rendered checks.

## Task 5 — Final review and restoration

- [x] Play with actual keyboard at normal time; inspect sequential screenshots instead of relying on automatic survival.
- [x] Run affected actual Play Mode and unchanged Core validation; preserve honest per-session results.
- [x] Capture before/after motion, turn and three-region screenshots.
- [x] Restore any temporary generation proxy, review inputs/save hooks and Editor settings, then save the playable scene.
- [x] Update validation and dependency/provenance documentation and link final comparison deliverables.

Evidence: [QUALITY-UPGRADE.md](QUALITY-UPGRADE.md). Desktop Editor scope is complete; this checklist does not certify a PICO device build or store release.
