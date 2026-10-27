# Riftline Commercial Review Fixes

## Scope

Production changes are limited to:

- `Assets/Riftline/Scripts/Core/RiftlineOpeningPresentation.cs`
- `Assets/Riftline/Scripts/Runtime/RiftlineOpeningDirector.cs`
- `Assets/Riftline/Scripts/UI/RiftlineUI.cs`
- `Assets/Riftline/Scripts/UI/RiftlineUILocalization.cs`

`RiftlineRunHud.cs` required no change. Tests and evidence are under `/tmp`.
No Unity control or Git commands were used. Audio, Game, World, and
RailArchitecture source were not modified.

## Changes

- `ReducedFlash` now restyles landing contact independently of
  `ReducedParticles`: normal width/alpha are `.055`/`.75`; reduced values are
  `.02`/`.18`. Styling runs every presentation call, outside the geometry
  refresh gate, so a paused settings change applies while geometry is frozen.
  No light or additional flash treatment was added.
- `OpeningFrame.ArchiveRecovered` is a public getter with an internal setter,
  backed only by current-run `FeedbackKind.Archive`. Existing reset, elapsed
  rollback, retry, and run-replacement paths clear it.
- Genuine non-Endless victory results show localized extraction confirmation.
  Archive confirmation additionally requires the current frame's archive flag,
  so it is absent for failures, safe exits, Endless, prior saves, and runs with
  no Archive feedback.
- Eligible victory results name the localized unlocked next contract in a
  dedicated `.33-.42` band below confirmation. The stable `Next` button name
  and action behavior are unchanged.
- Retry copy now says `满血重试 · 晶片与档案保留` /
  `Retry with full life. Chips and archives retained.`
- The drone base lateral offset is `4.9`, beyond the `3.9` playable boundary
  and clear of the `5.65` dock pillar. Phase (`+1.1`) and combo (`+.5`)
  outward reactions remain.

## Red Evidence

The linked-core probe failed to compile before the implementation because
`OpeningFrame.ArchiveRecovered` did not exist:

```sh
dotnet run --project /tmp/riftline-commercial-tests/CommercialOpening.csproj
```

Result: exit `1`, four `CS1061` errors. Output:
`/tmp/riftline-commercial-review-core-red.txt`.

The initial review probe failed all 10 original assertions:

```sh
bash /tmp/riftline-commercial-review-probe.sh /Users/bytedance/Desktop/Riftline
```

Result: exit `1`. Output:
`/tmp/riftline-commercial-review-source-red.txt`.

After adding the requested visual assertion, the same probe failed only the
`4.9` drone-offset assertion. Output:
`/tmp/riftline-commercial-review-visual-red.txt`.

After adding the result-plan assertions, it failed only the four missing
next-target/corrected-retry assertions. Output:
`/tmp/riftline-commercial-review-ui-red.txt`.

## Green Verification

Linked production Core behavior:

```sh
dotnet run --project /tmp/riftline-commercial-tests/CommercialOpening.csproj
```

Result: PASS `8/8`, including archive survival through terminal `Extracted`
and reset on same-instance retry and replacement. Output:
`/tmp/riftline-commercial-review-core-green.txt`.

Focused review assertions:

```sh
bash /tmp/riftline-commercial-review-probe.sh /Users/bytedance/Desktop/Riftline
```

Result: PASS `15/15`. Output:
`/tmp/riftline-commercial-review-source-green.txt`.

Prior Task 4 regressions:

```sh
bash /tmp/riftline-commercial-task-4-probe.sh Assets/Riftline/Scripts/UI
```

Result: PASS `17/17`. Output:
`/tmp/riftline-commercial-review-task4-green.txt`.

Full `Assembly-CSharp` compile using existing Unity-generated references,
without invoking Unity:

```sh
mkdir -p /tmp/riftline-commercial-review-build
/usr/local/share/dotnet/dotnet /Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll @Library/Bee/artifacts/1300b0aE.dag/Assembly-CSharp.rsp -out:/tmp/riftline-commercial-review-build/Assembly-CSharp.dll -refout:/tmp/riftline-commercial-review-build/Assembly-CSharp.ref.dll -pdb:/tmp/riftline-commercial-review-build/Assembly-CSharp.pdb
```

Result: exit `0`, no diagnostics. Output:
`/tmp/riftline-commercial-review-compile.txt`.

Preserved/current visual constants were checked with:

```sh
rg -n "new Vector3\(2f, 1\.6f, 1f\)|Mathf\.Lerp\(11f, 4f|new Vector3\(4\.9f|3\.05f|-150f" Assets/Riftline/Scripts/Runtime/RiftlineOpeningDirector.cs
rg -n "side \* 6\.15f, \.3f|new Color\(\.3f, \.4f, \.5f\)|new Color\(\.35f, \.4f, \.36f\)" Assets/Riftline/Scripts/Runtime/RiftlineRailArchitecture.cs
```

The drone fit height `1.6`, look-ahead `11..4`, height `3.05`, yaw `-150`,
rail `.3`, and brightened metal colors remain in source.

## Handoff

Main retains live bounds, turn, and font-scale layout validation. The audio
root cause was confirmed externally as stalled DSP; `ResetOutput` restored
actual sample positions, so no Audio source fix was made.
