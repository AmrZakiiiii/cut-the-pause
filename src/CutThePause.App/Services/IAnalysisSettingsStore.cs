namespace CutThePause.App.Services;

public interface IAnalysisSettingsStore
{
    AnalysisSettingsPreferences Load();

    bool Save(AnalysisSettingsPreferences preferences);
}
