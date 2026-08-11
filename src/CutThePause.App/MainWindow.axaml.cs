using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CutThePause.App.ViewModels;

namespace CutThePause.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnWindowClosing;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private async void OnImportVideoClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a video",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Video files")
                {
                    Patterns = new[] { "*.mp4", "*.mov", "*.m4v", "*.mkv" }
                }
            }
        });

        var file = files.FirstOrDefault();
        if (file is not null)
        {
            ViewModel.SetInputPath(file.Path.LocalPath);
        }
    }

    private async void OnChooseOutputClick(object? sender, RoutedEventArgs e)
    {
        var suggestedExtension = Path.GetExtension(ViewModel.SuggestedOutputFileName).TrimStart('.');
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export trimmed video",
            SuggestedFileName = ViewModel.SuggestedOutputFileName,
            DefaultExtension = string.IsNullOrWhiteSpace(suggestedExtension) ? "mp4" : suggestedExtension,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("MP4 video")
                {
                    Patterns = new[] { "*.mp4" }
                },
                new FilePickerFileType("MOV master (ProRes)")
                {
                    Patterns = new[] { "*.mov" }
                }
            }
        });

        if (file is not null)
        {
            ViewModel.SetOutputPath(file.Path.LocalPath);
        }
    }

    private async void OnAnalyzeClick(object? sender, RoutedEventArgs e) => await ViewModel.AnalyzeAsync();

    private async void OnExportClick(object? sender, RoutedEventArgs e) => await ViewModel.ExportAsync();

    private void OnResetClick(object? sender, RoutedEventArgs e) => ViewModel.ResetReview();

    private void OnOpenSourceClick(object? sender, RoutedEventArgs e) => ViewModel.OpenInputFile();

    private void OnDismissExportOverlayClick(object? sender, RoutedEventArgs e) => ViewModel.DismissExportOverlay();

    private void OnRevealExportedFileClick(object? sender, RoutedEventArgs e) => ViewModel.RevealExportedFile();

    private void OnCancelOperationClick(object? sender, RoutedEventArgs e) => ViewModel.CancelOperation();

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e) => ViewModel.CancelOperation();
}
