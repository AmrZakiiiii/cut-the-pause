namespace CutThePause.Infrastructure.Models;

public sealed class PcmAudioFile : IAsyncDisposable
{
    private int _disposed;

    public PcmAudioFile(string filePath, int sampleRate, long sampleCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        if (sampleCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        }

        FilePath = filePath;
        SampleRate = sampleRate;
        SampleCount = sampleCount;
    }

    public string FilePath { get; }

    public int SampleRate { get; }

    public long SampleCount { get; }

    public TimeSpan Duration => TimeSpan.FromSeconds((double)SampleCount / SampleRate);

    public FileStream OpenRead()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        return new FileStream(
            FilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.SequentialScan);
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            TryDeleteFile(FilePath);
        }

        return ValueTask.CompletedTask;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
