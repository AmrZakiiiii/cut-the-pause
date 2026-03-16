using System.Globalization;
using System.Text;
using CutThePause.Core.Models;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Ffmpeg;

public static class FfmpegExportCommandBuilder
{
    public static ExportCommandPlan Build(ExportRequest request)
    {
        if (request.KeepSegments.Count == 0)
        {
            throw new InvalidOperationException("At least one keep segment is required to export a trimmed video.");
        }

        var filterGraph = BuildFilterGraph(request.KeepSegments);
        var codecOptions = ResolveCodecOptions(request.Preset);

        var arguments = new List<string>
        {
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-i",
            request.InputPath,
            "-filter_complex",
            filterGraph,
            "-map",
            "[outv]",
            "-map",
            "[outa]",
            "-c:v",
            "libx264",
            "-preset",
            codecOptions.Preset,
            "-crf",
            codecOptions.Crf.ToString(CultureInfo.InvariantCulture),
            "-c:a",
            "aac",
            "-b:a",
            codecOptions.AudioBitrate,
            "-movflags",
            "+faststart",
            request.OutputPath
        };

        return new ExportCommandPlan(filterGraph, arguments);
    }

    private static string BuildFilterGraph(IReadOnlyList<KeepSegment> keepSegments)
    {
        var builder = new StringBuilder();

        for (var index = 0; index < keepSegments.Count; index++)
        {
            var segment = keepSegments[index];
            builder
                .Append("[0:v]trim=start=")
                .Append(FormatSeconds(segment.Start))
                .Append(":end=")
                .Append(FormatSeconds(segment.End))
                .Append(",setpts=PTS-STARTPTS[v")
                .Append(index)
                .Append("];")
                .Append("[0:a]atrim=start=")
                .Append(FormatSeconds(segment.Start))
                .Append(":end=")
                .Append(FormatSeconds(segment.End))
                .Append(",asetpts=PTS-STARTPTS[a")
                .Append(index)
                .Append("];");
        }

        for (var index = 0; index < keepSegments.Count; index++)
        {
            builder.Append("[v").Append(index).Append("][a").Append(index).Append(']');
        }

        builder
            .Append("concat=n=")
            .Append(keepSegments.Count.ToString(CultureInfo.InvariantCulture))
            .Append(":v=1:a=1[outv][outa]");

        return builder.ToString();
    }

    private static string FormatSeconds(TimeSpan value) =>
        value.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture);

    private static (string Preset, int Crf, string AudioBitrate) ResolveCodecOptions(ExportPreset preset) =>
        preset switch
        {
            ExportPreset.HigherQuality => ("slow", 16, "192k"),
            ExportPreset.SmallerFile => ("medium", 23, "128k"),
            _ => ("medium", 18, "160k")
        };
}
