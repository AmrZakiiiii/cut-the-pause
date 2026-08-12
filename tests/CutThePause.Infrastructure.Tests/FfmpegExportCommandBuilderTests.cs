using CutThePause.Core.Models;
using CutThePause.Infrastructure.Ffmpeg;

namespace CutThePause.Infrastructure.Tests;

public sealed class FfmpegExportCommandBuilderTests
{
    [Fact]
    public void Build_CreatesSeekedConcatInputsAndCodecArguments()
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

        Assert.Contains("[0:v]setpts=PTS-STARTPTS[v0]", commandPlan.FilterGraph);
        Assert.Contains("[0:a]asetpts=PTS-STARTPTS[a0]", commandPlan.FilterGraph);
        Assert.Contains("concat=n=2:v=1:a=1[outv][outa]", commandPlan.FilterGraph);
        Assert.Contains("-ss", commandPlan.Arguments);
        Assert.Contains("-t", commandPlan.Arguments);
        Assert.Equal(2, commandPlan.Arguments.Count(argument => argument == "-i"));
        Assert.Contains("pipe:1", commandPlan.Arguments);
        Assert.Contains("libx265", commandPlan.Arguments);
        Assert.Contains("main10", commandPlan.Arguments);
        Assert.Contains("yuv420p10le", commandPlan.Arguments);
        Assert.Contains("22", commandPlan.Arguments);
        Assert.False(commandPlan.UsesHardwareAcceleration);
        Assert.Equal("Software HEVC Main 10", commandPlan.EncoderLabel);
        Assert.Equal("output.mp4", commandPlan.Arguments[^1]);
    }

    [Fact]
    public void Build_CapsEachBatchAtRequestedOutputDuration()
    {
        var request = new ExportRequest(
            "input.mp4",
            "output.mp4",
            TimeSpan.FromSeconds(30),
            ExportPreset.Balanced,
            Array.Empty<CutCandidate>(),
            new[]
            {
                new KeepSegment(0, TimeSpan.Zero, TimeSpan.FromSeconds(0.87)),
                new KeepSegment(1, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(11.23)),
                new KeepSegment(2, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20.54))
            });

        var commandPlan = FfmpegExportCommandBuilder.Build(request);
        var outputDurationIndex = Array.LastIndexOf(commandPlan.Arguments.ToArray(), "-t");

        Assert.True(outputDurationIndex >= 0);
        Assert.Equal("2.64", commandPlan.Arguments[outputDurationIndex + 1]);
    }

    [Fact]
    public void Build_RejectsMoreThanMaximumSegmentsPerCommand()
    {
        var keepSegments = Enumerable.Range(0, FfmpegExportCommandBuilder.MaxSegmentsPerCommand + 1)
            .Select(index => new KeepSegment(
                index,
                TimeSpan.FromSeconds(index * 2),
                TimeSpan.FromSeconds(index * 2 + 1)))
            .ToArray();
        var request = new ExportRequest(
            "input.mp4",
            "output.mp4",
            TimeSpan.FromMinutes(2),
            ExportPreset.Balanced,
            Array.Empty<CutCandidate>(),
            keepSegments);

        var exception = Assert.Throws<ArgumentException>(() => FfmpegExportCommandBuilder.Build(request));

        Assert.Contains("at most 32 keep segments", exception.Message);
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

        Assert.Contains("hevc_videotoolbox", commandPlan.Arguments);
        Assert.Contains("main10", commandPlan.Arguments);
        Assert.Contains("p010le", commandPlan.Arguments);
        Assert.Contains("8000k", commandPlan.Arguments);
        Assert.True(commandPlan.UsesHardwareAcceleration);
        Assert.Equal("VideoToolbox HEVC Main 10", commandPlan.EncoderLabel);
    }

    [Fact]
    public void Build_ForMovOutput_UsesProResMasterEncoding()
    {
        var request = new ExportRequest(
            "input.mov",
            "output.mov",
            TimeSpan.FromSeconds(10),
            ExportPreset.HigherQuality,
            Array.Empty<CutCandidate>(),
            new[]
            {
                new KeepSegment(0, TimeSpan.Zero, TimeSpan.FromSeconds(3))
            });

        var commandPlan = FfmpegExportCommandBuilder.Build(request, preferHardwareAcceleration: true);

        Assert.Contains("prores_ks", commandPlan.Arguments);
        Assert.Contains("pcm_s16le", commandPlan.Arguments);
        Assert.Contains("3", commandPlan.Arguments);
        Assert.False(commandPlan.UsesHardwareAcceleration);
        Assert.Equal("ProRes MOV master", commandPlan.EncoderLabel);
    }

}
