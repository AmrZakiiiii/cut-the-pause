using CutThePause.Core.Models;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Ffmpeg;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Tests;

public sealed class FfmpegVideoExporterTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"cut-the-pause-export-{Guid.NewGuid():N}");

    public FfmpegVideoExporterTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task SuccessfulExport_CommitsSameExtensionTemporaryFileToFinalPath()
    {
        var finalPath = Path.Combine(_tempDirectory, "trimmed.mov");
        var request = CreateRequest(finalPath, ExportPreset.HigherQuality);
        var runner = new RecordingExportRunner(exitCode: 0);

        await new FfmpegVideoExporter(new FakeLocator(), runner).ExportAsync(request, null, CancellationToken.None);

        Assert.True(File.Exists(finalPath));
        Assert.NotEqual(finalPath, runner.LastRunArguments[^1]);
        Assert.Equal(".mov", Path.GetExtension(runner.LastRunArguments[^1]));
        Assert.False(File.Exists(runner.LastRunArguments[^1]));
        Assert.Equal("joined", await File.ReadAllTextAsync(finalPath));
    }

    [Fact]
    public async Task LargeExport_RendersBoundedBatchesBeforeJoiningThem()
    {
        var finalPath = Path.Combine(_tempDirectory, "trimmed.mp4");
        var keepSegments = Enumerable.Range(0, 100)
            .Select(index => new KeepSegment(
                index,
                TimeSpan.FromSeconds(index * 2),
                TimeSpan.FromSeconds(index * 2 + 1)))
            .ToArray();
        var request = new ExportRequest(
            "input.mp4",
            finalPath,
            TimeSpan.FromSeconds(200),
            ExportPreset.Balanced,
            Array.Empty<CutCandidate>(),
            keepSegments);
        var runner = new RecordingExportRunner(exitCode: 0);

        await new FfmpegVideoExporter(new FakeLocator(), runner).ExportAsync(request, null, CancellationToken.None);

        Assert.Equal(4, runner.ProgressArguments.Count);
        Assert.All(runner.ProgressArguments, arguments =>
            Assert.InRange(arguments.Count(argument => argument == "-i"), 1, FfmpegExportCommandBuilder.MaxSegmentsPerCommand));
        Assert.Single(runner.RunArguments);
        Assert.Equal("concat", runner.RunArguments[0][Array.IndexOf(runner.RunArguments[0].ToArray(), "-f") + 1]);
        Assert.True(File.Exists(finalPath));
    }

    [Fact]
    public async Task Export_RejectsSourceAsDestination()
    {
        var sourcePath = Path.Combine(_tempDirectory, "source.mp4");
        await File.WriteAllTextAsync(sourcePath, "source");
        var request = CreateRequest(sourcePath, ExportPreset.Balanced) with { InputPath = sourcePath };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new FfmpegVideoExporter(new FakeLocator(), new RecordingExportRunner(exitCode: 0))
                .ExportAsync(request, null, CancellationToken.None));
    }

    [Fact]
    public async Task Export_RejectsWhenNoKeepSegmentsRemain()
    {
        var request = CreateRequest(Path.Combine(_tempDirectory, "trimmed.mp4"), ExportPreset.Balanced) with
        {
            KeepSegments = Array.Empty<KeepSegment>()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new FfmpegVideoExporter(new FakeLocator(), new RecordingExportRunner(exitCode: 0))
                .ExportAsync(request, null, CancellationToken.None));
    }

    [Fact]
    public async Task Export_DoesNotPublishMp4ThatIsLargerThanSource()
    {
        var sourcePath = Path.Combine(_tempDirectory, "source.mp4");
        var outputPath = Path.Combine(_tempDirectory, "trimmed.mp4");
        await File.WriteAllTextAsync(sourcePath, "x");
        var request = CreateRequest(outputPath, ExportPreset.Balanced) with
        {
            InputPath = sourcePath,
            SourceDuration = TimeSpan.FromHours(1)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new FfmpegVideoExporter(new FakeLocator(), new RecordingExportRunner(exitCode: 0))
                .ExportAsync(request, null, CancellationToken.None));

        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task Export_RetriesWithSoftwareWhenHardwareEncoderFails()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var finalPath = Path.Combine(_tempDirectory, "hardware-fallback.mp4");
        var runner = new RecordingExportRunner(exitCode: 0, progressExitCodes: new[] { 1, 0 });

        await new FfmpegVideoExporter(new FakeLocator(), runner).ExportAsync(
            CreateRequest(finalPath, ExportPreset.Balanced),
            null,
            CancellationToken.None);

        Assert.Equal(2, runner.ProgressArguments.Count);
        Assert.Contains("hevc_videotoolbox", runner.ProgressArguments[0]);
        Assert.Contains("libx265", runner.ProgressArguments[1]);
        Assert.True(File.Exists(finalPath));
    }

    [Fact]
    public async Task FailedExport_RemovesTemporaryFileAndNeverPublishesFinalPath()
    {
        var finalPath = Path.Combine(_tempDirectory, "trimmed.mov");
        var runner = new RecordingExportRunner(exitCode: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new FfmpegVideoExporter(new FakeLocator(), runner).ExportAsync(
                CreateRequest(finalPath, ExportPreset.Balanced),
                null,
                CancellationToken.None));

        Assert.False(File.Exists(finalPath));
        Assert.False(File.Exists(runner.ProgressArguments[0][^1]));
    }

    private static ExportRequest CreateRequest(string outputPath, ExportPreset preset) => new(
        "input.mov",
        outputPath,
        TimeSpan.FromSeconds(10),
        preset,
        Array.Empty<CutCandidate>(),
        new[] { new KeepSegment(0, TimeSpan.Zero, TimeSpan.FromSeconds(3)) });

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private sealed class FakeLocator : IFfmpegLocator
    {
        public ValueTask<FfmpegBinaries> LocateAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new FfmpegBinaries("fake-ffmpeg", null));
    }

    private sealed class RecordingExportRunner : IFfmpegRunner
    {
        private readonly int _exitCode;
        private readonly Queue<int> _progressExitCodes;

        public RecordingExportRunner(int exitCode, IEnumerable<int>? progressExitCodes = null)
        {
            _exitCode = exitCode;
            _progressExitCodes = new Queue<int>(progressExitCodes ?? Array.Empty<int>());
        }

        public IReadOnlyList<string> LastRunArguments { get; private set; } = Array.Empty<string>();

        public List<IReadOnlyList<string>> ProgressArguments { get; } = new();

        public List<IReadOnlyList<string>> RunArguments { get; } = new();

        public Task<ProcessResult> RunWithProgressAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            Action<string> onStandardOutputLine,
            CancellationToken cancellationToken)
        {
            var copiedArguments = arguments.ToArray();
            ProgressArguments.Add(copiedArguments);
            File.WriteAllText(copiedArguments[^1], "rendered");
            var exitCode = _progressExitCodes.Count > 0 ? _progressExitCodes.Dequeue() : _exitCode;
            return Task.FromResult(new ProcessResult(exitCode, string.Empty, "synthetic failure"));
        }

        public Task<ProcessResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            var copiedArguments = arguments.ToArray();
            RunArguments.Add(copiedArguments);
            LastRunArguments = copiedArguments;
            File.WriteAllText(copiedArguments[^1], "joined");
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }

        public Task<BinaryProcessResult> RunBinaryAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken) =>
            Task.FromResult(new BinaryProcessResult(0, Array.Empty<byte>(), string.Empty));
    }
}
