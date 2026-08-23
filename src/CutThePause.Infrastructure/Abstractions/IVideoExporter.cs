using CutThePause.Core.Models;

namespace CutThePause.Infrastructure.Abstractions;

public interface IVideoExporter
{
    Task ExportAsync(
        ExportRequest request,
        IProgress<VideoExportProgress>? progress,
        CancellationToken cancellationToken,
        ExportExecutionOptions? executionOptions = null);
}
