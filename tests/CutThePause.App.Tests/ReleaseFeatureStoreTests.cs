using CutThePause.App.Services;
using CutThePause.Core.Models;

namespace CutThePause.App.Tests;

public sealed class ReleaseFeatureStoreTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"cut-the-pause-release-stores-{Guid.NewGuid():N}");

    public ReleaseFeatureStoreTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void HistoryStore_RoundTripsNewestEntriesAndAppliesLimit()
    {
        var store = new JsonHistoryStore(Path.Combine(_tempDirectory, "history.json"), maxEntries: 2);

        Assert.True(store.Append(CreateHistory("one")));
        Assert.True(store.Append(CreateHistory("two")));
        Assert.True(store.Append(CreateHistory("three")));

        var entries = store.Load();

        Assert.Equal(new[] { "three", "two" }, entries.Select(entry => entry.Id));
    }

    [Fact]
    public void HistoryStore_MalformedFileReturnsEmptyHistory()
    {
        var path = Path.Combine(_tempDirectory, "history.json");
        File.WriteAllText(path, "not-json");

        Assert.Empty(new JsonHistoryStore(path).Load());
    }

    [Fact]
    public void CustomPresetStore_ReplacesNamesCaseInsensitively()
    {
        var store = new JsonCustomPresetStore(Path.Combine(_tempDirectory, "presets.json"));

        Assert.True(store.Upsert(new NamedPreset("Voice Cleanup", 120, 150, 0, 0, 0.5f, ExportPreset.Balanced)));
        Assert.True(store.Upsert(new NamedPreset(" voice cleanup ", 200, 150, 10, 20, 0.6f, ExportPreset.SmallerFile)));

        var presets = store.Load();

        var preset = Assert.Single(presets);
        Assert.Equal("voice cleanup", preset.Name);
        Assert.Equal(200, preset.MinSilenceMs);
        Assert.Equal(ExportPreset.SmallerFile, preset.ExportPreset);
    }

    [Fact]
    public void CheckpointStore_RoundTripsAndFindsByFingerprint()
    {
        var path = Path.Combine(_tempDirectory, "checkpoints.json");
        var workingDirectory = Path.Combine(_tempDirectory, "job");
        Directory.CreateDirectory(workingDirectory);
        var checkpoint = CreateCheckpoint(workingDirectory, "fingerprint");
        var store = new JsonExportCheckpointStore(path);

        Assert.True(store.Save(checkpoint));

        var loaded = store.Find("fingerprint");

        Assert.NotNull(loaded);
        Assert.Equal(checkpoint.JobId, loaded!.JobId);
        Assert.Equal(checkpoint.Request.InputPath, loaded.Request.InputPath);
        Assert.Equal(checkpoint.Request.OutputPath, loaded.Request.OutputPath);
        Assert.Equal(checkpoint.Request.SourceDuration, loaded.Request.SourceDuration);
        Assert.Equal(checkpoint.Request.Preset, loaded.Request.Preset);
        Assert.Equal(checkpoint.Request.CutCandidates, loaded.Request.CutCandidates);
        Assert.Equal(checkpoint.Request.KeepSegments, loaded.Request.KeepSegments);
        Assert.Equal(checkpoint.CompletedBatchIndexes, loaded.CompletedBatchIndexes);
    }

    [Fact]
    public void CheckpointStore_DeleteDoesNotDeleteFilesOutsideWorkingDirectory()
    {
        var path = Path.Combine(_tempDirectory, "checkpoints.json");
        var workingDirectory = Path.Combine(_tempDirectory, "job");
        Directory.CreateDirectory(workingDirectory);
        var insideFile = Path.Combine(workingDirectory, "batch-0001.mp4");
        var outsideFile = Path.Combine(_tempDirectory, "important.txt");
        File.WriteAllText(insideFile, "generated");
        File.WriteAllText(outsideFile, "keep");
        var checkpoint = CreateCheckpoint(workingDirectory, "fingerprint") with
        {
            BatchPaths = new[] { insideFile, outsideFile }
        };
        var store = new JsonExportCheckpointStore(path);
        store.Save(checkpoint);

        Assert.True(store.Delete(checkpoint));
        Assert.False(File.Exists(insideFile));
        Assert.True(File.Exists(outsideFile));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static HistoryEntry CreateHistory(string id) => new(
        id,
        DateTimeOffset.UtcNow,
        HistoryEntryKind.Analysis,
        HistoryEntryStatus.Completed,
        $"{id}.mp4",
        null,
        id,
        null,
        null,
        "done");

    private static ExportCheckpoint CreateCheckpoint(string workingDirectory, string fingerprint) => new(
        Guid.NewGuid().ToString("N"),
        fingerprint,
        new ExportRequest(
            "input.mp4",
            "output.mp4",
            TimeSpan.FromSeconds(10),
            ExportPreset.Balanced,
            new[] { new CutCandidate(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "Detected silence") },
            new[] { new KeepSegment(0, TimeSpan.Zero, TimeSpan.FromSeconds(2)), new KeepSegment(1, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10)) }),
        workingDirectory,
        Array.Empty<string>(),
        Array.Empty<int>(),
        DateTimeOffset.UtcNow,
        "Software HEVC Main 10");
}
