namespace CutThePause.Core.Models;

public sealed record ExportRequest(
    string InputPath,
    string OutputPath,
    TimeSpan SourceDuration,
    ExportPreset Preset,
    IReadOnlyList<CutCandidate> CutCandidates,
    IReadOnlyList<KeepSegment> KeepSegments)
{
    public TimeSpan OutputDuration => KeepSegments.Aggregate(
        TimeSpan.Zero,
        static (current, keepSegment) => current + keepSegment.Duration);
}
