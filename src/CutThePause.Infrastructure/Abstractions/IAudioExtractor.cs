using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Abstractions;

public interface IAudioExtractor
{
    Task<PcmAudioData> ExtractAsync(string inputPath, CancellationToken cancellationToken);
}
