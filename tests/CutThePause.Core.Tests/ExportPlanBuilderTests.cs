using CutThePause.Core.Models;
using CutThePause.Core.Services;

namespace CutThePause.Core.Tests;

public sealed class ExportPlanBuilderTests
{
    [Fact]
    public void BuildRequest_RejoinsDisabledCutsIntoKeepSegments()
    {
        var request = ExportPlanBuilder.BuildRequest(
            "input.mp4",
            "output.mp4",
            TimeSpan.FromSeconds(10),
            new[]
            {
                new CutCandidate(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Cut 1", true),
                new CutCandidate(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(5), "Cut 2", false),
                new CutCandidate(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(8), "Cut 3", true)
            },
            ExportPreset.Balanced);

        Assert.Collection(
            request.KeepSegments,
            segment =>
            {
                Assert.Equal(TimeSpan.Zero, segment.Start);
                Assert.Equal(TimeSpan.FromSeconds(1), segment.End);
            },
            segment =>
            {
                Assert.Equal(TimeSpan.FromSeconds(2), segment.Start);
                Assert.Equal(TimeSpan.FromSeconds(7), segment.End);
            },
            segment =>
            {
                Assert.Equal(TimeSpan.FromSeconds(8), segment.Start);
                Assert.Equal(TimeSpan.FromSeconds(10), segment.End);
            });
    }
}
