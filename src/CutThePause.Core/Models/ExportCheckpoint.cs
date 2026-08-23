namespace CutThePause.Core.Models;

public sealed record ExportCheckpoint(
    string JobId,
    string RequestFingerprint,
    ExportRequest Request,
    string WorkingDirectory,
    IReadOnlyList<string> BatchPaths,
    IReadOnlyList<int> CompletedBatchIndexes,
    DateTimeOffset CreatedAt,
    string EncoderLabel);
