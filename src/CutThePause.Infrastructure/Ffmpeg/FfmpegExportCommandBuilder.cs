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
        var outputFormat = ResolveOutputFormat(request.OutputPath);
        var usesHardwareAcceleration = outputFormat == OutputFormat.Mp4 && preferHardwareAcceleration && OperatingSystem.IsMacOS();
        var codecArguments = outputFormat switch
        {
            OutputFormat.Mov => BuildMovCodecArguments(request.Preset),
            _ => usesHardwareAcceleration
                ? BuildHardwareCodecArguments(request.Preset)
                : BuildSoftwareCodecArguments(request.Preset)
        };
        var encoderLabel = outputFormat switch
        {
            OutputFormat.Mov => "ProRes MOV master",
            _ when usesHardwareAcceleration => "VideoToolbox H.264",
            _ => "Software H.264"
        };

        var arguments = new List<string>
        {
            "-hide_banner",
            "-loglevel",
            "error",
            "-progress",
            "pipe:1",
            "-nostats",
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

        return new ExportCommandPlan(filterGraph, arguments, usesHardwareAcceleration, encoderLabel);
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

    private static OutputFormat ResolveOutputFormat(string outputPath)
    {
        var extension = Path.GetExtension(outputPath);

        return extension.Equals(".mov", StringComparison.OrdinalIgnoreCase)
            ? OutputFormat.Mov
            : OutputFormat.Mp4;
    }

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

    private static IReadOnlyList<string> BuildMovCodecArguments(ExportPreset preset)
    {
        var profile = ResolveMovProfile(preset);

        return new[]
        {
            "-c:v",
            "prores_ks",
            "-profile:v",
            profile.ToString(CultureInfo.InvariantCulture),
            "-vendor",
            "apl0",
            "-pix_fmt",
            "yuv422p10le",
            "-c:a",
            "pcm_s16le"
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

    private static int ResolveMovProfile(ExportPreset preset) =>
        preset switch
        {
            ExportPreset.HigherQuality => 3,
            ExportPreset.SmallerFile => 1,
            _ => 2
        };

    private enum OutputFormat
    {
        Mp4,
        Mov
    }
}
