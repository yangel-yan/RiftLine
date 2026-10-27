# Riftline VibeLab Upload Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish the Riftline Unity source to `https://github.com/yangel-yan/RiftLine` and create a VibeLab cloud draft with a verified preview URL.

**Architecture:** Keep the Unity source repository independent from generated caches, local credentials, build outputs, and validation recordings. Derive VibeLab marketing assets from existing Riftline artwork and runtime captures, validate everything through the installed PICO release CLI, push `main`, then create a draft with `commit` while deliberately omitting the review-submitting `push` command.

**Tech Stack:** Unity 6000.6.7f1, Git/GitHub, macOS `sips`, YAML, `pico-cli` 0.2.2, `pico-release-cli-cn` 0.1.3

## Global Constraints

- Remote repository: `https://github.com/yangel-yan/RiftLine.git`, public, currently empty.
- VibeLab publication stops after `pico-release-cli-cn commit`; never run its `push` command in this task.
- App name is `Riftline`; version is `0.1.0`; AI tool is `Trae CLI`; AI model is `GPT-5`.
- App icon must be PNG/JPEG, exactly 1024x1024, and at most 4 MB.
- Landscape and detail images must be PNG/JPEG, exactly 2560x1440, and at most 5 MB each.
- Do not track Unity caches, local PICO state, credentials, APK/AAB files, build outputs, recordings, or machine-local IDE files.
- Do not claim physical-device performance, store certification, or review approval.

---

### Task 1: Establish Repository Hygiene

**Files:**
- Modify: `.gitignore`

**Interfaces:**
- Consumes: Empty GitHub repository connected as `origin` and the local `main` repository created during design approval.
- Produces: A committed ignore policy that every later `git add` command inherits.

- [ ] **Step 1: Replace the minimal ignore file with the Unity and local-tool policy**

Use `apply_patch` so `.gitignore` contains these entries:

```gitignore
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Ll]og/
UserSettings/
GeneratedAssets/

.pico-cli/
.trae/
.screenshots/
.vscode/
.idea/

*.csproj
*.sln
*.slnx
*.user
*.pidb
*.booproj
*.svd
*.apk
*.aab
*.keystore
*.jks
*.mp4
*.mov
*.log

.DS_Store
cli.save
文档读取授权.png
docs/**/harness-diagnostics/
docs/validation/
```

- [ ] **Step 2: Verify known local artifacts are ignored**

Run:

```bash
git check-ignore -v Library Temp Logs Builds GeneratedAssets .pico-cli .trae 1.0.0.apk docs/engagement/editor-playthrough.mp4
```

Expected: every path prints a matching `.gitignore` rule.

- [ ] **Step 3: Check the ignore change for whitespace errors**

Run:

```bash
git diff --check -- .gitignore
```

Expected: exit code 0 and no output.

- [ ] **Step 4: Commit repository hygiene**

Run:

```bash
git add .gitignore
git commit -m "chore: add Unity repository hygiene"
```

Expected: one commit that changes only `.gitignore`.

### Task 2: Commit the Unity Source Safely

**Files:**
- Add: `Assets/**`
- Add: `Packages/**`
- Add: `ProjectSettings/**`
- Add: project Markdown and JSON documentation not excluded by Task 1
- Exclude: all paths covered by `.gitignore`

**Interfaces:**
- Consumes: The ignore policy from Task 1.
- Produces: A source commit that VibeLab can build from GitHub.

- [ ] **Step 1: Stage the project source**

Run:

```bash
git add Assets Packages ProjectSettings docs
```

Expected: source and documentation are staged; caches, APKs, recordings, and local tool state remain absent.

- [ ] **Step 2: Audit staged paths for forbidden artifacts**

Run:

```bash
git diff --cached --name-only | rg '(^|/)(Library|Temp|Logs|Builds|GeneratedAssets|UserSettings|\.pico-cli|\.trae)(/|$)|\.(apk|aab|keystore|jks|mp4|mov|log)$'
```

Expected: exit code 1 and no matches.

- [ ] **Step 3: Audit staged files for GitHub's size limit**

Run:

```bash
git diff --cached --name-only -z | while IFS= read -r -d '' tracked_path; do tracked_size=$(stat -f%z "$tracked_path"); if [ "$tracked_size" -ge 94371840 ]; then printf '%s %s\n' "$tracked_size" "$tracked_path"; fi; done
```

Expected: no file at or above 90 MiB is printed.

- [ ] **Step 4: Scan staged text for credential material**

Run:

```bash
git diff --cached --name-only -z | xargs -0 rg -n -I '(BEGIN (RSA|OPENSSH|EC) PRIVATE KEY|ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|AKIA[0-9A-Z]{16}|secret_access_key|client_secret\s*[:=])'
```

Expected: exit code 1 and no credential matches.

- [ ] **Step 5: Review and commit the source snapshot**

Run:

```bash
git diff --cached --check
git diff --cached --stat
git commit -m "feat: add Riftline Unity project"
```

Expected: whitespace check exits 0 and the commit contains Unity source plus retained project documentation.

### Task 3: Create VibeLab Assets and Configuration

**Files:**
- Create: `assets/icon.png`
- Create: `assets/landscape.png`
- Create: `assets/detail-region-0.png`
- Create: `assets/detail-region-1.png`
- Create: `assets/detail-region-2.png`
- Modify: `vibelab.yaml`

**Interfaces:**
- Consumes: Existing `MenuArtwork.png`, `home-final.png`, and three region screenshots.
- Produces: CLI-valid release metadata and five deterministic presentation images.

- [ ] **Step 1: Generate the icon and 16:9 presentation assets**

Run:

```bash
mkdir -p assets
sips --resampleHeightWidth 1024 1024 Assets/Riftline/Art/Generated/MenuArtwork.png --out assets/icon.png
sips --cropToHeightWidth 1716 3050 docs/engagement/screens/home-final.png --out /tmp/riftline-vibelab-landscape-crop.png
sips --resampleHeightWidth 1440 2560 /tmp/riftline-vibelab-landscape-crop.png --out assets/landscape.png
sips --cropToHeightWidth 793 1410 docs/quality/screens/region-0-1.png --out /tmp/riftline-vibelab-region-0-crop.png
sips --resampleHeightWidth 1440 2560 /tmp/riftline-vibelab-region-0-crop.png --out assets/detail-region-0.png
sips --cropToHeightWidth 793 1410 docs/quality/screens/region-1-1.png --out /tmp/riftline-vibelab-region-1-crop.png
sips --resampleHeightWidth 1440 2560 /tmp/riftline-vibelab-region-1-crop.png --out assets/detail-region-1.png
sips --cropToHeightWidth 793 1410 docs/quality/screens/region-2-1.png --out /tmp/riftline-vibelab-region-2-crop.png
sips --resampleHeightWidth 1440 2560 /tmp/riftline-vibelab-region-2-crop.png --out assets/detail-region-2.png
```

Expected: `assets/icon.png` is 1024x1024; every other generated image is 2560x1440.

- [ ] **Step 2: Write the approved VibeLab configuration**

Use `apply_patch` to make `vibelab.yaml` exactly:

```yaml
app:
  name: Riftline
  slogan: Run the Rift. Recover the lost archives.
  description: >-
    Riftline is a PICO XR science-fiction rail runner. Switch lanes, jump,
    slide, and phase through hazards while recovering lost archives across
    18 contracts, three regions, and an endless mode. The project includes a
    bilingual Chinese and English interface, controller and hand input, and
    configurable comfort and accessibility options.
  tags:
    - XR
    - Runner
    - Sci-Fi
    - Action
  category: game
  icon: assets/icon.png
  landscape: assets/landscape.png
  detail_images:
    - assets/detail-region-0.png
    - assets/detail-region-1.png
    - assets/detail-region-2.png
  videos: []
version:
  name: 0.1.0
  notes: >-
    Initial VibeLab preview with 18 contracts, three authored regions,
    branching archive routes, an endless mode, bilingual menus, controller
    and hand input, and comfort settings. This draft records Editor-validated
    project content and does not claim physical-device performance or store
    certification.
build:
  repo_url: https://github.com/yangel-yan/RiftLine
  repo_visibility: public
  tech_stack: unity
  ai_tool: Trae CLI
  ai_model: GPT-5
  features:
    - hand_tracking
    - controller_support
  teammates: []
```

- [ ] **Step 3: Verify image dimensions, sizes, and configuration**

Run:

```bash
for image_path in assets/*.png; do sips -g pixelWidth -g pixelHeight "$image_path"; stat -f '%z %N' "$image_path"; done
/Users/bytedance/.local/bin/pico-cli tool exec pico-release-cli-cn -- --json add
```

Expected: icon reports 1024x1024 and less than 4194304 bytes; all other images report 2560x1440 and less than 5242880 bytes; CLI JSON reports `"ok":true` with only the expected dirty-tree warning.

- [ ] **Step 4: Commit the release configuration and assets**

Run:

```bash
git add assets vibelab.yaml
git commit -m "chore: configure VibeLab draft"
```

Expected: a commit containing only the five presentation images and `vibelab.yaml`.

### Task 4: Validate and Publish GitHub Main

**Files:**
- Verify: committed repository state only

**Interfaces:**
- Consumes: The source and release commits from Tasks 1-3.
- Produces: A public GitHub `main` branch at the exact local HEAD used by VibeLab.

- [ ] **Step 1: Run final local release validation on a clean tree**

Run:

```bash
git status --short
/Users/bytedance/.local/bin/pico-cli tool exec pico-release-cli-cn -- --json add
```

Expected: `git status --short` is empty and release validation reports `"ok":true` without a dirty-tree warning.

- [ ] **Step 2: Reconfirm that the remote is still empty**

Run:

```bash
git ls-remote --heads origin
```

Expected: no branch refs. If a ref appears, stop without force-pushing.

- [ ] **Step 3: Push the source branch**

Run:

```bash
git push -u origin main
```

Expected: GitHub accepts `main` and configures local upstream tracking.

- [ ] **Step 4: Verify remote and local commits match**

Run:

```bash
git fetch origin main
git rev-parse HEAD
git rev-parse origin/main
git status --short --branch
```

Expected: both hashes are identical and status reports `main...origin/main` with no divergence or working-tree changes.

### Task 5: Create and Verify the VibeLab Draft

**Files:**
- Read/write local CLI state under ignored `.pico-cli/`
- No project source changes expected

**Interfaces:**
- Consumes: Authenticated PICO account with `pico_vibe_lab` scope and a GitHub `main` branch matching local HEAD.
- Produces: VibeLab app ID, version ID, draft state, and preview URL.

- [ ] **Step 1: Create the cloud draft**

Run:

```bash
/Users/bytedance/.local/bin/pico-cli tool exec pico-release-cli-cn -- --json commit
```

Expected: JSON reports `"ok":true`, mode `app_create`, an app ID, a version ID, and a preview URL.

- [ ] **Step 2: Query the created draft from fresh remote state**

Run:

```bash
/Users/bytedance/.local/bin/pico-cli tool exec pico-release-cli-cn -- --json state
```

Expected: JSON identifies the editing version as a draft and recommends preview confirmation before any future review submission.

- [ ] **Step 3: Verify no review submission occurred**

Run:

```bash
git status --short --branch
git rev-parse HEAD
git ls-remote origin refs/heads/main
```

Expected: repository remains clean and local/remote commit hashes match. The VibeLab state query still identifies the editing version as a draft rather than a review or build-validation state.

- [ ] **Step 4: Report the handoff**

Report the GitHub URL, exact commit SHA, VibeLab app ID, version ID, preview URL, validation result, and the explicit fact that review submission was not performed.
