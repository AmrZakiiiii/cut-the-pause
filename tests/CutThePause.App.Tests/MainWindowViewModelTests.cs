using CutThePause.App.Services;
using CutThePause.App.ViewModels;
using CutThePause.Core.Models;
using CutThePause.Infrastructure;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;

namespace CutThePause.App.Tests;

public sealed class MainWindowViewModelTests
{
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
        viewModel.SelectedPreset = ExportPreset.HigherQuality;
        viewModel.SetInputPath("/tmp/first.mov");
        viewModel.SetInputPath("/tmp/second.mov");

        Assert.Equal("120", viewModel.MinSilenceMsText);
        Assert.Equal("0", viewModel.PaddingBeforeMsText);
        Assert.Equal("0", viewModel.PaddingAfterMsText);
        Assert.Equal(120, store.LastSaved.MinSilenceMs);
        Assert.Equal(ExportPreset.HigherQuality, store.LastSaved.ExportPreset);
    }

    [Fact]
    public void InvalidEdit_DoesNotOverwriteLastValidRecord()
    {
        var store = new InMemoryAnalysisSettingsStore(AnalysisSettingsPreferences.Defaults);
        var viewModel = CreateViewModel(store);

        viewModel.MinSilenceMsText = "120";
        viewModel.MinSilenceMsText = "not a number";

        Assert.Equal(120, store.LastSaved.MinSilenceMs);
    }

    [Fact]
    public void RevealSourceRequiresSelectedInput()
    {
        var viewModel = CreateViewModel(new InMemoryAnalysisSettingsStore(AnalysisSettingsPreferences.Defaults));

        Assert.False(viewModel.CanRevealSource);

        viewModel.SetInputPath("/tmp/source.mov");

        Assert.True(viewModel.CanRevealSource);
    }

    [Fact]
    public async Task AnalyzeAsync_ShowsAnalysisStateUntilWorkflowCompletes()
    {
        var vad = new DelayedVadAnalyzer();
        var viewModel = CreateViewModel(
            new InMemoryAnalysisSettingsStore(AnalysisSettingsPreferences.Defaults),
            new FixedMetadataReader(),
            vad);
        viewModel.SetInputPath("/tmp/long-video.mov");

        var analysisTask = viewModel.AnalyzeAsync();
        await vad.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForAsync(() => viewModel.AnalysisStageText.Contains("Detecting speech", StringComparison.Ordinal));

        Assert.True(viewModel.IsAnalyzing);
        Assert.True(viewModel.ShowAnalysisOverlay);
        Assert.False(viewModel.CanAnalyze);
        Assert.False(viewModel.CanChangeSource);

        vad.Completion.SetResult(new VadAnalysisResult(
            new[] { new SpeechSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)) },
            Array.Empty<string>()));
        await analysisTask;

        Assert.False(viewModel.IsAnalyzing);
        Assert.False(viewModel.ShowAnalysisOverlay);
    }

    [Fact]
    public async Task AnalyzeAsync_ClearsAnalysisStateWhenWorkflowFails()
    {
        var viewModel = CreateViewModel(
            new InMemoryAnalysisSettingsStore(AnalysisSettingsPreferences.Defaults),
            new ThrowingMetadataReader(),
            new FixedVadAnalyzer());
        viewModel.SetInputPath("/tmp/broken-video.mov");

        await viewModel.AnalyzeAsync();

        Assert.False(viewModel.IsAnalyzing);
        Assert.False(viewModel.ShowAnalysisOverlay);
        Assert.Equal("Analysis failed.", viewModel.StatusMessage);
    }

    private static MainWindowViewModel CreateViewModel(
        InMemoryAnalysisSettingsStore settingsStore,
        IVideoMetadataReader? metadataReader = null,
        IVadAnalyzer? vadAnalyzer = null)
    {
        var workflow = new VideoWorkflowService(
            metadataReader ?? new FixedMetadataReader(),
            new FixedAudioExtractor(),
            vadAnalyzer ?? new FixedVadAnalyzer(),
            new NoopVideoExporter());

        return new MainWindowViewModel(workflow, settingsStore);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }
}
