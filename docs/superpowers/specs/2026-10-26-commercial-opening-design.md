# Riftline commercial opening design

## Authorization and scope

The user asks: “继续优化，让游戏一上来就更有吸引力，达到商业游戏水平”.
Continue implementation under this authorization. Existing project constraints remain: Unity 6000.6.7f1, no Git repository, Unity AI Generator art/audio, Chinese and English UI, Editor validation with an isolated save. Keep temporary drivers and caches outside the workspace. Deliver final reports, comparison and video inside the workspace using computer links.

## Baseline and target

The current first contract lasts 60 seconds, has visible three-life damage, crystal trails, combo charging, a phase wall at 30 seconds and an archive fork at 33 seconds. Safe keyboard witnesses complete it. This is a working sequence, but the view remains a sparse floating rail surrounded by repeated distant towers. There is no visible reason to escape, the destination is abstract, and landing/large rewards have little physical response.

Target: snappy, flowing movement with charged anticipation and released payoff. Establish an understandable mission, a visible threat and a readable route in the first two running seconds. Give the first five-chip chain a distinct audiovisual response. Make phase, archive pickup and extraction progressively larger moments.

Alternatives considered:

| Approach | Tradeoff |
| --- | --- |
| Mission, visible pursuer and authored environment around the existing sequence | Selected. Makes the current actions meaningful and keeps deterministic hazard/health/progression behavior verifiable. |
| A separate pursuit meter, new failure rules and rewritten opening timings | Changes the difficulty and safe-route contract; broadens progression and onboarding regression work. |
| A cinematic opening with additional dialogue | Can establish context, but delays the first player action and requires more generated narrative assets. |

## Player-facing sequence

- Home: “追兵已锁定航道。穿过裂隙，取回档案，抵达撤离点。” Primary launch stays one click; no forced movie.
- 0–3 seconds: the rail is embedded in a lit industrial dock. A generated security drone appears outside the playable lanes. Current two-second hazard warning remains.
- 4–10 seconds: crystal pickup confirms contact; the first five-chip chain causes a short energy pulse, a pursuer reaction and a clear charge payoff.
- Jump/slide/turn: retain verified inputs and animation. Landing receives a short ground ring and a compact impact transient derived from the generated impact sound.
- 27–36 seconds: phase charge, wall and archive fork form the first larger release. Local energy effects react; HUD explicitly changes from recovering the archive to reaching extraction after actual archive feedback.
- 45–60 seconds: an extraction arch becomes visible on the actual final route. Completion celebrates the recovered archive and makes the next unlocked contract the primary result action.
- Failure: current reason/advice stays accurate. Retry clearly restores three lives and keeps collected progression.

## Runtime boundaries

1. A pure C# `RiftlineOpeningPresentation` owns presentation memory, observed landing/feedback timestamps and the current objective. Its `Evaluate(RunSimulation)` never mutates simulation, events, progression or save data.
2. `RiftlineRailArchitecture` owns reusable meshes, materials and pooled foreground dock modules. It consumes world route poses and regional colors; it never adds gameplay colliders.
3. `RiftlineOpeningDirector` owns the generated pursuer, local rings/trails and extraction arch. Threat derives from actual remaining health, reduced during active phase, and never creates an invisible additional failure rule.
4. `RiftlineWorld` exposes a read-only route pose query and calls architecture/director after positioning the runner. Existing navigation and tracked camera behavior stay intact.
5. Audio owns bounded voice segments and music dynamics. Pause/countdown freeze active gameplay voices and simulation-time cutoff timers. Settings volumes apply throughout.
6. Existing uGUI gets goal/escape wording, short opening cues and a clear next-contract action. Keep navigation button object names stable for existing input validation.

## Visual and VR constraints

- Preserve the runner and hazard silhouettes. Drone stays outside the ±3.9 m playable rail boundary. Decorative structure stays outside lanes or above 4 m.
- Reuse generated models and textures; authored rail structure uses shared PBR geometry and restrained emission. No replacement primitive character or downloaded stock asset.
- No head-camera shake, FOV animation, pitch/roll or artificial tracked pose changes.
- Reduced-flash/particle settings apply to the new energy and landing feedback.
- Cache meshes, materials, arrays and components. No allocation or Instantiate/Destroy loop in presentation updates. Destroy owned resources on shutdown and reuse them across retries.
- Target ≤30,000 additional active triangles and ≤24 additional active renderers in the first-region Editor view. Measure before/after rather than claiming a headset frame rate.

## Acceptance

1. Record a red baseline showing no visible pursuer, no extraction arch and no landing impact.
2. Test objective transitions, actual-health threat, archive memory, repeated evaluation, pause/countdown clocks, same-instance retry and new-run replacement without Core mutation.
3. Run normal-time keyboard gameplay through damage, combo, jump, slide, turns, phase, archive and completion; capture each meaningful beat after camera renders.
4. Verify new scenery/actors stay outside hazards and HUD, including both turn directions and fixed/smooth view modes.
5. Verify bilingual menus/results at font scale 1.4, mouse raycast target, retry and next-contract selection.
6. Measure resource counts across repeated retries and representative regional views. Check new effects/voices freeze during pause and reset without stale cues.
7. Run the existing Core suite once after integration and independent read-only code review. Fix actual important findings.
8. Save the scene, restore temporary input/save hooks and Editor options, and export/fully decode a clean actual-window video with real audio.

Editor evidence cannot establish retention, physical headset comfort or store certification. Assess the concrete first-minute experience and document those limits without treating automated passage as human playtest approval.
