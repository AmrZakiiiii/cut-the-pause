using CutThePause.Core.Models;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Tests;

public sealed class VideoWorkflowServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_ReportsStagesInOrder()
    {
        var stages = new List<string>();
        var workflow = CreateWorkflow(new ImmediateVadAnalyzer());

        await workflow.AnalyzeAsync(
            "input.mov",
            new AnalysisSettings(),
            CancellationToken.None,
            new RecordingProgress<AnalysisProgress>(progress => stages.Add(progress.Stage)));

        Assert.Equal(new[]
        {
            "Reading video metadata...",
            "Extracting audio...",
            "Detecting speech...",
            "Building waveform and review...",
            "Building review..."
        }, stages);
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotBlockCallerWhileSynchronousVadIsRunning()
    {
        using var vadStarted = new ManualResetEventSlim();
        using var releaseVad = new ManualResetEventSlim();
        var workflow = CreateWorkflow(new BlockingVadAnalyzer(vadStarted, releaseVad));
        var returned = new TaskCompletionSource<Task<AnalysisResult>>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                returned.SetResult(workflow.AnalyzeAsync("input.mov", new AnalysisSettings(), CancellationToken.None));
            }
            catch (Exception exception)
            {
                returned.SetException(exception);
            }
        });
        thread.Start();

        try
        {
            Assert.True(vadStarted.Wait(TimeSpan.FromSeconds(2)));
            await returned.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            releaseVad.Set();
        }

        var analysisTask = await returned.Task;
        await analysisTask;
        thread.Join(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task AnalyzeAsync_UsesFileBackedAudioWhenProductionComponentsSupportIt()
    {
        var audioPath = Path.Combine(Path.GetTempPath(), $"cut-the-pause-workflow-{Guid.NewGuid():N}.f32le");
        var samples = new[] { 0.1f, -0.5f, 0.2f, -1f };
        await File.WriteAllBytesAsync(
            audioPath,
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples.AsSpan()).ToArray());
        var progressStages = new List<string>();
        var streamingVad = new StreamingVadAnalyzer();
        var workflow = new VideoWorkflowService(
            new FixedMetadataReader(),
            new StreamingAudioExtractor(audioPath),
            streamingVad,
            new NoopVideoExporter());

        var result = await workflow.AnalyzeAsync(
            "input.mov",
            new AnalysisSettings(),
            CancellationToken.None,
            new RecordingProgress<AnalysisProgress>(progress => progressStages.Add(progress.Stage)));

        Assert.True(streamingVad.WasCalled);
        Assert.Contains("Detecting speech from streamed audio...", progressStages);
        Assert.Contains("Building waveform and review...", progressStages);
        Assert.NotEmpty(result.WaveformPeaks);
        Assert.False(File.Exists(audioPath));
    }

    private static VideoWorkflowService CreateWorkflow(IVadAnalyzer vadAnalyzer) => new(
        new FixedMetadataReader(),
        new FixedAudioExtractor(),
        vadAnalyzer,
        new NoopVideoExporter());

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;

        public RecordingProgress(Action<T> callback)
        {
            _callback = callback;
        }

        public void Report(T value) => _callback(value);
    }

    private sealed class FixedMetadataReader : IVideoMetadataReader
    {
        public Task<VideoMetadata> ReadAsync(string inputPath, CancellationToken cancellationToken) =>
            Task.FromResult(new VideoMetadata(TimeSpan.FromSeconds(10)));
    }

    private sealed class FixedAudioExtractor : IAudioExtractor
    {
        public Task<PcmAudioData> ExtractAsync(string inputPath, CancellationToken cancellationToken) =>
            Task.FromResult(new PcmAudioData(Array.Empty<float>(), 16_000));
    }

    private sealed class StreamingAudioExtractor : IAudioExtractor, IChunkedAudioExtractor
    {
        private readonly string _audioPath;

        public StreamingAudioExtractor(string audioPath)
        {
            _audioPath = audioPath;
        }

        public Task<PcmAudioData> ExtractAsync(string inputPath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PcmAudioFile> ExtractToFileAsync(string inputPath, CancellationToken cancellationToken) =>
            Task.FromResult(new PcmAudioFile(
                _audioPath,
                16_000,
                new FileInfo(_audioPath).Length / sizeof(float)));
    }

    private sealed class ImmediateVadAnalyzer : IVadAnalyzer
    {
        public Task<VadAnalysisResult> DetectSpeechAsync(
            PcmAudioData audio,
            AnalysisSettings settings,
            CancellationToken cancellationToken) =>
            Task.FromResult(new VadAnalysisResult(
                new[] { new SpeechSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)) },
                Array.Empty<string>()));
    }

    private sealed class BlockingVadAnalyzer : IVadAnalyzer
    {
        private readonly ManualResetEventSlim _started;
        private readonly ManualResetEventSlim _release;

        public BlockingVadAnalyzer(ManualResetEventSlim started, ManualResetEventSlim release)
        {
            _started = started;
            _release = release;
        }

        public Task<VadAnalysisResult> DetectSpeechAsync(
            PcmAudioData audio,
            AnalysisSettings settings,
            CancellationToken cancellationToken)
        {
            _started.Set();
            _release.Wait(cancellationToken);
            return Task.FromResult(new VadAnalysisResult(
                new[] { new SpeechSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)) },
                Array.Empty<string>()));
        }
    }

    private sealed class StreamingVadAnalyzer : IVadAnalyzer, IStreamingVadAnalyzer
    {
        public bool WasCalled { get; private set; }

        public Task<VadAnalysisResult> DetectSpeechAsync(
            PcmAudioData audio,
            AnalysisSettings settings,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<VadAnalysisResult> DetectSpeechAsync(
            PcmAudioFile audio,
            AnalysisSettings settings,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(new VadAnalysisResult(
                new[] { new SpeechSegment(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)) },
                Array.Empty<string>()));
        }
    }

    private sealed class NoopVideoExporter : IVideoExporter
    {
        public Task ExportAsync(
            ExportRequest request,
            IProgress<VideoExportProgress>? progress,
            CancellationToken cancellationToken,
            ExportExecutionOptions? executionOptions = null) =>
            Task.CompletedTask;
    }
}
