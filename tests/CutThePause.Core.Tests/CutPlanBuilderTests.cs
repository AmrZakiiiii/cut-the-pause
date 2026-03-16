using CutThePause.Core.Models;
using CutThePause.Core.Services;

namespace CutThePause.Core.Tests;

public sealed class CutPlanBuilderTests
{
    [Fact]
    public void BuildKeepSegments_MergesRangesSeparatedByShortSilence()
    {
        var settings = new AnalysisSettings
        {
            MinSilenceMs = 200,
            PaddingBeforeMs = 0,
            PaddingAfterMs = 0
        };

        var keepSegments = CutPlanBuilder.BuildKeepSegments(
            TimeSpan.FromSeconds(5),
            new[]
            {
                new SpeechSegment(TimeSpan.FromSeconds(0.3), TimeSpan.FromSeconds(1.0)),
                new SpeechSegment(TimeSpan.FromSeconds(1.1), TimeSpan.FromSeconds(2.0))
            },
            settings);

        var segment = Assert.Single(keepSegments);
        Assert.Equal(TimeSpan.FromSeconds(0.3), segment.Start);
        Assert.Equal(TimeSpan.FromSeconds(2.0), segment.End);
    }

    [Fact]
    public void Build_AddsLeadingAndTrailingCutsAroundPaddedSpeech()
    {
        var settings = new AnalysisSettings
        {
            MinSilenceMs = 250,
            PaddingBeforeMs = 100,
            PaddingAfterMs = 200
        };

        var result = CutPlanBuilder.Build(
            "demo.mp4",
            TimeSpan.FromSeconds(5),
            new[]
            {
                new SpeechSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2))
            },
            settings);

        var keepSegment = Assert.Single(result.KeepSegments);
        Assert.Equal(TimeSpan.FromMilliseconds(900), keepSegment.Start);
        Assert.Equal(TimeSpan.FromMilliseconds(2200), keepSegment.End);
        Assert.Equal(2, result.CutCandidates.Count);
    }
}
