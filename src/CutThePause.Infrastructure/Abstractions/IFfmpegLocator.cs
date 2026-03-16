using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Abstractions;

public interface IFfmpegLocator
{
    ValueTask<FfmpegBinaries> LocateAsync(CancellationToken cancellationToken);
}
