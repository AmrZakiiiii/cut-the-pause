using System.Text.Json;
using System.Text.Json.Serialization;
using CutThePause.Core.Models;

namespace CutThePause.App.Services;

public sealed class JsonExportCheckpointStore : IExportCheckpointStore
{
    private const int DefaultMaxCheckpoints = 10;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly int _maxCheckpoints;

    public JsonExportCheckpointStore(string? filePath = null, int maxCheckpoints = DefaultMaxCheckpoints)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? ResolveDefaultFilePath() : filePath;
        _maxCheckpoints = Math.Max(1, maxCheckpoints);
    }

    public IReadOnlyList<ExportCheckpoint> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return Array.Empty<ExportCheckpoint>();
            }

            var checkpoints = JsonSerializer.Deserialize<List<ExportCheckpoint>>(File.ReadAllText(_filePath), SerializerOptions);
            return checkpoints?
                .Where(static checkpoint => checkpoint is not null && !string.IsNullOrWhiteSpace(checkpoint.JobId))
                .OrderByDescending(static checkpoint => checkpoint.CreatedAt)
                .Take(_maxCheckpoints)
                .ToArray()
                ?? Array.Empty<ExportCheckpoint>();
        }
        catch (IOException)
        {
            return Array.Empty<ExportCheckpoint>();
        }
        catch (JsonException)
        {
            return Array.Empty<ExportCheckpoint>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<ExportCheckpoint>();
        }
    }

    public ExportCheckpoint? Find(string requestFingerprint) =>
        string.IsNullOrWhiteSpace(requestFingerprint)
            ? null
            : Load().FirstOrDefault(checkpoint => string.Equals(
                checkpoint.RequestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal));

    public bool Save(ExportCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (string.IsNullOrWhiteSpace(checkpoint.JobId) || string.IsNullOrWhiteSpace(checkpoint.WorkingDirectory))
        {
            return false;
        }

        var checkpoints = Load()
            .Where(existing => !string.Equals(existing.JobId, checkpoint.JobId, StringComparison.Ordinal))
            .Prepend(checkpoint)
            .Take(_maxCheckpoints)
            .ToArray();
        return SaveCheckpoints(checkpoints);
    }

    public bool Delete(ExportCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        var checkpoints = Load();
        var remaining = checkpoints
            .Where(existing => !string.Equals(existing.JobId, checkpoint.JobId, StringComparison.Ordinal))
            .ToArray();
        var saved = remaining.Length == checkpoints.Count || SaveCheckpoints(remaining);
        if (!saved)
        {
            return false;
        }

        DeleteGeneratedArtifacts(checkpoint);
        return true;
    }

    private bool SaveCheckpoints(IReadOnlyList<ExportCheckpoint> checkpoints)
    {
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(checkpoints, SerializerOptions));
            File.Move(temporaryPath, _filePath, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    private static void DeleteGeneratedArtifacts(ExportCheckpoint checkpoint)
    {
        string workingDirectory;
        try
        {
            workingDirectory = Path.GetFullPath(checkpoint.WorkingDirectory);
        }
        catch (ArgumentException)
        {
            return;
        }

        var prefix = workingDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? workingDirectory
            : workingDirectory + Path.DirectorySeparatorChar;

        foreach (var batchPath in checkpoint.BatchPaths)
        {
            try
            {
                var fullBatchPath = Path.GetFullPath(batchPath);
                if (fullBatchPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(fullBatchPath);
                }
            }
            catch (ArgumentException)
            {
            }
        }

        try
        {
            if (Directory.Exists(workingDirectory) && !Directory.EnumerateFileSystemEntries(workingDirectory).Any())
            {
                Directory.Delete(workingDirectory);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string ResolveDefaultFilePath()
    {
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(applicationData))
        {
            applicationData = Environment.CurrentDirectory;
        }

        return Path.Combine(applicationData, "Cut The Pause", "export-checkpoints.json");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
