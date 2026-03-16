using CutThePause.Core.Models;

namespace CutThePause.Core.Services;

public static class ExportPlanBuilder
{
    public static ExportRequest BuildRequest(
        string inputPath,
        string outputPath,
        TimeSpan sourceDuration,
        IEnumerable<CutCandidate> cutCandidates,
        ExportPreset preset)
    {
        ArgumentNullException.ThrowIfNull(cutCandidates);

        var candidates = cutCandidates
            .OrderBy(static candidate => candidate.Start)
            .ToArray();

        var keepSegments = BuildKeepSegments(sourceDuration, candidates.Where(static candidate => candidate.IsEnabled));

        return new ExportRequest(
            inputPath,
            outputPath,
            sourceDuration,
            preset,
            candidates,
            keepSegments);
    }

    public static IReadOnlyList<KeepSegment> BuildKeepSegments(
        TimeSpan sourceDuration,
        IEnumerable<CutCandidate> enabledCuts)
    {
        var orderedCuts = enabledCuts
            .OrderBy(static cut => cut.Start)
            .ToList();

        if (orderedCuts.Count == 0)
        {
            return sourceDuration > TimeSpan.Zero
                ? new[] { new KeepSegment(0, TimeSpan.Zero, sourceDuration) }
                : Array.Empty<KeepSegment>();
        }

        var keepSegments = new List<KeepSegment>();
        var cursor = TimeSpan.Zero;
        var order = 0;

        foreach (var cut in orderedCuts)
        {
            if (cut.Start > cursor)
            {
                keepSegments.Add(new KeepSegment(order++, cursor, cut.Start));
            }

            cursor = cut.End > cursor ? cut.End : cursor;
        }

        if (cursor < sourceDuration)
        {
            keepSegments.Add(new KeepSegment(order, cursor, sourceDuration));
        }

        return keepSegments;
    }
}
