using CutThePause.Core.Models;

namespace CutThePause.Infrastructure.Abstractions;

public interface IVideoExporter
{
    Task ExportAsync(ExportRequest request, CancellationToken cancellationToken);
}
