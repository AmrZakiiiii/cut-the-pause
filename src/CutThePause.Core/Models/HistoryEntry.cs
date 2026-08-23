namespace CutThePause.Core.Models;

public enum HistoryEntryKind
{
    Analysis,
    Export
}

public enum HistoryEntryStatus
{
    Completed,
    Failed,
    Canceled,
    Paused
}

public sealed record HistoryEntry(
    string Id,
    DateTimeOffset Timestamp,
    HistoryEntryKind Kind,
    HistoryEntryStatus Status,
    string InputPath,
    string? OutputPath,
    string SourceFingerprint,
    AnalysisResult? AnalysisResult,
    long? OutputBytes,
    string Message);
