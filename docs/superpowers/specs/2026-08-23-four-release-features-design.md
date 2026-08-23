# Four Remaining Features Release Design

**Date:** 2026-08-23

## Problem

Cut The Pause can currently analyze and export a video, but a long job is still a single disposable operation. A canceled or interrupted export must start again, completed work is not discoverable later, the review surface is limited to a list of detected cuts, and users cannot save several named detection profiles.

## Goals

- Resume a long export after an intentional pause or application interruption without rerendering verified completed batches.
- Keep a bounded, durable history of analyses and exports that can restore a review and its cut decisions.
- Show a bounded-memory waveform/timeline for an analyzed video and support both toggling detected ranges and adding manual cut ranges.
- Save, apply, replace, and delete named custom detection/export presets.
- Preserve the current quality contract: the selected output extension and `ExportPreset` remain authoritative, MP4 remains 10-bit HEVC, MOV remains the ProRes master path, and no quality downgrade is selected automatically.
- Keep the Avalonia UI responsive and keep all long work cancellable.

## Non-goals

- No change to the Silero model or speech-detection thresholds.
- No automatic quality downgrade, codec substitution, or format change to make a job finish faster.
- No copying of source or output media into application storage.
- No byte-level resume inside an interrupted FFmpeg batch; an interrupted batch is discarded and rerun, while completed batches are reused.
- No partial mid-inference resume for a canceled VAD pass. A completed analysis is cached and reusable; a canceled analysis starts again from the beginning because the current VAD contract does not expose serializable inference state.

## Design

### 1. Durable export checkpoints

Add `ExportCheckpoint` and `ExportExecutionOptions` models in the Core project. A checkpoint contains a stable job id, a SHA-256 request fingerprint, the original `ExportRequest`, the hidden working directory, deterministic batch paths, completed batch indexes, the encoder label, and timestamps. The fingerprint covers the normalized source path, source file size/last-write time when available, output path, preset, source duration, cut ranges, and keep ranges.

Extend `IVideoExporter.ExportAsync` and `VideoWorkflowService.ExportAsync` with an optional execution-options argument. The FFmpeg exporter creates a checkpoint before rendering and reports it through a callback after initialization and after every successfully completed batch. It uses deterministic batch files in a hidden working directory beside the requested output. On resume it validates the fingerprint, batch count, and file existence before reusing a batch. A canceled current batch is deleted; completed batches remain.

The app stores checkpoint manifests atomically under the user application-data directory and deletes both the manifest and generated batch files when the user explicitly discards a paused job or after a successful export. A paused export is shown separately from normal history and can be resumed or discarded. A hard process termination can therefore leave at most completed batch artifacts and a small manifest; the next launch can recover them without touching the source or final output.

Pause and cancel are separate UI actions. Pause cancels the active FFmpeg process but retains the checkpoint. Cancel discards the checkpoint after the operation returns. Closing the window requests a pause for an export so completed work remains recoverable.

### 2. Analysis/export history

Add a bounded `HistoryEntry` model containing an id, timestamp, kind, status, source/output paths, source fingerprint, optional full `AnalysisResult`, output byte count, and a user-facing message. `AnalysisResult` is extended with a downsampled waveform peak array, so a successful analysis history entry can restore the complete review without rereading the source.

Add an atomic `JsonHistoryStore` with a configurable path and maximum entry count. It keeps the newest entries, never copies media, and treats malformed or unavailable storage as an empty history. The view model records successful analysis and export completion, exposes history items in the UI, and can restore a selected analysis/review or reveal a selected output file. Restoring history does not silently change the current custom preset library.

The same stored completed analysis is the analysis checkpoint/cache: a matching source fingerprint and settings can be loaded from history rather than rerunning VAD. The UI makes this explicit through the history “Load” action.

### 3. Waveform/timeline review

Add a `WaveformPeakBuilder` that reduces either in-memory PCM or the existing file-backed PCM stream to at most 1,200 normalized absolute-amplitude peaks. The streaming path reads the temporary PCM sequentially and never loads the whole audio file. The workflow reports a “Building waveform and review...” stage and places the peaks on `AnalysisResult`.

Add a small Avalonia `WaveformTimeline` control that draws the waveform, detected cut overlays, and a drag selection. Clicking inside a detected cut raises a toggle event. Dragging a range longer than the minimum selection threshold raises a manual-range event. The view model adds manual `CutCandidate` records with reason `Manual cut`, keeps all candidates normalized through the existing `ExportPlanBuilder`, and exposes a refreshed timeline projection whenever a checkbox or manual range changes.

The timeline is review-only with respect to media playback; it does not decode video frames or create a second export path. All manual and detected decisions flow through the existing cut candidate collection and therefore affect the same duration summary and export request.

### 4. Named custom presets

Add a `NamedPreset` model containing a unique case-insensitive name and the five detection values plus the selected `ExportPreset`. Add an atomic `JsonCustomPresetStore` with load/upsert/delete operations and a bounded list.

The view model loads named presets at startup. The settings panel gains a name field, Save/Update, Apply, and Delete actions. Saving an existing name replaces it; applying a preset updates all detection fields and the export preset through the same validation/persistence path used by individual edits. Built-in `ExportPreset` values remain available and keep their current FFmpeg behavior.

### 5. UI and safety

The existing analysis and export overlays remain the primary progress surfaces. The export overlay changes its active controls to Pause and Cancel and reports when a job is resumable. The left panel adds compact paused-job, history, and custom-preset sections; the review panel adds the timeline above the existing detailed cut list.

All JSON stores use sibling temporary files and replacement, with non-fatal I/O handling. All UI operations remain asynchronous. Checkpoint batch files are created only in the hidden job directory derived from the selected output path, and cleanup validates that paths are inside that directory before deleting them.

## Testing strategy

- Core tests cover request fingerprints, checkpoint normalization, waveform peak reduction, manual cut merging, history serialization, and named-preset validation.
- Infrastructure tests cover file-backed and in-memory waveform reduction, workflow waveform staging, exporter checkpoint creation, batch reuse, cancellation retention, and final-output cleanup using fake FFmpeg runners.
- App tests cover history/preset store round trips, view-model restore/apply/save/delete behavior, pause versus cancel state, manual timeline range creation, and restoration of a saved analysis.
- Existing tests for settings persistence, long-video workflow, FFmpeg command construction, and quality-preserving MP4/MOV export remain green.
- Release verification runs the full test suite, publishes the self-contained Apple Silicon app, checks bundled FFmpeg/ffprobe/Silero assets, and verifies the Dock URL resolves to the rebuilt `artifacts/release/Cut The Pause.app` bundle.

## Acceptance criteria

- A multi-batch export paused after completed batches can be resumed and the fake runner is not asked to rerender those completed batches.
- A canceled export removes its generated checkpoint artifacts and never publishes a partial final file.
- A successful analysis displays a waveform for a long file without retaining the full PCM sample array.
- Clicking a detected range toggles the same candidate shown in the list; dragging a valid range creates a manual cut that changes the estimated output duration.
- Named presets survive an application restart and restore all six stored values.
- History restores a prior review, including enabled/disabled cuts and waveform peaks.
- MP4 remains 10-bit HEVC and MOV remains ProRes with the selected preset; no new feature alters that choice.
- The rebuilt app is the one referenced by the Dock-pinned bundle path.
