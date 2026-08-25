# Cut The Pause Market Readiness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Cut The Pause beta credible to market by completing five independently reviewable release, documentation, licensing, and website requirements.

**Architecture:** Keep the desktop application behavior unchanged. Strengthen repository automation and release artifacts around the existing .NET/Avalonia application, place durable release/legal documentation under `docs/`, and keep the marketing site as a dependency-light static asset that can be hosted for free.

**Tech Stack:** .NET 8, Avalonia, xUnit, GitHub Actions, Bash, static HTML/CSS/JavaScript.

**Spec:** User-provided five-row requirements table in the 2026-08-25 task.

## Global Constraints

- All five requirements are P0 and must pass review before the goal is complete.
- Preserve application behavior except where a regression test proves a required correction.
- Do not add Apple Developer ID signing or notarization and do not imply either exists.
- The macOS beta target is an unsigned Apple Silicon `osx-arm64` ZIP.
- Do not silently change the existing MIT license.
- The marketing deliverable is a lightweight static website, not a web application, and must remain free to host.
- Do not publish, push, create a release, or connect a live checkout/payment provider.

---

### Task 1: Solution and CI test reliability

**Files:**
- Modify: `CutThePause.sln`
- Modify/Create: `.github/workflows/*.yml`
- Test: `tests/CutThePause.Core.Tests/CutThePause.Core.Tests.csproj`
- Test: `tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj`
- Test: `tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj`

**Interfaces:**
- Consumes: existing .NET solution and three xUnit projects.
- Produces: a solution and CI workflow that restore/build and invoke all three test projects explicitly.

- [ ] Inspect solution membership, workflow triggers, SDK version, and current test discovery.
- [ ] Add regression coverage for any discovered omission without changing application behavior.
- [ ] Make Core, Infrastructure, and App test execution explicit in GitHub Actions.
- [ ] Run restore, build, and all three test projects; record exact results.

### Task 2: Unsigned Apple Silicon packaging

**Files:**
- Modify: `scripts/package-macos-app.sh`
- Modify: `packaging/macos/Info.plist`
- Modify/Create: `.github/workflows/*.yml`
- Create/Modify: packaging smoke-test scripts and release support files as required.

**Interfaces:**
- Consumes: published `osx-arm64` app, FFmpeg/FFprobe, Silero model, MIT license, and third-party notices.
- Produces: an unsigned macOS `.app` ZIP plus SHA-256 checksum containing binaries, model, licenses/notices, install guide, and version metadata.

- [ ] Define and test the required artifact layout.
- [ ] Package the app and all named runtime/support assets without signing or notarization.
- [ ] Generate deterministic version metadata and a SHA-256 checksum.
- [ ] Add artifact smoke tests that fail for missing files, wrong architecture/layout, or invalid checksum.
- [ ] Run shell validation and the smoke tests; record exact results and any offline limitations.

### Task 3: Beta installation and source-build documentation

**Files:**
- Modify: `README.md`
- Create/Modify: `docs/INSTALL_MACOS.md`
- Create/Modify: source-build documentation where repository conventions place it.

**Interfaces:**
- Consumes: the final unsigned ZIP/app layout from Task 2.
- Produces: truthful install, requirements, warning, troubleshooting, and source-build instructions.

- [ ] Document Apple Silicon and supported macOS requirements.
- [ ] Document ZIP installation and Finder Right-click → Open for the unsigned first launch.
- [ ] State known Gatekeeper warnings without claiming signing or notarization.
- [ ] Add troubleshooting for permissions/quarantine, FFmpeg/FFprobe, model, launch, and logs where supported by the app.
- [ ] Add reproducible source-build, test, run, and packaging commands.
- [ ] Validate every referenced path and command against the repository.

### Task 4: Commercial licensing posture

**Files:**
- Preserve: `LICENSE`
- Modify: `THIRD_PARTY_NOTICES.md`
- Create: `docs/COMMERCIAL_LICENSING.md`

**Interfaces:**
- Consumes: current MIT license, bundled/runtime FFmpeg posture, and Silero model/code notices.
- Produces: commercial-facing documentation of the open-source/supporter-funded beta and redistribution obligations.

- [ ] Verify the existing license and third-party notice claims against repository dependencies and packaging.
- [ ] Explain permissive MIT commercial use and redistribution conditions without offering legal advice.
- [ ] Explain the supporter-funded beta posture and distinguish support/payment from proprietary licensing.
- [ ] Document FFmpeg license/build-configuration implications and Silero code/model attribution obligations.
- [ ] Preserve `LICENSE` byte-for-byte and add actionable distributor checks/links.

### Task 5: Free static marketing website

**Files:**
- Create/Modify: a repository-appropriate static site directory and its assets.
- Modify: `README.md` only if needed to document local preview/deployment.

**Interfaces:**
- Consumes: repository branding/screenshots and truthful release/install/licensing claims.
- Produces: a responsive, accessible static marketing site suitable for free static hosting.

- [ ] Build a homepage with clear positioning, before/after demo, and download CTA.
- [ ] Include Apple Silicon compatibility and unsigned-install guidance.
- [ ] Include privacy, FAQ, support, changelog, terms, refund policy, and a clearly non-live hosted-checkout placeholder.
- [ ] Keep the site dependency-light, responsive, keyboard-accessible, and usable without a backend.
- [ ] Validate internal links, static serving, responsive layout, and absence of false signing/notarization/payment claims.

### Task 6: Integrated acceptance

**Files:**
- Review: all files changed by Tasks 1–5.

**Interfaces:**
- Consumes: the five reviewed task outputs.
- Produces: an acceptance ledger and verified combined repository state.

- [ ] Review the combined diff for cross-task contradictions and unintended changes.
- [ ] Run the full .NET build/test suite, packaging validations feasible locally, documentation/link checks, and static-site checks.
- [ ] Confirm `LICENSE` is unchanged and no signing/notarization or live-payment claim was introduced.
- [ ] Record final acceptance or route concrete findings back to the responsible implementer.
