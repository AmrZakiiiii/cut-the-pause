# Public Beta Release Closure Design

## Objective

Close the remaining market-readiness gaps by publishing a reproducible unsigned Apple Silicon beta, connecting the marketing site to that release, deploying the static site publicly, committing all intended work, and leaving the repository clean.

## Approved external changes

- Change `AmrZakiiiii/cut-the-pause` from private to public.
- Push the reviewed `main` branch.
- Publish a `v0.1.0-beta` GitHub prerelease containing the workflow-produced ZIP and SHA-256 checksum.
- Enable and deploy public GitHub Pages for the static site.
- Keep checkout disabled because no payment-provider URL exists.

## Release workflow

The release workflow remains the source of the distributable artifact. Its restore step must include `--runtime osx-arm64`, and the publish step must use `--no-restore` so a clean runner proves that the explicit runtime restore supplied the publish assets. The test-matrix validator must fail if either contract regresses.

The workflow will continue to run the Release build, all three test projects, package construction, checksum creation, and package smoke test. A manually dispatched successful run will produce the exact ZIP and checksum later attached to the GitHub prerelease. Publishing the release may trigger the existing release event again; that run must also succeed and attach the same named assets.

## Website release configuration

`website/script.js` will set `releaseUrl` to the exact public prerelease page:

`https://github.com/AmrZakiiiii/cut-the-pause/releases/tag/v0.1.0-beta`

The checkout URL remains empty. Existing safe-link validation remains in place, so release CTAs become active while checkout remains inactive.

## GitHub repository landing page

The root `README.md` will become a product-oriented GitHub landing page rather than a developer-first inventory. It will reuse the fresh screenshots already checked into `website/assets/`, including the current app overview and real analyzed-video timeline, so visitors see the present UI and the expanded feature set. The page will lead with the user outcome, explain the review-before-export workflow, make the unsigned Apple Silicon beta and macOS requirements explicit, and provide prominent links to the public beta, website, installation guide, source build, licensing, and support information. Technical architecture and contributor details remain available below the product pitch.

## Static hosting

GitHub Pages will publish the existing dependency-free `website/` directory through a dedicated Actions workflow on changes to `main` and through manual dispatch. The workflow will use GitHub's Pages artifact and deployment actions with the minimum required permissions. Relative asset paths and fragment links already support the repository subpath URL.

GitHub Pages is chosen instead of converting the site to OpenAI Sites because the approved site is already a complete dependency-free static artifact, the public GitHub repository is required for anonymous release downloads, and a GitHub-native deployment avoids adding a second hosting system or framework.

## Repository hygiene and commit boundaries

Temporary `.playwright-cli/` output will be removed and ignored. Generated build, publish, release, and browser-QA artifacts will remain outside Git tracking. All intended source, workflow, packaging, documentation, licensing, website, and screenshot changes will be committed. The final acceptance condition is `git status --short` returning no output after push, release publication, and Pages deployment.

The design document is committed separately as required by the design workflow. The implementation and release closure will use a final market-readiness commit after verification.

## Implementation ownership

A fresh GPT-5.6 Luna High subagent will implement the workflow, validator, website configuration, Pages workflow, repository landing page, release notes, and ignore-file changes without committing or publishing. The primary orchestrator will review the diff, run independent verification, commit, push, change repository visibility, operate GitHub Actions, publish the release, enable Pages, and verify the public endpoints.

## Verification

Before the implementation commit:

- `dotnet build CutThePause.sln --configuration Release --no-restore`
- Core, Infrastructure, and App tests: 75 total passing
- `scripts/validate-test-matrix.sh`
- Runtime-specific restore followed by `dotnet publish --no-restore`
- Real unsigned ZIP creation and `scripts/test-macos-package.sh`
- Shell and JavaScript syntax checks
- Website desktop/mobile browser checks with all assets and no console errors
- `git diff --check`
- License byte-identity and Silero hash checks

After push:

- The manually dispatched Release workflow completes successfully.
- Its ZIP and checksum artifact download and pass the package smoke test.
- `v0.1.0-beta` is publicly reachable and contains both files.
- The release URL configured in the website returns successfully.
- The Pages workflow completes and the public site returns successfully with the configured download CTA.
- Checkout remains disabled.
- `main` matches `origin/main` and the worktree is clean.

## Failure handling

- Do not publish a release from locally generated artifacts when the GitHub workflow fails.
- Do not enable the website release CTA until the intended release URL is fixed in source and the release is created in the approved sequence.
- If GitHub Pages cannot be enabled after the repository becomes public, stop with the commit and release preserved and report the exact GitHub limitation; do not silently introduce another host.
- Do not add Apple signing, Developer ID, or notarization.
- Do not enable checkout without a real payment-provider URL.
