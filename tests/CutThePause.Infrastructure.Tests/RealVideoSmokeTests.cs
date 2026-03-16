using CutThePause.Core.Models;
using CutThePause.Core.Services;
using CutThePause.Infrastructure.Ffmpeg;
using CutThePause.Infrastructure.Vad;

namespace CutThePause.Infrastructure.Tests;

public sealed class RealVideoSmokeTests
{
    [Fact]
    public async Task AnalyzeAndExport_WithProvidedRealClip_ProducesShorterOutput()
    {
        var repoRoot = ResolveRepoRoot();
        var inputPath = Path.Combine(repoRoot, "Real Test Video", "IMG_0278.MOV");

        if (!File.Exists(inputPath))
        {
            return;
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"cut-the-pause-smoke-{Guid.NewGuid():N}.mp4");
        var modelPath = Path.Combine(repoRoot, "assets", "models", "silero_vad.onnx");

        var ffmpegLocator = new DefaultFfmpegLocator();
        var ffmpegRunner = new ProcessFfmpegRunner();
        var metadataReader = new FfmpegVideoMetadataReader(ffmpegLocator, ffmpegRunner);
        var audioExtractor = new FfmpegAudioExtractor(ffmpegLocator, ffmpegRunner);
        var exporter = new FfmpegVideoExporter(ffmpegLocator, ffmpegRunner);
        var vadAnalyzer = new FallbackVadAnalyzer(new SileroVadAnalyzer(modelPath), new EnergyVadAnalyzer());
        var workflow = new VideoWorkflowService(metadataReader, audioExtractor, vadAnalyzer, exporter);
        var settings = new AnalysisSettings
        {
            MinSilenceMs = 320,
            MinSpeechMs = 140,
            PaddingBeforeMs = 70,
            PaddingAfterMs = 110,
            SpeechThreshold = 0.5f,
            ExportPreset = ExportPreset.SmallerFile
        };

        try
        {
            var analysis = await workflow.AnalyzeAsync(inputPath, settings, CancellationToken.None);

            Assert.NotEmpty(analysis.KeepSegments);
            Assert.NotEmpty(analysis.CutCandidates);
            Assert.True(analysis.RemovedDuration > TimeSpan.Zero);
            var primarySileroWarning = analysis.Warnings.FirstOrDefault(
                warning => warning.Contains("Primary Silero VAD could not run", StringComparison.OrdinalIgnoreCase));
            Assert.True(primarySileroWarning is null, primarySileroWarning);

            var request = ExportPlanBuilder.BuildRequest(
                inputPath,
                outputPath,
                analysis.Duration,
                analysis.CutCandidates,
                ExportPreset.SmallerFile);

            await workflow.ExportAsync(request, CancellationToken.None);

            Assert.True(File.Exists(outputPath));

            var outputMetadata = await metadataReader.ReadAsync(outputPath, CancellationToken.None);
            Assert.True(outputMetadata.Duration < analysis.Duration);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CutThePause.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
