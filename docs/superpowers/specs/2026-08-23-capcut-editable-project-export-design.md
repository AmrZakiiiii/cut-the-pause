# CapCut Editable Project Export Design

**Date:** 2026-08-23

**Status:** Approved design

**Initial platform:** macOS only
**Supported editor profile:** CapCut Desktop 8.7

## Summary

Cut The Pause will add a native CapCut project export alongside its existing MP4/MOV render export. The generated project will reference the original source media without copying or transcoding it and will represent every retained range as a separate, adjacent clip on one editable CapCut timeline.

The feature creates and registers a complete local CapCut draft rather than exporting a standalone project file. CapCut does not officially support importing third-party editable project files, so direct, transactional installation into CapCut's local project library is the only practical desktop workflow.

This first release is deliberately limited to CapCut Desktop 8.7 on macOS. Premiere Pro and Final Cut Pro interchange are future work built on the editor-neutral timeline plan introduced here.

## Goals

- Create a CapCut project from the exact keep-ranges currently selected in the review screen.
- Produce one editable CapCut clip per retained range, in order, without gaps or overlaps.
- Preserve the source video's embedded audio, orientation, dimensions, and frame rate.
- Reference the original source file so project creation is fast and consumes negligible additional storage.
- Install the project into either the standard CapCut draft root or a configured external draft root.
- Preserve every existing CapCut project and recover cleanly from interrupted or failed installation.
- Keep the existing rendered MP4/MOV export unchanged.
- Establish an editor-neutral timeline model suitable for later FCPXML/Premiere interchange exporters.

## Non-goals

- Windows support.
- CapCut versions other than the verified 8.7 profile.
- Mobile `.capcut` packages, CapCut cloud projects, or cross-device packaging.
- Copying source media into the generated project.
- Adding transitions, effects, captions, B-roll, or generated media.
- Importing one CapCut draft into another existing CapCut project.
- Premiere Pro or Final Cut Pro export in this implementation.
- Repairing, rewriting, or migrating existing CapCut drafts.

## Research Findings

### Official product limitation

CapCut's official help documentation states that editable third-party project files and cross-draft project imports are not supported. An exported project is normally flattened to video. Consequently, this design creates a native local draft that appears in CapCut's project library rather than promising a file that CapCut can import through its UI.

Official reference: <https://www.capcut.com/help/import-a-previous-project-into-the-current-project>

### Local CapCut 8.7 evidence

Twenty-eight real macOS CapCut projects were inspected read-only from:

`/Volumes/AmrZaki EXT/C/CapCut Drafts`

Project `0608` is a direct example of the required output:

- The same MOV is split into 12 adjacent timeline segments.
- Each segment has an independent `source_timerange` and cumulative `target_timerange`.
- CapCut creates a separate timeline video material for each segment even though all materials point to the same physical file.
- Those duplicate timeline materials share one `local_material_id`.
- `draft_meta_info.json` registers the physical source path once.
- Time values use integer microseconds on a 30 fps frame grid.

The inspected 8.7 project layout uses:

- Root `draft_content.json`, `.bak`, and `template-2.tmp` timeline copies.
- `Timelines/project.json` and its backup.
- `Timelines/<timeline-id>/` timeline mirrors and attachment files.
- `draft_meta_info.json` media registration sidecar.
- The standard internal `root_meta_info.json` registry, whose project entries may point to an external `draft_root_path`.

The root and nested timeline documents in healthy drafts are byte-identical. Three identity domains are distinct:

1. Registry/draft metadata `draft_id`.
2. Timeline document ID, also used by `main_timeline_id` and the timeline folder name.
3. `Timelines/project.json` document ID.

The registry and metadata `draft_timeline_materials_size` values correspond to registered material storage, not timeline JSON byte length. For this single-source exporter, the value will be the source file size.

### Public reverse-engineering references

The local findings agree with these open-source projects:

- <https://github.com/renezander030/capcut-cli>
- <https://github.com/ilyassesalama/CapShare>
- <https://github.com/capcutfor1month-oss/capcut-mcp-full>

These are implementation references, not an official CapCut SDK or compatibility guarantee.

## Chosen Approach

Build a native CapCut draft with a bundled, sanitized, versioned 8.7 schema profile. At runtime, compare that profile with the newest healthy local 8.7 project when one exists. Refuse unsupported or structurally incompatible environments instead of guessing.

Rejected alternatives:

- **Clone an existing project:** risks retaining effects, personal paths, identifiers, and stale state and requires a suitable donor on every system.
- **Automate CapCut's UI:** requires Accessibility permissions, is slow, and is fragile across UI changes.
- **Export a standalone `.capcut` package:** the desktop app has no supported editable-project import flow for such a package.

## Architecture

### `EditableTimelinePlan`

An editor-neutral immutable model produced from the same enabled keep-segments used by rendered export.

The plan contains:

- Absolute source path.
- Source duration.
- Width and height after applying rotation metadata.
- Rotation.
- Rational frame rate numerator and denominator.
- Audio-presence flag.
- Ordered clips with source start frame, source frame count, target start frame, and target frame count.
- Total timeline frame count.

The plan has no CapCut-specific IDs or JSON fields.

### `EditableTimelinePlanBuilder`

Consumes the current reviewed export plan and enriched media metadata. It converts time ranges to source-frame ranges, removes any range that rounds below one frame, rejects out-of-bounds ranges, and produces a contiguous target timeline.

### `CapCutEnvironmentProbe`

Responsible only for discovering and validating the CapCut environment:

- Locate the internal macOS registry at `~/Movies/CapCut/User Data/Projects/com.lveditor.draft/root_meta_info.json`.
- Detect whether CapCut is running.
- Read and validate the registry without mutating it.
- Confirm the supported CapCut 8.7 schema from recent healthy entries when available.
- Detect the most recently used mounted `draft_root_path`, including external volumes.
- Return the registry snapshot hash and filesystem metadata needed for the later race check.

The environment probe never creates or edits project files.

### `CapCutMac87Profile`

A bundled sanitized schema profile derived from real macOS 8.7 projects. It defines:

- Top-level draft keys and default values.
- The complete material and keyframe bucket sets.
- Timeline and media material shapes.
- Default per-segment companion material shapes.
- Required root, sidecar, backup, attachment, and nested timeline files.
- Registry and `draft_meta_info.json` entry shapes.
- Canonical timeline filename selection.

The profile supports both observed macOS timeline filenames, `draft_content.json` and `draft_info.json`. The environment probe chooses the locally observed filename. On an otherwise valid new 8.7 library with no projects, the bundled default is used. Every generated mirror uses the selected name and identical serialized bytes.

The profile contains no real user paths, device fingerprints, hardware identifiers, media names, or project identifiers.

### `CapCutDraftBuilder`

Converts an `EditableTimelinePlan` into an in-memory draft package:

- Generate distinct registry draft, timeline, and timeline-index UUIDs.
- Create one main video track.
- Create one segment and one video material per retained clip.
- Give all duplicate video materials the same generated `local_material_id` and original source path.
- Create the standard default companion materials required by the 8.7 profile for every segment.
- Register the physical source exactly once in the metadata sidecar.
- Set normal speed, normal volume, visible state, and no transitions/effects selected by the user.
- Set the canvas from the rotated source dimensions.
- Set metadata and registry material size to the original source file size.
- Serialize root, backup, template, and nested timeline mirrors from one canonical byte array.

The builder does not write to CapCut's library.

### `CapCutDraftValidator`

Validates the staged package independently of the builder:

- Required files and directories exist.
- All JSON files parse.
- Schema version and platform are supported.
- The three identity domains are distinct and cross-references are correct.
- All segment material and companion references resolve.
- One sidecar source entry exists for the physical media.
- Segment source ranges are valid.
- Target ranges begin at zero and are exactly adjacent.
- Project duration equals the final target end.
- Root, backup, template, and nested timeline mirrors are byte-identical.
- Registry entry paths and draft IDs agree with the package.
- No serialized path escapes the source, registry, project root, or generated project directory expected by the operation.

Validation returns structured errors and performs no recovery or mutation.

### `CapCutProjectInstaller`

Owns the transactional filesystem and registry operation. It accepts a validated package, registry snapshot, selected project root, and cancellation token. It never edits an existing draft folder.

## Timeline and Frame Semantics

`ffprobe` metadata collection will be extended to capture:

- Duration.
- Coded width and height.
- Rotation/display-matrix orientation.
- Rational average frame rate with a valid fallback to rational real frame rate.
- Audio stream presence.

For each keep-range:

1. Convert source start and end times to the nearest source frames using the rational frame rate.
2. Clamp to `[0, source frame count]`.
3. Require `endFrame > startFrame`.
4. Set target start frame to the cumulative prior target frame count.
5. Set target frame count equal to the source frame count.

CapCut microseconds are calculated only during serialization:

`microseconds = round(frameIndex * 1_000_000 * fpsDenominator / fpsNumerator)`

Each duration is derived from serialized end minus serialized start. This mirrors CapCut's observed one-microsecond distribution at frame boundaries while guaranteeing exact target adjacency.

Editable-project duration may differ from rendered export by less than one frame. That is an explicit consequence of editor frame-grid semantics.

## Generated CapCut Timeline

The first release generates:

- One primary video track.
- One video segment for every retained range.
- Embedded source audio through the video materials.
- Normal playback speed and volume.
- No gaps, overlaps, transitions, filters, text, B-roll, or user-facing effects.

Each segment has its own CapCut video material ID and default companion IDs. Duplicate materials point to the same absolute source path and share one `local_material_id`. The metadata sidecar contains one type-0 source entry with source duration, dimensions, file path, creation/import timestamps, and rough-cut range.

## Environment Compatibility Rules

Creation is allowed only when all conditions hold:

- Runtime OS is macOS.
- CapCut is not running.
- The internal registry exists and is valid JSON.
- The detected project layout is compatible with the 8.7 schema profile.
- The selected draft root exists, is mounted, and is writable.
- Source media exists, is readable, and has supported metadata.

If the external draft root recorded in settings or registry is disconnected, the app reports the expected volume and does not silently fall back to internal storage.

The app refuses newer or unknown CapCut layouts. Version support must be added through a new explicit schema profile and fixture suite.

## Transaction and Recovery

1. Read the registry bytes, cryptographic hash, size, and modification time.
2. Complete every preflight compatibility check.
3. Choose a unique display and folder name.
4. Build the package in a hidden staging directory under the selected project root so final rename remains on the same filesystem.
5. Flush every staged file and validate the complete package.
6. Recheck that CapCut remains closed, source and root remain available, and the registry bytes still match the original hash.
7. Atomically rename the staging directory to the final project directory.
8. Write a timestamped registry backup beside the registry.
9. Create the updated registry as a same-directory temporary file, flush it, parse it again, and atomically replace `root_meta_info.json`.
10. Re-read the installed registry and project and run final validation.

If any operation before step 7 fails, delete only the hidden staging directory. If registry installation or final validation fails after step 7, restore the original registry bytes and remove only the newly generated project directory. If automatic restoration itself fails, preserve the backup and generated folder, report their exact paths, and do not claim success.

Existing draft folders and entries are never rewritten, normalized, or deleted.

## User Experience

The review/export area will expose:

- Existing `Export Video…` action.
- New `Create CapCut Project` action.

The CapCut action is enabled only when a valid analysis exists, at least one keep-range is enabled, and the application is not busy.

Default project name:

`<source filename without extension> - Cut The Pause`

Collisions append ` (1)`, ` (2)`, and so on. Users can rename the project in CapCut.

Draft-root resolution order:

1. Remembered successful CapCut draft root, if mounted and compatible with the registry.
2. Most recently modified mounted `draft_root_path` in the registry.
3. User-selected folder from a CapCut Drafts folder picker.

The user-selected root is remembered only after a successful installation.

Progress stages:

- Checking CapCut.
- Building timeline.
- Validating project.
- Registering project.
- Verifying installation.

On success, show project name, separate clip count, editable duration, and destination. Expose `Open CapCut` and `Reveal Project` actions. Also state that the project references the original video and that moving, renaming, deleting, or disconnecting it will require relinking in CapCut.

Expected errors use actionable language for CapCut running, unsupported version, invalid registry, missing source, missing external volume, incompatible folder selection, registry race, and rollback failure.

## Testing Strategy

### Timeline unit tests

- Enabled and disabled review cuts.
- Source order and cumulative target positions.
- Boundary rounding and clamping.
- Ranges shorter than one frame.
- 24, 25, 30000/1001, 30, and 60 fps.
- Portrait/landscape rotation metadata.
- Empty and invalid plans.

### Schema and golden-fixture tests

- Compare output structure with a sanitized real 8.7 fixture.
- Assert required top-level keys, material buckets, keyframe buckets, and companion files.
- Assert unique and correctly related identity domains.
- Assert one segment material per clip and one physical source sidecar entry.
- Assert every material reference resolves.
- Assert all timeline mirrors are byte-identical.
- Assert absolute paths and material size fields are correct.

Dynamic values such as UUIDs and timestamps are injected through test abstractions so golden assertions remain deterministic.

### Installer integration tests

Use temporary fake internal registries and project roots to verify:

- Internal and external root separation.
- Unique naming and collisions.
- CapCut-running rejection.
- Malformed or unsupported registry rejection.
- Registry hash race rejection.
- Source disappearance and simulated external-root disappearance.
- Staging write and flush failures.
- Atomic registry replacement failure.
- Successful rollback after project-folder installation.
- Preservation of all pre-existing registry bytes and draft directories on failure.
- Successful final revalidation.

### Application integration tests

- Command enablement and busy-state behavior.
- Existing render-export behavior remains unchanged.
- Progress stages and actionable errors.
- Successful root persistence only after installation.
- Open CapCut and Reveal Project actions are offered only after success.

### Controlled live smoke test

After automated verification passes:

1. Generate small synthetic video with audio and known silent ranges.
2. Create a clearly named test project in the real supported CapCut library while CapCut is closed.
3. Launch CapCut and verify the project appears and opens.
4. Verify separate adjacent clips, embedded audio, expected source in-points, timeline duration, and absence of gaps.
5. Verify representative existing projects remain listed and unchanged.
6. Remove only the generated smoke-test project and its registry entry through a dedicated test cleanup operation, retaining the pre-test backup until cleanup verification completes.

If CapCut 8.7 is unavailable at verification time, automated and fixture verification can complete, but live compatibility must be reported as unverified rather than inferred.

## Acceptance Criteria

- A reviewed analysis can create a registered CapCut project without rendering or copying media.
- Each retained range appears as a separate editable clip in order on one video track.
- Source audio plays with the retained clips.
- Clip boundaries match the reviewed cuts within one source frame.
- The timeline has no gaps or overlaps.
- Portrait and landscape projects display with the correct orientation and canvas.
- The project appears in CapCut's local project list and opens in CapCut 8.7.
- The original source path is the only required media dependency.
- Existing CapCut project files are not modified.
- Existing rendered MP4/MOV export and its tests remain unchanged and passing.
- All unsupported or unsafe environments fail without partial installation.
- A failed post-install step restores the original registry and removes only newly generated files.

## Privacy and Security

- Generated profiles and fixtures contain no user media, file names, paths, device IDs, MAC addresses, account identifiers, or hardware fingerprints.
- Runtime project files include only data required by CapCut, including the user-selected source path and draft-root paths.
- Commands and errors must avoid logging the full contents of the CapCut registry or unrelated project metadata.
- Paths are serialized through the JSON library rather than string concatenation.
- Folder names are sanitized, final paths are containment-checked, and generated writes are restricted to the selected draft root and CapCut registry directory.

## Delegated Implementation

Per user instruction, implementation will be delegated through the `go-developer` workflow using the configured Ox Alpha Free model (`opencode/x-preview-f-free`). Codex remains the lead: it prepares the implementation brief, obtains explicit external-provider consent, reviews every diff, runs verification, and sends numbered fix rounds through the same bridge. Codex will not directly write implementation code.

### Isolated worktree requirement

Implementation must not occur in the current `main` checkout. Before delegated coding begins:

1. Confirm the current checkout is clean and still on the approved specification commit.
2. Add `.worktrees/` to `.gitignore` and commit that repository hygiene change if it is not already ignored.
3. Create branch `feature/capcut-editable-project-export` in `.worktrees/capcut-editable-project-export` using `git worktree add`.
4. Restore/build dependencies and run the complete baseline test suite inside the worktree.
5. Stop and report if the clean baseline does not pass; do not attribute pre-existing failures to the feature.

Every GO bridge invocation, numbered fix round, build, and automated test must run with the isolated worktree as its working directory. The bridge may inspect and modify repository context only through that worktree. The original checkout and any app process launched from it remain untouched.

The feature branch will not be merged into `main` automatically. After implementation, review, automated verification, and live compatibility reporting, Codex will present the branch and integration options for explicit user approval.

## Future Extensions

The `EditableTimelinePlan` is intentionally independent of CapCut. Future exporters can consume it to generate:

- Final Cut Pro XML (`.fcpxml`).
- Premiere-compatible Final Cut Pro XML interchange (`.xml`).
- Optional self-contained project packages that copy media.
- Explicit compatibility profiles for later CapCut versions.

Those extensions require separate designs, fixtures, and compatibility testing.
