using CutThePause.Core.Models;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;

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

    public async Task ExportAsync(
        ExportRequest request,
        IProgress<VideoExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        var binaries = await _locator.LocateAsync(cancellationToken).ConfigureAwait(false);
        var commandPlan = FfmpegExportCommandBuilder.Build(request, preferHardwareAcceleration: OperatingSystem.IsMacOS());
        ReportProgress(progress, request, 0d, TimeSpan.Zero, $"Preparing {commandPlan.EncoderLabel} export...", commandPlan.EncoderLabel);
        var result = await RunExportAsync(binaries.FfmpegPath, request, commandPlan, progress, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            ReportProgress(progress, request, 1d, request.OutputDuration, "Finalizing export...", commandPlan.EncoderLabel);
            return;
        }

        if (commandPlan.UsesHardwareAcceleration)
        {
            var fallbackPlan = FfmpegExportCommandBuilder.Build(request);
            ReportProgress(progress, request, 0d, TimeSpan.Zero, "Retrying with software H.264...", fallbackPlan.EncoderLabel);
            var fallbackResult = await RunExportAsync(binaries.FfmpegPath, request, fallbackPlan, progress, cancellationToken).ConfigureAwait(false);

            if (fallbackResult.ExitCode == 0)
            {
                ReportProgress(progress, request, 1d, request.OutputDuration, "Finalizing export...", fallbackPlan.EncoderLabel);
                return;
            }

            throw new InvalidOperationException(
                $"FFmpeg export failed with hardware acceleration and software fallback. Hardware: {result.StandardError} Software: {fallbackResult.StandardError}");
        }

        throw new InvalidOperationException($"FFmpeg export failed: {result.StandardError}");
    }

    private async Task<ProcessResult> RunExportAsync(
        string ffmpegPath,
        ExportRequest request,
        ExportCommandPlan commandPlan,
        IProgress<VideoExportProgress>? progress,
        CancellationToken cancellationToken) =>
        await _runner.RunWithProgressAsync(
            ffmpegPath,
            commandPlan.Arguments,
            line => OnProgressLine(line, request, commandPlan, progress),
            cancellationToken).ConfigureAwait(false);

    private static void OnProgressLine(
        string line,
        ExportRequest request,
        ExportCommandPlan commandPlan,
        IProgress<VideoExportProgress>? progress)
    {
        if (progress is null)
        {
            return;
        }

        if (TryParseEncodedDuration(line, out var encodedDuration))
        {
            var totalDuration = request.OutputDuration > TimeSpan.Zero ? request.OutputDuration : request.SourceDuration;
            var fraction = totalDuration > TimeSpan.Zero
                ? Math.Clamp(encodedDuration.TotalSeconds / totalDuration.TotalSeconds, 0d, 1d)
                : 0d;

            ReportProgress(progress, request, fraction, encodedDuration, "Rendering trimmed timeline...", commandPlan.EncoderLabel);
        }
        else if (line.Equals("progress=end", StringComparison.Ordinal))
        {
            ReportProgress(progress, request, 1d, request.OutputDuration, "Wrapping file...", commandPlan.EncoderLabel);
        }
    }

    private static bool TryParseEncodedDuration(string line, out TimeSpan encodedDuration)
    {
        encodedDuration = TimeSpan.Zero;

        if (line.StartsWith("out_time_ms=", StringComparison.Ordinal))
        {
            var value = line["out_time_ms=".Length..];
            if (long.TryParse(value, out var microseconds))
            {
                encodedDuration = TimeSpan.FromMilliseconds(microseconds / 1000d);
                return true;
            }
        }

        if (line.StartsWith("out_time_us=", StringComparison.Ordinal))
        {
            var value = line["out_time_us=".Length..];
            if (long.TryParse(value, out var microseconds))
            {
                encodedDuration = TimeSpan.FromMilliseconds(microseconds / 1000d);
                return true;
            }
        }

        if (line.StartsWith("out_time=", StringComparison.Ordinal))
        {
            var value = line["out_time=".Length..];
            if (TimeSpan.TryParse(value, out var parsed))
            {
                encodedDuration = parsed;
                return true;
            }
        }

        return false;
    }

    private static void ReportProgress(
        IProgress<VideoExportProgress>? progress,
        ExportRequest request,
        double fractionComplete,
        TimeSpan encodedDuration,
        string stage,
        string encoderLabel)
    {
        progress?.Report(new VideoExportProgress(
            fractionComplete,
            encodedDuration,
            request.OutputDuration,
            stage,
            encoderLabel));
    }
}
