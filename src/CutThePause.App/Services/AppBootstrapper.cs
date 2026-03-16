using CutThePause.App.ViewModels;
using CutThePause.Infrastructure;
using CutThePause.Infrastructure.Ffmpeg;
using CutThePause.Infrastructure.Vad;

namespace CutThePause.App.Services;

public static class AppBootstrapper
{
    public static MainWindow CreateMainWindow()
    {
        var ffmpegLocator = new DefaultFfmpegLocator();
        var ffmpegRunner = new ProcessFfmpegRunner();
        var audioExtractor = new FfmpegAudioExtractor(ffmpegLocator, ffmpegRunner);
        var metadataReader = new FfmpegVideoMetadataReader(ffmpegLocator, ffmpegRunner);
        var videoExporter = new FfmpegVideoExporter(ffmpegLocator, ffmpegRunner);
        var sileroAnalyzer = new SileroVadAnalyzer(ResolveModelPath());
        var fallbackAnalyzer = new EnergyVadAnalyzer();
        var vadAnalyzer = new FallbackVadAnalyzer(sileroAnalyzer, fallbackAnalyzer);
        var workflowService = new VideoWorkflowService(metadataReader, audioExtractor, vadAnalyzer, videoExporter);

        return new MainWindow
        {
            DataContext = new MainWindowViewModel(workflowService)
        };
    }

    private static string ResolveModelPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Models", "silero_vad.onnx"),
            Path.Combine(AppContext.BaseDirectory, "silero_vad.onnx"),
            Path.Combine(Environment.CurrentDirectory, "assets", "models", "silero_vad.onnx")
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }
}
