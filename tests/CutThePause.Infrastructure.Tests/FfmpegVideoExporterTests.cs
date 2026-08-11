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
        Assert.NotEqual(finalPath, runner.LastArguments[^1]);
        Assert.Equal(".mov", Path.GetExtension(runner.LastArguments[^1]));
        Assert.False(File.Exists(runner.LastArguments[^1]));
        Assert.Equal("rendered", await File.ReadAllTextAsync(finalPath));
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
        Assert.False(File.Exists(runner.LastArguments[^1]));
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

        public RecordingExportRunner(int exitCode)
        {
            _exitCode = exitCode;
        }

        public IReadOnlyList<string> LastArguments { get; private set; } = Array.Empty<string>();

        public Task<ProcessResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));

        public Task<ProcessResult> RunWithProgressAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            Action<string> onStandardOutputLine,
            CancellationToken cancellationToken)
        {
            LastArguments = arguments.ToArray();
            File.WriteAllText(LastArguments[^1], "rendered");
            return Task.FromResult(new ProcessResult(_exitCode, string.Empty, "synthetic failure"));
        }

        public Task<BinaryProcessResult> RunBinaryAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken) =>
            Task.FromResult(new BinaryProcessResult(0, Array.Empty<byte>(), string.Empty));
    }
}
