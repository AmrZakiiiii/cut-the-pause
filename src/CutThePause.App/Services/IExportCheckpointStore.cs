using CutThePause.Core.Models;

namespace CutThePause.App.Services;

public interface IExportCheckpointStore
{
    IReadOnlyList<ExportCheckpoint> Load();

    ExportCheckpoint? Find(string requestFingerprint);

    bool Save(ExportCheckpoint checkpoint);

    bool Delete(ExportCheckpoint checkpoint);
}
