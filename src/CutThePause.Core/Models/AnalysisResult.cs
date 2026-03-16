namespace CutThePause.Core.Models;

public sealed record AnalysisResult(
    string InputPath,
    TimeSpan Duration,
    AnalysisSettings Settings,
    IReadOnlyList<KeepSegment> KeepSegments,
    IReadOnlyList<CutCandidate> CutCandidates,
    IReadOnlyList<string> Warnings)
{
    public TimeSpan RemovedDuration => CutCandidates.Where(candidate => candidate.IsEnabled).Aggregate(
        TimeSpan.Zero,
        static (current, candidate) => current + candidate.Duration);

    public TimeSpan OutputDuration => KeepSegments.Aggregate(
        TimeSpan.Zero,
        static (current, keepSegment) => current + keepSegment.Duration);
}
