using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Abstractions;

public interface IChunkedAudioExtractor
{
    Task<PcmAudioFile> ExtractToFileAsync(string inputPath, CancellationToken cancellationToken);
}
