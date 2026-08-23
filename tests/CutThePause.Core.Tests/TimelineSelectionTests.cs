using CutThePause.Core.Models;
using CutThePause.Core.Services;

namespace CutThePause.Core.Tests;

public sealed class TimelineSelectionTests
{
    [Fact]
    public void TimeAt_ClampsPixelPositionToDuration()
    {
        Assert.Equal(TimeSpan.Zero, TimelineSelection.TimeAt(-10, 100, TimeSpan.FromSeconds(10)));
        Assert.Equal(TimeSpan.FromSeconds(5), TimelineSelection.TimeAt(50, 100, TimeSpan.FromSeconds(10)));
        Assert.Equal(TimeSpan.FromSeconds(10), TimelineSelection.TimeAt(120, 100, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void RangeFromPixels_ClampsAndRejectsShortSelections()
    {
        var range = TimelineSelection.RangeFromPixels(80, 20, 100, TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromSeconds(2), range!.Value.Start);
        Assert.Equal(TimeSpan.FromSeconds(8), range.Value.End);
        Assert.Null(TimelineSelection.RangeFromPixels(20, 20.5, 100, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void FindCutAtPixel_ReturnsCandidateIndex()
    {
        var cuts = new[]
        {
            new CutCandidate(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "Detected silence"),
            new CutCandidate(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(8), "Manual cut")
        };

        Assert.Equal(1, TimelineSelection.FindCutAtPixel(75, 100, TimeSpan.FromSeconds(10), cuts));
        Assert.Null(TimelineSelection.FindCutAtPixel(50, 100, TimeSpan.FromSeconds(10), cuts));
    }
}
