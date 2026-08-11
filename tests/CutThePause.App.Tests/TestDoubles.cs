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

    public void Save(AnalysisSettingsPreferences preferences) => LastSaved = preferences;
}

internal sealed class FixedMetadataReader : IVideoMetadataReader
{
    public Task<VideoMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken) =>
        Task.FromResult(new VideoMetadata(TimeSpan.FromSeconds(10)));
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

internal sealed class NoopVideoExporter : IVideoExporter
{
    public Task ExportAsync(
        ExportRequest request,
        IProgress<VideoExportProgress>? progress,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
