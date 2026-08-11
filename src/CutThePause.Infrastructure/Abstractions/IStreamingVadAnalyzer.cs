using CutThePause.Core.Models;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Abstractions;

public interface IStreamingVadAnalyzer
{
    Task<VadAnalysisResult> DetectSpeechAsync(
        PcmAudioFile audio,
        AnalysisSettings settings,
        CancellationToken cancellationToken);
}
