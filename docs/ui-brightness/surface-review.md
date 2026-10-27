# Rounded Image Surface Review

## Verdicts

- **Spec compliance: PASS, source-level.** The required API, palette, rounded fill/border/shadow geometry, inherited `Image` masking/raycast behavior, button API, callbacks, names, and layout behavior are preserved in the reviewed diff.
- **Quality: PASS WITH MINOR CONCERN.** No blocking correctness, lifecycle, or per-rebuild managed-allocation issue was found. One low-severity mesh-efficiency issue remains.
- **Severity summary:** 0 critical, 0 high, 0 medium, 1 low.

## Finding

### LOW: Thin square surfaces still emit a mostly degenerate 28-triangle fan

When the short axis is below 8, radius, border, and shadow are disabled, but `PopulatePerimeter` still writes seven identical points at each square corner. `AddFan` then emits 28 triangles, 24 of which have zero area. This should render correctly, but thin rules, meters, and damage-edge surfaces pay avoidable vertex/triangle processing. A dedicated four-corner quad path would remove the degenerate geometry.

References:

- `Assets/Riftline/Scripts/UI/RiftlineUISurface.cs:58-64`
- `Assets/Riftline/Scripts/UI/RiftlineUISurface.cs:102-143`
- `Assets/Riftline/Scripts/UI/RiftlineUISurface.cs:147-163`
- Concrete thin-surface callers: `Assets/Riftline/Scripts/UI/RiftlineUI.cs:349-350`, `Assets/Riftline/Scripts/UI/RiftlineUI.cs:489-490`, `Assets/Riftline/Scripts/UI/RiftlineRunHud.cs:123-125`

## Source-Level Checks

- **Geometry:** Rect dimensions are checked before generation; radius and border width are bounded by the short axis; inset geometry remains positive. The perimeter order and fan/ring index order are consistently clockwise. Rect changes rebuild from `GetPixelAdjustedRect()` rather than retained transformed points.
- **Image semantics:** `RiftlineUISurface` remains an `Image`, does not replace its material, and does not override masking, culling, or raycast methods. Fill vertices use inherited `Image.color`; configured border and shadow alpha follow its alpha. A zero `Image.color.a` clears the mesh. The explicit border color remaining independent of fill RGB is consistent with the required `Configure(..., borderColor, ...)` API.
- **Runtime lifecycle:** Four fixed 28-point arrays are allocated once per component and the corner unit points are static. `OnPopulateMesh` contains no managed collection/array allocation. No material, texture, sprite, event subscription, or disposable native resource is created.
- **Buttons/API:** `Panel(...)` still returns `Image`; `Button(...)` still returns `Button` with the same signature, names, callbacks, height settings, and creation notification. The derived component remains discoverable as `Image`. Normal, highlighted, pressed, selected, and disabled `ColorBlock` values are distinct. `Button` renderer tint multiplies the procedural vertex colors, so transitions affect fill, border, and shadow without replacing the base accent.
- **Scope preservation:** The diff changes only palette, shared surface creation/configuration, button tint values, and label padding. No production actions, progress/save, health/timing, XR pose/FOV, text/font, scrolling, or input-selection code is changed.

## Cannot Verify Without Main's Live Checks

- Unity compilation against the installed Unity/uGUI version.
- Actual stencil `Mask` and `RectMask2D` clipping, including shadow geometry outside the surface rect.
- Pointer raycasts and keyboard/controller selection during all button transitions.
- Transparent-to-visible rebuild behavior and zero-size/layout resize behavior in a live Canvas.
- Visual winding/culling, border continuity, canvas-scale appearance, and whether thin HUD/replay/damage surfaces remain subtle.
- Runtime profiling with representative contract/outfit rows and HUD surface counts.
