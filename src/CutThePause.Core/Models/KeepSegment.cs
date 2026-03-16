namespace CutThePause.Core.Models;

public sealed record KeepSegment(int Order, TimeSpan Start, TimeSpan End)
{
    public TimeSpan Duration => End - Start;

    public TimeRange Range => new(Start, End);
}
