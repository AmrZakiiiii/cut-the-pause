using CutThePause.Infrastructure.Models;
using CutThePause.Infrastructure.Waveform;

namespace CutThePause.Infrastructure.Tests;

public sealed class WaveformPeakBuilderTests
{
    [Fact]
    public void Build_InMemoryAudio_ReturnsNormalizedBoundedPeaks()
    {
        var audio = new PcmAudioData(new[] { 0.1f, -0.5f, 0.2f, -1f, 0.4f, 0.8f }, 16_000);

        var peaks = WaveformPeakBuilder.Build(audio, peakCount: 4, CancellationToken.None);

        Assert.Equal(4, peaks.Count);
        Assert.All(peaks, peak => Assert.InRange(peak, 0f, 1f));
        Assert.Contains(peaks, peak => peak >= 0.99f);
    }

    [Fact]
    public async Task Build_FileBackedAudio_ReadsSequentiallyAndBoundsPeakCount()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cut-the-pause-waveform-{Guid.NewGuid():N}.f32le");
        var samples = new[] { 0.1f, -0.5f, 0.2f, -1f, 0.4f, 0.8f };
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples.AsSpan()).ToArray());
        await stream.FlushAsync();
        await stream.DisposeAsync();

        try
        {
            await using var audio = new PcmAudioFile(path, 16_000, samples.Length);

            var peaks = WaveformPeakBuilder.Build(audio, peakCount: 3, CancellationToken.None);

            Assert.Equal(3, peaks.Count);
            Assert.All(peaks, peak => Assert.InRange(peak, 0f, 1f));
            Assert.Contains(peaks, peak => peak >= 0.99f);
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
