# Riftline UI and Brightness Final Review

## Verdict

**Ready for final integration with two Minor follow-ups.**

No Critical or Important findings were found. The implementation matches the UI/brightness plan and design intent without changing gameplay, save/progression, production button actions, XR tracked-pose/FOV, or camera/navigation contracts in the reviewed change.

The 12 SHA-256 hashes in `/tmp/riftline-brightness-full-diff.md` match the current workspace files. This review did not run Unity or rerun test suites. The recorded compile, 4,474-check live UI gate, 95-group Core run, lighting refresh, normal-time route, and luminance measurements are supplied integration evidence, not independently repeated results.

## Strengths

- `RiftlineUISurface` keeps the existing `Image`/`MaskableGraphic` material and stencil path, uses fixed per-component geometry arrays, creates no per-surface material or texture, guards non-finite/zero-sized rects, and follows inherited color/raycast behavior.
- Button names, callbacks, interactability, and result action selection remain intact. Home, secondary navigation, localized result confirmation/advice, Retry, Next, and Home retain their production actions.
- The runtime lighting path and serialized refresh agree on light, ambient, sky, fog, tone, bloom, and exposure values. Refresh reuses named lights, the existing Volume object, and existing profile component types.
- Active Mobile HDR is enabled while render scale `.8`, MSAA `1`, shadow distance `50`, light poses, ranges, cones, and shadow settings remain unchanged.
- Runtime regional sky/reflection selection, generated textures, road material copying, scenery LOD paths, and environment material destruction remain preserved.

## Issues

### Critical

None.

### Important

None.

### Minor

#### 1. The rounded hero mask includes the surface's shadow geometry

- **Files:** `Assets/Riftline/Scripts/UI/RiftlineUI.cs:438-443`; `Assets/Riftline/Scripts/UI/RiftlineUIElements.cs:56-61`; `Assets/Riftline/Scripts/UI/RiftlineUISurface.cs:66-78`
- **Evidence:** `Panel` enables shadow by default. The Home hero adds a stencil `Mask` to that same graphic with `showMaskGraphic=false`. `RiftlineUISurface` adds a four-unit expanded, three-unit downward shadow ring/fan with nonzero alpha. Unity's mask material writes stencil for alpha-clipped graphic geometry, so most of that shadow mesh becomes part of the child clipping region even though the mask graphic is hidden.
- **Why it matters:** The artwork can render a few pixels outside the intended rounded hero rect, weakening the exact rounded clipping promised by the surface/masking acceptance criterion. The affected area is bounded and does not affect input or page actions.
- **Fix:** Configure the hero mask surface with `shadow=false`, or use a separate shadow graphic behind a shadow-free rounded mask graphic.

#### 2. Thin surfaces rebuild 24 degenerate triangles per mesh

- **Files:** `Assets/Riftline/Scripts/UI/RiftlineUISurface.cs:58-64,117-163`; hot caller `Assets/Riftline/Scripts/UI/RiftlineRunHud.cs:293-301`
- **Evidence:** Thin surfaces set radius to zero but still generate seven coincident perimeter points per corner and a 28-triangle fan. Only four triangles have area. Four damage-edge surfaces receive changing alpha every frame during the `.65s` hit fade, so each affected frame rebuilds 112 triangles, 96 degenerate.
- **Why it matters:** This is avoidable Canvas mesh work during damage feedback. The count is small and bounded, so it is not a frame-budget blocker.
- **Fix:** Emit a four-vertex/two-triangle quad for thin surfaces.

## Readiness

**Ready: Yes.** The two findings are bounded presentation/performance polish issues. Neither breaks gameplay, save/progression, input actions, locale flow, lighting persistence, or XR contracts. They can be fixed before release polish without blocking the current recording/restoration completion.

Editor evidence supports only Editor behavior and pixel luminance. It does not establish headset performance, comfort, retention, or store acceptance.
