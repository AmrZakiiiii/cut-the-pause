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
}
