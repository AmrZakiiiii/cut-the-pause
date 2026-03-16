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
        : $"{Path.GetFileNameWithoutExtension(_inputPath)}.trimmed.mp4";

    public bool HasInput => !string.IsNullOrWhiteSpace(_inputPath);

    public bool CanAnalyze => !_isBusy && HasInput;

    public bool CanExport => !_isBusy && _analysisResult is not null && !string.IsNullOrWhiteSpace(_outputPath);

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
            var outputDirectory = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await _workflowService.ExportAsync(request, CancellationToken.None);
            StatusMessage = $"Export complete: {_outputPath}";
        }
        catch (Exception exception)
        {
            WarningsText = exception.Message;
            StatusMessage = "Export failed.";
        }
        finally
        {
            SetBusy(false, StatusMessage);
        }
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
        var fileName = $"{Path.GetFileNameWithoutExtension(inputPath)}.trimmed.mp4";
        return Path.Combine(directory, fileName);
    }

    private static int ParseInteger(string? text, int fallback) =>
        int.TryParse(text, out var parsed) ? parsed : fallback;

    private static float ParseFloat(string? text, float fallback) =>
        float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static string FormatDuration(TimeSpan value) => value.ToString(@"hh\:mm\:ss\.fff");
}
