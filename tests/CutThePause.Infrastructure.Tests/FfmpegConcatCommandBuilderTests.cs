using CutThePause.Infrastructure.Ffmpeg;

namespace CutThePause.Infrastructure.Tests;

public sealed class FfmpegConcatCommandBuilderTests
{
    [Fact]
    public void Build_UsesConcatDemuxerAndStreamCopy()
    {
        var arguments = FfmpegConcatCommandBuilder.Build("segments.txt", "joined.mp4");
        var array = arguments.ToArray();

        Assert.Equal("concat", array[Array.IndexOf(array, "-f") + 1]);
        Assert.Equal("0", array[Array.IndexOf(array, "-safe") + 1]);
        Assert.Equal("segments.txt", array[Array.IndexOf(array, "-i") + 1]);
        Assert.Equal("copy", array[Array.IndexOf(array, "-c") + 1]);
        Assert.Equal("joined.mp4", array[^1]);
    }
}
