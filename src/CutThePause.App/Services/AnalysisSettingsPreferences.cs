using CutThePause.Core.Models;

namespace CutThePause.App.Services;

public sealed record AnalysisSettingsPreferences(
    int MinSilenceMs,
    int MinSpeechMs,
    int PaddingBeforeMs,
    int PaddingAfterMs,
    float SpeechThreshold,
    ExportPreset ExportPreset)
{
    public static AnalysisSettingsPreferences Defaults { get; } = new(
        MinSilenceMs: 350,
        MinSpeechMs: 150,
        PaddingBeforeMs: 80,
        PaddingAfterMs: 120,
        SpeechThreshold: 0.5f,
        ExportPreset: ExportPreset.Balanced);

    public static AnalysisSettingsPreferences Normalize(AnalysisSettingsPreferences? value)
    {
        if (value is null)
        {
            return Defaults;
        }

        return new AnalysisSettingsPreferences(
            value.MinSilenceMs >= 0 ? value.MinSilenceMs : Defaults.MinSilenceMs,
            value.MinSpeechMs > 0 ? value.MinSpeechMs : Defaults.MinSpeechMs,
            value.PaddingBeforeMs >= 0 ? value.PaddingBeforeMs : Defaults.PaddingBeforeMs,
            value.PaddingAfterMs >= 0 ? value.PaddingAfterMs : Defaults.PaddingAfterMs,
            float.IsFinite(value.SpeechThreshold) && value.SpeechThreshold is >= 0f and <= 1f
                ? value.SpeechThreshold
                : Defaults.SpeechThreshold,
            Enum.IsDefined(value.ExportPreset) ? value.ExportPreset : Defaults.ExportPreset);
    }
}
