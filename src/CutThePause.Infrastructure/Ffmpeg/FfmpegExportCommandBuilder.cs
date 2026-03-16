using System.Globalization;
using System.Text;
using CutThePause.Core.Models;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Ffmpeg;

public static class FfmpegExportCommandBuilder
{
    public static ExportCommandPlan Build(ExportRequest request, bool preferHardwareAcceleration = false)
    {
        if (request.KeepSegments.Count == 0)
        {
            throw new InvalidOperationException("At least one keep segment is required to export a trimmed video.");
        }

        var filterGraph = BuildFilterGraph(request.KeepSegments);
        var usesHardwareAcceleration = preferHardwareAcceleration && OperatingSystem.IsMacOS();
        var codecArguments = usesHardwareAcceleration
            ? BuildHardwareCodecArguments(request.Preset)
            : BuildSoftwareCodecArguments(request.Preset);

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
        };

        arguments.AddRange(codecArguments);
        arguments.Add(request.OutputPath);

        return new ExportCommandPlan(filterGraph, arguments, usesHardwareAcceleration);
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

    private static IReadOnlyList<string> BuildHardwareCodecArguments(ExportPreset preset)
    {
        var options = ResolveHardwareCodecOptions(preset);

        return new[]
        {
            "-c:v",
            "h264_videotoolbox",
            "-allow_sw",
            "1",
            "-realtime",
            "1",
            "-prio_speed",
            "1",
            "-profile:v",
            "high",
            "-pix_fmt",
            "yuv420p",
            "-b:v",
            options.VideoBitrate,
            "-c:a",
            "aac",
            "-b:a",
            options.AudioBitrate,
            "-movflags",
            "+faststart"
        };
    }

    private static IReadOnlyList<string> BuildSoftwareCodecArguments(ExportPreset preset)
    {
        var options = ResolveSoftwareCodecOptions(preset);

        return new[]
        {
            "-c:v",
            "libx264",
            "-preset",
            options.Preset,
            "-crf",
            options.Crf.ToString(CultureInfo.InvariantCulture),
            "-pix_fmt",
            "yuv420p",
            "-c:a",
            "aac",
            "-b:a",
            options.AudioBitrate,
            "-movflags",
            "+faststart"
        };
    }

    private static (string Preset, int Crf, string AudioBitrate) ResolveSoftwareCodecOptions(ExportPreset preset) =>
        preset switch
        {
            ExportPreset.HigherQuality => ("fast", 18, "192k"),
            ExportPreset.SmallerFile => ("faster", 24, "128k"),
            _ => ("veryfast", 20, "160k")
        };

    private static (string VideoBitrate, string AudioBitrate) ResolveHardwareCodecOptions(ExportPreset preset) =>
        preset switch
        {
            ExportPreset.HigherQuality => ("8000k", "192k"),
            ExportPreset.SmallerFile => ("3500k", "128k"),
            _ => ("5500k", "160k")
        };
}
