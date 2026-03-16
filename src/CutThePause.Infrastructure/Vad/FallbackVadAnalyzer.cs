using CutThePause.Core.Models;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;
using Microsoft.ML.OnnxRuntime;

namespace CutThePause.Infrastructure.Vad;

public sealed class FallbackVadAnalyzer : IVadAnalyzer
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
            var warnings = fallbackResult.Warnings
                .Append($"Primary Silero VAD could not run: {exception.Message}")
                .Distinct()
                .ToArray();

            return fallbackResult with { Warnings = warnings };
        }
    }
}
