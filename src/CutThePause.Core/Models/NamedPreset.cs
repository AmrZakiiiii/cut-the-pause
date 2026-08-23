namespace CutThePause.Core.Models;

public sealed record NamedPreset(
    string Name,
    int MinSilenceMs,
    int MinSpeechMs,
    int PaddingBeforeMs,
    int PaddingAfterMs,
    float SpeechThreshold,
    ExportPreset ExportPreset)
{
    public AnalysisSettings ToAnalysisSettings() => new()
    {
        MinSilenceMs = MinSilenceMs,
        MinSpeechMs = MinSpeechMs,
        PaddingBeforeMs = PaddingBeforeMs,
        PaddingAfterMs = PaddingAfterMs,
        SpeechThreshold = SpeechThreshold,
        ExportPreset = ExportPreset
    };

    public static NamedPreset FromSettings(string name, AnalysisSettings settings) => new(
        name,
        settings.MinSilenceMs,
        settings.MinSpeechMs,
        settings.PaddingBeforeMs,
        settings.PaddingAfterMs,
        settings.SpeechThreshold,
        settings.ExportPreset);
}
