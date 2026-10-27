# Riftline engagement implementation plan

Goal: visible survival stakes, a direct start and useful action/reward feedback in the opening minute.

Architecture: extend the existing deterministic RunSimulation, preserve the generated art library and add a small UI feedback presenter. The world-space uGUI remains integrated with NavigationRig. All camera tracking remains untouched.

Constraints: Unity 6000.6.7f1; no Git repository; Editor testing only; temporary harnesses outside Assets; source code in English; UI copy in Chinese and English.

## Task 1 — Survival and opening rhythm

Files: `Scripts/Core/RunSimulation.cs`, `RiftlineTypes.cs`, `TrackGenerator.cs`, `GameCatalog.cs`; checks in `Editor/RiftlineCoreValidation.cs`.

- [x] Run `/tmp/riftline-engagement-baseline.cs`: record first crate's current immediate failure and idle tutorial's silent recovery.
- [x] Add three-life state, one contact per obstacle, 1.25-second simulation-time invulnerability and damage metadata/feedback. Preserve gap/collapse/missed-turn failure.
- [x] Reset life/streak on start; freeze invulnerability through pause and resume countdown. Update analytic event boundaries and existing collision expectations.
- [x] Introduce chip streaks, break on damage/missed chip, reward thresholds 5/10/20 with charge and multiplier.
- [x] Make initial countdown one second, expose its actual remaining time; retain three-second resume.
- [x] Shorten contract 0 to 60 seconds, add first-30-second crystal trails, phase charge/wall and archive fork. Keep clear safe routes and two-second warning geometry.
- [x] Run focused survival regressions and complete independent Core witnesses; update old unlimited-tutorial expectations to explicit health loss.

## Task 2 — Playable UI and feedback

Files: `Scripts/UI/RiftlineUI.cs`, `RiftlineUIElements.cs`, `RiftlineUILocalization.cs`, new `RiftlineRunHud.cs`; `Scripts/Runtime/RiftlineGame.cs`, `RiftlineInput.cs`, `RiftlineAudio.cs`, `RiftlineWorld.cs`.

- [x] Homepage: cropped generated hero artwork, larger title/CTA, direct `StartRecommendedContract()` selecting first unfinished unlocked contract; Enter invokes the same action.
- [x] HUD: separated three-life segments, numeric life, progress/score, streak and phase cards; one contextual upcoming hazard prompt instead of all instructions.
- [x] Event presenter: immediately update life on damage, lost-life callout and edge accents; collected-chip counter pulse and threshold payoff. Timers use simulation time and respect reduced flashes.
- [x] Reuse generated Fail/NearMiss clips for Damage/Combo with controlled pitch/volume; make damage haptics distinct.
- [x] Test actual clicks, Enter, contextual prompts, life loss, reduced-flash mode, pause/failure/retry and bilingual page/large-font navigation in Editor.

## Task 3 — Evidence and restoration

- [x] Record normal-time gameplay with real keyboard input, life loss and later success/retry.
- [x] Inspect captured images/video for readable HUD and menus; repair defects discovered.
- [x] Save scene, stop Play Mode and clear isolated-save/input hooks. Restore Editor settings.
- [x] Publish comparison, video and concise evidence report under `docs/engagement/`; state the exact Editor-only coverage.

Evidence: `docs/ENGAGEMENT-UPGRADE.md`, `docs/engagement/report.json`, `core-all.json`, `recording-live.json`, `recovery-live.json` and `restoration.json`. Temporary test drivers remain outside Assets. Failed harness reports are retained with their causes.
