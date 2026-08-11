using System.Runtime.InteropServices;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Ffmpeg;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Tests;

public sealed class FfmpegAudioExtractorTests
{
    [Fact]
    public async Task ExtractAsync_UsesTemporaryPcmFileAndReturnsSamples()
    {
        var expected = new[] { 0.1f, -0.2f, 0.75f };
        var runner = new RecordingAudioRunner(expected);
        var extractor = new FfmpegAudioExtractor(
            new FakeLocator(),
            runner);

        var result = await extractor.ExtractAsync("input.mov", CancellationToken.None);

        Assert.Equal(16_000, result.SampleRate);
        Assert.Equal(expected, result.Samples);
        Assert.NotNull(runner.OutputPath);
        Assert.False(File.Exists(runner.OutputPath));
    }

    [Fact]
    public async Task ExtractToFileAsync_TransfersCleanupOwnershipToCaller()
    {
        var expected = new[] { 0.1f, -0.2f, 0.75f };
        var runner = new RecordingAudioRunner(expected);
        var extractor = new FfmpegAudioExtractor(new FakeLocator(), runner);

        await using (var audioFile = await extractor.ExtractToFileAsync("input.mov", CancellationToken.None))
        {
            Assert.Equal(expected.LongLength, audioFile.SampleCount);
            Assert.True(File.Exists(audioFile.FilePath));
            Assert.Equal(16_000, audioFile.SampleRate);
        }

        Assert.NotNull(runner.OutputPath);
        Assert.False(File.Exists(runner.OutputPath));
    }

    private sealed class FakeLocator : IFfmpegLocator
    {
        public ValueTask<FfmpegBinaries> LocateAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new FfmpegBinaries("fake-ffmpeg", null));
    }

    private sealed class RecordingAudioRunner : IFfmpegRunner
    {
        private readonly float[] _samples;

        public RecordingAudioRunner(float[] samples)
        {
            _samples = samples;
        }

        public string? OutputPath { get; private set; }

        public async Task<ProcessResult> RunAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            OutputPath = arguments[^1];
            var bytes = MemoryMarshal.AsBytes(_samples.AsSpan()).ToArray();
            await File.WriteAllBytesAsync(OutputPath, bytes, cancellationToken);
            return new ProcessResult(0, string.Empty, string.Empty);
        }

        public Task<ProcessResult> RunWithProgressAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            Action<string> onStandardOutputLine,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BinaryProcessResult> RunBinaryAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
