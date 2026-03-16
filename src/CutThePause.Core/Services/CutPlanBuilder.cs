using CutThePause.Core.Models;

namespace CutThePause.Core.Services;

public static class CutPlanBuilder
{
    public static AnalysisResult Build(
        string inputPath,
        TimeSpan duration,
        IEnumerable<SpeechSegment> speechSegments,
        AnalysisSettings settings,
        IEnumerable<string>? warnings = null)
    {
        var keepSegments = BuildKeepSegments(duration, speechSegments, settings);
        var cutCandidates = BuildCutCandidates(duration, keepSegments);
        var warningList = warnings?.Where(static warning => !string.IsNullOrWhiteSpace(warning)).Distinct().ToArray()
            ?? Array.Empty<string>();

        return new AnalysisResult(
            inputPath,
            duration,
            settings,
            keepSegments,
            cutCandidates,
            warningList);
    }

    public static IReadOnlyList<KeepSegment> BuildKeepSegments(
        TimeSpan duration,
        IEnumerable<SpeechSegment> speechSegments,
        AnalysisSettings settings)
    {
        ArgumentNullException.ThrowIfNull(speechSegments);
        ArgumentNullException.ThrowIfNull(settings);

        if (duration <= TimeSpan.Zero)
        {
            return Array.Empty<KeepSegment>();
        }

        var paddedSpeechRanges = speechSegments
            .OrderBy(static segment => segment.Start)
            .Select(segment => segment.Range.Expand(settings.PaddingBefore, settings.PaddingAfter, duration))
            .Where(static range => !range.IsEmpty)
            .ToList();

        if (paddedSpeechRanges.Count == 0)
        {
            return new[] { new KeepSegment(0, TimeSpan.Zero, duration) };
        }

        var mergedRanges = MergeRanges(paddedSpeechRanges, settings.MinSilence);
        if (mergedRanges.Count > 0 && mergedRanges[0].Start <= settings.MinSilence)
        {
            mergedRanges[0] = new TimeRange(TimeSpan.Zero, mergedRanges[0].End);
        }

        if (mergedRanges.Count > 0)
        {
            var lastIndex = mergedRanges.Count - 1;
            if (duration - mergedRanges[lastIndex].End <= settings.MinSilence)
            {
                mergedRanges[lastIndex] = new TimeRange(mergedRanges[lastIndex].Start, duration);
            }
        }

        return mergedRanges
            .Select(static (range, index) => new KeepSegment(index, range.Start, range.End))
            .ToArray();
    }

    public static IReadOnlyList<CutCandidate> BuildCutCandidates(
        TimeSpan sourceDuration,
        IEnumerable<KeepSegment> keepSegments)
    {
        ArgumentNullException.ThrowIfNull(keepSegments);

        var orderedKeepSegments = keepSegments
            .OrderBy(static segment => segment.Start)
            .ToList();

        if (orderedKeepSegments.Count == 0)
        {
            return Array.Empty<CutCandidate>();
        }

        var cuts = new List<CutCandidate>();
        var cursor = TimeSpan.Zero;

        foreach (var keepSegment in orderedKeepSegments)
        {
            if (keepSegment.Start > cursor)
            {
                cuts.Add(new CutCandidate(cursor, keepSegment.Start, "Detected silence"));
            }

            cursor = keepSegment.End;
        }

        if (cursor < sourceDuration)
        {
            cuts.Add(new CutCandidate(cursor, sourceDuration, "Detected silence"));
        }

        return cuts;
    }

    private static List<TimeRange> MergeRanges(IReadOnlyList<TimeRange> ranges, TimeSpan minSilence)
    {
        if (ranges.Count == 0)
        {
            return new List<TimeRange>();
        }

        var merged = new List<TimeRange> { ranges[0] };

        for (var index = 1; index < ranges.Count; index++)
        {
            var current = ranges[index];
            var previous = merged[^1];
            var gap = current.Start - previous.End;

            if (gap <= minSilence)
            {
                merged[^1] = new TimeRange(previous.Start, current.End > previous.End ? current.End : previous.End);
                continue;
            }

            merged.Add(current);
        }

        return merged;
    }
}
