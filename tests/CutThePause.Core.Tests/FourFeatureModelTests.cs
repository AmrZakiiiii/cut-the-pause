using CutThePause.Core.Models;
using CutThePause.Core.Services;

namespace CutThePause.Core.Tests;

public sealed class FourFeatureModelTests
{
    [Fact]
    public void EquivalentExportRequests_HaveTheSameFingerprint()
    {
        var first = CreateRequest(new CutCandidate(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Detected silence"));
        var second = CreateRequest(new CutCandidate(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Detected silence"));

        Assert.Equal(
            ExportRequestFingerprint.Compute(first),
            ExportRequestFingerprint.Compute(second));
    }

    [Fact]
    public void ChangingOneCut_ChangesTheFingerprint()
    {
        var first = CreateRequest(new CutCandidate(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Detected silence"));
        var second = CreateRequest(new CutCandidate(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2.1), "Detected silence"));

        Assert.NotEqual(
            ExportRequestFingerprint.Compute(first),
            ExportRequestFingerprint.Compute(second));
    }

    [Fact]
    public void WaveformReducer_ReturnsBoundedNormalizedPeaks()
    {
        var peaks = WaveformPeakReducer.Reduce(
            new[] { -0.25f, 0.5f, -2f, 0.1f, 0.75f, -0.4f }.AsSpan(),
            peakCount: 3);

        Assert.Equal(3, peaks.Count);
        Assert.All(peaks, peak => Assert.InRange(peak, 0f, 1f));
        Assert.Contains(peaks, peak => peak >= 0.99f);
    }

    [Fact]
    public void NamedPreset_ConvertsToAnalysisSettings()
    {
        var preset = new NamedPreset("Voice cleanup", 120, 150, 0, 0, 0.5f, ExportPreset.HigherQuality);

        var settings = preset.ToAnalysisSettings();

        Assert.Equal(120, settings.MinSilenceMs);
        Assert.Equal(150, settings.MinSpeechMs);
        Assert.Equal(0, settings.PaddingBeforeMs);
        Assert.Equal(0, settings.PaddingAfterMs);
        Assert.Equal(0.5f, settings.SpeechThreshold);
        Assert.Equal(ExportPreset.HigherQuality, settings.ExportPreset);
    }

    [Fact]
    public void CutPlanBuilder_PreservesWaveformPeaks()
    {
        var waveform = new[] { 0.1f, 0.8f, 0.2f };

        var result = CutPlanBuilder.Build(
            "demo.mp4",
            TimeSpan.FromSeconds(5),
            new[] { new SpeechSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)) },
            new AnalysisSettings(),
            waveformPeaks: waveform);

        Assert.Equal(waveform, result.WaveformPeaks);
    }

    private static ExportRequest CreateRequest(CutCandidate cut) => new(
        "demo.mp4",
        "demo.trimmed.mp4",
        TimeSpan.FromSeconds(10),
        ExportPreset.Balanced,
        new[] { cut },
        new[]
        {
            new KeepSegment(0, TimeSpan.Zero, cut.Start),
            new KeepSegment(1, cut.End, TimeSpan.FromSeconds(10))
        });
}
