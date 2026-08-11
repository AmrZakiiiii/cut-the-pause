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
        if (request.KeepSegments.Count == 0)
        {
            throw new InvalidOperationException("There are no enabled keep segments to export.");
        }

        EnsureDistinctInputAndOutput(request);

        var temporaryOutputPath = CreateTemporaryOutputPath(request.OutputPath);

        try
        {
            var outputDirectory = Path.GetDirectoryName(request.OutputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            EnsureSufficientFreeSpace(request);

            var binaries = await _locator.LocateAsync(cancellationToken).ConfigureAwait(false);
            var preferHardwareAcceleration = OperatingSystem.IsMacOS() && !IsMovOutput(request.OutputPath);
            var attempt = await RenderBatchesAsync(
                binaries.FfmpegPath,
                request,
                temporaryOutputPath,
                preferHardwareAcceleration,
                progress,
                cancellationToken).ConfigureAwait(false);

            if (attempt.Succeeded)
            {
                EnsureOutputSizeIsAcceptable(request, temporaryOutputPath);
                CommitTemporaryOutput(temporaryOutputPath, request.OutputPath);
                ReportProgress(progress, request, 1d, request.OutputDuration, "Finalizing export...", attempt.EncoderLabel);
                return;
            }

            if (preferHardwareAcceleration)
            {
                TryDeleteTemporaryOutput(temporaryOutputPath);
                ReportProgress(
                    progress,
                    request,
                    0d,
                    TimeSpan.Zero,
                    "Retrying with software HEVC Main 10...",
                    "Software HEVC Main 10");

                var fallbackAttempt = await RenderBatchesAsync(
                    binaries.FfmpegPath,
                    request,
                    temporaryOutputPath,
                    preferHardwareAcceleration: false,
                    progress,
                    cancellationToken).ConfigureAwait(false);

                if (fallbackAttempt.Succeeded)
                {
                    EnsureOutputSizeIsAcceptable(request, temporaryOutputPath);
                    CommitTemporaryOutput(temporaryOutputPath, request.OutputPath);
                    ReportProgress(progress, request, 1d, request.OutputDuration, "Finalizing export...", fallbackAttempt.EncoderLabel);
                    return;
                }

                throw new InvalidOperationException(
                    $"FFmpeg export failed with hardware acceleration and software fallback. Hardware: {attempt.StandardError} Software: {fallbackAttempt.StandardError}");
            }

            throw new InvalidOperationException($"FFmpeg export failed: {attempt.StandardError}");
        }
        finally
        {
            TryDeleteTemporaryOutput(temporaryOutputPath);
        }
    }

    private async Task<RenderAttempt> RenderBatchesAsync(
        string ffmpegPath,
        ExportRequest request,
        string temporaryOutputPath,
        bool preferHardwareAcceleration,
        IProgress<VideoExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        var batchPaths = new List<string>();
        var attemptId = Guid.NewGuid().ToString("N");
        var batchCount = (request.KeepSegments.Count + FfmpegExportCommandBuilder.MaxSegmentsPerCommand - 1)
            / FfmpegExportCommandBuilder.MaxSegmentsPerCommand;
        var completedDuration = TimeSpan.Zero;
        var encoderLabel = preferHardwareAcceleration ? "VideoToolbox HEVC Main 10" : "Software HEVC Main 10";
        string? concatListPath = null;

        try
        {
            for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batchSegments = request.KeepSegments
                    .Skip(batchIndex * FfmpegExportCommandBuilder.MaxSegmentsPerCommand)
                    .Take(FfmpegExportCommandBuilder.MaxSegmentsPerCommand)
                    .ToArray();
                var batchPath = CreateBatchOutputPath(request.OutputPath, attemptId, batchIndex);
                batchPaths.Add(batchPath);

                var batchRequest = request with
                {
                    KeepSegments = batchSegments,
                    OutputPath = batchPath
                };
                var commandPlan = FfmpegExportCommandBuilder.Build(batchRequest, preferHardwareAcceleration);
                encoderLabel = commandPlan.EncoderLabel;
                ReportProgress(
                    progress,
                    request,
                    FractionForDuration(completedDuration, request.OutputDuration),
                    completedDuration,
                    $"Preparing batch {batchIndex + 1} of {batchCount}...",
                    commandPlan.EncoderLabel);

                var result = await _runner.RunWithProgressAsync(
                    ffmpegPath,
                    commandPlan.Arguments,
                    line => OnBatchProgressLine(
                        line,
                        request,
                        batchRequest,
                        commandPlan,
                        progress,
                        completedDuration,
                        batchIndex,
                        batchCount),
                    cancellationToken).ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    return new RenderAttempt(false, result.StandardError, commandPlan.EncoderLabel);
                }

                completedDuration += batchRequest.OutputDuration;
            }

            cancellationToken.ThrowIfCancellationRequested();
            concatListPath = CreateConcatListPath(request.OutputPath, attemptId);
            await WriteConcatListAsync(concatListPath, batchPaths, cancellationToken).ConfigureAwait(false);

            ReportProgress(
                progress,
                request,
                Math.Clamp(FractionForDuration(completedDuration, request.OutputDuration), 0d, 0.98d),
                completedDuration,
                "Joining rendered batches...",
                encoderLabel);

            var concatResult = await _runner.RunAsync(
                ffmpegPath,
                FfmpegConcatCommandBuilder.Build(concatListPath, temporaryOutputPath),
                cancellationToken).ConfigureAwait(false);
            if (concatResult.ExitCode != 0)
            {
                return new RenderAttempt(false, concatResult.StandardError, encoderLabel);
            }

            return new RenderAttempt(true, string.Empty, encoderLabel);
        }
        finally
        {
            foreach (var batchPath in batchPaths)
            {
                TryDeleteTemporaryOutput(batchPath);
            }

            if (concatListPath is not null)
            {
                TryDeleteTemporaryOutput(concatListPath);
            }
        }
    }

    private static async Task WriteConcatListAsync(
        string concatListPath,
        IReadOnlyList<string> batchPaths,
        CancellationToken cancellationToken)
    {
        var lines = batchPaths.Select(static path => $"file '{EscapeConcatPath(path)}'");
        await File.WriteAllLinesAsync(concatListPath, lines, cancellationToken).ConfigureAwait(false);
    }

    private static string EscapeConcatPath(string path) =>
        path.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);

    private static void OnBatchProgressLine(
        string line,
        ExportRequest request,
        ExportRequest batchRequest,
        ExportCommandPlan commandPlan,
        IProgress<VideoExportProgress>? progress,
        TimeSpan completedDuration,
        int batchIndex,
        int batchCount)
    {
        if (progress is null)
        {
            return;
        }

        if (TryParseEncodedDuration(line, out var encodedDuration))
        {
            var batchDuration = batchRequest.OutputDuration;
            var clampedBatchDuration = batchDuration > TimeSpan.Zero
                ? TimeSpan.FromTicks(Math.Min(encodedDuration.Ticks, batchDuration.Ticks))
                : TimeSpan.Zero;
            var totalDuration = request.OutputDuration;
            var overallDuration = completedDuration + clampedBatchDuration;
            var fraction = totalDuration > TimeSpan.Zero
                ? Math.Clamp(overallDuration.TotalSeconds / totalDuration.TotalSeconds, 0d, 1d)
                : 0d;

            ReportProgress(
                progress,
                request,
                fraction,
                overallDuration,
                $"Rendering batch {batchIndex + 1} of {batchCount}...",
                commandPlan.EncoderLabel);
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
            Math.Clamp(fractionComplete, 0d, 1d),
            encodedDuration,
            request.OutputDuration,
            stage,
            encoderLabel));
    }

    private static double FractionForDuration(TimeSpan duration, TimeSpan totalDuration) =>
        totalDuration > TimeSpan.Zero
            ? duration.TotalSeconds / totalDuration.TotalSeconds
            : 0d;

    private static void EnsureDistinctInputAndOutput(ExportRequest request)
    {
        var inputPath = Path.GetFullPath(request.InputPath);
        var outputPath = Path.GetFullPath(request.OutputPath);
        if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The export destination must be different from the source video.");
        }
    }

    private static void EnsureSufficientFreeSpace(ExportRequest request)
    {
        if (!File.Exists(request.InputPath) || request.OutputDuration <= TimeSpan.Zero || request.SourceDuration <= TimeSpan.Zero)
        {
            return;
        }

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(request.OutputPath));
        var root = Path.GetPathRoot(outputDirectory);
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        DriveInfo drive;
        try
        {
            drive = new DriveInfo(root);
        }
        catch (ArgumentException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        var sourceBytes = new FileInfo(request.InputPath).Length;
        var durationRatio = Math.Clamp(
            request.OutputDuration.TotalSeconds / request.SourceDuration.TotalSeconds,
            0.01d,
            1d);
        var formatMultiplier = IsMovOutput(request.OutputPath) ? 8d : 1.25d;
        var estimatedOutputBytes = sourceBytes * durationRatio * formatMultiplier;
        var estimatedWorkingBytes = (estimatedOutputBytes * 2.2d) + (256d * 1024d * 1024d);

        if (drive.AvailableFreeSpace < estimatedWorkingBytes)
        {
            throw new IOException(
                $"There is not enough free space for this export. The export needs approximately {FormatBytes(estimatedWorkingBytes)} available, but only {FormatBytes(drive.AvailableFreeSpace)} is free.");
        }
    }

    private static void EnsureOutputSizeIsAcceptable(ExportRequest request, string temporaryOutputPath)
    {
        if (IsMovOutput(request.OutputPath) ||
            request.SourceDuration < TimeSpan.FromMinutes(30) ||
            request.OutputDuration >= request.SourceDuration ||
            !File.Exists(request.InputPath) ||
            !File.Exists(temporaryOutputPath))
        {
            return;
        }

        var sourceBytes = new FileInfo(request.InputPath).Length;
        var outputBytes = new FileInfo(temporaryOutputPath).Length;
        if (outputBytes >= sourceBytes)
        {
            throw new InvalidOperationException(
                $"The quality-preserving MP4 render would be {FormatBytes(outputBytes)}, which is not smaller than the {FormatBytes(sourceBytes)} source. The file was not published; choose a smaller-file preset or review the cut list.");
        }
    }

    private static string FormatBytes(double bytes)
    {
        var units = new[] { "B", "KiB", "MiB", "GiB", "TiB" };
        var unitIndex = 0;
        while (bytes >= 1024d && unitIndex < units.Length - 1)
        {
            bytes /= 1024d;
            unitIndex++;
        }

        return $"{bytes:0.0} {units[unitIndex]}";
    }

    private static string CreateTemporaryOutputPath(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        var fileName = Path.GetFileNameWithoutExtension(outputPath);
        var extension = Path.GetExtension(outputPath);
        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.cut-the-pause{extension}");
    }

    private static string CreateBatchOutputPath(string outputPath, string attemptId, int batchIndex)
    {
        var directory = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        var fileName = Path.GetFileNameWithoutExtension(outputPath);
        var extension = Path.GetExtension(outputPath);
        return Path.Combine(directory, $".{fileName}.{attemptId}.batch-{batchIndex + 1:D4}{extension}");
    }

    private static string CreateConcatListPath(string outputPath, string attemptId)
    {
        var directory = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        var fileName = Path.GetFileNameWithoutExtension(outputPath);
        return Path.Combine(directory, $".{fileName}.{attemptId}.concat.txt");
    }

    private static bool IsMovOutput(string outputPath) =>
        Path.GetExtension(outputPath).Equals(".mov", StringComparison.OrdinalIgnoreCase);

    private static void CommitTemporaryOutput(string temporaryOutputPath, string outputPath) =>
        File.Move(temporaryOutputPath, outputPath, overwrite: true);

    private static void TryDeleteTemporaryOutput(string temporaryOutputPath)
    {
        try
        {
            if (File.Exists(temporaryOutputPath))
            {
                File.Delete(temporaryOutputPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record RenderAttempt(bool Succeeded, string StandardError, string EncoderLabel);
}
