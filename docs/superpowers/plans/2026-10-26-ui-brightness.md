# UI and Brightness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Main agent owns Unity and serialized assets; independent source owners may work concurrently under disjoint scope.

**Goal:** Replace demo-like UI surfaces/hierarchy and brighten Riftline's scene with verified, readable actual gameplay.

**Architecture:** An Image-compatible vector surface supplies consistent UI material styling; the existing runtime UI retains controls/state while improving composition. Runtime environment and idempotent scene-builder refresh share the brighter lighting treatment.

**Tech Stack:** Unity 6000.6.7f1, C#, uGUI, URP Volume, existing generated assets, Unity Pipeline.

## Global constraints

- No Git repository; edit the authorized shared workspace with original snapshots under `/tmp/riftline-ui-brightness-original`.
- Preserve button names, production actions, health/timings, progress/save and XR tracked pose/FOV.
- Follow existing legacy Text/system CJK font setup; no TMP migration or package changes.
- Font .8–1.4, Chinese/English, reduced-flash/particle settings, ScrollRect and input selection semantics.
- Only main agent may run Unity commands, refresh assets, mutate scene/profile/material or record.
- Final evidence `docs/ui-brightness/`; intermediates `/tmp`. Ordinary save untouched. No hardware/retention/store claim.

## Task 1 — Shared visual surfaces

**Files:** Create `Assets/Riftline/Scripts/UI/RiftlineUISurface.cs`; modify `RiftlineUIElements.cs`.

**Interface:** `public sealed class RiftlineUISurface : UnityEngine.UI.Image` with `Configure(float radius, float borderWidth, Color borderColor, bool shadow)` or an equivalently explicit style method. Existing Panel/Button helper signatures return Image/Button unchanged.

- [x] Capture current flat UI and exact helper behavior before edits.
- [x] Implement finite rounded fan/border geometry with cached corner points, vertical shading, optional soft shadow; guard zero-size rects and clamp corner radius.
- [x] Use shared UI/Default material/stencil support; preserve inherited color, masking and raycast behavior. No textures/materials created per surface.
- [x] Theme: Navy Color32(31,46,68,255), NavyRaised(46,64,88,255), White(245,249,255,255), Muted(178,199,221,255), Cyan(94,222,241,255), CyanDim(47,111,140,255), Amber(255,203,112,255).
- [x] Panel uses rounded surfaces without turning thin lines/meters into oversized corners. Button has distinct normal/hover/pressed/selected/disabled colors, label padding, and same callback/name/layout behavior.
- [x] Run compile/geometry/masking checks through main agent and independent task review.

## Task 2 — Brighter world lighting

**Files:** Modify `RiftlineEnvironment.cs`, `RiftlineSceneBuilder.cs`, `RiftlineWorld.cs` (fog/region steel values only), `RiftlineRailArchitecture.cs` (metal palette only).

**Interface:** Public static `RiftlineSceneBuilder.RefreshPresentationLighting()` idempotently updates existing named lights, sky material and PresentationVolume without rebuilding a scene. Main agent invokes it after compile. BuildPostProcessing configures existing/new profile consistently.

- [x] Save baseline light/camera/Volume/ambient/sky information. Verify URP HDR, Base camera postprocess, profile/overrides and layer mask.
- [x] Correct active pipeline HDR: live Quality override is Mobile_RPAsset with supportsHDR=false, while default PC asset is true. Refresh enables HDR on actual active URP plus default if distinct; preserve .8 renderScale, MSAA and other budgets. Main agent saves the active asset.
- [x] Apply initial key/fill/rim values from spec; reuse existing named scene lights and local poses.
- [x] Update runtime regional ambient and ground bounce; sky .9→1.05, lighter fog/.0045 density, brighter structural steel.
- [x] Neutral tonemapping, exposure .45, contrast 4, saturation 6; bloom .18. Add missing Volume components once and persist sub-assets through existing builder patterns.
- [x] Use the public refresh for serialized state; repeated refresh must not duplicate components, lights or assets.
- [x] Verify regional changes retain correct generated skies/reflections/textures/LODs, measure same-pose luminance, and independently review this source scope.

## Task 3 — Page and HUD composition

**Files:** Modify `RiftlineUI.cs` and `RiftlineRunHud.cs`; focused partial UI file allowed if useful.

- [x] Main/inner panels use consistent rounded steel-blue surfaces, restrained accent decoration and spacing.
- [x] Home retains large generated artwork, a clean title/mission hierarchy, a strong warm-gold Run action and lighter utility navigation.
- [x] Results show separate score/distance/chip stat cards; retain localized true extraction/archive confirmation, advice and Next/Retry decisions.
- [x] HUD/cue/feedback/subtitle plates use lighter translucent theme; preserve all life/charge/progress/binding information and sensible font-1.4 bounds.
- [x] Inspect every secondary page and pause, scrolling/masking, both locales and real raycasts.

## Task 4 — Integration and evidence

- [x] Unity compile; mesh/component/render checks; focused live page/large-font/hover/scroll/retry tests in an isolated save.
- [x] Run normal-time first contract using existing external actual keyboard/mouse witness; verify real damage, phase, archive/extraction, next and failure retry.
- [x] Existing Core `dotnet run --project /tmp/riftline-engagement-tests/Engagement.csproj -- all` once after functional integration. UI styling verified by actual render/input tests rather than source-mirroring assertions.
- [x] Independent final read-only source review; fix material logical/render-lifecycle findings.
- [x] Capture same-pose before/after, three-region views and clean actual-window clip with real audio; inspect decoded frames and fully decode video/audio.
- [x] Save scene, stop Play, restore Stats/options/hooks/devices and compare ordinary save hashes. Write `docs/UI-BRIGHTNESS-UPGRADE.md`.

## Execution contracts

Working commands: `unity command editor status`, `unity command script run --file /tmp/name.cs --entry Type.Method`, `unity command editor.playmode start/stop`. AutoRefresh off; `/tmp/riftline-quality-refresh.py` privately invokes Assets/Refresh. Do not print descriptor eval tokens.

External live scripts must avoid EditorSettings/AssetDatabase references; configure those in a separate Edit Mode bootstrap. Capture screenshots and UI transitions at least two actual rendered frames apart. ForceUpdateCanvases and verify raycast before clicking. Activate macOS Unity explicitly. Restore InputSystem devices and normal options at completion.

Spec/plan self-review: disjoint source ownership and idempotent serialized refresh established; no placeholders, new gameplay rules or framework migrations. Continue under existing authorization.
