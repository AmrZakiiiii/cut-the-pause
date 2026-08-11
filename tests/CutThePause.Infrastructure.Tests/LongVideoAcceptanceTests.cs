using System.Globalization;
using CutThePause.Core.Models;
using CutThePause.Core.Services;
using CutThePause.Infrastructure;
using CutThePause.Infrastructure.Ffmpeg;
using CutThePause.Infrastructure.Vad;

namespace CutThePause.Infrastructure.Tests;

public sealed class LongVideoAcceptanceTests
{
    [Fact]
    public async Task AnalyzeAndExport_UserLongVideo_UsesRequestedSettingsAndQualityPath()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CUTTHEPAUSE_RUN_LONG_VIDEO"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var inputPath = Environment.GetEnvironmentVariable("CUTTHEPAUSE_LONG_VIDEO_PATH")
            ?? "/Volumes/AmrZaki EXT/Oka w Tarek/IMG_6595.MOV";
        if (!File.Exists(inputPath))
        {
            return;
        }

        var outputPath = Environment.GetEnvironmentVariable("CUTTHEPAUSE_LONG_VIDEO_OUTPUT")
            ?? Path.Combine(
                Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory,
                $"{Path.GetFileNameWithoutExtension(inputPath)}.trimmed.quality-check.mov");
        var keepOutput = string.Equals(
            Environment.GetEnvironmentVariable("CUTTHEPAUSE_KEEP_LONG_VIDEO_OUTPUT"),
            "1",
            StringComparison.Ordinal);
        var modelPath = ResolveModelPath(inputPath);
        var ffmpegLocator = new DefaultFfmpegLocator();
        var ffmpegRunner = new ProcessFfmpegRunner();
        var metadataReader = new FfmpegVideoMetadataReader(ffmpegLocator, ffmpegRunner);
        var audioExtractor = new FfmpegAudioExtractor(ffmpegLocator, ffmpegRunner);
        var exporter = new FfmpegVideoExporter(ffmpegLocator, ffmpegRunner);
        var vadAnalyzer = new FallbackVadAnalyzer(new SileroVadAnalyzer(modelPath), new EnergyVadAnalyzer());
        var workflow = new VideoWorkflowService(metadataReader, audioExtractor, vadAnalyzer, exporter);
        var settings = new AnalysisSettings
        {
            MinSilenceMs = 120,
            MinSpeechMs = 150,
            PaddingBeforeMs = 0,
            PaddingAfterMs = 0,
            SpeechThreshold = 0.5f,
            ExportPreset = ExportPreset.Balanced
        };

        try
        {
            var analysis = await workflow.AnalyzeAsync(inputPath, settings, CancellationToken.None);

            Assert.Equal(settings, analysis.Settings);
            Assert.NotEmpty(analysis.KeepSegments);
            Console.WriteLine($"Long-video analysis: source={analysis.Duration}, cuts={analysis.CutCandidates.Count}, output={analysis.OutputDuration}, removed={analysis.RemovedDuration}");

            var request = BuildExportRequest(inputPath, outputPath, analysis, settings);
            var commandPlan = FfmpegExportCommandBuilder.Build(request, preferHardwareAcceleration: true);

            Assert.Contains("prores_ks", commandPlan.Arguments);
            Assert.Contains("pcm_s16le", commandPlan.Arguments);
            Assert.Equal("ProRes MOV master", commandPlan.EncoderLabel);
            Assert.False(commandPlan.UsesHardwareAcceleration);
            Assert.Equal("2", commandPlan.Arguments[Array.IndexOf(commandPlan.Arguments.ToArray(), "-profile:v") + 1]);

            if (string.Equals(Environment.GetEnvironmentVariable("CUTTHEPAUSE_LONG_VIDEO_ANALYZE_ONLY"), "1", StringComparison.Ordinal))
            {
                return;
            }

            await workflow.ExportAsync(request, null, CancellationToken.None);

            Assert.True(File.Exists(outputPath));
            var outputMetadata = await metadataReader.ReadAsync(outputPath, CancellationToken.None);
            Console.WriteLine($"Long-video export: output={outputPath}, duration={outputMetadata.Duration}, bytes={new FileInfo(outputPath).Length}");
            Assert.True(outputMetadata.Duration > TimeSpan.Zero);
            Assert.True(outputMetadata.Duration < analysis.Duration);
        }
        finally
        {
            if (!keepOutput && File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static string ResolveModelPath(string inputPath)
    {
        var inputDirectory = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var repositoryRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repositoryRoot is not null && !File.Exists(Path.Combine(repositoryRoot.FullName, "CutThePause.sln")))
        {
            repositoryRoot = repositoryRoot.Parent;
        }

        return new[]
        {
            Path.Combine(repositoryRoot?.FullName ?? inputDirectory, "assets", "models", "silero_vad.onnx"),
            Path.Combine(AppContext.BaseDirectory, "silero_vad.onnx")
        }.FirstOrDefault(File.Exists)
            ?? Path.Combine(repositoryRoot?.FullName ?? inputDirectory, "assets", "models", "silero_vad.onnx");
    }

    private static ExportRequest BuildExportRequest(
        string inputPath,
        string outputPath,
        AnalysisResult analysis,
        AnalysisSettings settings)
    {
        var probeSecondsText = Environment.GetEnvironmentVariable("CUTTHEPAUSE_LONG_VIDEO_PROBE_SECONDS");
        if (!double.TryParse(probeSecondsText, NumberStyles.Float, CultureInfo.InvariantCulture, out var probeSeconds) || probeSeconds <= 0)
        {
            return ExportPlanBuilder.BuildRequest(
                inputPath,
                outputPath,
                analysis.Duration,
                analysis.CutCandidates,
                settings.ExportPreset);
        }

        return new ExportRequest(
            inputPath,
            outputPath,
            analysis.Duration,
            settings.ExportPreset,
            analysis.CutCandidates,
            LimitKeepSegments(analysis.KeepSegments, TimeSpan.FromSeconds(probeSeconds)));
    }

    private static IReadOnlyList<KeepSegment> LimitKeepSegments(
        IReadOnlyList<KeepSegment> keepSegments,
        TimeSpan maximumDuration)
    {
        var limited = new List<KeepSegment>();
        var remaining = maximumDuration;

        foreach (var segment in keepSegments)
        {
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var duration = segment.Duration <= remaining ? segment.Duration : remaining;
            limited.Add(new KeepSegment(limited.Count, segment.Start, segment.Start + duration));
            remaining -= duration;
        }

        return limited;
    }
}
