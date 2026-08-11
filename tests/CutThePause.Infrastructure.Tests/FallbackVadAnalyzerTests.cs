using CutThePause.Core.Models;
using CutThePause.Infrastructure.Models;
using CutThePause.Infrastructure.Vad;

namespace CutThePause.Infrastructure.Tests;

public sealed class FallbackVadAnalyzerTests
{
    [Fact]
    public async Task FileBackedAnalysis_FallsBackWhenSileroModelIsUnavailable()
    {
        var audioPath = Path.Combine(
            Path.GetTempPath(),
            $"cut-the-pause-fallback-{Guid.NewGuid():N}.f32le");
        await File.WriteAllBytesAsync(audioPath, new byte[16_000 * sizeof(float)]);

        try
        {
            await using var audio = new PcmAudioFile(audioPath, 16_000, 16_000);
            var analyzer = new FallbackVadAnalyzer(
                new SileroVadAnalyzer(Path.Combine(audioPath, "missing-silero-vad.onnx")),
                new EnergyVadAnalyzer());

            var result = await analyzer.DetectSpeechAsync(
                audio,
                new AnalysisSettings(),
                CancellationToken.None);

            Assert.Contains(result.Warnings, warning => warning.Contains("Primary Silero VAD could not run", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(audioPath))
            {
                File.Delete(audioPath);
            }
        }
    }
}
