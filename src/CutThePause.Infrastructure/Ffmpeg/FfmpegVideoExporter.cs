using CutThePause.Core.Models;
using CutThePause.Infrastructure.Abstractions;

namespace CutThePause.Infrastructure.Ffmpeg;

public sealed class FfmpegVideoExporter : IVideoExporter
{
    private readonly IFfmpegLocator _locator;
    private readonly IFfmpegRunner _runner;

    public FfmpegVideoExporter(IFfmpegLocator locator, IFfmpegRunner runner)
    {
        _locator = locator;
        _runner = runner;
    }

    public async Task ExportAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        var binaries = await _locator.LocateAsync(cancellationToken).ConfigureAwait(false);
        var commandPlan = FfmpegExportCommandBuilder.Build(request, preferHardwareAcceleration: OperatingSystem.IsMacOS());
        var result = await _runner.RunAsync(binaries.FfmpegPath, commandPlan.Arguments, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            return;
        }

        if (commandPlan.UsesHardwareAcceleration)
        {
            var fallbackPlan = FfmpegExportCommandBuilder.Build(request);
            var fallbackResult = await _runner.RunAsync(binaries.FfmpegPath, fallbackPlan.Arguments, cancellationToken).ConfigureAwait(false);

            if (fallbackResult.ExitCode == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"FFmpeg export failed with hardware acceleration and software fallback. Hardware: {result.StandardError} Software: {fallbackResult.StandardError}");
        }

        throw new InvalidOperationException($"FFmpeg export failed: {result.StandardError}");
    }
}
