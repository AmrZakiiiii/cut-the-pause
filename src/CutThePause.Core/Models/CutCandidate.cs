namespace CutThePause.Core.Models;

public sealed record CutCandidate(TimeSpan Start, TimeSpan End, string Reason, bool IsEnabled = true)
{
    public TimeSpan Duration => End - Start;

    public TimeRange Range => new(Start, End);
}
