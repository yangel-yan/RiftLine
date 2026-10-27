# Riftline Commercial Opening Final Re-review

## Verdict

- **Spec compliance (reviewed source scope): PASS.**
- **Code quality: PASS.**
- **Severity count:** 0 P0, 0 P1, 0 P2.
- **Prior findings:** 2/2 resolved.

No new functional, state-lifecycle, or action/copy divergence was found in the fix wave. Final experiential acceptance still depends on the separate live QA already in flight for regional/resource, real-audio, actual-window, and decoded-video evidence.

## Findings

No P0-P2 findings.

## Prior Finding Resolution

### Resolved: reduced-flash landing treatment

`Assets/Riftline/Scripts/Runtime/RiftlineOpeningDirector.cs:148-159,184-189`

- Landing contact now uses the live `ReducedFlash` value independently of `ReducedParticles`.
- Reduced mode changes width from `.055` to `.02` and maximum alpha from `.75` to `.18`.
- `ApplyLandingStyle` runs outside the geometry-refresh condition. During pause, geometry remains frozen while a settings change still restyles the existing landing renderer on the next presentation update.
- The landing renderer remains governed separately by `ReducedParticles`; no light or extra flash was introduced.

This satisfies the spec's requirement that reduced-flash and reduced-particle settings apply to new landing feedback.

### Resolved: extraction/archive completion payoff

`Assets/Riftline/Scripts/Core/RiftlineOpeningPresentation.cs:62-79,96-112,115-128`  
`Assets/Riftline/Scripts/UI/RiftlineUI.cs:773-831`

- `ArchiveRecovered` is set only by current-run `FeedbackKind.Archive`.
- It survives terminal `Extracted` evaluation, while reset, elapsed rollback, same-instance retry, contract replacement, and run replacement clear it.
- Extraction confirmation requires genuine non-Endless victory.
- `ARCHIVE SECURED` additionally requires this run's archive feedback. It is absent for failure, safe exit, Endless, prior-save ownership, and completion without archive feedback.

The result archive copy therefore reflects observed run truth rather than save-state inference.

## Result Actions And Copy

`Assets/Riftline/Scripts/UI/RiftlineUI.cs:823-888`

- The displayed next target, enabled `Next` button, primary accent, and Enter shortcut all use `TryGetNextContract`.
- That predicate requires a real victory, non-Endless result, in-range successor, and unlocked successor.
- Catalog contract IDs are contiguous and array-aligned (`0..17`), so `ContractId + 1` names and launches the same successor.
- Failure and safe-exit paths keep Retry primary. The retry promise is accurate: `StartRun` restores full health, while settlement/archive collection preserves earned chips and archives.

No copy/action mismatch or invalid next-run path was found.

## Large-Font HUD Review

`Assets/Riftline/Scripts/UI/RiftlineRunHud.cs:143-179,209-253`  
`Assets/Riftline/Scripts/UI/RiftlineUILocalization.cs:17-25,129-142`

The supplied green evidence reports `actualPreferredHeight <= rectHeight` for all nine English HUD labels at font scale `1.4`.

- Long remaining-distance text is hidden above `1.15`, but current distance remains in the main readout and route progress remains visible.
- The objective still communicates the next required action.
- Phase and objective-detail copy is shorter without losing instruction.
- Life-rule copy remains explicit about one-life damage and gap failure.
- Cue-card height scales from `94` to `131.6` at `1.4`; its position remains below the top HUD and separate from the feedback card.

This is consistent with the spec's readability requirement and does not remove required gameplay information. Chinese and in-headset visual confirmation remain part of live QA, not this static verdict.

## Evidence Reviewed

- `/tmp/riftline-commercial-final-review.md`
- `/tmp/riftline-commercial-review-fixes.md`
- `/tmp/riftline-commercial-final-current.diff`
- `/tmp/riftline-commercial-hud-font-red.txt`
- `/tmp/riftline-commercial-hud-font-green.txt`
- `docs/superpowers/specs/2026-10-26-commercial-opening-design.md`
- Current source context and supplied green outputs: Core `8/8`, focused assertions `15/15`, Task 4 `17/17`, and full `Assembly-CSharp` compile exit `0`.

No Unity control, source modification, or test rerun was performed.
