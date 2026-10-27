# Riftline UI and brightness design

## User correction and authorization

User: “整体的UI还是像个demo，再就是色调太暗了”.

Continue the previously authorized visual-quality implementation and Editor validation. Proceed autonomously with a concrete implemented result; do not restart a permission cycle. No Git repository. Discussion Chinese, code English. Preserve generated art/audio, gameplay rules, save/progression and XR tracking semantics. Intermediate drivers/snapshots stay under `/tmp`; final screenshots/report/video under `docs/ui-brightness/`. Generated deliverable links use `file:///`.

## Diagnosis

All pages share near-black `RiftlineUIElements.Navy=(7,12,20)` and flat rectangular Image/Button backgrounds. Main canvas has two nested opaque backgrounds and thin generic cyan rules. HUD uses three large black rectangular cards, basic life rectangles, a full-width progress rule and black action/feedback plates. Menu/result content works, but typography and container styling do not establish a finished game identity.

Scene shadows are suppressed by dark trilight values (ground .045/.055/.065), tinted weak runner fill, dark structural metal and ACES without positive exposure. Sky exposure is .9. Runtime Environment reapplies its regional ambient values; changing scene lighting alone would not survive play or region changes.

Live preflight also reproduced `UniversalRenderPipeline.asset=Mobile_RPAsset, supportsHDR=False` twice. The builder only enabled HDR on `GraphicsSettings.defaultRenderPipeline`, while the active Quality override uses `Assets/Settings/Mobile_RPAsset.asset`. The active Mobile renderer has postProcessData, Base camera postprocessing, layer 0 Volume and per-pixel additional lights/limit 4. Configure the actual active pipeline's HDR flag; preserve renderScale .8, MSAA and shadow budgets. This is a configuration consistency gap, not proof that HDR alone accounts for the dark appearance.

## Direction

Selected: a brighter steel-blue console with white typography, restrained cyan detail and a warm gold primary action. Rounded surfaces, vertical shading, fine borders and soft shadow produce consistent material depth. Home gets cleaner mission hierarchy and a larger artwork window; results separate score/distance/reward into stat cards. All secondary pages and pause share the same treatment. HUD keeps every existing health/goal/binding value and position envelope while using lighter translucent cards and inset meters.

Alternatives considered:

| Option | Implication |
| --- | --- |
| Unified steel-blue surfaces plus balanced scene illumination | Selected; fixes both complaints across the interface and play view. |
| Exposure and palette adjustment alone | Faster, but preserves the flat demo-like hierarchy. |
| New UI framework and replacement artwork | Broad migration risk and unnecessary asset replacement. |

## Implementation boundaries

1. `RiftlineUISurface : UnityEngine.UI.Image` supplies rounded geometry, restrained vertical shading, border and optional soft shadow. Image compatibility preserves existing references, button ColorTint, raycasts, stencil/ScrollRect masks and meters. No per-frame texture/material creation.
2. `RiftlineUIElements` uses the shared surface for panels/buttons, a brighter palette, visible selected/hover states and consistent inset labels. Existing helper signatures and button names remain stable.
3. Main owner updates `RiftlineUI`/`RiftlineRunHud`: home hierarchy, result stat cards, lighter HUD/cues/subtitles. Keep true archive/extraction confirmation, next/retry selection, locale, scroll persistence, font .8–1.4 and all event logic.
4. Lighting owner updates `RiftlineEnvironment`, `RiftlineSceneBuilder`, fog/material values in `RiftlineWorld`, and structural metal in `RiftlineRailArchitecture`. Raise regional midtones/ground bounce; key/fill/rim illumination remains bounded and stationary relative to the existing navigation frame.
5. An idempotent editor refresh applies the same builder settings to the existing scene/VolumeProfile/SkyMaterial. No scene rebuild or package changes. Main agent alone controls Unity and changes serialized assets.

## Initial targets to validate visually

- Theme background `(31,46,68)`, raised `(46,64,88)`, white `(245,249,255)`, muted `(178,199,221)` in Color32.
- Panels radius 18–24 UI units; borders 1–2 units; buttons radius 14–18; short meters reduce radius to fit.
- Key directional intensity 2.6, color .94/.97/1; runner fill intensity 36, color .88/.94/1, existing cone/range; warm rim intensity 14, color 1/.72/.42, existing range. No additional shadowed lights.
- Orbital ambient sky .58/.68/.82, equator .48/.57/.70, ground .24/.29/.36; preserve violet and green regional character with comparable brightness.
- Sky exposure 1.05; neutral tonemapping; ColorAdjustments postExposure .45, contrast 4, saturation 6; bloom remains subtle .18. Parameters may be tuned after same-pose screenshot inspection.
- Fog stays atmospheric but uses lighter regional colors and density .0045.
- Structural metal uses brighter neutral steel while generated textures/LOD assignments remain intact.

## Acceptance

- Record actual before/after Home, Contracts, Outfits, Archives, Settings, HUD and results.
- Confirm shared surfaces have finite nonzero mesh geometry, valid indices, sensible rounded bounds, inherited raycast/masking and no growth across page changes/retries.
- Actual mouse Run/Settings/Back/wheel, failure Retry and victory Next/Enter retain production behavior.
- Both languages at font 1.4: inspect visible label bounds and button hit targets; real three-life damage, energy and goal values remain accurate.
- Same-pose gameplay image shows a measurable increase in midtone luminance and visibly readable runner/road/hazards. Inspect all three regions and turn modes; do not claim device frame rate or player preference from pixel metrics.
- Normal-time first-contract input run completes with real damage, phase, archive/extraction and next transition; existing Core suite passes.
- Independent read-only functional review focuses on mesh/alpha/masking/lifetime, actual settings, locale/layout and unchanged action semantics.
- Save scene and restore normal Editor options, input/save hooks and ordinary save. Export a clean actual-window clip with audio, inspect frames and fully decode.

No retention, commercial store acceptance or headset comfort conclusion follows from Editor screenshots and automation.
