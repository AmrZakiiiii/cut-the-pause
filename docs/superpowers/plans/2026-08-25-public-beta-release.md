# Public Beta Release Implementation Plan

> Execute through subagent-driven development. The implementer changes repository files only; the primary orchestrator owns review, commits, and all GitHub mutations.

**Goal:** Close the public beta release gaps, turn the GitHub README into an attractive product landing page using current screenshots, publish the static website, and leave `main` clean and synchronized.

**Constraints:** Unsigned Apple Silicon only; no Developer ID or notarization; checkout remains disabled; use the existing static site and current screenshots; do not commit generated artifacts.

## Task 1: Make the release workflow reproducible

**Files:** `.github/workflows/release.yml`, `scripts/validate-test-matrix.sh`

- Add `--runtime osx-arm64` to the solution restore.
- Add `--no-restore` to the app publish.
- Ensure the release attachment step has only the permission it requires.
- Extend the validator so it fails if either runtime restore or no-restore publish regresses, while retaining explicit Core, Infrastructure, and App test checks.
- Run the validator and relevant syntax checks.

## Task 2: Connect and deploy the static website

**Files:** `website/script.js`, `.github/workflows/pages.yml`

- Set the release URL to `https://github.com/AmrZakiiiii/cut-the-pause/releases/tag/v0.1.0-beta`.
- Keep `checkoutUrl` empty.
- Add a GitHub Pages workflow that uploads only `website/`, deploys through the official Pages actions, uses minimum permissions, supports manual dispatch, and deploys relevant `main` changes.
- Validate JavaScript and workflow structure.

## Task 3: Upgrade the GitHub repository landing page

**Files:** `README.md`, existing assets under `website/assets/`

- Rewrite the README as a polished, concise product landing page for creators.
- Embed current screenshots from `website/assets/`, especially the updated app overview and analyzed-video timeline/review views; do not present old UI as current.
- Highlight the expanded feature set: local analysis, timeline review, editable cuts, detection controls, export choices/progress/cancellation, bundled dependencies, and privacy.
- Add clear beta download, website, install, source-build, support, license, and third-party notices links.
- State Apple Silicon/macOS requirements and unsigned/not-notarized first-launch instructions accurately.
- Retain compact developer setup, repository layout, testing, and commercial/licensing posture.

## Task 4: Add public beta release notes and repository hygiene

**Files:** `docs/releases/v0.1.0-beta.md`, `.gitignore`, `.playwright-cli/`

- Add release notes describing the user-facing beta, fresh UI/features, supported platform, unsigned install warning, checksum, and documentation.
- Ignore `.playwright-cli/` and remove its temporary QA output from the intended commit.
- Do not add build, publish, ZIP, checksum, or browser-generated artifacts.

## Task 5: Orchestrator verification, commit, and publication

- Review the full diff for scope and correctness.
- Run runtime restore, Release build, all three test projects, validator, runtime publish with `--no-restore`, package creation, package smoke test, shell/JavaScript syntax, website browser checks, license/model integrity, and `git diff --check`.
- Commit all intended market-readiness work and push `main`.
- Make the approved repository public.
- Enable GitHub Pages and verify its workflow and public URL.
- Dispatch and verify the release workflow, download and smoke-test its artifact, then publish `v0.1.0-beta` with the workflow ZIP/checksum and verify the release-triggered workflow.
- Confirm the release URL and website CTA work publicly, checkout remains disabled, `main` equals `origin/main`, and `git status --short` is empty.
