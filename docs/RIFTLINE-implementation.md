# RIFTLINE VR Implementation Plan

**Goal:** Deliver the full offline RIFTLINE VR game in this Unity project, with AI Generator artwork and verified Editor gameplay.

**Architecture:** A deterministic run simulation owns commands, collisions, scoring, progression and procedural events. Unity presentation samples the simulation without attaching the tracked camera to the runner. A stationary observation frame, world-space menus and Input System actions support both Editor keyboard/mouse play and PICO controllers.

**Tech Stack:** Unity 6000.6.7f1, URP 17.3, Input System 1.20, PICO XR SDK already installed, Unity AI Assistant 2.21 asset generators.

## Global Constraints

- User authorized direct end-to-end implementation; no additional design approval required.
- Validate in Unity Editor only. Do not build APKs or claim physical-device performance or comfort validation.
- Ship 18 contracts, 3 regions, endless mode, 30 archives, 8 cosmetic outfits.
- Seven paid outfits cost 6700 chips in total; first contract completions award 3600 chips in total.
- Initial speed 6.5 m/s; acceleration 0.008 m/s²; cap 10 m/s.
- Lane spacing 2.6 m; lane transition 0.32 s; runner 5.5 m forward and 1.1 m below viewing height.
- Input edges: stick ±0.6 activation, ±0.25 reset; trigger 0.65. Buffer 200 ms.
- Turns announced 2 seconds ahead, realignment 0.65 s; 1.1 seconds of safe exit; turns separated by at least 3 seconds.
- Phase duration 0.8 s; near miss energy 20% or 30%; multiplier penalty 0.5, minimum 1.
- Pause, tracking loss and menus stop gameplay time, distance and scoring. Archives save immediately.
- Three rotating integrity-checked saves; UI in Simplified Chinese and English.
- Artwork and audio are created through Unity AI Generator. Procedural assembly, UI layout and gameplay indicators use generated assets.

## Task 1: Simulation, content and offline progression

Files: `Assets/Riftline/Scripts/Core/`, `Assets/Riftline/Scripts/Core/GameCatalog.cs`.

- [x] Implement catalog with all contracts, archives, cosmetics and 37 explicitly identified path templates.
- [x] Implement run state, edge-safe command handling, obstacle events, deterministic reachable generation, tutorial and three boss recipes.
- [x] Implement scoring, first-clear rewards, unlocks, immediate archive persistence and three rotating saves.
- [x] Verify speed integral, 10000 seeds in each mode, collisions, phase, pause and economy using an Editor validation harness.

## Task 2: Unity presentation and input

Files: `Assets/Riftline/Scripts/Runtime/`, `Assets/Riftline/Editor/`.

- [x] Create game controller with public menu/settings/run API.
- [x] Build pooled road, obstacle and scenery presentation from AI prefabs; generated rigged runner and 15 humanoid animation states.
- [x] Keep head tracking independent of runner actions; implement smooth, stepped and fixed alignment.
- [x] Implement controller and keyboard actions, world-space pointer input, tracking/focus pause, haptics and audio.
- [x] Add deterministic scene builder, one primary camera/listener, quality settings and serialized asset links.

## Task 3: Interface and accessibility

Files: `Assets/Riftline/Scripts/UI/`.

- [x] Build designed world-space home, contract selection, outfit preview/store, archive library and settings.
- [x] Add countdown, restrained cockpit HUD, wrist phase display, turn announcements, pause and failure replay.
- [x] Expose comfort preset, alignment, mask, posture, handedness, button remapping, feedback volumes, visuals and rest reminder.
- [x] Keep UI operable with Editor mouse and implement controller rays; physical controller verification is outside the requested scope.

## Task 4: AI asset production

Files: `Assets/Riftline/Art/Generated/`, `Assets/Riftline/Audio/Generated/`.

- [x] Generate runner, rig, 15 principal animation clips, cockpit, platform, controller, eight obstacle families and collectible.
- [x] Generate three regional landmarks and shared regional surface materials.
- [x] Generate cinematic menu art, action symbols, music and distinct gameplay/UI sounds.
- [x] Inspect generated assets, configure import, prefab scale, materials and native mesh LOD.
- [x] Record generation model, prompt, output and status in an asset provenance manifest.

## Task 5: Integration and Editor acceptance

- [x] Run Unity compilation and fix all introduced errors.
- [x] Execute full content/generation/progression validation.
- [x] Run Play Mode interactions: new game, tutorial, commands, all turn modes, phase, each obstacle, loss, retry, pause/resume, safe exit, contract finish, archive, outfit and language; verify damaged-save recovery in the Editor Core harness.
- [x] Run accelerated autopilot across both branches of 18 contracts and endless region transitions using real simulation and presentation.
- [x] Inspect screenshots at home, gameplay and results; verify no missing materials/assets.
- [x] Deliver playable `Assets/Riftline/Scenes/Riftline.unity`, controls guide and honest Editor validation report.

## Interpretation of unspecified mobile rules

The supplied VR document references mobile scoring and contract details without including their tables. This implementation defines reproducible base scoring (distance, chips and near misses), contract names/objectives and star targets in the catalog. Those are production choices, not recovered mobile data. Contract duration remains 3–7 minutes, with tutorial assistance and replayable deterministic routes.

## Verification record

The source is document `P0b2d85GkoTCcWx9lq8cM210nle`, revision 30, retrieved on 2026-10-23. The complete game is integrated in the project scripts and saved scene; temporary staged implementations are not runtime dependencies.

Actual Unity Editor Core validation passed 79 groups, 504 independent executed witnesses, 20000 complete streams and 420 road-height witnesses. All 37 templates have semantic event coverage. The validator also exercises collision boundaries, frame partitioning, failed storage, duplicate settlements, corrupted-save recovery, tutorial recovery and earned outfit progression. Evidence: [core-validation.json](validation/core-validation.json).

The full Play Mode session durably completed 37 content runs: all 18 primary routes, all 18 alternate routes and 600.009 seconds of endless play. It earned 54 stars, acquired all 30 archives and purchased/equipped/reloaded all eight outfits, spending 6700 earned chips with 577 remaining. Three alignment modes and pause/resume on an elevated route were observed with an independent camera snapshot. Runtime errors, warnings and missed game-camera frames were zero.

The full-session JSON deliberately retains `Status: Failed`: its subsequent manual keyboard check discovered a held lower-priority steering edge after a jump. The source now latches steering before higher-priority input returns. A separate actual queued-keyboard regression passed all four manual runs. These results must be read together; the full suite has not been represented as a single successful rerun. Evidence: [play-mode-validation.json](validation/play-mode-validation.json), [input-regression-before.json](validation/input-regression-before.json), [manual-input-validation.json](validation/manual-input-validation.json).

All required source art and audio were generated through Unity AI Generator: 16 game mesh families, Eve's generated rig, 15 four-second humanoid animations with 130 curves each, three regional and eight outfit materials, menu art, action symbols, music and 11 feedback sounds. Every one of the 88 recorded operations has a present output. The scene library references the actual generated files and all game meshes have native reduced LODs. See [asset-provenance.json](asset-provenance.json) and [DEPENDENCIES.md](DEPENDENCIES.md).

Final terminal-page visibility passed 72 checks across an actual 300-second boss victory and 600.022-second endless run. The original failure recorded 15 active hazard/turn indicators; the corrected terminal results recorded zero and were visually inspected. Natural collision, moving generated-runner replay, queued same-seed retry, pause/resume and safe exit passed 105 checks. Both targeted sessions recorded zero runtime errors, warnings or missed frames. Evidence: [terminal-visibility-before.json](validation/terminal-visibility-before.json), [terminal-visibility-validation.json](validation/terminal-visibility-validation.json), [failure-retry-validation.json](validation/failure-retry-validation.json).

[CONTROLS.md](CONTROLS.md) documents ordinary Editor play; [EDITOR-VALIDATION.md](EDITOR-VALIDATION.md) records the complete evidence and desktop measurement limits.
