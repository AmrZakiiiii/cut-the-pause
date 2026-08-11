# Long-Video Workflow Reliability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task with review checkpoints.

**Goal:** Make Cut The Pause remember detection settings, stay visibly responsive during long analysis, and complete or cleanly fail long exports while preserving the selected MOV/MP4 format, preset, and quality behavior.

**Architecture:** Keep the existing `VideoWorkflowService` pipeline and FFmpeg command builder. Add a small app-level JSON preference store injected into `MainWindowViewModel`, a staged analysis-progress callback, and a transaction boundary around FFmpeg output. Move the synchronous VAD invocation to a worker thread and expose separate analysis/export presentation state in the existing Avalonia window.

**Tech Stack:** .NET 8, C# records/interfaces, Avalonia 11.3.12, FFmpeg, xUnit 2.5.3, `System.Text.Json`.

## Global Constraints

- The user’s latest valid detection values and export preset must persist across video changes and application restarts.
- The real acceptance input is `/Volumes/AmrZaki EXT/Oka w Tarek/IMG_6595.MOV`.
- The acceptance settings are minimum silence `120`, minimum speech `150`/the current stored value, padding before `0`, padding after `0`, and the current speech threshold.
- MOV input continues to default to MOV/ProRes; MP4 never becomes the automatic replacement for MOV output.
- No output format, codec, preset, bitrate, or pixel-format setting may be silently changed to make a long export faster.
- Failed or canceled exports must not leave a partial file at the user-selected final path.
- All production behavior changes require a failing test before implementation.

---

## File Map

- Create `src/CutThePause.App/Services/AnalysisSettingsPreferences.cs` for typed persisted values, defaults, and normalization.
- Create `src/CutThePause.App/Services/IAnalysisSettingsStore.cs` for the load/save boundary used by the view model.
- Create `src/CutThePause.App/Services/JsonAnalysisSettingsStore.cs` for application-data path resolution, JSON serialization, atomic replacement, and non-fatal storage errors.
- Modify `src/CutThePause.App/ViewModels/MainWindowViewModel.cs` for preference hydration/persistence, analysis progress state, action gating, and workflow progress wiring.
- Modify `src/CutThePause.App/Services/AppBootstrapper.cs` to inject the default JSON settings store.
- Modify `src/CutThePause.App/MainWindow.axaml` to bind action gates and add the analysis-progress overlay.
- Modify `src/CutThePause.Infrastructure/VideoWorkflowService.cs` to report analysis stages and offload synchronous VAD execution.
- Create `src/CutThePause.Core/Models/AnalysisProgress.cs` for the workflow-to-UI stage message.
- Modify `src/CutThePause.Infrastructure/Ffmpeg/FfmpegVideoExporter.cs` to render to a same-extension temporary file and commit it only after success.
- Modify `src/CutThePause.Infrastructure/Ffmpeg/ProcessFfmpegRunner.cs` to await both output streams and terminate the process tree when cancellation interrupts a run.
- Create `tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj` for view-model and settings tests.
- Create `tests/CutThePause.App.Tests/AnalysisSettingsStoreTests.cs` for preference persistence behavior.
- Create `tests/CutThePause.App.Tests/MainWindowViewModelTests.cs` for view-model settings, source switching, and analysis presentation behavior.
- Create `tests/CutThePause.App.Tests/TestDoubles.cs` for reusable fake workflow collaborators and an in-memory settings store.
- Create `tests/CutThePause.Infrastructure.Tests/VideoWorkflowServiceTests.cs` for stage ordering and worker-thread VAD execution.
- Create `tests/CutThePause.Infrastructure.Tests/FfmpegVideoExporterTests.cs` for transactional output and failure cleanup.
- Create `tests/CutThePause.Infrastructure.Tests/ProcessFfmpegRunnerTests.cs` for cancellation/process cleanup on macOS/Linux.
- Create `tests/CutThePause.Infrastructure.Tests/LongVideoAcceptanceTests.cs` for the opt-in real-video analysis/export acceptance command.

## Task 1: Add the test project and persisted settings boundary

**Files:**
- Create: `tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj`
- Create: `tests/CutThePause.App.Tests/AnalysisSettingsStoreTests.cs`
- Create: `src/CutThePause.App/Services/AnalysisSettingsPreferences.cs`
- Create: `src/CutThePause.App/Services/IAnalysisSettingsStore.cs`
- Create: `src/CutThePause.App/Services/JsonAnalysisSettingsStore.cs`

**Interfaces:**
- `AnalysisSettingsPreferences.Defaults` produces `350`, `150`, `80`, `120`, `0.5f`, and `ExportPreset.Balanced`.
- `AnalysisSettingsPreferences.Normalize(AnalysisSettingsPreferences? value)` returns a record with invalid individual fields replaced by their corresponding defaults.
- `IAnalysisSettingsStore.Load()` returns normalized preferences.
- `IAnalysisSettingsStore.Save(AnalysisSettingsPreferences preferences)` persists normalized preferences without throwing storage errors to callers.
- `JsonAnalysisSettingsStore(string? filePath = null)` uses the supplied path in tests or `<ApplicationData>/Cut The Pause/detection-settings.json` in production.

- [ ] **Step 1: Add the app test project and write the failing store tests.**

  Add the same `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, and `coverlet.collector` versions used by the existing test projects. Reference `src/CutThePause.App/CutThePause.App.csproj`.

  Add tests with these assertions:

  ```csharp
  [Fact]
  public void MissingFile_ReturnsExistingDefaults()
  {
      var store = new JsonAnalysisSettingsStore(Path.Combine(_tempDirectory, "settings.json"));

      var result = store.Load();

      Assert.Equal(350, result.MinSilenceMs);
      Assert.Equal(150, result.MinSpeechMs);
      Assert.Equal(80, result.PaddingBeforeMs);
      Assert.Equal(120, result.PaddingAfterMs);
      Assert.Equal(0.5f, result.SpeechThreshold);
      Assert.Equal(ExportPreset.Balanced, result.ExportPreset);
  }

  [Fact]
  public void SaveThenLoad_RoundTripsAllValues()
  {
      var path = Path.Combine(_tempDirectory, "settings.json");
      var store = new JsonAnalysisSettingsStore(path);
      var expected = new AnalysisSettingsPreferences(120, 150, 0, 0, 0.5f, ExportPreset.HigherQuality);

      store.Save(expected);

      Assert.Equal(expected, store.Load());
      Assert.True(File.Exists(path));
      Assert.Empty(Directory.EnumerateFiles(_tempDirectory, "*.tmp"));
  }

  [Fact]
  public void LoadWithInvalidFields_UsesDefaultsOnlyForInvalidFields()
  {
      var path = Path.Combine(_tempDirectory, "settings.json");
      File.WriteAllText(path, "{\"MinSilenceMs\":-1,\"MinSpeechMs\":220,\"PaddingBeforeMs\":0,\"PaddingAfterMs\":-5,\"SpeechThreshold\":2,\"ExportPreset\":\"SmallerFile\"}");

      var result = new JsonAnalysisSettingsStore(path).Load();

      Assert.Equal(350, result.MinSilenceMs);
      Assert.Equal(220, result.MinSpeechMs);
      Assert.Equal(0, result.PaddingBeforeMs);
      Assert.Equal(120, result.PaddingAfterMs);
      Assert.Equal(0.5f, result.SpeechThreshold);
      Assert.Equal(ExportPreset.SmallerFile, result.ExportPreset);
  }
  ```

- [ ] **Step 2: Run the focused test file and verify it fails for the missing settings types.**

  Run:

  ```bash
  dotnet test tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj --filter FullyQualifiedName~AnalysisSettingsStoreTests --verbosity minimal
  ```

  Expected: compilation failure because the settings types and project implementation do not exist yet.

- [ ] **Step 3: Implement the typed preferences and store.**

  Define `AnalysisSettingsPreferences` as a sealed record with the six typed values and a `Defaults` property. Normalize non-negative integer fields, require `MinSpeechMs > 0`, require `SpeechThreshold` in `[0, 1]`, and accept only defined `ExportPreset` values. Preserve valid sibling fields when one field is invalid.

  Implement `JsonAnalysisSettingsStore.Load()` with `JsonSerializer.Deserialize`, a `JsonStringEnumConverter` so named presets round-trip, normalization, and a default return for missing/malformed files. Implement `Save()` by creating the parent directory, writing UTF-8 JSON to a unique sibling `.tmp` file, replacing the target with `File.Move(temp, target, true)`, and deleting the temporary file in a `finally` block. Catch file and JSON exceptions in both methods so settings storage never blocks the main workflow.

- [ ] **Step 4: Run the focused tests and verify they pass.**

  Run the same `dotnet test` command from Step 2.

  Expected: all `AnalysisSettingsStoreTests` pass with zero failures.

- [ ] **Step 5: Commit the settings boundary.**

  ```bash
  git add tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj tests/CutThePause.App.Tests/AnalysisSettingsStoreTests.cs src/CutThePause.App/Services/AnalysisSettingsPreferences.cs src/CutThePause.App/Services/IAnalysisSettingsStore.cs src/CutThePause.App/Services/JsonAnalysisSettingsStore.cs
  git commit -m "feat: persist detection settings"
  ```

## Task 2: Hydrate and persist settings in the view model

**Files:**
- Create: `tests/CutThePause.App.Tests/TestDoubles.cs`
- Create: `tests/CutThePause.App.Tests/MainWindowViewModelTests.cs`
- Modify: `src/CutThePause.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/CutThePause.App/Services/AppBootstrapper.cs`

**Interfaces:**
- `MainWindowViewModel(VideoWorkflowService workflowService, IAnalysisSettingsStore? settingsStore = null)` loads settings once at construction.
- Each valid detection text edit and preset edit calls `IAnalysisSettingsStore.Save` with the current typed record.
- `SetInputPath(string path)` saves current settings before changing paths and never resets detection fields.

- [ ] **Step 1: Write failing view-model tests.**

  Add an in-memory store that records the last saved preferences and returns a configurable initial record. Build the view model with a real `VideoWorkflowService` whose collaborators are no-op test doubles; these tests do not touch FFmpeg.

  Add tests for:

  ```csharp
  [Fact]
  public void Constructor_LoadsPersistedValues()
  {
      var stored = new AnalysisSettingsPreferences(120, 150, 0, 0, 0.5f, ExportPreset.HigherQuality);
      var viewModel = CreateViewModel(new InMemoryAnalysisSettingsStore(stored));

      Assert.Equal("120", viewModel.MinSilenceMsText);
      Assert.Equal("150", viewModel.MinSpeechMsText);
      Assert.Equal("0", viewModel.PaddingBeforeMsText);
      Assert.Equal("0", viewModel.PaddingAfterMsText);
      Assert.Equal(ExportPreset.HigherQuality, viewModel.SelectedPreset);
  }

  [Fact]
  public void ValidEdits_AreSavedAndSourceSwitchDoesNotResetThem()
  {
      var store = new InMemoryAnalysisSettingsStore(AnalysisSettingsPreferences.Defaults);
      var viewModel = CreateViewModel(store);

      viewModel.MinSilenceMsText = "120";
      viewModel.PaddingBeforeMsText = "0";
      viewModel.PaddingAfterMsText = "0";
      viewModel.SetInputPath("/tmp/first.mov");
      viewModel.SetInputPath("/tmp/second.mov");

      Assert.Equal("120", viewModel.MinSilenceMsText);
      Assert.Equal("0", viewModel.PaddingBeforeMsText);
      Assert.Equal("0", viewModel.PaddingAfterMsText);
      Assert.Equal(120, store.LastSaved.MinSilenceMs);
  }

  [Fact]
  public void InvalidEdit_DoesNotOverwriteLastValidRecord()
  {
      var store = new InMemoryAnalysisSettingsStore(new AnalysisSettingsPreferences(120, 150, 0, 0, 0.5f, ExportPreset.Balanced));
      var viewModel = CreateViewModel(store);

      viewModel.MinSilenceMsText = "not a number";

      Assert.Equal(120, store.LastSaved.MinSilenceMs);
  }
  ```

- [ ] **Step 2: Run the focused view-model tests and verify they fail.**

  ```bash
  dotnet test tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj --filter FullyQualifiedName~MainWindowViewModelTests --verbosity minimal
  ```

  Expected: failures because the constructor does not hydrate preferences and the text/preset setters do not persist them.

- [ ] **Step 3: Implement view-model preference hydration and persistence.**

  Add `_settingsStore`, load `AnalysisSettingsPreferences` in the constructor, and format numeric values with invariant culture. Add `TryBuildPersistedPreferences` that parses all five text fields with range checks; return without saving when any field is invalid. Call it from the five text setters, `SelectedPreset`, `SetInputPath`, and immediately before analysis. Keep `BuildSettings()`’s existing fallback behavior for an invalid field so the current UI remains usable.

  Update `AppBootstrapper.CreateMainWindow()` to construct `new JsonAnalysisSettingsStore()` and pass it to the view model. Do not alter `ResolvePreferredOutputExtension`; MOV input must still suggest `.mov`.

- [ ] **Step 4: Run the focused view-model tests and verify they pass.**

  Run the Step 2 command and expect all tests to pass.

- [ ] **Step 5: Commit the view-model settings integration.**

  ```bash
  git add tests/CutThePause.App.Tests/TestDoubles.cs tests/CutThePause.App.Tests/MainWindowViewModelTests.cs src/CutThePause.App/ViewModels/MainWindowViewModel.cs src/CutThePause.App/Services/AppBootstrapper.cs
  git commit -m "feat: keep detection settings across videos"
  ```

## Task 3: Add staged analysis progress and background VAD execution

**Files:**
- Create: `src/CutThePause.Core/Models/AnalysisProgress.cs`
- Create: `tests/CutThePause.Infrastructure.Tests/VideoWorkflowServiceTests.cs`
- Modify: `src/CutThePause.Infrastructure/VideoWorkflowService.cs`

**Interfaces:**
- `public sealed record AnalysisProgress(string Stage);`
- `VideoWorkflowService.AnalyzeAsync(string inputPath, AnalysisSettings settings, CancellationToken cancellationToken, IProgress<AnalysisProgress>? progress = null)` reports four stages in order.

- [ ] **Step 1: Write failing workflow tests.**

  Use test doubles for metadata, audio extraction, VAD, and export. Add:

  ```csharp
  [Fact]
  public async Task AnalyzeAsync_ReportsStagesInOrder()
  {
      var stages = new List<string>();
      var workflow = CreateWorkflow(new ImmediateVadAnalyzer());

      await workflow.AnalyzeAsync("input.mov", new AnalysisSettings(), CancellationToken.None,
          new Progress<AnalysisProgress>(progress => stages.Add(progress.Stage)));

      Assert.Equal(new[]
      {
          "Reading video metadata...",
          "Extracting audio...",
          "Detecting speech...",
          "Building review..."
      }, stages);
  }

  [Fact]
  public async Task AnalyzeAsync_DoesNotBlockCallerWhileSynchronousVadIsRunning()
  {
      using var vadStarted = new ManualResetEventSlim();
      using var releaseVad = new ManualResetEventSlim();
      var workflow = CreateWorkflow(new BlockingVadAnalyzer(vadStarted, releaseVad));
      var returned = new TaskCompletionSource<Task<AnalysisResult>>(TaskCreationOptions.RunContinuationsAsynchronously);

      var thread = new Thread(() => returned.SetResult(workflow.AnalyzeAsync("input.mov", new AnalysisSettings(), CancellationToken.None)));
      thread.Start();

      Assert.True(vadStarted.Wait(TimeSpan.FromSeconds(2)));
      Assert.True(returned.Task.Wait(TimeSpan.FromSeconds(2)));
      releaseVad.Set();
      await returned.Task.Result;
  }
  ```

  The second test intentionally invokes `AnalyzeAsync` on a dedicated thread and checks that the method returns a task while the synchronous fake VAD is blocked. Without worker-thread offloading, the thread cannot publish the returned task.

- [ ] **Step 2: Run the focused workflow tests and verify they fail.**

  ```bash
  dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter FullyQualifiedName~VideoWorkflowServiceTests --verbosity minimal
  ```

  Expected: the stage list is empty and the blocking test times out/fails because the current workflow calls the synchronous VAD directly.

- [ ] **Step 3: Implement analysis progress and worker-thread VAD.**

  Add the `AnalysisProgress` record. In `VideoWorkflowService.AnalyzeAsync`, report each stage immediately before its component call, wrap the VAD call in `Task.Run(() => _vadAnalyzer.DetectSpeechAsync(audio, settings, cancellationToken), cancellationToken)`, await it with `ConfigureAwait(false)`, report `Building review...`, and then call `CutPlanBuilder.Build`. Keep the existing warning logic and three-argument call compatibility by making the progress parameter optional.

- [ ] **Step 4: Run the focused workflow tests and verify they pass.**

  Run the Step 2 command. Expected: both tests pass with zero failures.

- [ ] **Step 5: Commit the workflow progress change.**

  ```bash
  git add src/CutThePause.Core/Models/AnalysisProgress.cs src/CutThePause.Infrastructure/VideoWorkflowService.cs tests/CutThePause.Infrastructure.Tests/VideoWorkflowServiceTests.cs
  git commit -m "feat: report responsive analysis progress"
  ```

## Task 4: Present analysis progress and gate conflicting UI actions

**Files:**
- Modify: `src/CutThePause.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/CutThePause.App/MainWindow.axaml`
- Modify: `tests/CutThePause.App.Tests/MainWindowViewModelTests.cs`

**Interfaces:**
- `MainWindowViewModel.IsAnalyzing` is `true` from analysis start until success/failure.
- `MainWindowViewModel.AnalysisStageText` contains the current workflow stage.
- `MainWindowViewModel.ShowAnalysisOverlay` mirrors `IsAnalyzing`.
- `MainWindowViewModel.CanChangeSource`, `CanChooseOutput`, and `CanResetReview` are false while `_isBusy` is true.

- [ ] **Step 1: Write failing analysis-presentation tests.**

  Add a delayed fake VAD with a `TaskCompletionSource<VadAnalysisResult>`. Start `viewModel.AnalyzeAsync()`, wait until the fake VAD has been called, and assert:

  ```csharp
  Assert.True(viewModel.IsAnalyzing);
  Assert.True(viewModel.ShowAnalysisOverlay);
  Assert.False(viewModel.CanAnalyze);
  Assert.False(viewModel.CanChangeSource);
  Assert.Contains("Detecting speech", viewModel.AnalysisStageText);
  ```

  Release the fake VAD, await the task, and assert `IsAnalyzing` and `ShowAnalysisOverlay` are false. Add a second test where metadata throws and assert the overlay is false and `StatusMessage == "Analysis failed."`.

- [ ] **Step 2: Run the focused app tests and verify they fail.**

  ```bash
  dotnet test tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj --filter FullyQualifiedName~AnalysisPresentation --verbosity minimal
  ```

  Expected: compilation or assertion failures because the analysis state properties do not exist and `AnalyzeAsync` only updates the footer message.

- [ ] **Step 3: Implement the view-model analysis state.**

  Add `_isAnalyzing` and `_analysisStageText`, computed `ShowAnalysisOverlay`, and the three action-gate properties. Start an indeterminate presentation before the workflow call, pass `new Progress<AnalysisProgress>(progress => AnalysisStageText = progress.Stage)` to `AnalyzeAsync`, and clear the state in `finally`. Raise `CanChangeSource`, `CanChooseOutput`, and `CanResetReview` from `RaiseStateProperties`. Guard direct `SetInputPath`, `SetOutputPath`, and `ResetReview` calls when busy.

- [ ] **Step 4: Add the analysis overlay and bindings.**

  In `MainWindow.axaml`, bind Import Video, Reveal Source, Choose Output, and Reset Review to the new action gates. Add a top-level overlay adjacent to the export overlay with:

  ```xml
  <Border Grid.RowSpan="2" Grid.ColumnSpan="2" Background="#B20B111A" IsVisible="{Binding ShowAnalysisOverlay}">
    <Border Width="420" Background="#101923" BorderBrush="#2A394B" BorderThickness="1" CornerRadius="28" Padding="24" HorizontalAlignment="Center" VerticalAlignment="Center">
      <StackPanel Spacing="16">
        <TextBlock FontSize="28" FontWeight="Bold" Foreground="{StaticResource BrandTextBrush}" Text="Analysis In Progress" />
        <TextBlock Foreground="{StaticResource BrandMutedBrush}" Text="Cut The Pause is scanning the video audio. This window is still working." TextWrapping="Wrap" />
        <ProgressBar IsIndeterminate="True" Height="12" Foreground="{StaticResource BrandCyanBrush}" Background="#1A2634" />
        <TextBlock Foreground="{StaticResource BrandTextBrush}" FontWeight="SemiBold" Text="{Binding AnalysisStageText}" HorizontalAlignment="Center" />
      </StackPanel>
    </Border>
  </Border>
  ```

  Keep the existing export overlay and its quality/encoder detail unchanged.

- [ ] **Step 5: Run app tests and build the restored UI.**

  ```bash
  dotnet test tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj --filter FullyQualifiedName~AnalysisPresentation --verbosity minimal
  dotnet build src/CutThePause.App/CutThePause.App.csproj --configuration Release --no-restore
  ```

  Expected: focused tests pass and the Avalonia project builds with the new bindings.

- [ ] **Step 6: Commit the analysis UI change.**

  ```bash
  git add src/CutThePause.App/ViewModels/MainWindowViewModel.cs src/CutThePause.App/MainWindow.axaml tests/CutThePause.App.Tests/MainWindowViewModelTests.cs
  git commit -m "feat: show long analysis progress"
  ```

## Task 5: Make FFmpeg export output transactional and cancellable

**Files:**
- Create: `tests/CutThePause.Infrastructure.Tests/FfmpegVideoExporterTests.cs`
- Create: `tests/CutThePause.Infrastructure.Tests/ProcessFfmpegRunnerTests.cs`
- Modify: `src/CutThePause.Infrastructure/Ffmpeg/FfmpegVideoExporter.cs`
- Modify: `src/CutThePause.Infrastructure/Ffmpeg/ProcessFfmpegRunner.cs`

**Interfaces:**
- `FfmpegVideoExporter.ExportAsync` keeps the existing public signature and writes the final output only after a successful FFmpeg run.
- Temporary render files use the requested final extension and live beside the final output.
- `ProcessFfmpegRunner` kills the entire child process tree when its cancellation token interrupts `RunAsync`, `RunWithProgressAsync`, or `RunBinaryAsync`.

- [ ] **Step 1: Write failing exporter tests.**

  Add a fake locator returning `FfmpegBinaries("fake-ffmpeg", null)` and a fake runner that records the argument list. For success, it writes a marker to `arguments[^1]` and returns exit code `0`. For failure, it writes the marker and returns exit code `1`.

  Add these tests:

  ```csharp
  [Fact]
  public async Task SuccessfulExport_CommitsSameExtensionTemporaryFileToFinalPath()
  {
      var finalPath = Path.Combine(_tempDirectory, "trimmed.mov");
      var request = CreateRequest(finalPath, ExportPreset.HigherQuality);
      var runner = new RecordingExportRunner(exitCode: 0);

      await new FfmpegVideoExporter(new FakeLocator(), runner).ExportAsync(request, null, CancellationToken.None);

      Assert.True(File.Exists(finalPath));
      Assert.NotEqual(finalPath, runner.LastArguments[^1]);
      Assert.Equal(".mov", Path.GetExtension(runner.LastArguments[^1]));
      Assert.False(File.Exists(runner.LastArguments[^1]));
  }

  [Fact]
  public async Task FailedExport_RemovesTemporaryFileAndNeverPublishesFinalPath()
  {
      var finalPath = Path.Combine(_tempDirectory, "trimmed.mov");
      var runner = new RecordingExportRunner(exitCode: 1);

      await Assert.ThrowsAsync<InvalidOperationException>(() =>
          new FfmpegVideoExporter(new FakeLocator(), runner).ExportAsync(CreateRequest(finalPath, ExportPreset.Balanced), null, CancellationToken.None));

      Assert.False(File.Exists(finalPath));
      Assert.False(File.Exists(runner.LastArguments[^1]));
  }
  ```

- [ ] **Step 2: Run the focused exporter tests and verify they fail.**

  ```bash
  dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter FullyQualifiedName~FfmpegVideoExporterTests --verbosity minimal
  ```

  Expected: the success test sees the final path passed directly to FFmpeg and the failure test sees a partial final file.

- [ ] **Step 3: Implement transactional output.**

  Create a unique sibling path using `.{filename}.{guid}.cut-the-pause{extension}`. Clone the request with `request with { OutputPath = temporaryPath }` before building the command plan. Use that render request for hardware/software attempts, retain the original request for progress totals, and call `File.Move(temporaryPath, request.OutputPath, true)` only after the selected attempt succeeds. Put cleanup in `finally` and delete only the generated temporary path. Preserve the current command builder, encoder selection, preset handling, and MOV/MP4 extension behavior.

- [ ] **Step 4: Run the focused exporter tests and verify they pass.**

  Run the Step 2 command and expect both tests to pass.

- [ ] **Step 5: Write the failing process-cancellation test.**

  On macOS/Linux, create an executable temporary shell script that writes its PID to a file and sleeps for 30 seconds. Start `ProcessFfmpegRunner.RunAsync` with a cancellation token, wait for the PID file, cancel, assert `OperationCanceledException`, and assert the recorded process has exited. Return early on unsupported platforms.

  Run:

  ```bash
  dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter FullyQualifiedName~ProcessFfmpegRunnerTests --verbosity minimal
  ```

  Expected: the current runner throws but leaves the child process alive, so the test fails.

- [ ] **Step 6: Implement concurrent stream completion and process-tree cleanup.**

  Await stdout, stderr, and `WaitForExitAsync` together in both process paths. Wrap each process run in cancellation-aware cleanup that calls `Kill(entireProcessTree: true)` when the token interrupts the wait, then waits briefly for process exit before rethrowing. Do not change standard-output progress parsing or FFmpeg arguments.

- [ ] **Step 7: Run process tests and the complete Infrastructure test project.**

  ```bash
  dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --filter FullyQualifiedName~ProcessFfmpegRunnerTests --verbosity minimal
  dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --verbosity minimal
  ```

  Expected: the cancellation test and all existing Infrastructure tests pass.

- [ ] **Step 8: Commit the export reliability change.**

  ```bash
  git add src/CutThePause.Infrastructure/Ffmpeg/FfmpegVideoExporter.cs src/CutThePause.Infrastructure/Ffmpeg/ProcessFfmpegRunner.cs tests/CutThePause.Infrastructure.Tests/FfmpegVideoExporterTests.cs tests/CutThePause.Infrastructure.Tests/ProcessFfmpegRunnerTests.cs
  git commit -m "fix: make long exports failure-safe"
  ```

## Task 6: Add opt-in real-video acceptance coverage and verify the requested workflow

**Files:**
- Create: `tests/CutThePause.Infrastructure.Tests/LongVideoAcceptanceTests.cs`
- Modify: `README.md` only if the final user-facing export/analysis behavior needs a concise documentation update.

**Interfaces:**
- The acceptance test uses `/Volumes/AmrZaki EXT/Oka w Tarek/IMG_6595.MOV` by default and supports `CUTTHEPAUSE_LONG_VIDEO_PATH` for an equivalent relocated file.
- The test runs only when `CUTTHEPAUSE_RUN_LONG_VIDEO=1` is set.
- The test uses `AnalysisSettings { MinSilenceMs = 120, MinSpeechMs = 150, PaddingBeforeMs = 0, PaddingAfterMs = 0, SpeechThreshold = 0.5f, ExportPreset = ExportPreset.Balanced }`.
- The test keeps the exported output when `CUTTHEPAUSE_KEEP_LONG_VIDEO_OUTPUT=1`; otherwise it deletes only its own output.

- [ ] **Step 1: Write the opt-in acceptance test.**

  Build the same real `VideoWorkflowService` used by the existing smoke test. Resolve the input path, return without doing work when the opt-in variable is absent or the file is missing, analyze with the exact settings above, assert the analysis has a non-empty keep plan, and export to `IMG_6595.trimmed.quality-check.mov` in the same directory. Keep the output when requested. For the quality-preservation assertion, build the command plan for the output path and assert it remains the ProRes MOV plan (`prores_ks`, `pcm_s16le`, and the requested Balanced profile) rather than changing to an MP4 or lower-quality fallback.

- [ ] **Step 2: Run the focused acceptance test against the user’s video.**

  ```bash
  CUTTHEPAUSE_RUN_LONG_VIDEO=1 CUTTHEPAUSE_KEEP_LONG_VIDEO_OUTPUT=1 \
    dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj \
    --filter FullyQualifiedName~LongVideoAcceptanceTests --verbosity normal
  ```

  Expected: analysis completes with minimum silence `120`, zero padding, current minimum speech, a non-empty cut/keep plan, and export completes to the same-directory MOV output without a quality/preset substitution. If the available volume cannot hold the selected quality output, record the exact capacity/size evidence instead of deleting the source or silently changing the format.

- [ ] **Step 3: Run the complete verification suite and Release build.**

  ```bash
  dotnet test tests/CutThePause.Core.Tests/CutThePause.Core.Tests.csproj --verbosity minimal
  dotnet test tests/CutThePause.Infrastructure.Tests/CutThePause.Infrastructure.Tests.csproj --verbosity minimal
  dotnet test tests/CutThePause.App.Tests/CutThePause.App.Tests.csproj --verbosity minimal
  dotnet build src/CutThePause.App/CutThePause.App.csproj --configuration Release
  git status --short
  git diff --check HEAD~1
  ```

  Expected: all tests pass, the Release build exits with code `0`, and `git diff --check` reports no whitespace errors. Review the final diff to ensure the only restored/developed files are the app source, implementation files, tests, and the approved design/plan documents.

- [ ] **Step 4: Commit the acceptance coverage and final documentation.**

  ```bash
  git add tests/CutThePause.Infrastructure.Tests/LongVideoAcceptanceTests.cs README.md
  git commit -m "test: cover the long-video workflow"
  ```
