# Riftline pure hand controls design

## Status

Approved on 2026-10-27.

This specification defines a mandatory hand-tracking control scheme for Riftline. It is seated-first, supports standing play, and provides no controller fallback in PICO builds. Editor-only keyboard simulation may remain available for development.

## Goals

- Replace runtime controller input with reliable hand gestures while preserving the existing runner rules.
- Keep frequent actions low-amplitude and usable for a 30-minute seated session.
- Prevent ordinary hand motion, tracking jitter, and tracking reacquisition from generating commands.
- Provide clear confirmation without controller haptics.
- Reuse proven XR Hands and XR Interaction Toolkit sample behavior where it fits.

## Existing boundary

Riftline gameplay already converges on these discrete `GameCommand` values:

- `Left`
- `Right`
- `Jump`
- `Slide`
- `Phase`
- `Pause`

The hand system must translate gestures into this command boundary. It must not add gesture logic to `RunSimulation`, alter collision rules, or change scoring and progression.

Relevant references reviewed:

- `Assets/Samples/XR Interaction Toolkit/3.6.1/Hands Interaction Demo/HandsDemoScene.unity`
- `Assets/Samples/XR Interaction Toolkit/3.6.1/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab`
- `Assets/Samples/XR Interaction Toolkit/3.6.1/Hands Interaction Demo/Scripts/PokeGestureDetector.cs`
- `Assets/Samples/XR Interaction Toolkit/3.6.1/Hands Interaction Demo/Scripts/TeleportHandGesture.cs`
- `Assets/Samples/XR Interaction Toolkit/3.6.1/Hands Interaction Demo/Scripts/JoystickPinchHandGesture.cs`
- `Assets/Samples/XR Hands/1.7.3/Gestures/HandGestures.unity`
- `Assets/Samples/XR Hands/1.7.3/Gestures/Examples/Scripts/StaticHandGesture.cs`
- `Assets/Samples/PICO XR/6.1.1/PICO Interaction Demo/Starter.unity`
- `Assets/Riftline/Scripts/Runtime/RiftlineInput.cs`
- `Assets/Riftline/Scripts/Core/RunSimulation.cs`

## Selected approach

Use low-amplitude event gestures.

The alternatives were rejected for these reasons:

| Approach | Decision |
| --- | --- |
| Low-amplitude event gestures | Selected. Matches discrete runner commands, supports seated play, and permits explicit arming, hysteresis, and rearming. |
| Continuous Pinch virtual joystick | Rejected as the primary model. Continuous direction and recenter behavior do not match lane-based commands and conflict more easily with UI Pinch. |
| Large arm or body gestures | Rejected. More expressive but too tiring, less reliable while seated, and more likely to leave the tracking volume. |

The selected design may reuse displacement sampling from `JoystickPinchHandGesture`, but it emits one command per armed gesture. It is not a continuous joystick.

## Gesture mapping

The player selects a dominant hand during first-run onboarding. All gameplay mappings mirror for left-handed use.

### Frequent runner commands

The dominant hand controls `Left`, `Right`, `Jump`, and `Slide`.

1. The player forms a thumb-index Pinch while the other fingers remain relaxed rather than fully clenched.
2. The system records the Pinch position as an anchor.
3. The player moves the Pinch a short distance in the HMD camera plane.
4. Left, right, up, and down map to `Left`, `Right`, `Jump`, and `Slide`.
5. Crossing a valid direction threshold emits exactly one command.
6. The player must release Pinch before another directional command can arm.

Camera-plane coordinates make the mapping independent of world yaw and wrist roll. A stationary Pinch that is released produces no gameplay command.

### Phase

The dominant hand transitions from relaxed/open to a full fist and maintains the fist for 180 ms. The system emits `Phase` once, then remains latched until the hand opens again.

Open/Fist recognition uses mutually exclusive XR Hands shapes. A Pinch cannot simultaneously qualify as a Phase fist.

### Pause and menu

The non-dominant palm faces the player's head and remains stable for 450 ms. This emits `Pause` and opens a hand-anchored menu.

Gameplay recognizers and UI interactors are mutually exclusive:

- Running: gameplay gestures enabled; UI Pinch, poke, and ray selection disabled.
- Menu or paused: gameplay gestures disabled; hand UI interaction enabled.

The menu may reuse the `HandsDemoScene` hand-menu presentation and its `NearFarInteractor`, `XRPokeInteractor`, and Pinch Select behavior. One interaction stack owns UI selection; PICO and generic XRI interactors must not both process the same Pinch.

## Directional recognition state machine

The initial tunable values are:

| Setting | Initial value |
| --- | --- |
| Pinch press threshold | `0.75` |
| Pinch release threshold | `0.35` |
| Neutral/open stability | `80 ms` |
| Direction displacement | `6 cm` |
| Direction time window | `350 ms` |
| Dominant-axis ratio | `1.35` |
| Rearm cooldown after release | `80 ms` |
| Candidate tracking-loss cancellation | `100 ms` |
| Sustained dominant-hand loss auto-pause | `300 ms` |
| Phase fist hold | `180 ms` |
| Pause palm hold | `450 ms` |

These are first-device-test baselines, not immutable gameplay constants.

The directional states are:

1. `Neutral`: tracking is valid and the dominant hand has been relaxed/open for 80 ms.
2. `Armed`: Pinch strength crosses `0.75`; capture the anchor and start the 350 ms window.
3. `Candidate`: measure camera-plane displacement from the anchor.
4. `Fired`: when displacement exceeds 6 cm and one axis is at least 1.35 times the other, send one `GameCommand`.
5. `Latched`: ignore further displacement until Pinch falls below `0.35`.
6. Return to `Neutral` after release and an 80 ms cooldown.

An ambiguous diagonal does not fire. The candidate stays visible until the player resolves a dominant axis, releases Pinch, or exceeds the time window.

## Tracking and failure behavior

- Tracking confidence loss lasting 100 ms cancels an armed or candidate gesture without emitting a command.
- Reacquisition never infers or replays a gesture from the new pose.
- Reacquisition requires a fresh `Neutral` state before arming.
- Dominant-hand loss lasting 300 ms while running automatically pauses the run. It must not count as player failure.
- After auto-pause, the dominant hand must remain open and tracked for 300 ms. The player then explicitly resumes.
- Non-dominant hand loss does not interrupt running because that hand is only required to open the pause menu.
- A run cannot start its countdown until the dominant hand is tracked and neutral.
- Handedness changes are allowed only outside an active run.

## Feedback

Riftline cannot rely on controller vibration. Gesture feedback therefore uses both visual and audio confirmation.

### Directional gestures

- On Pinch arm, show a small semi-transparent four-direction outline at the captured anchor.
- Visualize displacement toward the current candidate direction.
- On command fire, pulse the selected direction for approximately 80 ms, play a short spatial confirmation sound, and trigger the existing world or rail response.
- On ambiguous diagonal or expired input, provide a restrained reject tick and return animation. Do not emit a command.

### Phase

- While the fist is held, show an 180 ms charge ring.
- On activation, pulse the hand outline and the existing Phase world effect.
- Keep the gesture latched until the hand opens.

### Pause

- Show an intent preview before the 450 ms palm hold completes.
- Anchor the menu to the non-dominant hand only after Pause is confirmed.

Hand visuals should be lightweight outlines or restrained hand-mesh feedback rather than opaque, always-on hands. They must remain readable over Riftline's environment without obscuring hazards.

## Runtime components

The implementation should preserve narrow ownership boundaries:

| Component | Responsibility |
| --- | --- |
| `RiftlineHandInput` | Own run/menu mode, consume confirmed gesture events, and call `RiftlineGame.SendCommand()`. |
| `RiftlineDirectionalGesture` | Implement dominant-hand Pinch anchor, displacement, direction classification, latching, and rearming. |
| `RiftlineStaticGesture` | Recognize Open/Fist transition and Phase hold timing. |
| `RiftlineHandTrackingGuard` | Track confidence, cancel candidates, gate run start, and request auto-pause on sustained loss. |
| `RiftlineHandFeedback` | Render anchor, direction, hold, rejection, and tracking feedback; play confirmation audio. |
| `RiftlineHandMenuController` | Switch gameplay recognizers and XRI menu interactors atomically. |
| `RiftlineHandControlSettings` | Store tunable thresholds and default handedness without embedding values in recognizer logic. |

Gesture recognizers expose semantic events such as directional command confirmed, Phase confirmed, candidate cancelled, and tracking unavailable. They do not directly modify simulation state.

`RiftlineHandInput` is the only new component allowed to cross into the existing `GameCommand` interface. This keeps hand recognition testable without a running game.

## Sample reuse and PICO integration

- Use `XRHandSubsystem` and `XRHandTrackingEvents` as the joint and tracking source.
- Reuse `XRHandSkeletonDriver` and `XRHandMeshController` only where needed for hand presentation.
- Adapt `JoystickPinchHandGesture` concepts for anchor capture, displacement projection, and smoothing.
- Reuse XR Hands Open/Fist assets and the exclusivity behavior demonstrated by `StaticHandGesture`.
- Reuse the Hands Interaction Demo's hand-menu, poke, and near/far interaction concepts in menu mode.
- Keep PICO hand tracking enabled in `PXR_ProjectSetting`.
- Do not enable controller interactors as a runtime fallback in the PICO release.

## Onboarding and calibration

First-run onboarding lasts approximately 30 seconds:

1. Select dominant hand.
2. Hold the dominant hand in a comfortable chest-level control zone.
3. Complete Left, Right, Jump, and Slide once each.
4. Complete Phase once.
5. Open the hand menu and select Resume once.

The onboarding records whether the default 6 cm directional threshold is reachable comfortably. It may choose a per-player threshold clamped between 4.5 cm and 7.5 cm. Pinch strength thresholds, hold times, axis ratio, and tracking-loss times remain controlled by `RiftlineHandControlSettings`.

Calibration persists with settings and can be rerun outside an active run. The system must not silently recalibrate while running.

## Validation

### Deterministic tests

Feed recorded or synthetic hand samples into recognizers and verify:

- Press/release hysteresis near both Pinch thresholds.
- Exactly one command per Pinch.
- All four camera-plane directions under different head yaw and wrist roll.
- Diagonal ambiguity and time-window expiration.
- No command after candidate cancellation or tracking reacquisition.
- Phase Open/Fist exclusivity and one-shot latching.
- Atomic run/menu interactor switching.
- Left-handed mirroring.
- Existing 0.2-second gameplay input buffering remains unchanged.

### Editor and simulator checks

- Use synthetic hand data to exercise visual feedback and menu switching.
- Use PICO SpaceOS simulation for lifecycle and XR session checks.
- Do not treat Editor or simulator results as physical-device tracking acceptance.

### Physical PICO acceptance

- At least 98% successful recognition for each command after onboarding.
- Fewer than one unintended gameplay command per 10 minutes of normal hand motion.
- P95 latency from threshold crossing to `GameCommand` emission no greater than 120 ms.
- No duplicate, inferred, or replayed command after tracking loss.
- No simultaneous gameplay and UI response to one Pinch.
- Automatic pause occurs after sustained dominant-hand loss and never records a collision during the paused state.
- A 30-minute seated session has a self-reported arm/hand fatigue score no greater than 2 out of 5.
- Standing play remains functional without requiring a separate mapping.

Test cold start, pause/resume, recenter, handedness selection, and repeated tracking loss on physical PICO hardware.

## Non-goals

- Controller fallback in the PICO release.
- Continuous locomotion or analog steering.
- Body, leg, or torso tracking.
- Voice commands.
- Changes to `RunSimulation`, obstacle timing, scoring, collision, save data, or progression.
- Automatic difficulty compensation based on tracking quality.

