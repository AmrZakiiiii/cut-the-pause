using CutThePause.App.Services;
using CutThePause.App.ViewModels;
using CutThePause.Core.Models;
using CutThePause.Infrastructure;
using CutThePause.Infrastructure.Abstractions;

namespace CutThePause.App.Tests;

public sealed class MainWindowReleaseFeatureTests
{
    [Fact]
    public void Constructor_LoadsHistoryAndNamedPresets()
    {
        var history = new InMemoryHistoryStore(new[] { CreateHistoryEntry() });
        var presets = new InMemoryCustomPresetStore(new[]
        {
            new NamedPreset("Voice cleanup", 120, 150, 0, 0, 0.5f, ExportPreset.HigherQuality)
        });
        var viewModel = CreateViewModel(history, presets, new InMemoryExportCheckpointStore());

        Assert.Single(viewModel.HistoryEntries);
        Assert.Equal("Voice cleanup", Assert.Single(viewModel.CustomPresets).Name);
    }

    [Fact]
    public void LoadSelectedHistory_RestoresReviewCutsAndWaveform()
    {
        var history = new InMemoryHistoryStore(new[] { CreateHistoryEntry() });
        var viewModel = CreateViewModel(history, new InMemoryCustomPresetStore(), new InMemoryExportCheckpointStore());
        viewModel.SelectedHistoryEntry = Assert.Single(viewModel.HistoryEntries);

        viewModel.LoadSelectedHistory();

        Assert.Equal("/tmp/history-source.mp4", viewModel.InputPathDisplay);
        Assert.Equal("/tmp/history-output.mp4", viewModel.OutputPathDisplay);
        Assert.Single(viewModel.CutCandidates);
        Assert.False(viewModel.CutCandidates[0].IsEnabled);
        Assert.Equal(0.8f, viewModel.WaveformPeaks[1]);
    }

    [Fact]
    public void NamedPreset_SaveApplyAndDeleteRoundTripsAllSettings()
    {
        var presetStore = new InMemoryCustomPresetStore();
        var viewModel = CreateViewModel(new InMemoryHistoryStore(), presetStore, new InMemoryExportCheckpointStore());
        viewModel.MinSilenceMsText = "120";
        viewModel.MinSpeechMsText = "150";
        viewModel.PaddingBeforeMsText = "0";
        viewModel.PaddingAfterMsText = "0";
        viewModel.SpeechThresholdText = "0.65";
        viewModel.SelectedPreset = ExportPreset.HigherQuality;
        viewModel.CustomPresetNameText = "  Interview  ";

        viewModel.SaveCustomPreset();

        var saved = Assert.Single(viewModel.CustomPresets);
        Assert.Equal("Interview", saved.Name);
        viewModel.MinSilenceMsText = "400";
        viewModel.SelectedCustomPreset = saved;
        viewModel.ApplySelectedCustomPreset();

        Assert.Equal("120", viewModel.MinSilenceMsText);
        Assert.Equal("0.65", viewModel.SpeechThresholdText);
        Assert.Equal(ExportPreset.HigherQuality, viewModel.SelectedPreset);

        viewModel.DeleteSelectedCustomPreset();

        Assert.Empty(viewModel.CustomPresets);
    }

    [Fact]
    public async Task ManualTimelineCut_ChangesSummaryAndCanBeToggled()
    {
        var viewModel = CreateViewModel(new InMemoryHistoryStore(), new InMemoryCustomPresetStore(), new InMemoryExportCheckpointStore());
        viewModel.SetInputPath("/tmp/manual-source.mp4");
        await viewModel.AnalyzeAsync();

        viewModel.AddManualCut(new TimeRange(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)));

        var cut = Assert.Single(viewModel.CutCandidates);
        Assert.Equal("Manual cut", cut.Reason);
        Assert.Contains("00:00:01.000", viewModel.RemovedDurationText, StringComparison.Ordinal);

        viewModel.ToggleCutAt(TimeSpan.FromSeconds(2.5));

        Assert.False(cut.IsEnabled);
        Assert.Equal("00:00:00.000", viewModel.RemovedDurationText);
    }

    [Fact]
    public async Task PauseRetainsCheckpoint_AndCancelDiscardsIt()
    {
        var checkpointStore = new InMemoryExportCheckpointStore();
        var exporter = new BlockingCheckpointExporter();
        var viewModel = CreateViewModel(
            new InMemoryHistoryStore(),
            new InMemoryCustomPresetStore(),
            checkpointStore,
            exporter);
        viewModel.SetInputPath("/tmp/pause-source.mp4");
        await viewModel.AnalyzeAsync();

        var exportTask = viewModel.ExportAsync();
        await exporter.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        viewModel.PauseOperation();
        await exportTask;

        Assert.Contains("paused", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(checkpointStore.Load());
        Assert.Single(viewModel.PausedExports);

        viewModel.SelectedPausedExport = viewModel.PausedExports[0];
        exporter.ResetStarted();
        var resumedTask = viewModel.ResumeSelectedExport();
        await exporter.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        viewModel.CancelOperation();
        await resumedTask;

        Assert.Empty(checkpointStore.Load());
        Assert.Contains("canceled", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static MainWindowViewModel CreateViewModel(
        IHistoryStore historyStore,
        ICustomPresetStore presetStore,
        IExportCheckpointStore checkpointStore,
        IVideoExporter? exporter = null)
    {
        var workflow = new VideoWorkflowService(
            new FixedMetadataReader(),
            new FixedAudioExtractor(),
            new FixedVadAnalyzer(),
            exporter ?? new NoopVideoExporter());
        return new MainWindowViewModel(
            workflow,
            new InMemoryAnalysisSettingsStore(AnalysisSettingsPreferences.Defaults),
            historyStore,
            presetStore,
            checkpointStore);
    }

    private static HistoryEntry CreateHistoryEntry()
    {
        var cut = new CutCandidate(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Detected silence", false);
        var analysis = new AnalysisResult(
            "/tmp/history-source.mp4",
            TimeSpan.FromSeconds(10),
            new AnalysisSettings(),
            new[] { new KeepSegment(0, TimeSpan.Zero, TimeSpan.FromSeconds(10)) },
            new[] { cut },
            Array.Empty<string>())
        {
            WaveformPeaks = new[] { 0.2f, 0.8f, 0.3f }
        };

        return new HistoryEntry(
            "history-entry",
            DateTimeOffset.UtcNow,
            HistoryEntryKind.Analysis,
            HistoryEntryStatus.Completed,
            analysis.InputPath,
            "/tmp/history-output.mp4",
            "history-source-fingerprint",
            analysis,
            null,
            "Analysis complete.");
    }
}
