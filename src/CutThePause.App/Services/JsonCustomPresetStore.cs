using System.Text.Json;
using System.Text.Json.Serialization;
using CutThePause.Core.Models;

namespace CutThePause.App.Services;

public sealed class JsonCustomPresetStore : ICustomPresetStore
{
    private const int DefaultMaxPresets = 30;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly int _maxPresets;

    public JsonCustomPresetStore(string? filePath = null, int maxPresets = DefaultMaxPresets)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? ResolveDefaultFilePath() : filePath;
        _maxPresets = Math.Max(1, maxPresets);
    }

    public IReadOnlyList<NamedPreset> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return Array.Empty<NamedPreset>();
            }

            var presets = JsonSerializer.Deserialize<List<NamedPreset>>(File.ReadAllText(_filePath), SerializerOptions)
                ?? new List<NamedPreset>();
            return presets
                .Select(Normalize)
                .Where(static preset => preset is not null)
                .Select(static preset => preset!)
                .Take(_maxPresets)
                .ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<NamedPreset>();
        }
        catch (JsonException)
        {
            return Array.Empty<NamedPreset>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<NamedPreset>();
        }
    }

    public bool Upsert(NamedPreset preset)
    {
        var normalized = Normalize(preset);
        if (normalized is null)
        {
            return false;
        }

        var presets = Load()
            .Where(existing => !string.Equals(existing.Name, normalized.Name, StringComparison.OrdinalIgnoreCase))
            .Prepend(normalized)
            .Take(_maxPresets)
            .ToArray();
        return SavePresets(presets);
    }

    public bool Delete(string name)
    {
        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return false;
        }

        var presets = Load();
        var remaining = presets
            .Where(preset => !string.Equals(preset.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return remaining.Length != presets.Count && SavePresets(remaining);
    }

    private bool SavePresets(IReadOnlyList<NamedPreset> presets)
    {
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(presets, SerializerOptions));
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

    private static NamedPreset? Normalize(NamedPreset? preset)
    {
        if (preset is null ||
            string.IsNullOrWhiteSpace(preset.Name) ||
            preset.MinSilenceMs < 0 ||
            preset.MinSpeechMs <= 0 ||
            preset.PaddingBeforeMs < 0 ||
            preset.PaddingAfterMs < 0 ||
            !float.IsFinite(preset.SpeechThreshold) ||
            preset.SpeechThreshold is < 0f or > 1f ||
            !Enum.IsDefined(preset.ExportPreset))
        {
            return null;
        }

        return preset with { Name = preset.Name.Trim() };
    }

    private static string ResolveDefaultFilePath()
    {
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(applicationData))
        {
            applicationData = Environment.CurrentDirectory;
        }

        return Path.Combine(applicationData, "Cut The Pause", "custom-presets.json");
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
