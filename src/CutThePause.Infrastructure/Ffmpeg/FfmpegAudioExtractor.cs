using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Ffmpeg;

public sealed class FfmpegAudioExtractor : IAudioExtractor
{
    private readonly IFfmpegLocator _locator;
    private readonly IFfmpegRunner _runner;

    public FfmpegAudioExtractor(IFfmpegLocator locator, IFfmpegRunner runner)
    {
        _locator = locator;
        _runner = runner;
    }

    public async Task<PcmAudioData> ExtractAsync(string inputPath, CancellationToken cancellationToken)
    {
        var binaries = await _locator.LocateAsync(cancellationToken).ConfigureAwait(false);
        var arguments = new[]
        {
            "-hide_banner",
            "-loglevel",
            "error",
            "-i",
            inputPath,
            "-vn",
            "-ac",
            "1",
            "-ar",
            "16000",
            "-f",
            "f32le",
            "pipe:1"
        };

        var result = await _runner.RunBinaryAsync(binaries.FfmpegPath, arguments, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg audio extraction failed: {result.StandardError}");
        }

        var samples = new float[result.StandardOutput.Length / sizeof(float)];
        Buffer.BlockCopy(result.StandardOutput, 0, samples, 0, result.StandardOutput.Length);

        return new PcmAudioData(samples, 16000);
    }
}
