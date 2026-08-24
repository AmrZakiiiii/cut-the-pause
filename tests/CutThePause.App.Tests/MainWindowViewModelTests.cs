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
    public void MovInput_SuggestsMp4DeliveryOutput()
    {
        var viewModel = CreateViewModel(new InMemoryAnalysisSettingsStore(AnalysisSettingsPreferences.Defaults));

        viewModel.SetInputPath("/tmp/source.mov");

        Assert.Equal("source.trimmed.mp4", viewModel.SuggestedOutputFileName);
        Assert.EndsWith("source.trimmed.mp4", viewModel.OutputPathDisplay, StringComparison.Ordinal);
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
    public void SettingsSurviveViewModelRecreationWithJsonStore()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cut-the-pause-view-model-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var settingsPath = Path.Combine(directory, "detection-settings.json");

        try
        {
            var firstViewModel = CreateViewModel(new JsonAnalysisSettingsStore(settingsPath));
            firstViewModel.MinSilenceMsText = "120";
            firstViewModel.MinSpeechMsText = "150";
            firstViewModel.PaddingBeforeMsText = "0";
            firstViewModel.PaddingAfterMsText = "0";
            firstViewModel.SelectedPreset = ExportPreset.HigherQuality;

            var recreatedViewModel = CreateViewModel(new JsonAnalysisSettingsStore(settingsPath));

            Assert.Equal("120", recreatedViewModel.MinSilenceMsText);
            Assert.Equal("150", recreatedViewModel.MinSpeechMsText);
            Assert.Equal("0", recreatedViewModel.PaddingBeforeMsText);
            Assert.Equal("0", recreatedViewModel.PaddingAfterMsText);
            Assert.Equal(ExportPreset.HigherQuality, recreatedViewModel.SelectedPreset);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void SettingsSaveFailure_IsShownToTheUser()
    {
        var viewModel = CreateViewModel(new FailingAnalysisSettingsStore());

        viewModel.MinSilenceMsText = "120";

        Assert.Contains("could not be saved", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
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
    public async Task AnalyzeAsync_NotifiesReviewBindingsWhenWaveformIsReady()
    {
        var viewModel = CreateViewModel(
            new InMemoryAnalysisSettingsStore(AnalysisSettingsPreferences.Defaults),
            audioExtractor: new WaveformAudioExtractor());
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        viewModel.SetInputPath("/tmp/waveform-source.mov");
        changedProperties.Clear();

        await viewModel.AnalyzeAsync();

        Assert.NotEmpty(viewModel.WaveformPeaks);
        Assert.Contains(nameof(MainWindowViewModel.WaveformPeaks), changedProperties);
        Assert.Contains(nameof(MainWindowViewModel.AnalysisDuration), changedProperties);
    }

    [Fact]
    public async Task TimelineExpansion_RequiresAnalysisAndCanBeClosed()
    {
        var viewModel = CreateViewModel(new InMemoryAnalysisSettingsStore(AnalysisSettingsPreferences.Defaults));

        viewModel.OpenTimeline();

        Assert.False(viewModel.IsTimelineExpanded);

        viewModel.SetInputPath("/tmp/timeline-source.mov");
        await viewModel.AnalyzeAsync();
        viewModel.OpenTimeline();

        Assert.True(viewModel.IsTimelineExpanded);

        viewModel.CloseTimeline();

        Assert.False(viewModel.IsTimelineExpanded);
    }

    [Fact]
    public async Task CancelOperation_CancelsAnalysisAndRestoresIdleState()
    {
        var vad = new DelayedVadAnalyzer();
        var viewModel = CreateViewModel(
            new InMemoryAnalysisSettingsStore(AnalysisSettingsPreferences.Defaults),
            new FixedMetadataReader(),
            vad);
        viewModel.SetInputPath("/tmp/long-video.mov");

        var analysisTask = viewModel.AnalyzeAsync();
        await vad.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(viewModel.CanCancelOperation);
        viewModel.CancelOperation();
        await analysisTask;

        Assert.Equal("Analysis canceled.", viewModel.StatusMessage);
        Assert.False(viewModel.IsAnalyzing);
        Assert.False(viewModel.CanCancelOperation);
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
        IAnalysisSettingsStore settingsStore,
        IVideoMetadataReader? metadataReader = null,
        IVadAnalyzer? vadAnalyzer = null,
        IAudioExtractor? audioExtractor = null)
    {
        var workflow = new VideoWorkflowService(
            metadataReader ?? new FixedMetadataReader(),
            audioExtractor ?? new FixedAudioExtractor(),
            vadAnalyzer ?? new FixedVadAnalyzer(),
            new NoopVideoExporter());

        return new MainWindowViewModel(workflow, settingsStore);
    }

    private sealed class WaveformAudioExtractor : IAudioExtractor
    {
        public Task<PcmAudioData> ExtractAsync(string inputPath, CancellationToken cancellationToken) =>
            Task.FromResult(new PcmAudioData(new[] { 0.1f, -0.5f, 0.2f, -1f }, 16_000));
    }

}
