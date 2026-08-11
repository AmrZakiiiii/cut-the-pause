namespace CutThePause.Infrastructure.Ffmpeg;

public static class FfmpegConcatCommandBuilder
{
    public static IReadOnlyList<string> Build(string concatListPath, string outputPath) =>
        new[]
        {
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-f",
            "concat",
            "-safe",
            "0",
            "-i",
            concatListPath,
            "-c",
            "copy",
            "-movflags",
            "+faststart",
            outputPath
        };
}
