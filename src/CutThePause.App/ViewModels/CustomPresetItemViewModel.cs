using CutThePause.Core.Models;

namespace CutThePause.App.ViewModels;

public sealed class CustomPresetItemViewModel
{
    public CustomPresetItemViewModel(NamedPreset preset)
    {
        Preset = preset;
    }

    public NamedPreset Preset { get; }

    public string Name => Preset.Name;

    public string Summary => $"Silence {Preset.MinSilenceMs} ms · Speech {Preset.MinSpeechMs} ms · {Preset.ExportPreset}";
}
