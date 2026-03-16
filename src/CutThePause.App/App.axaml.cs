using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CutThePause.App.Services;
using CutThePause.App.ViewModels;

namespace CutThePause.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = AppBootstrapper.CreateMainWindow();
            desktop.MainWindow = mainWindow;
            _ = TryStartShowcaseAsync(mainWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task TryStartShowcaseAsync(MainWindow window)
    {
        var inputPath = ResolveArgumentValue("--showcase-input");
        var shouldAnalyze = Program.StartupArguments.Contains("--showcase-analyzed", StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(inputPath) || window.DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        Dispatcher.UIThread.Post(async () => await viewModel.LoadShowcaseAsync(inputPath, shouldAnalyze), DispatcherPriority.Background);
        await Task.CompletedTask;
    }

    private static string? ResolveArgumentValue(string option)
    {
        for (var index = 0; index < Program.StartupArguments.Count - 1; index++)
        {
            if (Program.StartupArguments[index].Equals(option, StringComparison.OrdinalIgnoreCase))
            {
                return Program.StartupArguments[index + 1];
            }
        }

        return null;
    }
}
