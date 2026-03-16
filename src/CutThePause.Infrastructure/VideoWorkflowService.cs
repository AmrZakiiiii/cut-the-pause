using CutThePause.Core.Models;
using CutThePause.Core.Services;
using CutThePause.Infrastructure.Abstractions;

namespace CutThePause.Infrastructure;

public sealed class VideoWorkflowService
{
    private readonly IVideoMetadataReader _metadataReader;
    private readonly IAudioExtractor _audioExtractor;
    private readonly IVadAnalyzer _vadAnalyzer;
    private readonly IVideoExporter _videoExporter;

    public VideoWorkflowService(
        IVideoMetadataReader metadataReader,
        IAudioExtractor audioExtractor,
        IVadAnalyzer vadAnalyzer,
        IVideoExporter videoExporter)
    {
        _metadataReader = metadataReader;
        _audioExtractor = audioExtractor;
        _vadAnalyzer = vadAnalyzer;
        _videoExporter = videoExporter;
    }

    public async Task<AnalysisResult> AnalyzeAsync(
        string inputPath,
        AnalysisSettings settings,
        CancellationToken cancellationToken)
    {
        var metadata = await _metadataReader.ReadAsync(inputPath, cancellationToken).ConfigureAwait(false);
        var audio = await _audioExtractor.ExtractAsync(inputPath, cancellationToken).ConfigureAwait(false);
        var vadResult = await _vadAnalyzer.DetectSpeechAsync(audio, settings, cancellationToken).ConfigureAwait(false);

        var warnings = vadResult.Warnings.ToList();
        if (vadResult.SpeechSegments.Count == 0)
        {
            warnings.Add("No speech segments were detected, so the current analysis keeps the full video to avoid destructive edits.");
        }

        return CutPlanBuilder.Build(inputPath, metadata.Duration, vadResult.SpeechSegments, settings, warnings);
    }

    public Task ExportAsync(
        ExportRequest request,
        IProgress<VideoExportProgress>? progress,
        CancellationToken cancellationToken) =>
        _videoExporter.ExportAsync(request, progress, cancellationToken);
}
