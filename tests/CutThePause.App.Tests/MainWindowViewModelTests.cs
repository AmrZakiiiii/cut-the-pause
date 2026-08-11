using CutThePause.App.Services;
using CutThePause.App.ViewModels;
using CutThePause.Core.Models;
using CutThePause.Infrastructure;

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

    private static MainWindowViewModel CreateViewModel(InMemoryAnalysisSettingsStore settingsStore)
    {
        var workflow = new VideoWorkflowService(
            new FixedMetadataReader(),
            new FixedAudioExtractor(),
            new FixedVadAnalyzer(),
            new NoopVideoExporter());

        return new MainWindowViewModel(workflow, settingsStore);
    }
}
