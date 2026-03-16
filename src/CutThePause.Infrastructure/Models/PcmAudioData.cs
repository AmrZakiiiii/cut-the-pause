namespace CutThePause.Infrastructure.Models;

public sealed record PcmAudioData(float[] Samples, int SampleRate)
{
    public TimeSpan Duration => SampleRate <= 0
        ? TimeSpan.Zero
        : TimeSpan.FromSeconds((double)Samples.Length / SampleRate);
}
