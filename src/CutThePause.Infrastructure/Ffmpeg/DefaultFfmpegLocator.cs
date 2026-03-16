using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Ffmpeg;

public sealed class DefaultFfmpegLocator : IFfmpegLocator
{
    private const string FfmpegEnvironmentVariable = "CUTTHEPAUSE_FFMPEG_PATH";
    private const string FfprobeEnvironmentVariable = "CUTTHEPAUSE_FFPROBE_PATH";

    public ValueTask<FfmpegBinaries> LocateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ffmpegPath = ResolveExplicitBinary(FfmpegEnvironmentVariable)
            ?? ResolveBundledBinary("ffmpeg")
            ?? ResolveFromPath("ffmpeg");

        if (ffmpegPath is null)
        {
            throw new FileNotFoundException(
                "FFmpeg was not found. Install it on your machine or set CUTTHEPAUSE_FFMPEG_PATH.");
        }

        var ffprobePath = ResolveExplicitBinary(FfprobeEnvironmentVariable)
            ?? ResolveSiblingBinary(ffmpegPath, "ffprobe")
            ?? ResolveBundledBinary("ffprobe")
            ?? ResolveFromPath("ffprobe");

        return ValueTask.FromResult(new FfmpegBinaries(ffmpegPath, ffprobePath));
    }

    private static string? ResolveExplicitBinary(string environmentVariable)
    {
        var configuredPath = Environment.GetEnvironmentVariable(environmentVariable);
        return !string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath) ? configuredPath : null;
    }

    private static string? ResolveBundledBinary(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(AppContext.BaseDirectory, "Tools", fileName),
            Path.Combine(AppContext.BaseDirectory, "Resources", fileName),
            Path.Combine(Environment.CurrentDirectory, fileName),
            Path.Combine(Environment.CurrentDirectory, "tools", fileName)
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? ResolveSiblingBinary(string ffmpegPath, string siblingFileName)
    {
        var directory = Path.GetDirectoryName(ffmpegPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var path = Path.Combine(directory, siblingFileName);
        return File.Exists(path) ? path : null;
    }

    private static string? ResolveFromPath(string fileName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
