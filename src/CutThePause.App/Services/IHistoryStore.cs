using CutThePause.Core.Models;

namespace CutThePause.App.Services;

public interface IHistoryStore
{
    IReadOnlyList<HistoryEntry> Load();

    bool Append(HistoryEntry entry);

    bool Remove(string id);
}
