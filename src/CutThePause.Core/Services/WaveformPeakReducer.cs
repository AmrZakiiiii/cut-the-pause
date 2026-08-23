namespace CutThePause.Core.Services;

public static class WaveformPeakReducer
{
    public static IReadOnlyList<float> Reduce(ReadOnlySpan<float> samples, int peakCount)
    {
        if (samples.Length == 0 || peakCount <= 0)
        {
            return Array.Empty<float>();
        }

        var actualPeakCount = Math.Min(samples.Length, peakCount);
        var peaks = new float[actualPeakCount];

        for (var peakIndex = 0; peakIndex < actualPeakCount; peakIndex++)
        {
            var start = (int)((long)peakIndex * samples.Length / actualPeakCount);
            var end = (int)((long)(peakIndex + 1) * samples.Length / actualPeakCount);
            end = Math.Max(end, start + 1);
            end = Math.Min(end, samples.Length);

            var peak = 0f;
            for (var sampleIndex = start; sampleIndex < end; sampleIndex++)
            {
                var sample = samples[sampleIndex];
                if (!float.IsFinite(sample))
                {
                    continue;
                }

                peak = Math.Max(peak, MathF.Abs(sample));
            }

            peaks[peakIndex] = Math.Clamp(peak, 0f, 1f);
        }

        return peaks;
    }
}
