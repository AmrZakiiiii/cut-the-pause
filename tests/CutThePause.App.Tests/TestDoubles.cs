using CutThePause.App.Services;
using CutThePause.Core.Models;
using CutThePause.Core.Services;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;

namespace CutThePause.App.Tests;

internal sealed class InMemoryAnalysisSettingsStore : IAnalysisSettingsStore
{
    public InMemoryAnalysisSettingsStore(AnalysisSettingsPreferences initial)
    {
        LastSaved = initial;
    }

    public AnalysisSettingsPreferences LastSaved { get; private set; }

    public AnalysisSettingsPreferences Load() => LastSaved;

    public bool Save(AnalysisSettingsPreferences preferences)
    {
        LastSaved = preferences;
        return true;
    }
}

internal sealed class FailingAnalysisSettingsStore : IAnalysisSettingsStore
{
    public AnalysisSettingsPreferences Load() => AnalysisSettingsPreferences.Defaults;

    public bool Save(AnalysisSettingsPreferences preferences) => false;
}

internal sealed class FixedMetadataReader : IVideoMetadataReader
{
    public Task<VideoMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken) =>
        Task.FromResult(new VideoMetadata(TimeSpan.FromSeconds(10)));
}

internal sealed class ThrowingMetadataReader : IVideoMetadataReader
{
    public Task<VideoMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("metadata failure");
}

internal sealed class FixedAudioExtractor : IAudioExtractor
{
    public Task<PcmAudioData> ExtractAsync(string inputPath, CancellationToken cancellationToken) =>
        Task.FromResult(new PcmAudioData(Array.Empty<float>(), 16_000));
}

internal sealed class FixedVadAnalyzer : IVadAnalyzer
{
    public Task<VadAnalysisResult> DetectSpeechAsync(
        PcmAudioData audio,
        AnalysisSettings settings,
        CancellationToken cancellationToken) =>
        Task.FromResult(new VadAnalysisResult(Array.Empty<SpeechSegment>(), Array.Empty<string>()));
}

internal sealed class DelayedVadAnalyzer : IVadAnalyzer
{
    public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<VadAnalysisResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<VadAnalysisResult> DetectSpeechAsync(
        PcmAudioData audio,
        AnalysisSettings settings,
        CancellationToken cancellationToken)
    {
        Started.TrySetResult(true);
        cancellationToken.Register(() => Completion.TrySetCanceled(cancellationToken));
        return Completion.Task;
    }
}

internal sealed class NoopVideoExporter : IVideoExporter
{
    public Task ExportAsync(
        ExportRequest request,
        IProgress<VideoExportProgress>? progress,
        CancellationToken cancellationToken,
        ExportExecutionOptions? executionOptions = null) =>
        Task.CompletedTask;
}

internal sealed class InMemoryHistoryStore : IHistoryStore
{
    private readonly List<HistoryEntry> _entries;

    public InMemoryHistoryStore(IEnumerable<HistoryEntry>? entries = null)
    {
        _entries = entries?.ToList() ?? new List<HistoryEntry>();
    }

    public IReadOnlyList<HistoryEntry> Load() => _entries.ToArray();

    public bool Append(HistoryEntry entry)
    {
        _entries.RemoveAll(existing => existing.Id == entry.Id);
        _entries.Insert(0, entry);
        return true;
    }

    public bool Remove(string id) => _entries.RemoveAll(entry => entry.Id == id) > 0;
}

internal sealed class InMemoryCustomPresetStore : ICustomPresetStore
{
    private readonly List<NamedPreset> _presets;

    public InMemoryCustomPresetStore(IEnumerable<NamedPreset>? presets = null)
    {
        _presets = presets?.ToList() ?? new List<NamedPreset>();
    }

    public IReadOnlyList<NamedPreset> Load() => _presets.ToArray();

    public bool Upsert(NamedPreset preset)
    {
        _presets.RemoveAll(existing => string.Equals(existing.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
        _presets.Insert(0, preset);
        return true;
    }

    public bool Delete(string name) => _presets.RemoveAll(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
}

internal sealed class InMemoryExportCheckpointStore : IExportCheckpointStore
{
    private readonly List<ExportCheckpoint> _checkpoints = new();

    public IReadOnlyList<ExportCheckpoint> Load() => _checkpoints.ToArray();

    public ExportCheckpoint? Find(string requestFingerprint) =>
        _checkpoints.FirstOrDefault(checkpoint => checkpoint.RequestFingerprint == requestFingerprint);

    public bool Save(ExportCheckpoint checkpoint)
    {
        _checkpoints.RemoveAll(existing => existing.JobId == checkpoint.JobId);
        _checkpoints.Insert(0, checkpoint);
        return true;
    }

    public bool Delete(ExportCheckpoint checkpoint) => _checkpoints.RemoveAll(existing => existing.JobId == checkpoint.JobId) > 0;
}

internal sealed class BlockingCheckpointExporter : IVideoExporter
{
    public TaskCompletionSource<bool> Started { get; private set; } = CreateSignal();

    public void ResetStarted() => Started = CreateSignal();

    public async Task ExportAsync(
        ExportRequest request,
        IProgress<VideoExportProgress>? progress,
        CancellationToken cancellationToken,
        ExportExecutionOptions? executionOptions = null)
    {
        var checkpoint = executionOptions?.Checkpoint ?? new ExportCheckpoint(
            Guid.NewGuid().ToString("N"),
            ExportRequestFingerprint.Compute(request),
            request,
            Path.Combine(Path.GetTempPath(), $"cut-the-pause-test-job-{Guid.NewGuid():N}"),
            Array.Empty<string>(),
            Array.Empty<int>(),
            DateTimeOffset.UtcNow,
            "Software HEVC Main 10");
        executionOptions?.CheckpointSaved?.Invoke(checkpoint);
        Started.TrySetResult(true);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private static TaskCompletionSource<bool> CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
