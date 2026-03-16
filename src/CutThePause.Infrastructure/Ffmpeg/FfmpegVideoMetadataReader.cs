using System.Globalization;
using System.Text.RegularExpressions;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Ffmpeg;

public sealed class FfmpegVideoMetadataReader : IVideoMetadataReader
{
    private readonly IFfmpegLocator _locator;
    private readonly IFfmpegRunner _runner;

    public FfmpegVideoMetadataReader(IFfmpegLocator locator, IFfmpegRunner runner)
    {
        _locator = locator;
        _runner = runner;
    }

    public async Task<VideoMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken)
    {
        var binaries = await _locator.LocateAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(binaries.FfprobePath))
        {
            var probeResult = await _runner.RunAsync(
                binaries.FfprobePath,
                new[]
                {
                    "-v",
                    "error",
                    "-show_entries",
                    "format=duration",
                    "-of",
                    "default=noprint_wrappers=1:nokey=1",
                    inputPath
                },
                cancellationToken).ConfigureAwait(false);

            if (probeResult.ExitCode == 0 &&
                double.TryParse(
                    probeResult.StandardOutput.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var seconds))
            {
                return new VideoMetadata(TimeSpan.FromSeconds(seconds));
            }
        }

        var ffmpegResult = await _runner.RunAsync(
            binaries.FfmpegPath,
            new[] { "-i", inputPath },
            cancellationToken).ConfigureAwait(false);

        var match = Regex.Match(ffmpegResult.StandardError, @"Duration:\s(?<value>\d{2}:\d{2}:\d{2}\.\d{2})");
        if (match.Success &&
            TimeSpan.TryParse(match.Groups["value"].Value, CultureInfo.InvariantCulture, out var duration))
        {
            return new VideoMetadata(duration);
        }

        throw new InvalidOperationException("Unable to determine video duration from FFmpeg/FFprobe output.");
    }
}
