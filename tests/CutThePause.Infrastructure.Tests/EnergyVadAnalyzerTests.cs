using System.Runtime.InteropServices;
using CutThePause.Core.Models;
using CutThePause.Infrastructure.Models;
using CutThePause.Infrastructure.Vad;

namespace CutThePause.Infrastructure.Tests;

public sealed class EnergyVadAnalyzerTests
{
    [Fact]
    public async Task DetectSpeechAsync_FindsSyntheticSpeechWindow()
    {
        const int sampleRate = 16000;
        var silence = new float[sampleRate];
        var speech = Enumerable.Range(0, sampleRate)
            .Select(static index => MathF.Sin(index / 10f) * 0.25f)
            .ToArray();

        var samples = silence.Concat(speech).Concat(silence).ToArray();
        var analyzer = new EnergyVadAnalyzer();

        var result = await analyzer.DetectSpeechAsync(
            new PcmAudioData(samples, sampleRate),
            new AnalysisSettings
            {
                MinSpeechMs = 150
            },
            CancellationToken.None);

        var segment = Assert.Single(result.SpeechSegments);
        Assert.InRange(segment.Start.TotalSeconds, 0.9, 1.1);
        Assert.InRange(segment.End.TotalSeconds, 1.9, 2.2);
    }

    [Fact]
    public async Task DetectSpeechAsync_ReadsFileBackedAudioWithoutLoadingTheWholeStream()
    {
        const int sampleRate = 16_000;
        var silence = new float[sampleRate];
        var speech = Enumerable.Range(0, sampleRate)
            .Select(static index => MathF.Sin(index / 10f) * 0.25f)
            .ToArray();
        var samples = silence.Concat(speech).Concat(silence).ToArray();
        var path = Path.Combine(Path.GetTempPath(), $"cut-the-pause-vad-{Guid.NewGuid():N}.f32le");

        await File.WriteAllBytesAsync(path, MemoryMarshal.AsBytes(samples.AsSpan()).ToArray());
        try
        {
            await using (var audio = new PcmAudioFile(path, sampleRate, samples.LongLength))
            {
                var result = await new EnergyVadAnalyzer().DetectSpeechAsync(
                    audio,
                    new AnalysisSettings { MinSpeechMs = 150 },
                    CancellationToken.None);

                var segment = Assert.Single(result.SpeechSegments);
                Assert.InRange(segment.Start.TotalSeconds, 0.9, 1.1);
                Assert.InRange(segment.End.TotalSeconds, 1.9, 2.2);
                Assert.True(File.Exists(path));
            }

            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
