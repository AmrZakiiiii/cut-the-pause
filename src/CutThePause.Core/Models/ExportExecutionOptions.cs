namespace CutThePause.Core.Models;

public sealed record ExportExecutionOptions(
    ExportCheckpoint? Checkpoint = null,
    Action<ExportCheckpoint>? CheckpointSaved = null);
