# RIFTLINE — Commercial Presentation Upgrade

## Scope and authorization

The user rejected the first delivery's visual and gameplay quality. The specific complaints are incorrect character animation, no camera follow after turns, uncertainty about XR Origin, absent skybox, and an overall prototype appearance.

The existing authorization is to complete the game directly. This upgrade proceeds without a design-approval pause. Art and audio sources continue to come from Unity AI Generator. Validation remains Unity Editor only; no APK or physical-device test is authorized by this scope.

## Observed baseline

- Native Editor inspection found one XR Origin, one Camera and one Audio Listener in `Assets/Riftline/Scenes/Riftline.unity`.
- The scene has the built-in skybox material, but the camera uses `SolidColor`, HDR is disabled and postprocessing is disabled. Consequently there is no visible authored sky environment.
- A real keyboard review ran for 36.001 seconds, executing seven ordinary key presses and capturing 89 actual Game-view frames. It uses isolated save data and no production autopilot.
- During a 90-degree route turn, both the camera's world yaw and its local yaw remained zero. `RiftlineWorld` applies the inverse heading to the entire outside world instead of turning a navigation rig.
- Actual Run images show a severely folded pose. The current mixer compresses entire four-second generated clips into .85 seconds for Run, .4 seconds for most actions, repeatedly selects lane clips from displacement, and leaves all inputs advancing in the background.
- Avatar `isValid`/`isHuman` and nonempty animation curves do not establish a correct bind pose, retargeted stance, gait, floor contact or action timing.
- Enlarged copies of the same landmark, flat lighting, unstructured scenery and undersized HUD text fail the intended commercial presentation.

Intermediate evidence is under `/tmp/riftline-quality-before`; selected comparison evidence will be retained with the final review.

## Approach

| Approach | Result and tradeoff |
| --- | --- |
| Preserve Core; rebuild animation and presentation | Selected. Keeps verified contracts, archives, economy and saves while directly addressing all reported defects. |
| Change lighting and colors only | Low implementation cost, but leaves the movement/rig defects and weak environment composition unresolved. |
| Rewrite gameplay and presentation together | Adds migration and content-regression risk without a demonstrated Core defect. |

## Character motion

First compare the generated skeleton's current transforms to the mesh bind poses, and sample Idle/Run/Jump/Slide independently of the runtime mixer. Measure head/hip/foot ordering in world coordinates, inspect rendered contact sheets, and compare foot-IK on/off. This distinguishes an invalid reference pose or root-height convention from poor source motion and playback timing.

Normalize the reference pose and rebuild the humanoid Avatar when required. Preserve mesh bind alignment and generated textures. Replace visibly unsuitable source clips through Generator; derive stationary, seamless gait segments from generated motion. The animation player owns explicit locomotion/action transitions and selected clip time:

- Idle and Run loop with stable cadence and no repeated restart.
- Jump, fast fall, landing and slide follow actual simulation state and duration.
- Lane changes and turns start once per committed transition; small root lean supplies directional readability.
- Phase uses a restrained overlay; fail/victory play once and hold their final pose.
- Pause freezes all motion; countdown does not re-run an entire start clip every frame.
- Track the selected state, clip time and weights as real diagnostics.

No code-created substitute character, canned stock animation or silently disabled animation is an acceptable fix.

## XR navigation

Introduce a dedicated `RiftlineNavigationRig` between Navigation Core and the existing XR Origin. The navigation frame also anchors cockpit, menus and runner presentation. The existing tracked Camera remains a child of XR Origin/Camera Floor Offset, with its device-local pose fully owned by TrackedPoseDriver.

Smooth mode turns the navigation frame through the existing .65-second alignment curve; stepped mode performs two 45-degree steps; fixed mode keeps its heading and unfolds the route. Outside-world presentation retains its calibrated orientation and scrolls along the sampled route without applying inverse camera yaw.

Calibration preserves the XR Origin's world pose while changing the navigation frame, so recentering does not double the HMD's local yaw. Starting/retrying first resets route alignment and then calibrates from the current head direction. Paused recentering retains the current presented heading. During ordinary lane/jump/slide movement, the navigation frame must not acquire lateral, vertical, pitch or roll motion.

Update acceptance to distinguish stable tracked local pose from intentionally changing navigation yaw. The old check that every camera ancestor must remain unchanged is not a correct follow-camera specification.

## Environment, materials and lighting

Generate authored skyboxes for orbital docks, neon rain city and quantum ruins. Assign them to the actual camera/RenderSettings and integrate reflection/ambient lighting. Sky transition follows the existing regional six-second transition.

Use a restrained HDR/ACES/Bloom setup and MSAA without motion blur, chromatic aberration or view-wide flashes. Recompose existing/generated scenery into foreground, middle distance and skyline; reduce distorted landmark scaling and repetitive silhouettes. The rail deck needs readable lane surfaces, edge structure, controlled emission and contact grounding. Hazards retain clear shape and orange/cyan semantics with moderate line thickness.

Keep generated character texture detail during cosmetic changes. Tune regional PBR materials and avoid replacing all character submaterials with a single unrelated tiled material.

## Interface and feedback

Raise actual Game-view text size and contrast, organize HUD information into readable cockpit zones, and remove the cut-off desktop controller/wrist fragment. Keep the center corridor clear. Home, contract list, outfits, archives, settings, pause and results share a coherent dark industrial palette, consistent spacing and deliberate headings. Retain Chinese/English and mouse/XR-ray operation.

Local phase effects, action cues and contact shadows should reinforce motion without masking the runner. All existing generated sound cues and save semantics remain.

## Acceptance gates

1. Play the tutorial with actual keyboard events at normal time, then inspect motion footage for Idle, Run, lane transitions, Jump, Slide, Turn, Fail and recovery.
2. Compare rendered source-motion samples before/after; demonstrate a natural upright stance, feet below the hips in grounded locomotion and visible alternating strides. Verify no gait restart during ordinary running and no unexpected action repetition.
3. A normal 90-degree turn reaches camera world yaw approximately 90 degrees in smooth/stepped modes while camera local tracked pose is unchanged. Fixed mode remains fixed. Test both turn directions, pause/countdown and recenter/retry continuity.
4. Exactly one valid XR Origin, camera and listener remain in the saved scene. Runtime diagnostics clearly separate desktop fallback from actual XR.
5. All three authored sky environments render. Inspect regional gameplay, silhouettes, surface detail, shadow grounding, readable HUD and bilingual menu/result screens.
6. Run compilation, affected Play Mode regressions and the unchanged Core suite. Keep genuine failures and make no device-frame-rate or store-certification claim from Editor evidence.
7. Remove temporary review devices/hooks, restore Editor play options and any process proxy, stop temporary generation transport, save the scene and deliver actual comparison images with an updated report.
