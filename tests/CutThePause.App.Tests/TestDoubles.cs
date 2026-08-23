using CutThePause.App.Services;
using CutThePause.Core.Models;
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
