using CutThePause.Core.Models;

namespace CutThePause.App.ViewModels;

public sealed class ExportCheckpointItemViewModel
{
    public ExportCheckpointItemViewModel(ExportCheckpoint checkpoint)
    {
        Checkpoint = checkpoint;
    }

    public ExportCheckpoint Checkpoint { get; }

    public string Title => Path.GetFileName(Checkpoint.Request.OutputPath) is { Length: > 0 } fileName
        ? fileName
        : Checkpoint.Request.OutputPath;

    public string Subtitle => $"{Checkpoint.CompletedBatchIndexes.Count} completed batch(es) · {Checkpoint.EncoderLabel}";
}
