namespace CutThePause.Core.Models;

public sealed record AnalysisSettings
{
    public int MinSilenceMs { get; init; } = 350;

    public int MinSpeechMs { get; init; } = 150;

    public int PaddingBeforeMs { get; init; } = 80;

    public int PaddingAfterMs { get; init; } = 120;

    public float SpeechThreshold { get; init; } = 0.5f;

    public ExportPreset ExportPreset { get; init; } = ExportPreset.Balanced;

    public TimeSpan MinSilence => TimeSpan.FromMilliseconds(MinSilenceMs);

    public TimeSpan MinSpeech => TimeSpan.FromMilliseconds(MinSpeechMs);

    public TimeSpan PaddingBefore => TimeSpan.FromMilliseconds(PaddingBeforeMs);

    public TimeSpan PaddingAfter => TimeSpan.FromMilliseconds(PaddingAfterMs);
}
