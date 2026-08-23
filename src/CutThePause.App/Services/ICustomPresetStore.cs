using CutThePause.Core.Models;

namespace CutThePause.App.Services;

public interface ICustomPresetStore
{
    IReadOnlyList<NamedPreset> Load();

    bool Upsert(NamedPreset preset);

    bool Delete(string name);
}
