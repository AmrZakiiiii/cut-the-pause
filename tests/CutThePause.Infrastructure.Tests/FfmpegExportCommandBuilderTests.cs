using CutThePause.Core.Models;
using CutThePause.Infrastructure.Ffmpeg;

namespace CutThePause.Infrastructure.Tests;

public sealed class FfmpegExportCommandBuilderTests
{
    [Fact]
    public void Build_CreatesConcatFilterAndCodecArguments()
    {
        var request = new ExportRequest(
            "input.mp4",
            "output.mp4",
            TimeSpan.FromSeconds(10),
            ExportPreset.SmallerFile,
            Array.Empty<CutCandidate>(),
            new[]
            {
                new KeepSegment(0, TimeSpan.Zero, TimeSpan.FromSeconds(3)),
                new KeepSegment(1, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(10))
            });

        var commandPlan = FfmpegExportCommandBuilder.Build(request);

        Assert.Contains("concat=n=2:v=1:a=1[outv][outa]", commandPlan.FilterGraph);
        Assert.Contains("libx264", commandPlan.Arguments);
        Assert.Contains("24", commandPlan.Arguments);
        Assert.False(commandPlan.UsesHardwareAcceleration);
        Assert.Equal("output.mp4", commandPlan.Arguments[^1]);
    }

    [Fact]
    public void Build_OnMacWithPreferredHardwareAcceleration_UsesVideoToolboxEncoder()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var request = new ExportRequest(
            "input.mp4",
            "output.mp4",
            TimeSpan.FromSeconds(10),
            ExportPreset.Balanced,
            Array.Empty<CutCandidate>(),
            new[]
            {
                new KeepSegment(0, TimeSpan.Zero, TimeSpan.FromSeconds(3))
            });

        var commandPlan = FfmpegExportCommandBuilder.Build(request, preferHardwareAcceleration: true);

        Assert.Contains("h264_videotoolbox", commandPlan.Arguments);
        Assert.Contains("5500k", commandPlan.Arguments);
        Assert.True(commandPlan.UsesHardwareAcceleration);
    }
}
