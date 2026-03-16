namespace CutThePause.Core.Models;

public sealed record SpeechSegment(TimeSpan Start, TimeSpan End, float Confidence = 1.0f)
{
    public TimeSpan Duration => End - Start;

    public TimeRange Range => new(Start, End);
}
