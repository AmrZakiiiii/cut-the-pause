using System.Text.Json;
using System.Text.Json.Serialization;

namespace CutThePause.App.Services;

public sealed class JsonAnalysisSettingsStore : IAnalysisSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly string _filePath;

    public JsonAnalysisSettingsStore(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? ResolveDefaultFilePath() : filePath;
    }

    public AnalysisSettingsPreferences Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return AnalysisSettingsPreferences.Defaults;
            }

            var json = File.ReadAllText(_filePath);
            var preferences = JsonSerializer.Deserialize<AnalysisSettingsPreferences>(json, SerializerOptions);
            return AnalysisSettingsPreferences.Normalize(preferences);
        }
        catch (IOException)
        {
            return AnalysisSettingsPreferences.Defaults;
        }
        catch (JsonException)
        {
            return AnalysisSettingsPreferences.Defaults;
        }
        catch (UnauthorizedAccessException)
        {
            return AnalysisSettingsPreferences.Defaults;
        }
    }

    public bool Save(AnalysisSettingsPreferences preferences)
    {
        var normalized = AnalysisSettingsPreferences.Normalize(preferences);
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(normalized, SerializerOptions);
            File.WriteAllText(temporaryPath, json);
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
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
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

    private static string ResolveDefaultFilePath()
    {
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(applicationData))
        {
            applicationData = Environment.CurrentDirectory;
        }

        return Path.Combine(applicationData, "Cut The Pause", "detection-settings.json");
    }
}
