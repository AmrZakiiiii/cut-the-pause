using CutThePause.App.Services;
using CutThePause.App.ViewModels;
using CutThePause.Core.Models;
using CutThePause.Infrastructure;
using CutThePause.Infrastructure.Abstractions;

namespace CutThePause.App.Tests;

public sealed class WorkspaceNavigationTests
{
    [Fact]
    public void FreshViewModel_StartsOnNewCutView()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(WorkspaceView.NewCut, viewModel.CurrentView);
        Assert.True(viewModel.IsNewCutViewActive);
        Assert.False(viewModel.IsReviewViewActive);
        Assert.False(viewModel.IsHistoryViewActive);
        Assert.False(viewModel.IsHelpViewActive);
        Assert.False(viewModel.ShowExportTray);
    }

    [Fact]
    public void SetInputPath_ActivatesReviewView()
    {
        var viewModel = CreateViewModel();

        viewModel.SetInputPath("/tmp/source.mov");

        Assert.Equal(WorkspaceView.Review, viewModel.CurrentView);
        Assert.True(viewModel.IsReviewViewActive);
    }

    [Fact]
    public async Task AnalysisCompletion_ReturnsToReviewFromAnyView()
    {
        var viewModel = CreateViewModel();
        viewModel.SetInputPath("/tmp/source.mov");
        viewModel.ShowView(WorkspaceView.NewCut);

        await viewModel.AnalyzeAsync();

        Assert.Equal(WorkspaceView.Review, viewModel.CurrentView);
    }

    [Fact]
    public async Task Navigation_DoesNotDestroyReviewState()
    {
        var viewModel = CreateViewModel();
        viewModel.SetInputPath("/tmp/source.mov");
        await viewModel.AnalyzeAsync();
        viewModel.AddManualCut(new TimeRange(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)));

        viewModel.ShowView(WorkspaceView.History);
        viewModel.ShowView(WorkspaceView.Help);
        viewModel.ShowView(WorkspaceView.NewCut);
        viewModel.ShowView(WorkspaceView.Review);

        Assert.Single(viewModel.CutCandidates);
        Assert.Equal("Manual cut", viewModel.CutCandidates[0].Reason);
        Assert.Equal(WorkspaceView.Review, viewModel.CurrentView);
    }

    [Fact]
    public void LoadSelectedHistory_ActivatesReviewView()
    {
        var history = new InMemoryHistoryStore(new[] { CreateHistoryEntry() });
        var viewModel = CreateViewModel(history);
        viewModel.SelectedHistoryEntry = Assert.Single(viewModel.HistoryEntries);
        viewModel.ShowView(WorkspaceView.History);

        viewModel.LoadSelectedHistory();

        Assert.Equal(WorkspaceView.Review, viewModel.CurrentView);
        Assert.Single(viewModel.CutCandidates);
    }

    [Fact]
    public async Task ExportTray_FollowsJobAndPausedStates()
    {
        var checkpointStore = new InMemoryExportCheckpointStore();
        var exporter = new BlockingCheckpointExporter();
        var viewModel = CreateViewModel(checkpointStore: checkpointStore, exporter: exporter);
        viewModel.SetInputPath("/tmp/pause-source.mp4");
        await viewModel.AnalyzeAsync();

        Assert.False(viewModel.ShowExportTray);

        var exportTask = viewModel.ExportAsync();
        await exporter.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(viewModel.ShowExportTray);
        Assert.True(viewModel.ShowExportOverlay);

        viewModel.PauseOperation();
        await exportTask;

        Assert.Contains("paused", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(viewModel.PausedExports);
        Assert.True(viewModel.ShowExportTray);

        viewModel.SelectedPausedExport = viewModel.PausedExports[0];
        viewModel.DiscardSelectedExport();

        Assert.False(viewModel.ShowExportTray);
    }

    [Fact]
    public void OutputFormat_SwitchesExtensionWithoutChangingLocation()
    {
        var viewModel = CreateViewModel();
        viewModel.SetInputPath("/tmp/renders/source.mov");
        var directory = Path.GetDirectoryName(viewModel.OutputPathDisplay);

        Assert.True(viewModel.IsMp4OutputFormat);
        Assert.False(viewModel.IsMovOutputFormat);
        Assert.EndsWith("source.trimmed.mp4", viewModel.OutputPathDisplay, StringComparison.Ordinal);

        viewModel.SetOutputFormat(".mov");

        Assert.True(viewModel.IsMovOutputFormat);
        Assert.EndsWith("source.trimmed.mov", viewModel.OutputPathDisplay, StringComparison.Ordinal);
        Assert.Equal(directory, Path.GetDirectoryName(viewModel.OutputPathDisplay));

        viewModel.SetOutputFormat(".mp4");

        Assert.True(viewModel.IsMp4OutputFormat);
        Assert.EndsWith("source.trimmed.mp4", viewModel.OutputPathDisplay, StringComparison.Ordinal);
    }

    private static MainWindowViewModel CreateViewModel(
        IHistoryStore? historyStore = null,
        IExportCheckpointStore? checkpointStore = null,
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
            historyStore ?? new InMemoryHistoryStore(),
            new InMemoryCustomPresetStore(),
            checkpointStore ?? new InMemoryExportCheckpointStore());
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
