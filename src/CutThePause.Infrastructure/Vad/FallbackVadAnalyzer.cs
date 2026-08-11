using CutThePause.Core.Models;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;
using Microsoft.ML.OnnxRuntime;

namespace CutThePause.Infrastructure.Vad;

public sealed class FallbackVadAnalyzer : IVadAnalyzer, IStreamingVadAnalyzer
{
    private readonly IVadAnalyzer _primaryAnalyzer;
    private readonly IVadAnalyzer _fallbackAnalyzer;

    public FallbackVadAnalyzer(IVadAnalyzer primaryAnalyzer, IVadAnalyzer fallbackAnalyzer)
    {
        _primaryAnalyzer = primaryAnalyzer;
        _fallbackAnalyzer = fallbackAnalyzer;
    }

    public async Task<VadAnalysisResult> DetectSpeechAsync(
        PcmAudioData audio,
        AnalysisSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _primaryAnalyzer.DetectSpeechAsync(audio, settings, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or InvalidOperationException or OnnxRuntimeException or DllNotFoundException or TypeInitializationException)
        {
            var fallbackResult = await _fallbackAnalyzer.DetectSpeechAsync(audio, settings, cancellationToken).ConfigureAwait(false);
            return AddFallbackWarning(fallbackResult, exception);
        }
    }

    public async Task<VadAnalysisResult> DetectSpeechAsync(
        PcmAudioFile audio,
        AnalysisSettings settings,
        CancellationToken cancellationToken)
    {
        if (_primaryAnalyzer is not IStreamingVadAnalyzer primaryAnalyzer ||
            _fallbackAnalyzer is not IStreamingVadAnalyzer fallbackAnalyzer)
        {
            throw new InvalidOperationException("The configured VAD analyzers do not support file-backed audio.");
        }

        try
        {
            return await primaryAnalyzer.DetectSpeechAsync(audio, settings, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or InvalidOperationException or OnnxRuntimeException or DllNotFoundException or TypeInitializationException)
        {
            var fallbackResult = await fallbackAnalyzer.DetectSpeechAsync(audio, settings, cancellationToken).ConfigureAwait(false);
            return AddFallbackWarning(fallbackResult, exception);
        }
    }

    private static VadAnalysisResult AddFallbackWarning(VadAnalysisResult fallbackResult, Exception exception)
    {
        var warnings = fallbackResult.Warnings
            .Append($"Primary Silero VAD could not run: {exception.Message}")
            .Distinct()
            .ToArray();

        return fallbackResult with { Warnings = warnings };
    }
}
