using System.Runtime.InteropServices;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Vad;

internal interface IPcmFrameReader : IDisposable
{
    int ReadFrame(float[] destination, CancellationToken cancellationToken);
}

internal sealed class ArrayPcmFrameReader : IPcmFrameReader
{
    private readonly float[] _samples;
    private int _offset;

    public ArrayPcmFrameReader(float[] samples)
    {
        _samples = samples;
    }

    public int ReadFrame(float[] destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_offset >= _samples.Length)
        {
            return 0;
        }

        var count = Math.Min(destination.Length, _samples.Length - _offset);
        Array.Copy(_samples, _offset, destination, 0, count);
        if (count < destination.Length)
        {
            Array.Clear(destination, count, destination.Length - count);
        }

        _offset += count;
        return count;
    }

    public void Dispose()
    {
    }
}

internal sealed class FilePcmFrameReader : IPcmFrameReader
{
    private const int ByteBufferSize = 64 * 1024;
    private readonly FileStream _stream;
    private readonly byte[] _byteBuffer = new byte[ByteBufferSize + sizeof(float)];
    private readonly float[] _floatBuffer = new float[ByteBufferSize / sizeof(float)];
    private int _pendingByteCount;
    private int _floatOffset;
    private int _floatCount;
    private bool _endOfFile;

    public FilePcmFrameReader(PcmAudioFile audio)
    {
        _stream = audio.OpenRead();
    }

    public int ReadFrame(float[] destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();

        if (destination.Length == 0)
        {
            return 0;
        }

        var count = 0;
        while (count < destination.Length)
        {
            if (!FillFloatBuffer(cancellationToken))
            {
                break;
            }

            var available = _floatCount - _floatOffset;
            var copyCount = Math.Min(destination.Length - count, available);
            Array.Copy(_floatBuffer, _floatOffset, destination, count, copyCount);
            _floatOffset += copyCount;
            count += copyCount;
        }

        if (count < destination.Length)
        {
            Array.Clear(destination, count, destination.Length - count);
        }

        return count;
    }

    public void Dispose() => _stream.Dispose();

    private bool FillFloatBuffer(CancellationToken cancellationToken)
    {
        if (_floatOffset < _floatCount)
        {
            return true;
        }

        if (_endOfFile)
        {
            return false;
        }

        var totalByteCount = _pendingByteCount;
        while (totalByteCount < ByteBufferSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var readLength = Math.Min(ByteBufferSize - totalByteCount, _byteBuffer.Length - totalByteCount);
            var read = _stream.Read(_byteBuffer, totalByteCount, readLength);
            if (read == 0)
            {
                _endOfFile = true;
                break;
            }

            totalByteCount += read;
            if (totalByteCount >= ByteBufferSize)
            {
                break;
            }
        }

        var alignedByteCount = totalByteCount - (totalByteCount % sizeof(float));
        if (alignedByteCount == 0)
        {
            _pendingByteCount = totalByteCount;
            return false;
        }

        var floatCount = alignedByteCount / sizeof(float);
        MemoryMarshal.Cast<byte, float>(_byteBuffer.AsSpan(0, alignedByteCount))
            .CopyTo(_floatBuffer.AsSpan(0, floatCount));
        _floatOffset = 0;
        _floatCount = floatCount;

        _pendingByteCount = totalByteCount - alignedByteCount;
        if (_pendingByteCount > 0)
        {
            _byteBuffer.AsSpan(alignedByteCount, _pendingByteCount)
                .CopyTo(_byteBuffer.AsSpan(0, _pendingByteCount));
        }

        return true;
    }
}
