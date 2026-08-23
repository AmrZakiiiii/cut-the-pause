using CutThePause.Core.Models;

namespace CutThePause.App.ViewModels;

public sealed class HistoryEntryItemViewModel
{
    public HistoryEntryItemViewModel(HistoryEntry entry)
    {
        Entry = entry;
    }

    public HistoryEntry Entry { get; }

    public string Title => Path.GetFileName(Entry.InputPath) is { Length: > 0 } fileName
        ? fileName
        : Entry.InputPath;

    public string Subtitle => $"{Entry.Kind} · {Entry.Status} · {Entry.Timestamp.LocalDateTime:g}";

    public string OutputText => string.IsNullOrWhiteSpace(Entry.OutputPath)
        ? "Analysis review"
        : Entry.OutputPath;

    public bool CanLoad => Entry.Status == HistoryEntryStatus.Completed && Entry.AnalysisResult is not null;
}
