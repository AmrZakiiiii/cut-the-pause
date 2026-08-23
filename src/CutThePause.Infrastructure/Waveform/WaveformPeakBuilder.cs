using System.Runtime.InteropServices;
using CutThePause.Core.Services;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Waveform;

public static class WaveformPeakBuilder
{
    public const int DefaultPeakCount = 1_200;

    public static IReadOnlyList<float> Build(
        PcmAudioData audio,
        int peakCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);
        cancellationToken.ThrowIfCancellationRequested();
        return WaveformPeakReducer.Reduce(audio.Samples, peakCount);
    }

    public static IReadOnlyList<float> Build(
        PcmAudioFile audio,
        int peakCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);
        if (peakCount <= 0 || audio.SampleCount <= 0)
        {
            return Array.Empty<float>();
        }

        var actualPeakCount = (int)Math.Min(audio.SampleCount, peakCount);
        var peaks = new float[actualPeakCount];
        var byteBuffer = new byte[64 * 1024];
        var carry = new byte[sizeof(float)];
        var carryCount = 0;
        long sampleIndex = 0;

        using var stream = audio.OpenRead();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = stream.Read(byteBuffer, 0, byteBuffer.Length);
            if (read == 0)
            {
                break;
            }

            var offset = 0;
            if (carryCount > 0)
            {
                var needed = sizeof(float) - carryCount;
                var copied = Math.Min(needed, read);
                Buffer.BlockCopy(byteBuffer, 0, carry, carryCount, copied);
                carryCount += copied;
                offset += copied;

                if (carryCount == sizeof(float))
                {
                    UpdatePeak(carry, 0, peaks, ref sampleIndex, audio.SampleCount);
                    carryCount = 0;
                }
            }

            var remaining = read - offset;
            var alignedLength = remaining - (remaining % sizeof(float));
            if (alignedLength > 0)
            {
                var samples = MemoryMarshal.Cast<byte, float>(byteBuffer.AsSpan(offset, alignedLength));
                foreach (var sample in samples)
                {
                    UpdatePeak(sample, peaks, ref sampleIndex, audio.SampleCount);
                }

                offset += alignedLength;
            }

            var trailing = read - offset;
            if (trailing > 0)
            {
                Buffer.BlockCopy(byteBuffer, offset, carry, 0, trailing);
                carryCount = trailing;
            }
        }

        if (carryCount != 0)
        {
            throw new InvalidDataException("The PCM waveform source ended on a partial sample.");
        }

        return peaks;
    }

    private static void UpdatePeak(
        byte[] buffer,
        int offset,
        float[] peaks,
        ref long sampleIndex,
        long sampleCount)
    {
        var sample = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(offset, sizeof(float)))[0];
        UpdatePeak(sample, peaks, ref sampleIndex, sampleCount);
    }

    private static void UpdatePeak(
        float sample,
        float[] peaks,
        ref long sampleIndex,
        long sampleCount)
    {
        if (sampleIndex >= sampleCount)
        {
            return;
        }

        var peakIndex = (int)Math.Min(
            peaks.Length - 1,
            sampleIndex * peaks.Length / sampleCount);
        if (float.IsFinite(sample))
        {
            peaks[peakIndex] = Math.Max(peaks[peakIndex], Math.Clamp(MathF.Abs(sample), 0f, 1f));
        }

        sampleIndex++;
    }
}
