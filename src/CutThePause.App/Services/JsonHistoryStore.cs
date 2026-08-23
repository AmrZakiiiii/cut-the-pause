using System.Text.Json;
using System.Text.Json.Serialization;
using CutThePause.Core.Models;

namespace CutThePause.App.Services;

public sealed class JsonHistoryStore : IHistoryStore
{
    private const int DefaultMaxEntries = 30;
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly string _filePath;
    private readonly int _maxEntries;

    public JsonHistoryStore(string? filePath = null, int maxEntries = DefaultMaxEntries)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? ResolveDefaultFilePath() : filePath;
        _maxEntries = Math.Max(1, maxEntries);
    }

    public IReadOnlyList<HistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return Array.Empty<HistoryEntry>();
            }

            var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(_filePath), SerializerOptions);
            return entries?
                .Where(static entry => entry is not null && !string.IsNullOrWhiteSpace(entry.Id))
                .OrderByDescending(static entry => entry.Timestamp)
                .Take(_maxEntries)
                .ToArray()
                ?? Array.Empty<HistoryEntry>();
        }
        catch (IOException)
        {
            return Array.Empty<HistoryEntry>();
        }
        catch (JsonException)
        {
            return Array.Empty<HistoryEntry>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<HistoryEntry>();
        }
    }

    public bool Append(HistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.Id))
        {
            return false;
        }

        var entries = Load()
            .Where(existing => !string.Equals(existing.Id, entry.Id, StringComparison.Ordinal))
            .Prepend(entry)
            .Take(_maxEntries)
            .ToArray();
        return SaveEntries(entries);
    }

    public bool Remove(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var entries = Load();
        var remaining = entries.Where(entry => !string.Equals(entry.Id, id, StringComparison.Ordinal)).ToArray();
        return remaining.Length != entries.Count && SaveEntries(remaining);
    }

    private bool SaveEntries(IReadOnlyList<HistoryEntry> entries)
    {
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entries, SerializerOptions));
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
            TryDelete(temporaryPath);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string ResolveDefaultFilePath()
    {
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(applicationData))
        {
            applicationData = Environment.CurrentDirectory;
        }

        return Path.Combine(applicationData, "Cut The Pause", "history.json");
    }

    private static void TryDelete(string path)
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
