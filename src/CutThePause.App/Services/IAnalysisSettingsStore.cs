namespace CutThePause.App.Services;

public interface IAnalysisSettingsStore
{
    AnalysisSettingsPreferences Load();

    void Save(AnalysisSettingsPreferences preferences);
}
