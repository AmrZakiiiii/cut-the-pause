using System.Globalization;
using CutThePause.Core.Models;
using CutThePause.Core.Services;
using CutThePause.Infrastructure;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Ffmpeg;
using CutThePause.Infrastructure.Models;
using CutThePause.Infrastructure.Vad;

namespace CutThePause.Infrastructure.Tests;

public sealed class LongVideoAcceptanceTests
{
    [Fact]
    public async Task AnalyzeAndExport_UserLongVideo_UsesRequestedSettingsAndQualityPath()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CUTTHEPAUSE_RUN_LONG_VIDEO"), "1", StringComparison.Ordinal) ||
            !string.Equals(Environment.GetEnvironmentVariable("CUTTHEPAUSE_MACHINE_IDLE"), "1", StringComparison.Ordinal))
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
                $"{Path.GetFileNameWithoutExtension(inputPath)}.trimmed.quality-check.mp4");
        var keepOutput = string.Equals(
            Environment.GetEnvironmentVariable("CUTTHEPAUSE_KEEP_LONG_VIDEO_OUTPUT"),
            "1",
            StringComparison.Ordinal);
        var modelPath = ResolveModelPath(inputPath);
        var ffmpegLocator = new DefaultFfmpegLocator();
        var ffmpegRunner = new ProcessFfmpegRunner();
        var metadataReader = new FfmpegVideoMetadataReader(ffmpegLocator, ffmpegRunner);
        var audioExtractor = ResolveAudioExtractor(ffmpegLocator, ffmpegRunner);
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
            var commandPlan = FfmpegExportCommandBuilder.Build(
                request with
                {
                    KeepSegments = request.KeepSegments
                        .Take(FfmpegExportCommandBuilder.MaxSegmentsPerCommand)
                        .ToArray()
                },
                preferHardwareAcceleration: true);

            Assert.Contains(OperatingSystem.IsMacOS() ? "hevc_videotoolbox" : "libx265", commandPlan.Arguments);
            Assert.Contains("main10", commandPlan.Arguments);
            Assert.Contains(OperatingSystem.IsMacOS() ? "p010le" : "yuv420p10le", commandPlan.Arguments);
            Assert.Contains("hvc1", commandPlan.Arguments);
            Assert.DoesNotContain("prores_ks", commandPlan.Arguments);
            Assert.Contains("HEVC Main 10", commandPlan.EncoderLabel);

            if (string.Equals(Environment.GetEnvironmentVariable("CUTTHEPAUSE_LONG_VIDEO_ANALYZE_ONLY"), "1", StringComparison.Ordinal))
            {
                return;
            }

            await workflow.ExportAsync(request, null, CancellationToken.None);

            Assert.True(File.Exists(outputPath));
            var outputMetadata = await metadataReader.ReadAsync(outputPath, CancellationToken.None);
            var outputBytes = new FileInfo(outputPath).Length;
            Console.WriteLine($"Long-video export: output={outputPath}, duration={outputMetadata.Duration}, bytes={outputBytes}");
            Assert.True(outputMetadata.Duration > TimeSpan.Zero);
            Assert.True(outputMetadata.Duration < analysis.Duration);
            Assert.True(outputBytes < new FileInfo(inputPath).Length);
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

    private static IAudioExtractor ResolveAudioExtractor(
        IFfmpegLocator ffmpegLocator,
        IFfmpegRunner ffmpegRunner)
    {
        var pcmPath = Environment.GetEnvironmentVariable("CUTTHEPAUSE_LONG_VIDEO_PCM_PATH");
        return !string.IsNullOrWhiteSpace(pcmPath) && File.Exists(pcmPath)
            ? new PreExtractedAudioFile(pcmPath)
            : new FfmpegAudioExtractor(ffmpegLocator, ffmpegRunner);
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

    private sealed class PreExtractedAudioFile : IAudioExtractor, IChunkedAudioExtractor
    {
        private readonly string _path;

        public PreExtractedAudioFile(string path)
        {
            _path = path;
        }

        public Task<PcmAudioData> ExtractAsync(string inputPath, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The long-video acceptance test uses the file-backed audio path.");

        public Task<PcmAudioFile> ExtractToFileAsync(string inputPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sampleCount = new FileInfo(_path).Length / sizeof(float);
            return Task.FromResult(new PcmAudioFile(_path, 16_000, sampleCount));
        }
    }
}
