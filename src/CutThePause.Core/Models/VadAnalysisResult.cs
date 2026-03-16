namespace CutThePause.Core.Models;

public sealed record VadAnalysisResult(
    IReadOnlyList<SpeechSegment> SpeechSegments,
    IReadOnlyList<string> Warnings);
