using CutThePause.App.Services;
using CutThePause.Core.Models;

namespace CutThePause.App.Tests;

public sealed class AnalysisSettingsStoreTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"cut-the-pause-settings-{Guid.NewGuid():N}");

    public AnalysisSettingsStoreTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

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

        Assert.True(store.Save(expected));

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

    [Fact]
    public void Save_ReturnsFalseWhenTheSettingsPathCannotBeCreated()
    {
        var blockingFile = Path.Combine(_tempDirectory, "not-a-directory");
        File.WriteAllText(blockingFile, "blocker");
        var store = new JsonAnalysisSettingsStore(Path.Combine(blockingFile, "settings.json"));

        var saved = store.Save(AnalysisSettingsPreferences.Defaults);

        Assert.False(saved);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
