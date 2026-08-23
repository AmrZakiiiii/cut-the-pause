# Four Remaining Features Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task with review checkpoints.

**Goal:** Implement resumable exports, analysis/export history, waveform/timeline review editing, and named custom presets, then test and rebuild the Dock-pinned macOS app.

**Architecture:** Keep `VideoWorkflowService`, `ExportPlanBuilder`, and the existing FFmpeg command builders as the single media pipeline. Add serializable Core records for checkpoints, history snapshots, waveform data, and named presets; add small app-level JSON stores; and let the view model orchestrate persistence and UI state. Add one bounded-memory waveform reducer and one Avalonia drawing control without introducing a second media renderer.

**Tech Stack:** .NET 8, C# records/interfaces, Avalonia 11.3.12, FFmpeg, `System.Text.Json`, xUnit, Apple Silicon self-contained publish, macOS Dock bundle packaging.

**Spec:** `docs/superpowers/specs/2026-08-23-four-release-features-design.md`

## Global Constraints

- The selected output extension and `ExportPreset` remain authoritative; MP4 remains 10-bit HEVC and MOV remains ProRes.
- No source or final output media is copied into application-data storage.
- Waveform storage is limited to at most 1,200 normalized peaks and file-backed analysis remains streaming.
- Interrupted batches are rerun; only verified completed batches are reused.
- Explicit Cancel deletes generated checkpoint artifacts; Pause retains them for Resume.
- Malformed or unavailable JSON stores are non-fatal and never block analysis/export.
- Production changes require a failing focused test before implementation.
- The final verification must publish to `artifacts/publish/osx-arm64`, rebuild `artifacts/release/Cut The Pause.app`, and verify the Dock URL points at that bundle.

## File Map

- Create `src/CutThePause.Core/Models/ExportCheckpoint.cs` for durable batch checkpoint state.
- Create `src/CutThePause.Core/Models/ExportExecutionOptions.cs` for optional checkpoint callbacks.
- Create `src/CutThePause.Core/Models/HistoryEntry.cs` for analysis/export history snapshots.
- Create `src/CutThePause.Core/Models/NamedPreset.cs` for saved detection/export profiles.
- Modify `src/CutThePause.Core/Models/AnalysisResult.cs` to carry waveform peaks with a compatibility default.
- Modify `src/CutThePause.Core/Services/CutPlanBuilder.cs` to accept waveform peaks.
- Create `src/CutThePause.Core/Services/ExportRequestFingerprint.cs` for deterministic request identity.
- Create `src/CutThePause.Core/Services/WaveformPeakReducer.cs` for bounded-memory peak reduction.
- Modify `src/CutThePause.Infrastructure/Abstractions/IVideoExporter.cs` and `src/CutThePause.Infrastructure/VideoWorkflowService.cs` for execution options and waveform generation.
- Create `src/CutThePause.Infrastructure/Waveform/WaveformPeakBuilder.cs` for PCM file/array reduction.
- Modify `src/CutThePause.Infrastructure/Ffmpeg/FfmpegVideoExporter.cs` to create, reuse, report, and clean batch checkpoints.
- Create `src/CutThePause.App/Services/IHistoryStore.cs` and `JsonHistoryStore.cs` for atomic history persistence.
- Create `src/CutThePause.App/Services/ICustomPresetStore.cs` and `JsonCustomPresetStore.cs` for named preset persistence.
- Create `src/CutThePause.App/Services/IExportCheckpointStore.cs` and `JsonExportCheckpointStore.cs` for checkpoint manifests and safe artifact cleanup.
- Create `src/CutThePause.App/ViewModels/HistoryEntryItemViewModel.cs` and `CustomPresetItemViewModel.cs` for list display/binding.
- Create `src/CutThePause.App/Controls/WaveformTimeline.axaml.cs` for waveform drawing and pointer selection events.
- Modify `src/CutThePause.App/ViewModels/MainWindowViewModel.cs` for all four feature flows and state projections.
- Modify `src/CutThePause.App/MainWindow.axaml` and `.axaml.cs` for preset/history/checkpoint controls and timeline events.
- Modify `src/CutThePause.App/Services/AppBootstrapper.cs` to inject the three new JSON stores.
- Create/modify focused tests under `tests/CutThePause.Core.Tests`, `tests/CutThePause.Infrastructure.Tests`, and `tests/CutThePause.App.Tests`.

### Task 1: Add Core models, fingerprints, and reducer contracts

**Files:**
- Create: `src/CutThePause.Core/Models/ExportCheckpoint.cs`
- Create: `src/CutThePause.Core/Models/ExportExecutionOptions.cs`
- Create: `src/CutThePause.Core/Models/HistoryEntry.cs`
- Create: `src/CutThePause.Core/Models/NamedPreset.cs`
- Create: `src/CutThePause.Core/Services/ExportRequestFingerprint.cs`
- Create: `src/CutThePause.Core/Services/WaveformPeakReducer.cs`
- Modify: `src/CutThePause.Core/Models/AnalysisResult.cs`
- Modify: `src/CutThePause.Core/Services/CutPlanBuilder.cs`
- Test: `tests/CutThePause.Core.Tests/FourFeatureModelTests.cs`

**Interfaces:**
- `ExportCheckpoint` stores `JobId`, `RequestFingerprint`, `ExportRequest Request`, `WorkingDirectory`, `IReadOnlyList<string> BatchPaths`, `IReadOnlyList<int> CompletedBatchIndexes`, `DateTimeOffset CreatedAt`, and `string EncoderLabel`.
- `ExportExecutionOptions` stores an optional `ExportCheckpoint? Checkpoint` and optional `Action<ExportCheckpoint>? CheckpointSaved`.
- `HistoryEntry` stores id, timestamp, kind/status enums, source fingerprint, input/output paths, optional `AnalysisResult`, optional output bytes, and a message.
- `NamedPreset` stores a non-empty name and the six settings values and exposes `ToAnalysisSettings()`.
- `ExportRequestFingerprint.Compute(ExportRequest request)` returns a stable SHA-256 hex string over paths, durations, preset, cuts, keep segments, and source file metadata when available.
- `WaveformPeakReducer.Reduce(ReadOnlySpan<float> samples, int peakCount)` returns at most `peakCount` finite values in `[0,1]`; empty input returns an empty array.

- [ ] **Step 1: Write failing Core tests.** Add tests proving two equivalent requests have the same fingerprint, changing one cut changes it, waveform reduction bounds/normalizes peaks, named presets round-trip settings, and `CutPlanBuilder.Build` preserves a passed peak array.
- [ ] **Step 2: Run the focused Core tests and verify they fail.** Run `dotnet test tests/CutThePause.Core.Tests/CutThePause.Core.Tests.csproj --filter FourFeatureModelTests --verbosity minimal`; expected failure is missing types/methods.
- [ ] **Step 3: Implement the records and pure services.** Use `System.Security.Cryptography.SHA256`, invariant formatting, and deterministic ordering. Add `WaveformPeaks` as an optional final `AnalysisResult` parameter defaulting to `Array.Empty<float>()` so existing constructors compile. Pass peaks through `CutPlanBuilder.Build`.
- [ ] **Step 4: Run the focused tests and verify they pass.** Run the same command; expected all new tests pass with the existing Core tests.
- [ ] **Step 5: Commit the Core foundation.** Run `git add` for the listed Core and test files and commit with `feat: add release feature models`.

### Task 2: Add bounded PCM waveform generation and workflow wiring

**Files:**
- Create: `src/CutThePause.Infrastructure/Waveform/WaveformPeakBuilder.cs`
- Modify: `src/CutThePause.Infrastructure/VideoWorkflowService.cs`
- Test: `tests/CutThePause.Infrastructure.Tests/WaveformPeakBuilderTests.cs`
- Modify: `tests/CutThePause.Infrastructure.Tests/VideoWorkflowServiceTests.cs`

**Interfaces:**
- `WaveformPeakBuilder.Build(PcmAudioData audio, int peakCount, CancellationToken cancellationToken)` returns normalized peaks without retaining a second full sample copy.
- `WaveformPeakBuilder.Build(PcmAudioFile audio, int peakCount, CancellationToken cancellationToken)` reads sequentially from `PcmAudioFile.OpenRead()` and returns at most `peakCount` values.
- `VideoWorkflowService.AnalyzeAsync` reports `Building waveform and review...` and passes the generated peaks to `CutPlanBuilder.Build` for both in-memory and streaming paths.

- [ ] **Step 1: Write failing waveform/file-backed workflow tests.** Use a small `.f32le` fixture with known amplitudes, assert file-backed peaks are normalized and bounded, and extend stage-order assertions to include waveform building and non-empty result peaks.
- [ ] **Step 2: Run focused infrastructure tests and verify they fail.** Run `dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter "WaveformPeakBuilderTests|VideoWorkflowServiceTests" --verbosity minimal`; expected failures are missing builder/stage/peaks.
- [ ] **Step 3: Implement the reducer adapter.** For file-backed audio, calculate bin assignment from `SampleCount`, read float buffers with `MemoryMarshal.Cast<byte,float>`, track only per-bin maxima, and check cancellation between reads. For in-memory audio, delegate to the Core reducer.
- [ ] **Step 4: Wire both workflow branches.** Build waveform after VAD returns and before review construction, report the new stage, and keep the `await using` file lifetime around both operations.
- [ ] **Step 5: Run focused and existing workflow tests.** Verify all pass, including streaming-audio cleanup and caller-thread responsiveness.
- [ ] **Step 6: Commit waveform generation.** Commit with `feat: add bounded waveform analysis`.

### Task 3: Add JSON stores for history, presets, and checkpoints

**Files:**
- Create: `src/CutThePause.App/Services/IHistoryStore.cs`
- Create: `src/CutThePause.App/Services/JsonHistoryStore.cs`
- Create: `src/CutThePause.App/Services/ICustomPresetStore.cs`
- Create: `src/CutThePause.App/Services/JsonCustomPresetStore.cs`
- Create: `src/CutThePause.App/Services/IExportCheckpointStore.cs`
- Create: `src/CutThePause.App/Services/JsonExportCheckpointStore.cs`
- Test: `tests/CutThePause.App.Tests/ReleaseFeatureStoreTests.cs`

**Interfaces:**
- `IHistoryStore.Load()` returns newest-first entries; `Append(HistoryEntry entry)` atomically retains the newest 30; `Remove(string id)` removes one entry.
- `ICustomPresetStore.Load()` returns newest-first `NamedPreset` values; `Upsert(NamedPreset preset)` replaces case-insensitive duplicate names; `Delete(string name)` removes one name.
- `IExportCheckpointStore.Load()` returns pending checkpoint manifests; `Find(string requestFingerprint)` finds a matching valid manifest; `Save(ExportCheckpoint checkpoint)` atomically replaces by job id; `Delete(ExportCheckpoint checkpoint)` removes the manifest and only generated files inside its working directory.
- Default paths are below `<ApplicationData>/Cut The Pause/`, with `history.json`, `custom-presets.json`, and `export-checkpoints.json`; constructors accept explicit paths for tests.

- [ ] **Step 1: Write failing store tests.** Cover missing/malformed files, round trips, bounded history, case-insensitive preset replacement, and checkpoint deletion refusing a file outside `WorkingDirectory`.
- [ ] **Step 2: Run the focused App store tests and verify they fail.** Run `dotnet test tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj --filter ReleaseFeatureStoreTests --verbosity minimal`.
- [ ] **Step 3: Implement all three stores with shared atomic JSON behavior.** Use `JsonStringEnumConverter`, `WriteIndented`, unique sibling `.tmp` files, and non-fatal I/O handling. Normalize names by trimming and reject blank names. `JsonExportCheckpointStore.Delete` must compare full paths and delete only files whose parent is the checkpoint working directory.
- [ ] **Step 4: Run focused tests and verify they pass.** Run the same command plus the existing settings-store tests.
- [ ] **Step 5: Commit the persistence stores.** Commit with `feat: persist history presets and export checkpoints`.

### Task 4: Add resumable exporter execution

**Files:**
- Modify: `src/CutThePause.Infrastructure/Abstractions/IVideoExporter.cs`
- Modify: `src/CutThePause.Infrastructure/VideoWorkflowService.cs`
- Modify: `src/CutThePause.Infrastructure/Ffmpeg/FfmpegVideoExporter.cs`
- Modify: `tests/CutThePause.Infrastructure.Tests/FfmpegVideoExporterTests.cs`
- Modify: `tests/CutThePause.App.Tests/TestDoubles.cs`

**Interfaces:**
- `IVideoExporter.ExportAsync(request, progress, cancellationToken, ExportExecutionOptions? options = null)` retains source compatibility for callers that omit options.
- `VideoWorkflowService.ExportAsync` forwards the optional options unchanged.
- `FfmpegVideoExporter` creates a new checkpoint when options has none, reports it before the first batch, reports a new state after each successful batch, and reuses only completed batch files whose fingerprint and index match.

- [ ] **Step 1: Write failing exporter checkpoint tests.** Add fake-runner tests that cancel after batch 1 and assert the callback received one completed batch, resume with that checkpoint and assert the runner executes only the remaining batch plus concat, and assert a fingerprint mismatch renders all batches.
- [ ] **Step 2: Run focused exporter tests and verify they fail.** Run `dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter FfmpegVideoExporterTests --verbosity minimal`; expected compilation/API or assertion failures.
- [ ] **Step 3: Implement checkpoint-aware batch paths.** Derive a hidden working directory beside the output, create deterministic `batch-0001.ext` paths, validate checkpoint request fingerprint and batch count, skip valid completed batches, and call `CheckpointSaved` after each completed batch. Delete only the active incomplete batch in cancellation/failure cleanup; keep verified completed batches.
- [ ] **Step 4: Preserve hardware/software quality behavior.** If a checkpoint has an encoder label, use the matching hardware/software mode on resume. If hardware fails before a valid completion, clear that attempt and run the existing software fallback with the same request preset and pixel format. Never alter the output extension or selected `ExportPreset`.
- [ ] **Step 5: Complete success and failure cleanup.** Keep the existing same-extension transaction output, concatenate all batch files, and leave final-path publication until successful completion. Ensure the exporter callback state is available to the app after cancellation.
- [ ] **Step 6: Run focused and full infrastructure tests.** Run the exporter filter, then `dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --verbosity minimal`.
- [ ] **Step 7: Commit resumable exporter behavior.** Commit with `feat: resume long exports from checkpoints`.

### Task 5: Add view-model history, presets, waveform editing, and pause state

**Files:**
- Create: `src/CutThePause.App/ViewModels/HistoryEntryItemViewModel.cs`
- Create: `src/CutThePause.App/ViewModels/CustomPresetItemViewModel.cs`
- Modify: `src/CutThePause.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/CutThePause.App/Services/AppBootstrapper.cs`
- Modify: `tests/CutThePause.App.Tests/MainWindowViewModelTests.cs`
- Modify: `tests/CutThePause.App.Tests/TestDoubles.cs`

**Interfaces:**
- Preserve the existing constructor call shape with optional `IHistoryStore`, `ICustomPresetStore`, and `IExportCheckpointStore` parameters; production bootstrap injects JSON implementations and tests inject fakes.
- Expose `ObservableCollection<HistoryEntryItemViewModel> HistoryEntries`, `SelectedHistoryEntry`, `LoadSelectedHistory()`, and `CanLoadSelectedHistory`.
- Expose `ObservableCollection<CustomPresetItemViewModel> CustomPresets`, `CustomPresetNameText`, `SelectedCustomPreset`, `SaveCustomPreset()`, `ApplySelectedCustomPreset()`, and `DeleteSelectedCustomPreset()`.
- Expose `IReadOnlyList<float> WaveformPeaks`, `IReadOnlyList<CutCandidate> TimelineCuts`, `AddManualCut(TimeRange range)`, and `ToggleCutAt(TimeSpan position)`.
- Expose `CanPauseOperation`, `CanResumeSelectedExport`, `PauseOperation()`, `CancelOperation()`, and `ResumeSelectedExport()`.

- [ ] **Step 1: Write failing view-model tests.** Cover recording analysis/export history, restoring enabled/disabled cuts and waveform, saving/applying/deleting a named preset, adding a manual range to the export summary, toggling a candidate by timeline position, and retaining a checkpoint on Pause while deleting it on Cancel.
- [ ] **Step 2: Run focused App tests and verify they fail.** Run `dotnet test tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj --filter MainWindowViewModelTests --verbosity minimal`.
- [ ] **Step 3: Implement store hydration and projections.** Load history/presets/checkpoints in the constructor, map model entries to display view models, and notify all dependent properties after changes. Use the existing `BuildSettings`, `PersistSettingsIfValid`, `PopulateCuts`, and `RecalculateSummary` paths so restored/apply operations are normalized consistently.
- [ ] **Step 4: Implement history recording/restoration.** Append successful analysis and export entries with source fingerprints and output byte counts. Restore input/output paths, analysis result, warnings, candidate enablement, waveform, and summary from a selected successful history entry without rerunning VAD.
- [ ] **Step 5: Implement manual timeline decisions.** Convert drag ranges to clamped `CutCandidate` values with reason `Manual cut`, reject zero/too-short ranges, keep candidates ordered, and raise `TimelineCuts` after every checkbox/manual change. Toggle only the candidate containing a clicked time.
- [ ] **Step 6: Implement named presets and operation state.** Upsert trimmed names, apply all fields, expose pause/cancel distinctions, save the active checkpoint through the store callback, and delete it only for explicit cancellation or successful completion. Resume loads the checkpoint request/review and calls the same export path.
- [ ] **Step 7: Run focused and full App tests.** Run the filter, then `dotnet test tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj --verbosity minimal`.
- [ ] **Step 8: Commit view-model behavior.** Commit with `feat: add review history presets and pause controls`.

### Task 6: Add the waveform control and Avalonia UI wiring

**Files:**
- Create: `src/CutThePause.App/Controls/WaveformTimeline.axaml.cs`
- Modify: `src/CutThePause.App/MainWindow.axaml`
- Modify: `src/CutThePause.App/MainWindow.axaml.cs`
- Test: `tests/CutThePause.App.Tests/TimelineControlTests.cs` (non-rendering event/selection tests if supported by Avalonia test host)

**Interfaces:**
- `WaveformTimeline` has bindable `Peaks`, `Cuts`, and `Duration` properties and raises `CutClicked` with a `TimeSpan` and `RangeSelected` with a `TimeRange`.
- A click on a cut range raises `CutClicked`; a drag of at least 100 ms raises `RangeSelected`; smaller drags do not mutate the review.

- [ ] **Step 1: Write failing control event tests or a pure selection helper test.** Verify time-to-pixel conversion, clamping, detected-cut click selection, and the 100 ms minimum drag threshold. If Avalonia cannot host the control in the test project, extract the conversion/selection helper to Core and test that helper.
- [ ] **Step 2: Run the focused test and verify it fails.** Run the corresponding App test filter.
- [ ] **Step 3: Implement the control.** Draw a dark track, cut overlays, normalized waveform bars, midpoint markers, and a temporary selection rectangle. Capture the pointer during a drag, convert coordinates using `Duration`, and invalidate when bindable properties change. Use no video/audio decoding in the control.
- [ ] **Step 4: Wire XAML and code-behind.** Add the timeline above the existing cut list with instructions, add named-preset/history/paused-job controls and buttons, bind action gates, and route control events to the view model.
- [ ] **Step 5: Build the app project and fix binding/XAML errors.** Run `dotnet build src/CutThePause.App/CutThePause.App.csproj --configuration Release` and correct only errors caused by this feature wiring.
- [ ] **Step 6: Commit the UI.** Commit with `feat: add waveform timeline and job panels`.

### Task 7: Full verification, package, and Dock verification

**Files:**
- Modify: `README.md` only if the new user-visible workflows need documented controls.
- Generate/replace: `artifacts/publish/osx-arm64/`
- Generate/replace: `artifacts/release/Cut The Pause.app`

- [ ] **Step 1: Run all automated tests.** Run `dotnet test --verbosity minimal` from the repository root and record the exact project/test counts.
- [ ] **Step 2: Run the release build.** Run `dotnet build CutThePause.sln --configuration Release` and confirm Core, Infrastructure, App, and all test projects compile.
- [ ] **Step 3: Publish the self-contained Apple Silicon app.** Run `dotnet publish src/CutThePause.App/CutThePause.App.csproj --configuration Release --runtime osx-arm64 --self-contained true --output artifacts/publish/osx-arm64`.
- [ ] **Step 4: Rebuild the Dock bundle.** Run `bash scripts/package-macos-app.sh artifacts/publish/osx-arm64 "$(brew --prefix ffmpeg)" artifacts/release` and confirm the bundle contains the new executable, bundled FFmpeg/ffprobe, and Silero model.
- [ ] **Step 5: Verify the pinned path and bundle identity.** Run `defaults read com.apple.dock persistent-apps`, confirm the URL is `file:///Users/amrzaky/Desktop/Video%20Silence%20Removal/artifacts/release/Cut%20The%20Pause.app/`, then compare the bundle executable timestamp/hash with the freshly published build.
- [ ] **Step 6: Run a non-destructive app smoke check.** Launch the rebuilt app only if safe, confirm it opens, shows the new timeline/history/preset controls, and close it without starting the 7.6 GB acceptance export.
- [ ] **Step 7: Review the diff and report evidence.** Run `git status --short`, `git diff --stat`, and capture the test/build/package results before claiming completion.
