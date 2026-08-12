using System.Globalization;
using System.Text;
using CutThePause.Core.Models;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Ffmpeg;

public static class FfmpegExportCommandBuilder
{
    public const int MaxSegmentsPerCommand = 32;

    public static ExportCommandPlan Build(ExportRequest request, bool preferHardwareAcceleration = false)
    {
        if (request.KeepSegments.Count == 0)
        {
            throw new InvalidOperationException("At least one keep segment is required to export a trimmed video.");
        }

        if (request.KeepSegments.Count > MaxSegmentsPerCommand)
        {
            throw new ArgumentException(
                $"An FFmpeg export command may contain at most {MaxSegmentsPerCommand} keep segments. Split the export into bounded batches.",
                nameof(request));
        }

        var filterGraph = BuildFilterGraph(request.KeepSegments);
        var outputFormat = ResolveOutputFormat(request.OutputPath);
        var usesHardwareAcceleration = outputFormat == OutputFormat.Mp4 && preferHardwareAcceleration && OperatingSystem.IsMacOS();
        var codecArguments = outputFormat switch
        {
            OutputFormat.Mov => BuildMovCodecArguments(request.Preset),
            _ => usesHardwareAcceleration
                ? BuildHardwareHevcCodecArguments(request.Preset)
                : BuildSoftwareHevcCodecArguments(request.Preset)
        };
        var encoderLabel = outputFormat switch
        {
            OutputFormat.Mov => "ProRes MOV master",
            _ when usesHardwareAcceleration => "VideoToolbox HEVC Main 10",
            _ => "Software HEVC Main 10"
        };

        var arguments = new List<string>
        {
            "-hide_banner",
            "-loglevel",
            "error",
            "-progress",
            "pipe:1",
            "-nostats",
            "-y"
        };

        foreach (var segment in request.KeepSegments)
        {
            arguments.Add("-ss");
            arguments.Add(FormatSeconds(segment.Start));
            arguments.Add("-t");
            arguments.Add(FormatSeconds(segment.Duration));
            arguments.Add("-i");
            arguments.Add(request.InputPath);
        }

        arguments.AddRange(new[]
        {
            "-filter_complex",
            filterGraph,
            "-map",
            "[outv]",
            "-map",
            "[outa]",
            "-map_metadata",
            "0",
            "-map_chapters",
            "0"
        });

        arguments.AddRange(codecArguments);
        arguments.Add("-t");
        arguments.Add(FormatSeconds(request.OutputDuration));
        arguments.Add(request.OutputPath);

        return new ExportCommandPlan(filterGraph, arguments, usesHardwareAcceleration, encoderLabel);
    }

    private static string BuildFilterGraph(IReadOnlyList<KeepSegment> keepSegments)
    {
        var builder = new StringBuilder();

        for (var index = 0; index < keepSegments.Count; index++)
        {
            builder
                .Append('[')
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(":v]setpts=PTS-STARTPTS[v")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append("];[")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(":a]asetpts=PTS-STARTPTS[a")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append("];");
        }

        for (var index = 0; index < keepSegments.Count; index++)
        {
            builder
                .Append("[v")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append("][a")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(']');
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

    private static IReadOnlyList<string> BuildHardwareHevcCodecArguments(ExportPreset preset)
    {
        var options = ResolveHardwareHevcCodecOptions(preset);

        return new[]
        {
            "-c:v",
            "hevc_videotoolbox",
            "-profile:v",
            "main10",
            "-allow_sw",
            "1",
            "-pix_fmt",
            "p010le",
            "-tag:v",
            "hvc1",
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

    private static IReadOnlyList<string> BuildSoftwareHevcCodecArguments(ExportPreset preset)
    {
        var options = ResolveSoftwareHevcCodecOptions(preset);

        return new[]
        {
            "-c:v",
            "libx265",
            "-preset",
            options.Preset,
            "-crf",
            options.Crf.ToString(CultureInfo.InvariantCulture),
            "-profile:v",
            "main10",
            "-pix_fmt",
            "yuv420p10le",
            "-tag:v",
            "hvc1",
            "-c:a",
            "aac",
            "-b:a",
            options.AudioBitrate,
            "-movflags",
            "+faststart"
        };
    }

    private static (string Preset, int Crf, string AudioBitrate) ResolveSoftwareHevcCodecOptions(ExportPreset preset) =>
        preset switch
        {
            ExportPreset.HigherQuality => ("slow", 16, "192k"),
            ExportPreset.SmallerFile => ("medium", 22, "128k"),
            _ => ("medium", 18, "160k")
        };

    private static (string VideoBitrate, string AudioBitrate) ResolveHardwareHevcCodecOptions(ExportPreset preset) =>
        preset switch
        {
            ExportPreset.HigherQuality => ("10000k", "192k"),
            ExportPreset.SmallerFile => ("5500k", "128k"),
            _ => ("8000k", "160k")
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
