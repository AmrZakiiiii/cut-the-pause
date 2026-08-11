# Long-Video Workflow Reliability Design

**Date:** 2026-08-11

## Problem

Long videos expose three workflow failures in Cut The Pause:

1. Detection values are initialized from hard-coded defaults and are not stored as user preferences. The UI should keep the latest valid values when switching videos and after restarting the app.
2. Analysis has no dedicated in-progress presentation. The VAD implementations perform their CPU-heavy loops synchronously before returning a task, so a long analysis can make the Avalonia UI look frozen.
3. Exporting a long MOV can take a long time, especially through the existing ProRes path. The app needs to remain responsive, keep showing trustworthy progress, and return to a usable state after a failed render rather than leaving a partial or apparently stuck export.

The current checkout also has the tracked Avalonia UI files deleted as pre-existing working-tree changes. The implementation workspace restores those files from the last committed application source; no unrelated user changes are restored into the shared checkout until the verified patch is ready.

## Goals

- Persist the latest valid detection values and export preset across video changes and application restarts.
- Make long analysis visibly active and keep the UI responsive while audio/VAD work runs.
- Make long exports observable and failure-safe without changing the user’s selected output format or reducing encoding quality.
- Preserve the existing MOV-to-ProRes behavior and all existing MP4 quality presets. MP4 must not become the automatic default for MOV input, and no preset may be silently changed.
- Add focused regression coverage for persistence, analysis responsiveness/progress, and export process/output handling.

## Non-goals

- No redesign of the speech-detection algorithm.
- No replacement of the FFmpeg filter graph with a segmented-export architecture.
- No automatic quality downgrade, codec substitution, or preset selection based on video duration.
- No background job queue or cross-video analysis history.

## Design

### 1. Persisted detection preferences

Add a small app-level settings store with a testable interface and a JSON implementation. The stored record contains the five detection values and the selected `ExportPreset` as typed values. The JSON file lives below the current user’s application-data directory in a Cut The Pause subdirectory.

The view model receives the store through its constructor. On construction it loads the last valid record; missing, malformed, or out-of-range records fall back field-by-field to the existing defaults. Text properties continue to support the current text-box editing experience. A property update persists only when the text parses as a valid value, so an intermediate invalid edit cannot poison the next launch. The preset persists when it changes.

The view model also persists the current values immediately before analysis and before changing source video. `SetInputPath` does not reset detection values. Store writes use a temporary file in the same directory followed by replacement, and storage failures are swallowed so a preferences problem never prevents analysis or export.

### 2. Responsive, staged analysis

Extend the workflow service with an optional `IProgress<AnalysisProgress>` callback. It reports these user-facing stages:

- Reading video metadata
- Extracting audio
- Detecting speech
- Building the review

The workflow reports each stage at the component boundary. The VAD call is started on a worker thread because the current Silero and energy analyzers do synchronous CPU work inside methods that return `Task.FromResult`. Cancellation is passed through unchanged.

The view model exposes `IsAnalyzing`, `AnalysisStageText`, and `ShowAnalysisOverlay`. `AnalyzeAsync` starts the presentation before entering the workflow, updates the stage text from progress callbacks, and clears the overlay in every completion path. The overlay uses an indeterminate progress bar because total analysis time is not known reliably from the source duration. Existing `CanAnalyze`/`CanExport` gating remains active while the workflow is busy; the source and output actions are also disabled during analysis/export.

### 3. Quality-preserving export reliability

The selected output path and preset remain authoritative:

- A MOV input continues to suggest a MOV output and the existing ProRes encoder path.
- An MP4 output continues to use the selected MP4 preset, including the existing higher-quality option.
- The app never changes a user’s format or preset to make a long export faster.

Harden the FFmpeg export boundary by rendering to a temporary file in the destination directory with the same final extension, then moving it into the requested output path only after FFmpeg exits successfully. A failed or canceled render removes only its own temporary file. Progress continues to describe the final requested path and selected encoder. The process runner drains standard output and standard error concurrently and awaits both streams along with process exit so a verbose FFmpeg process cannot block on a full pipe.

The existing export overlay remains visible for the entire render and reports the selected encoder, encoded duration, total output duration, and current stage. On failure it closes cleanly, preserves the error in the warning area, and leaves the window ready for another attempt. On success it keeps the current completion/reveal behavior.

## Error handling

- Preferences load/save errors are non-fatal and use defaults or the in-memory current values.
- Analysis exceptions clear the analysis overlay, leave the prior review state consistent, and show the exception in the existing warning surface.
- Export exceptions clear the busy/export state, delete only the temporary render file, and show the FFmpeg error without claiming completion.
- Cancellation and process-exit errors propagate through the existing task boundary and receive the same cleanup treatment.

## Testing strategy

- Unit-test JSON preference round-tripping, default fallback, invalid-value handling, and atomic-save behavior with temporary directories.
- Unit-test view-model settings initialization, valid-edit persistence, preset persistence, and source switching without resetting settings using an in-memory test store and a fake workflow.
- Unit-test workflow progress stage ordering and verify the VAD invocation is not performed synchronously on the caller thread.
- Unit-test export command/pipeline behavior with a fake FFmpeg runner: the final path is not written until success, a failed render removes its temporary file, and the selected format/preset is unchanged.
- Add a process-runner regression test that writes enough stderr to fill a pipe while emitting progress, proving the runner completes instead of waiting on an undrained stream.
- Run the existing Core and Infrastructure test projects, the new App tests, and a Release build of the restored Avalonia app. Use the existing real-video smoke tests when the sample media is available.

## Acceptance criteria

- Changing all detection fields and the export preset, selecting another video, and restarting the app restores the latest valid values.
- Starting analysis immediately shows a visible in-progress state; the window repaints and remains responsive while a long VAD operation runs.
- A long MOV export continues to show progress and cannot leave a partial output at the requested path after failure.
- MOV/ProRes remains the default for MOV input, and MP4 output never silently lowers the selected quality.
- All focused and existing automated tests pass, and the application Release build succeeds.
