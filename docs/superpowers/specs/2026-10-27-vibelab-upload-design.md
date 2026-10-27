# Riftline VibeLab Upload Design

## Objective

Publish the current Riftline Unity project to the empty public GitHub repository
`https://github.com/yangel-yan/RiftLine`, then create a VibeLab cloud draft and
return its preview URL. This upload stops at `pico-release-cli-cn commit`; it
does not run `push`, submit build validation, or request review.

## VibeLab Metadata

- App name: `Riftline`
- Slogan: `Run the Rift. Recover the lost archives.`
- Category: `game`
- Tags: `XR`, `Runner`, `Sci-Fi`, `Action`
- Version: `0.1.0`, matching the Unity PlayerSettings bundle version
- AI tool: `Trae CLI`
- AI model: `GPT-5`
- Repository visibility: `public`
- Technology stack: `unity`

The app description will describe Riftline as a PICO XR science-fiction rail
runner built around lane changes, jumping, sliding, and phase traversal. It
will mention the 18 contracts, endless mode, bilingual interface, and comfort
settings already present in the project.

The release notes will identify this as the initial VibeLab preview and list
the same implemented content without claiming physical-device performance or
store certification.

## Presentation Assets

The VibeLab asset directory will contain:

- A square icon cropped from
  `Assets/Riftline/Art/Generated/MenuArtwork.png`, retaining the runner and
  rift gate.
- A landscape image based on
  `docs/engagement/screens/home-final.png`, showing the branded main menu.
- Three detail images from existing runtime captures, one for each authored
  environment region.

This combines polished project key art with evidence of the actual interface
and gameplay. Existing source images will be copied or cropped; no replacement
art will be generated.

## Repository Content

The Git repository will include the Unity source needed to open the project:
`Assets`, `Packages`, `ProjectSettings`, project documentation, VibeLab assets,
and `vibelab.yaml`.

Generated Unity caches, local settings, logs, IDE files, APKs, build outputs,
PICO CLI credentials/state, and other machine-local artifacts will be ignored.
Before the first source commit, tracked candidates will be checked for secrets
and GitHub's per-file size limit.

## Upload Flow

1. Initialize the local repository on `main` and connect the supplied GitHub
   remote without replacing any remote history.
2. Add the standard Unity and local-tool exclusions.
3. Create the VibeLab presentation assets and fill `vibelab.yaml`.
4. Run `pico-release-cli-cn add` until local configuration, assets, Git state,
   and repository checks pass.
5. Commit and push the prepared Unity source to GitHub.
6. Run `pico-release-cli-cn commit` to create the VibeLab cloud draft.
7. Verify the GitHub branch, VibeLab state, app ID, and preview URL.

## Failure Boundaries

- If remote history appears before the first push, stop and reconcile it rather
  than force-pushing.
- If GitHub authentication or file-size checks fail, preserve the local commits
  and report the exact blocker.
- If VibeLab validation rejects metadata or image dimensions, adjust only the
  rejected fields or derived presentation assets and rerun `add`.
- Do not run `pico-release-cli-cn push` in this task.
- Do not claim PICO device performance or certification from Editor evidence.

## Success Criteria

- GitHub `main` contains the prepared Unity project without caches, credentials,
  APKs, or build outputs.
- `pico-release-cli-cn add` succeeds.
- `pico-release-cli-cn commit` succeeds and returns a VibeLab app ID and preview
  URL.
- A fresh `state` query confirms the created draft without submitting it for
  review.
