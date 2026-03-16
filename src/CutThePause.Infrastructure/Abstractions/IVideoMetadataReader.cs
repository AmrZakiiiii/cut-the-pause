using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Abstractions;

public interface IVideoMetadataReader
{
    Task<VideoMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken);
}
