using CutThePause.Core.Models;
using CutThePause.Core.Services;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Waveform;

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
        CancellationToken cancellationToken,
        IProgress<AnalysisProgress>? progress = null)
    {
        progress?.Report(new AnalysisProgress("Reading video metadata..."));
        var metadata = await _metadataReader.ReadAsync(inputPath, cancellationToken).ConfigureAwait(false);

        progress?.Report(new AnalysisProgress("Extracting audio..."));
        VadAnalysisResult vadResult;
        if (_audioExtractor is IChunkedAudioExtractor chunkedAudioExtractor &&
            _vadAnalyzer is IStreamingVadAnalyzer streamingVadAnalyzer)
        {
            await using var audioFile = await chunkedAudioExtractor
                .ExtractToFileAsync(inputPath, cancellationToken)
                .ConfigureAwait(false);

            progress?.Report(new AnalysisProgress("Detecting speech from streamed audio..."));
            vadResult = await Task.Run(
                () => streamingVadAnalyzer.DetectSpeechAsync(audioFile, settings, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            progress?.Report(new AnalysisProgress("Building waveform and review..."));
            var waveformPeaks = await Task.Run(
                () => WaveformPeakBuilder.Build(audioFile, WaveformPeakBuilder.DefaultPeakCount, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            progress?.Report(new AnalysisProgress("Building review..."));
            return CutPlanBuilder.Build(
                inputPath,
                metadata.Duration,
                vadResult.SpeechSegments,
                settings,
                warnings: AddWarnings(vadResult),
                waveformPeaks);
        }
        else
        {
            var audio = await _audioExtractor.ExtractAsync(inputPath, cancellationToken).ConfigureAwait(false);

            progress?.Report(new AnalysisProgress("Detecting speech..."));
            vadResult = await Task.Run(
                () => _vadAnalyzer.DetectSpeechAsync(audio, settings, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            progress?.Report(new AnalysisProgress("Building waveform and review..."));
            var waveformPeaks = await Task.Run(
                () => WaveformPeakBuilder.Build(audio, WaveformPeakBuilder.DefaultPeakCount, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            progress?.Report(new AnalysisProgress("Building review..."));
            return CutPlanBuilder.Build(
                inputPath,
                metadata.Duration,
                vadResult.SpeechSegments,
                settings,
                warnings: AddWarnings(vadResult),
                waveformPeaks);
        }
    }

    public Task ExportAsync(
        ExportRequest request,
        IProgress<VideoExportProgress>? progress,
        CancellationToken cancellationToken,
        ExportExecutionOptions? executionOptions = null)
        => _videoExporter.ExportAsync(request, progress, cancellationToken, executionOptions);

    private static IReadOnlyList<string> AddWarnings(VadAnalysisResult vadResult)
    {
        var warnings = vadResult.Warnings.ToList();
        if (vadResult.SpeechSegments.Count == 0)
        {
            warnings.Add("No speech segments were detected, so the current analysis keeps the full video to avoid destructive edits.");
        }

        return warnings;
    }
}
