using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CutThePause.App.Controls;
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

    private void OnShowNewCutClick(object? sender, RoutedEventArgs e) => ViewModel.ShowView(WorkspaceView.NewCut);

    private void OnShowReviewClick(object? sender, RoutedEventArgs e) => ViewModel.ShowView(WorkspaceView.Review);

    private void OnShowHistoryClick(object? sender, RoutedEventArgs e) => ViewModel.ShowView(WorkspaceView.History);

    private void OnShowHelpClick(object? sender, RoutedEventArgs e) => ViewModel.ShowView(WorkspaceView.Help);

    private void OnDismissExportOverlayClick(object? sender, RoutedEventArgs e) => ViewModel.DismissExportOverlay();

    private void OnRevealExportedFileClick(object? sender, RoutedEventArgs e) => ViewModel.RevealExportedFile();

    private void OnExpandTimelineClick(object? sender, RoutedEventArgs e) => ViewModel.OpenTimeline();

    private void OnCloseTimelineClick(object? sender, RoutedEventArgs e) => ViewModel.CloseTimeline();

    private void OnCancelOperationClick(object? sender, RoutedEventArgs e) => ViewModel.CancelOperation();

    private void OnPauseOperationClick(object? sender, RoutedEventArgs e) => ViewModel.PauseOperation();

    private void OnSaveCustomPresetClick(object? sender, RoutedEventArgs e) => ViewModel.SaveCustomPreset();

    private void OnApplyCustomPresetClick(object? sender, RoutedEventArgs e) => ViewModel.ApplySelectedCustomPreset();

    private void OnDeleteCustomPresetClick(object? sender, RoutedEventArgs e) => ViewModel.DeleteSelectedCustomPreset();

    private void OnLoadHistoryClick(object? sender, RoutedEventArgs e) => ViewModel.LoadSelectedHistory();

    private void OnMp4FormatClick(object? sender, RoutedEventArgs e) => ViewModel.SetOutputFormat(".mp4");

    private void OnMovFormatClick(object? sender, RoutedEventArgs e) => ViewModel.SetOutputFormat(".mov");

    private void OnResumePausedItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ExportCheckpointItemViewModel item })
        {
            ViewModel.SelectedPausedExport = item;
            _ = ViewModel.ResumeSelectedExport();
        }
    }

    private void OnDiscardPausedItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ExportCheckpointItemViewModel item })
        {
            ViewModel.SelectedPausedExport = item;
            ViewModel.DiscardSelectedExport();
        }
    }

    private void OnTimelineCutClicked(object? sender, TimelinePositionEventArgs e) => ViewModel.ToggleCutAt(e.Position);

    private void OnTimelineRangeSelected(object? sender, TimelineRangeSelectedEventArgs e) => ViewModel.AddManualCut(e.Range);

    private void OnDropZoneDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) && ViewModel.CanChangeSource
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDropZoneDrop(object? sender, DragEventArgs e)
    {
        var file = e.DataTransfer.TryGetFiles()?.FirstOrDefault();
        if (file?.TryGetLocalPath() is { } localPath)
        {
            ViewModel.SetInputPath(localPath);
        }

        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var meta = e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (meta && e.Key == Key.O)
        {
            if (ViewModel.CanChangeSource)
            {
                OnImportVideoClick(this, new RoutedEventArgs());
            }

            e.Handled = true;
        }
        else if (meta && e.Key == Key.E)
        {
            if (ViewModel.CanExport)
            {
                _ = ViewModel.ExportAsync();
            }

            e.Handled = true;
        }
        else if (meta && e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (ViewModel.CanAnalyze)
            {
                _ = ViewModel.AnalyzeAsync();
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (ViewModel.IsTimelineExpanded)
            {
                ViewModel.CloseTimeline();
            }
            else
            {
                ViewModel.CancelOperation();
            }

            e.Handled = true;
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (ViewModel.IsExporting)
        {
            ViewModel.PauseOperation();
        }
        else
        {
            ViewModel.CancelOperation();
        }
    }
}
