using CutThePause.Core.Models;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Abstractions;

public interface IVadAnalyzer
{
    Task<VadAnalysisResult> DetectSpeechAsync(PcmAudioData audio, AnalysisSettings settings, CancellationToken cancellationToken);
}
