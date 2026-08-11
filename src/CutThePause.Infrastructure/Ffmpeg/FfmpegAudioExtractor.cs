using System.Runtime.InteropServices;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Ffmpeg;

public sealed class FfmpegAudioExtractor : IAudioExtractor, IChunkedAudioExtractor
{
    private const int SampleRate = 16_000;
    private readonly IFfmpegLocator _locator;
    private readonly IFfmpegRunner _runner;

    public FfmpegAudioExtractor(IFfmpegLocator locator, IFfmpegRunner runner)
    {
        _locator = locator;
        _runner = runner;
    }

    public async Task<PcmAudioData> ExtractAsync(string inputPath, CancellationToken cancellationToken)
    {
        await using var audioFile = await ExtractToFileAsync(inputPath, cancellationToken).ConfigureAwait(false);
        if (audioFile.SampleCount > int.MaxValue)
        {
            throw new InvalidOperationException("The extracted PCM audio is too large for the in-memory compatibility path.");
        }

        var samples = new float[(int)audioFile.SampleCount];
        await using var stream = audioFile.OpenRead();
        await Task.Run(
            () => stream.ReadExactly(MemoryMarshal.AsBytes(samples.AsSpan())),
            cancellationToken).ConfigureAwait(false);

        return new PcmAudioData(samples, audioFile.SampleRate);
    }

    public async Task<PcmAudioFile> ExtractToFileAsync(string inputPath, CancellationToken cancellationToken)
    {
        var binaries = await _locator.LocateAsync(cancellationToken).ConfigureAwait(false);
        var temporaryAudioPath = Path.Combine(
            Path.GetTempPath(),
            $"cut-the-pause-audio-{Guid.NewGuid():N}.f32le");
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
            SampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-f",
            "f32le",
            temporaryAudioPath
        };

        var ownershipTransferred = false;
        try
        {
            var result = await _runner.RunAsync(binaries.FfmpegPath, arguments, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"FFmpeg audio extraction failed: {result.StandardError}");
            }

            if (!File.Exists(temporaryAudioPath))
            {
                throw new InvalidOperationException("FFmpeg did not produce an audio stream.");
            }

            var byteLength = new FileInfo(temporaryAudioPath).Length;
            if (byteLength % sizeof(float) != 0)
            {
                throw new InvalidOperationException("FFmpeg produced an invalid or oversized PCM audio stream.");
            }

            var audioFile = new PcmAudioFile(temporaryAudioPath, SampleRate, byteLength / sizeof(float));
            ownershipTransferred = true;
            return audioFile;
        }
        finally
        {
            if (!ownershipTransferred)
            {
                TryDeleteTemporaryAudio(temporaryAudioPath);
            }
        }
    }

    private static void TryDeleteTemporaryAudio(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
