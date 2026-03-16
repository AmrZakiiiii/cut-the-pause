namespace CutThePause.Core.Models;

public readonly record struct TimeRange
{
    public TimeRange(TimeSpan start, TimeSpan end)
    {
        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "End must be greater than or equal to start.");
        }

        Start = start;
        End = end;
    }

    public TimeSpan Start { get; }

    public TimeSpan End { get; }

    public TimeSpan Duration => End - Start;

    public bool IsEmpty => Duration <= TimeSpan.Zero;

    public TimeRange Expand(TimeSpan before, TimeSpan after, TimeSpan maxDuration)
    {
        var start = Start - before;
        var end = End + after;

        if (start < TimeSpan.Zero)
        {
            start = TimeSpan.Zero;
        }

        if (end > maxDuration)
        {
            end = maxDuration;
        }

        return new TimeRange(start, end);
    }
}
