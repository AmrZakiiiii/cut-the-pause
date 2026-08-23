using CutThePause.Core.Models;

namespace CutThePause.Core.Services;

public static class TimelineSelection
{
    public static TimeSpan TimeAt(double x, double width, TimeSpan duration)
    {
        if (width <= 0d || duration <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var ratio = Math.Clamp(x / width, 0d, 1d);
        return TimeSpan.FromTicks((long)(duration.Ticks * ratio));
    }

    public static TimeRange? RangeFromPixels(
        double startX,
        double endX,
        double width,
        TimeSpan duration,
        TimeSpan? minimumDuration = null)
    {
        if (width <= 0d || duration <= TimeSpan.Zero)
        {
            return null;
        }

        var start = TimeAt(Math.Min(startX, endX), width, duration);
        var end = TimeAt(Math.Max(startX, endX), width, duration);
        var minimum = minimumDuration ?? TimeSpan.FromMilliseconds(100);
        return end - start >= minimum ? new TimeRange(start, end) : null;
    }

    public static int? FindCutAtPixel(
        double x,
        double width,
        TimeSpan duration,
        IReadOnlyList<CutCandidate> cuts)
    {
        ArgumentNullException.ThrowIfNull(cuts);
        var time = TimeAt(x, width, duration);
        for (var index = 0; index < cuts.Count; index++)
        {
            if (time >= cuts[index].Start && time <= cuts[index].End)
            {
                return index;
            }
        }

        return null;
    }
}
