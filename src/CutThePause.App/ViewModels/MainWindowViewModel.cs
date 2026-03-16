using System.Collections.ObjectModel;
using System.Diagnostics;
using CutThePause.Core.Models;
using CutThePause.Core.Services;
using CutThePause.Infrastructure;

namespace CutThePause.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly VideoWorkflowService _workflowService;
    private AnalysisResult? _analysisResult;
    private string? _inputPath;
    private string? _outputPath;
    private string _statusMessage = "Choose a source video to start.";
    private string _warningsText = string.Empty;
    private bool _isBusy;
    private string _minSilenceMsText = "350";
    private string _minSpeechMsText = "150";
    private string _paddingBeforeMsText = "80";
    private string _paddingAfterMsText = "120";
    private string _speechThresholdText = "0.50";
    private ExportPreset _selectedPreset = ExportPreset.Balanced;
    private string _originalDurationText = "--";
    private string _removedDurationText = "--";
    private string _outputDurationText = "--";
    private bool _isExporting;
    private bool _isExportCompleted;
    private double _exportProgressValue;
    private string _exportProgressPercentText = "0%";
    private string _exportProgressStageText = "Waiting to start export.";
    private string _exportProgressDetailText = "No export in progress.";
    private string _exportOverlayTitle = "Export In Progress";
    private string _exportOverlaySubtitle = "Cut The Pause is rendering your trimmed timeline now.";
    private string _exportedOutputPath = string.Empty;

    public MainWindowViewModel(VideoWorkflowService workflowService)
    {
        _workflowService = workflowService;
        CutCandidates = new ObservableCollection<CutCandidateItemViewModel>();
        PresetOptions = Enum.GetValues<ExportPreset>();
    }

    public ObservableCollection<CutCandidateItemViewModel> CutCandidates { get; }

    public IReadOnlyList<ExportPreset> PresetOptions { get; }

    public string InputPathDisplay => string.IsNullOrWhiteSpace(_inputPath) ? "No video selected yet." : _inputPath;

    public string OutputPathDisplay => string.IsNullOrWhiteSpace(_outputPath) ? "No export path selected yet." : _outputPath;

    public string SuggestedOutputFileName => string.IsNullOrWhiteSpace(_inputPath)
        ? "trimmed-video.mp4"
        : $"{Path.GetFileNameWithoutExtension(_inputPath)}.trimmed{ResolvePreferredOutputExtension(_inputPath)}";

    public bool HasInput => !string.IsNullOrWhiteSpace(_inputPath);

    public bool CanAnalyze => !_isBusy && HasInput;

    public bool CanExport => !_isBusy && _analysisResult is not null && !string.IsNullOrWhiteSpace(_outputPath);

    public bool IsExporting
    {
        get => _isExporting;
        private set => SetProperty(ref _isExporting, value);
    }

    public bool IsExportCompleted
    {
        get => _isExportCompleted;
        private set
        {
            if (SetProperty(ref _isExportCompleted, value))
            {
                OnPropertyChanged(nameof(ShowExportOverlay));
            }
        }
    }

    public bool ShowExportOverlay => IsExporting || IsExportCompleted;

    public bool HasCuts => CutCandidates.Count > 0;

    public bool ShowEmptyState => !HasCuts;

    public bool HasWarnings => !string.IsNullOrWhiteSpace(_warningsText);

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string WarningsText
    {
        get => _warningsText;
        private set
        {
            if (SetProperty(ref _warningsText, value))
            {
                OnPropertyChanged(nameof(HasWarnings));
            }
        }
    }

    public string MinSilenceMsText
    {
        get => _minSilenceMsText;
        set => SetProperty(ref _minSilenceMsText, value);
    }

    public string MinSpeechMsText
    {
        get => _minSpeechMsText;
        set => SetProperty(ref _minSpeechMsText, value);
    }

    public string PaddingBeforeMsText
    {
        get => _paddingBeforeMsText;
        set => SetProperty(ref _paddingBeforeMsText, value);
    }

    public string PaddingAfterMsText
    {
        get => _paddingAfterMsText;
        set => SetProperty(ref _paddingAfterMsText, value);
    }

    public string SpeechThresholdText
    {
        get => _speechThresholdText;
        set => SetProperty(ref _speechThresholdText, value);
    }

    public ExportPreset SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                RecalculateSummary();
            }
        }
    }

    public string OriginalDurationText
    {
        get => _originalDurationText;
        private set => SetProperty(ref _originalDurationText, value);
    }

    public string RemovedDurationText
    {
        get => _removedDurationText;
        private set => SetProperty(ref _removedDurationText, value);
    }

    public string OutputDurationText
    {
        get => _outputDurationText;
        private set => SetProperty(ref _outputDurationText, value);
    }

    public double ExportProgressValue
    {
        get => _exportProgressValue;
        private set => SetProperty(ref _exportProgressValue, value);
    }

    public string ExportProgressPercentText
    {
        get => _exportProgressPercentText;
        private set => SetProperty(ref _exportProgressPercentText, value);
    }

    public string ExportProgressStageText
    {
        get => _exportProgressStageText;
        private set => SetProperty(ref _exportProgressStageText, value);
    }

    public string ExportProgressDetailText
    {
        get => _exportProgressDetailText;
        private set => SetProperty(ref _exportProgressDetailText, value);
    }

    public string ExportOverlayTitle
    {
        get => _exportOverlayTitle;
        private set => SetProperty(ref _exportOverlayTitle, value);
    }

    public string ExportOverlaySubtitle
    {
        get => _exportOverlaySubtitle;
        private set => SetProperty(ref _exportOverlaySubtitle, value);
    }

    public string ExportedOutputPath
    {
        get => _exportedOutputPath;
        private set => SetProperty(ref _exportedOutputPath, value);
    }

    public void SetInputPath(string path)
    {
        _inputPath = path;
        _outputPath = SuggestOutputPath(path);
        _analysisResult = null;
        ResetReview();

        StatusMessage = "Video selected. Adjust the settings, then run analysis.";
        OnPropertyChanged(nameof(InputPathDisplay));
        OnPropertyChanged(nameof(OutputPathDisplay));
        OnPropertyChanged(nameof(SuggestedOutputFileName));
        RaiseStateProperties();
    }

    public void SetOutputPath(string path)
    {
        _outputPath = path;
        OnPropertyChanged(nameof(OutputPathDisplay));
        RaiseStateProperties();
    }

    public async Task AnalyzeAsync()
    {
        if (string.IsNullOrWhiteSpace(_inputPath))
        {
            StatusMessage = "Choose a source video before analyzing.";
            return;
        }

        try
        {
            SetBusy(true, "Analyzing audio and detecting silence...");
            var settings = BuildSettings();
            _analysisResult = await _workflowService.AnalyzeAsync(_inputPath, settings, CancellationToken.None);

            PopulateCuts(_analysisResult.CutCandidates);
            WarningsText = string.Join(Environment.NewLine, _analysisResult.Warnings);
            StatusMessage = $"Analysis complete. {_analysisResult.CutCandidates.Count} cut candidates detected.";
            RecalculateSummary();
            RaiseStateProperties();
        }
        catch (Exception exception)
        {
            WarningsText = exception.Message;
            StatusMessage = "Analysis failed.";
        }
        finally
        {
            SetBusy(false, StatusMessage);
        }
    }

    public async Task ExportAsync()
    {
        if (_analysisResult is null || string.IsNullOrWhiteSpace(_inputPath) || string.IsNullOrWhiteSpace(_outputPath))
        {
            StatusMessage = "Analyze a video and choose an export path before exporting.";
            return;
        }

        try
        {
            SetBusy(true, "Rendering trimmed video with FFmpeg...");
            var request = BuildExportRequest();
            var progress = new Progress<VideoExportProgress>(OnExportProgressReported);
            var outputDirectory = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            BeginExportPresentation(request);
            await _workflowService.ExportAsync(request, progress, CancellationToken.None);
            StatusMessage = $"Export complete: {_outputPath}";
            CompleteExportPresentation(request);
        }
        catch (Exception exception)
        {
            WarningsText = exception.Message;
            StatusMessage = "Export failed.";
        }
        finally
        {
            if (!IsExportCompleted)
            {
                EndExportPresentation();
            }

            SetBusy(false, StatusMessage);
        }
    }

    public async Task LoadShowcaseAsync(string inputPath, bool analyze)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            return;
        }

        SetInputPath(inputPath);

        if (analyze)
        {
            await AnalyzeAsync();
        }
    }

    public void DismissExportOverlay()
    {
        IsExporting = false;
        IsExportCompleted = false;
    }

    public void RevealExportedFile()
    {
        if (string.IsNullOrWhiteSpace(_outputPath) || !File.Exists(_outputPath))
        {
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            var process = new ProcessStartInfo("open");
            process.ArgumentList.Add("-R");
            process.ArgumentList.Add(_outputPath);
            Process.Start(process);
            return;
        }

        OpenInputFile();
    }

    public void ResetReview()
    {
        _analysisResult = null;

        foreach (var item in CutCandidates)
        {
            item.PropertyChanged -= OnCutCandidatePropertyChanged;
        }

        CutCandidates.Clear();
        WarningsText = string.Empty;
        OriginalDurationText = "--";
        RemovedDurationText = "--";
        OutputDurationText = "--";
        OnPropertyChanged(nameof(HasCuts));
        OnPropertyChanged(nameof(ShowEmptyState));
        RaiseStateProperties();
    }

    public void OpenInputFile()
    {
        if (string.IsNullOrWhiteSpace(_inputPath) || !File.Exists(_inputPath))
        {
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            var process = new ProcessStartInfo("open");
            process.ArgumentList.Add(_inputPath);
            Process.Start(process);
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo(_inputPath) { UseShellExecute = true });
        }
    }

    private AnalysisSettings BuildSettings() => new()
    {
        MinSilenceMs = ParseInteger(_minSilenceMsText, 350),
        MinSpeechMs = ParseInteger(_minSpeechMsText, 150),
        PaddingBeforeMs = ParseInteger(_paddingBeforeMsText, 80),
        PaddingAfterMs = ParseInteger(_paddingAfterMsText, 120),
        SpeechThreshold = ParseFloat(_speechThresholdText, 0.5f),
        ExportPreset = SelectedPreset
    };

    private ExportRequest BuildExportRequest()
    {
        if (_analysisResult is null || string.IsNullOrWhiteSpace(_inputPath) || string.IsNullOrWhiteSpace(_outputPath))
        {
            throw new InvalidOperationException("The export request is not ready yet.");
        }

        return ExportPlanBuilder.BuildRequest(
            _inputPath,
            _outputPath,
            _analysisResult.Duration,
            CutCandidates.Select(static candidate => candidate.ToModel()),
            SelectedPreset);
    }

    private void PopulateCuts(IEnumerable<CutCandidate> cutCandidates)
    {
        foreach (var item in CutCandidates)
        {
            item.PropertyChanged -= OnCutCandidatePropertyChanged;
        }

        CutCandidates.Clear();
        foreach (var cut in cutCandidates)
        {
            var item = new CutCandidateItemViewModel(cut);
            item.PropertyChanged += OnCutCandidatePropertyChanged;
            CutCandidates.Add(item);
        }

        OnPropertyChanged(nameof(HasCuts));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void OnCutCandidatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CutCandidateItemViewModel.IsEnabled))
        {
            RecalculateSummary();
        }
    }

    private void RecalculateSummary()
    {
        if (_analysisResult is null)
        {
            return;
        }

        var exportRequest = ExportPlanBuilder.BuildRequest(
            _analysisResult.InputPath,
            _outputPath ?? SuggestedOutputFileName,
            _analysisResult.Duration,
            CutCandidates.Select(static candidate => candidate.ToModel()),
            SelectedPreset);

        OriginalDurationText = FormatDuration(_analysisResult.Duration);
        RemovedDurationText = FormatDuration(exportRequest.CutCandidates.Where(static cut => cut.IsEnabled).Aggregate(
            TimeSpan.Zero,
            static (current, cut) => current + cut.Duration));
        OutputDurationText = FormatDuration(exportRequest.OutputDuration);
    }

    private void SetBusy(bool isBusy, string message)
    {
        _isBusy = isBusy;
        StatusMessage = message;
        RaiseStateProperties();
    }

    private void RaiseStateProperties()
    {
        OnPropertyChanged(nameof(HasInput));
        OnPropertyChanged(nameof(CanAnalyze));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(InputPathDisplay));
        OnPropertyChanged(nameof(OutputPathDisplay));
    }

    private static string SuggestOutputPath(string inputPath)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        var fileName = $"{Path.GetFileNameWithoutExtension(inputPath)}.trimmed{ResolvePreferredOutputExtension(inputPath)}";
        return Path.Combine(directory, fileName);
    }

    private void BeginExportPresentation(ExportRequest request)
    {
        IsExporting = true;
        IsExportCompleted = false;
        ExportProgressValue = 0d;
        ExportProgressPercentText = "0%";
        ExportOverlayTitle = "Export In Progress";
        ExportOverlaySubtitle = "Cut The Pause is rendering your trimmed timeline now.";
        ExportProgressStageText = "Preparing export...";
        ExportProgressDetailText = $"Target: {Path.GetExtension(request.OutputPath).ToLowerInvariant()}";
        ExportedOutputPath = request.OutputPath;
        OnPropertyChanged(nameof(ShowExportOverlay));
    }

    private void CompleteExportPresentation(ExportRequest request)
    {
        IsExporting = false;
        IsExportCompleted = true;
        ExportProgressValue = 100d;
        ExportProgressPercentText = "100%";
        ExportOverlayTitle = "Export Complete";
        ExportOverlaySubtitle = "Your trimmed video is ready. Review the path below or reveal it in Finder.";
        ExportProgressStageText = "Render finished successfully.";
        ExportProgressDetailText = request.OutputPath;
        ExportedOutputPath = request.OutputPath;
    }

    private void EndExportPresentation()
    {
        IsExporting = false;
        IsExportCompleted = false;
        OnPropertyChanged(nameof(ShowExportOverlay));
    }

    private void OnExportProgressReported(VideoExportProgress progress)
    {
        ExportProgressValue = Math.Clamp(progress.FractionComplete * 100d, 0d, 100d);
        ExportProgressPercentText = $"{ExportProgressValue:0}%";
        ExportProgressStageText = progress.Stage;
        ExportProgressDetailText = $"{progress.EncoderLabel} · {FormatDuration(progress.EncodedDuration)} / {FormatDuration(progress.TotalDuration)}";
    }

    private static string ResolvePreferredOutputExtension(string inputPath)
    {
        var extension = Path.GetExtension(inputPath);

        return extension.Equals(".mov", StringComparison.OrdinalIgnoreCase)
            ? ".mov"
            : ".mp4";
    }

    private static int ParseInteger(string? text, int fallback) =>
        int.TryParse(text, out var parsed) ? parsed : fallback;

    private static float ParseFloat(string? text, float fallback) =>
        float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static string FormatDuration(TimeSpan value) => value.ToString(@"hh\:mm\:ss\.fff");
}
