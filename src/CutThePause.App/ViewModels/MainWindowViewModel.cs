using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CutThePause.App.Services;
using CutThePause.Core.Models;
using CutThePause.Core.Services;
using CutThePause.Infrastructure;

namespace CutThePause.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly VideoWorkflowService _workflowService;
    private readonly IAnalysisSettingsStore _settingsStore;
    private readonly IHistoryStore _historyStore;
    private readonly ICustomPresetStore _customPresetStore;
    private readonly IExportCheckpointStore _exportCheckpointStore;
    private AnalysisResult? _analysisResult;
    private string? _inputPath;
    private string? _outputPath;
    private string _statusMessage = "Choose a source video to start.";
    private string _warningsText = string.Empty;
    private bool _isBusy;
    private bool _isAnalyzing;
    private string _analysisStageText = "Preparing analysis...";
    private string _minSilenceMsText = "350";
    private string _minSpeechMsText = "150";
    private string _paddingBeforeMsText = "80";
    private string _paddingAfterMsText = "120";
    private string _speechThresholdText = "0.50";
    private string _customPresetNameText = string.Empty;
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
    private CancellationTokenSource? _operationCancellationSource;
    private HistoryEntryItemViewModel? _selectedHistoryEntry;
    private CustomPresetItemViewModel? _selectedCustomPreset;
    private ExportCheckpointItemViewModel? _selectedPausedExport;
    private ExportCheckpoint? _activeCheckpoint;
    private bool _pauseRequested;
    private bool _discardRequested;
    private WorkspaceView _currentView = WorkspaceView.NewCut;
    private readonly Stopwatch _analysisStopwatch = new();
    private readonly Stopwatch _exportStopwatch = new();
    private string _analysisElapsedText = "00:00";

    public MainWindowViewModel(
        VideoWorkflowService workflowService,
        IAnalysisSettingsStore? settingsStore = null,
        IHistoryStore? historyStore = null,
        ICustomPresetStore? customPresetStore = null,
        IExportCheckpointStore? exportCheckpointStore = null)
    {
        _workflowService = workflowService;
        _settingsStore = settingsStore ?? new JsonAnalysisSettingsStore();
        _historyStore = historyStore ?? new NullHistoryStore();
        _customPresetStore = customPresetStore ?? new NullCustomPresetStore();
        _exportCheckpointStore = exportCheckpointStore ?? new NullExportCheckpointStore();
        var preferences = _settingsStore.Load();
        _minSilenceMsText = preferences.MinSilenceMs.ToString(CultureInfo.InvariantCulture);
        _minSpeechMsText = preferences.MinSpeechMs.ToString(CultureInfo.InvariantCulture);
        _paddingBeforeMsText = preferences.PaddingBeforeMs.ToString(CultureInfo.InvariantCulture);
        _paddingAfterMsText = preferences.PaddingAfterMs.ToString(CultureInfo.InvariantCulture);
        _speechThresholdText = preferences.SpeechThreshold.ToString("0.##", CultureInfo.InvariantCulture);
        _selectedPreset = preferences.ExportPreset;
        CutCandidates = new ObservableCollection<CutCandidateItemViewModel>();
        PresetOptions = Enum.GetValues<ExportPreset>();
        HistoryEntries = new ObservableCollection<HistoryEntryItemViewModel>(_historyStore.Load().Select(static entry => new HistoryEntryItemViewModel(entry)));
        CustomPresets = new ObservableCollection<CustomPresetItemViewModel>(_customPresetStore.Load().Select(static preset => new CustomPresetItemViewModel(preset)));
        PausedExports = new ObservableCollection<ExportCheckpointItemViewModel>(_exportCheckpointStore.Load().Select(static checkpoint => new ExportCheckpointItemViewModel(checkpoint)));
    }

    public ObservableCollection<CutCandidateItemViewModel> CutCandidates { get; }

    public IReadOnlyList<ExportPreset> PresetOptions { get; }

    public ObservableCollection<HistoryEntryItemViewModel> HistoryEntries { get; }

    public ObservableCollection<CustomPresetItemViewModel> CustomPresets { get; }

    public ObservableCollection<ExportCheckpointItemViewModel> PausedExports { get; }

    public string InputPathDisplay => string.IsNullOrWhiteSpace(_inputPath) ? "No video selected yet." : _inputPath;

    public string OutputPathDisplay => string.IsNullOrWhiteSpace(_outputPath) ? "No export path selected yet." : _outputPath;

    public string SuggestedOutputFileName => string.IsNullOrWhiteSpace(_inputPath)
        ? "trimmed-video.mp4"
        : $"{Path.GetFileNameWithoutExtension(_inputPath)}.trimmed{ResolvePreferredOutputExtension(_inputPath)}";

    public bool HasInput => !string.IsNullOrWhiteSpace(_inputPath);

    public bool CanAnalyze => !_isBusy && HasInput;

    public bool CanExport => !_isBusy && _analysisResult is not null && !string.IsNullOrWhiteSpace(_outputPath);

    public bool CanChangeSource => !_isBusy;

    public bool CanRevealSource => !_isBusy && HasInput;

    public bool CanChooseOutput => !_isBusy;

    public bool CanResetReview => !_isBusy;

    public bool CanCancelOperation => _isBusy && _operationCancellationSource is not null;

    public bool CanPauseOperation => IsExporting && CanCancelOperation;

    public bool CanResumeSelectedExport => !_isBusy && SelectedPausedExport is not null;

    public bool CanDiscardSelectedExport => !_isBusy && SelectedPausedExport is not null;

    public bool CanLoadSelectedHistory => !_isBusy && SelectedHistoryEntry?.CanLoad == true;

    public bool CanSaveCustomPreset => !_isBusy && !string.IsNullOrWhiteSpace(CustomPresetNameText) && TryBuildPersistedPreferences(out _);

    public bool CanApplySelectedCustomPreset => !_isBusy && SelectedCustomPreset is not null;

    public bool CanDeleteSelectedCustomPreset => !_isBusy && SelectedCustomPreset is not null;

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        private set
        {
            if (SetProperty(ref _isAnalyzing, value))
            {
                OnPropertyChanged(nameof(ShowAnalysisOverlay));
            }
        }
    }

    public bool ShowAnalysisOverlay => IsAnalyzing;

    public string AnalysisStageText
    {
        get => _analysisStageText;
        private set => SetProperty(ref _analysisStageText, value);
    }

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

    public bool ShowExportTray => IsExporting || IsExportCompleted || HasPausedExports;

    public WorkspaceView CurrentView
    {
        get => _currentView;
        private set
        {
            if (SetProperty(ref _currentView, value))
            {
                OnPropertyChanged(nameof(IsNewCutViewActive));
                OnPropertyChanged(nameof(IsReviewViewActive));
                OnPropertyChanged(nameof(IsHistoryViewActive));
                OnPropertyChanged(nameof(IsHelpViewActive));
            }
        }
    }

    public bool IsNewCutViewActive => CurrentView == WorkspaceView.NewCut;

    public bool IsReviewViewActive => CurrentView == WorkspaceView.Review;

    public bool IsHistoryViewActive => CurrentView == WorkspaceView.History;

    public bool IsHelpViewActive => CurrentView == WorkspaceView.Help;

    public void ShowView(WorkspaceView view) => CurrentView = view;

    public string AnalysisElapsedText
    {
        get => _analysisElapsedText;
        private set => SetProperty(ref _analysisElapsedText, value);
    }

    public bool IsMovOutputFormat => string.Equals(Path.GetExtension(_outputPath), ".mov", StringComparison.OrdinalIgnoreCase);

    public bool IsMp4OutputFormat => !IsMovOutputFormat;

    public bool HasAnalysisWithoutCuts => _analysisResult is not null && !HasCuts;

    public string CutSummaryText => $"{CutCandidates.Count(static item => item.IsEnabled)} of {CutCandidates.Count} ranges will be removed";

    public bool HasCuts => CutCandidates.Count > 0;

    public bool ShowEmptyState => !HasAnalysis;

    public bool HasWarnings => !string.IsNullOrWhiteSpace(_warningsText);

    public bool HasAnalysis => _analysisResult is not null;

    public bool HasHistory => HistoryEntries.Count > 0;

    public bool HasPausedExports => PausedExports.Count > 0;

    public IReadOnlyList<float> WaveformPeaks => _analysisResult?.WaveformPeaks ?? Array.Empty<float>();

    public TimeSpan AnalysisDuration => _analysisResult?.Duration ?? TimeSpan.Zero;

    public IReadOnlyList<CutCandidate> TimelineCuts => CutCandidates
        .Select(static candidate => candidate.ToModel())
        .ToArray();

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
        set
        {
            if (SetProperty(ref _minSilenceMsText, value))
            {
                PersistSettingsIfValid();
            }
        }
    }

    public string MinSpeechMsText
    {
        get => _minSpeechMsText;
        set
        {
            if (SetProperty(ref _minSpeechMsText, value))
            {
                PersistSettingsIfValid();
            }
        }
    }

    public string PaddingBeforeMsText
    {
        get => _paddingBeforeMsText;
        set
        {
            if (SetProperty(ref _paddingBeforeMsText, value))
            {
                PersistSettingsIfValid();
            }
        }
    }

    public string PaddingAfterMsText
    {
        get => _paddingAfterMsText;
        set
        {
            if (SetProperty(ref _paddingAfterMsText, value))
            {
                PersistSettingsIfValid();
            }
        }
    }

    public string SpeechThresholdText
    {
        get => _speechThresholdText;
        set
        {
            if (SetProperty(ref _speechThresholdText, value))
            {
                PersistSettingsIfValid();
            }
        }
    }

    public string CustomPresetNameText
    {
        get => _customPresetNameText;
        set
        {
            if (SetProperty(ref _customPresetNameText, value))
            {
                RaisePresetStateProperties();
            }
        }
    }

    public HistoryEntryItemViewModel? SelectedHistoryEntry
    {
        get => _selectedHistoryEntry;
        set
        {
            if (SetProperty(ref _selectedHistoryEntry, value))
            {
                RaiseStateProperties();
            }
        }
    }

    public CustomPresetItemViewModel? SelectedCustomPreset
    {
        get => _selectedCustomPreset;
        set
        {
            if (SetProperty(ref _selectedCustomPreset, value))
            {
                RaisePresetStateProperties();
            }
        }
    }

    public ExportCheckpointItemViewModel? SelectedPausedExport
    {
        get => _selectedPausedExport;
        set
        {
            if (SetProperty(ref _selectedPausedExport, value))
            {
                RaiseStateProperties();
            }
        }
    }

    public ExportPreset SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                RecalculateSummary();
                PersistSettingsIfValid();
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
        if (_isBusy)
        {
            return;
        }

        PersistSettingsIfValid();
        _inputPath = path;
        _outputPath = SuggestOutputPath(path);
        _analysisResult = null;
        ResetReview();

        StatusMessage = "Video selected. Adjust the settings, then run analysis.";
        OnPropertyChanged(nameof(InputPathDisplay));
        OnPropertyChanged(nameof(OutputPathDisplay));
        OnPropertyChanged(nameof(SuggestedOutputFileName));
        OnOutputFormatChanged();
        ShowView(WorkspaceView.Review);
        RaiseStateProperties();
    }

    public void SetOutputPath(string path)
    {
        if (_isBusy)
        {
            return;
        }

        _outputPath = path;
        OnPropertyChanged(nameof(OutputPathDisplay));
        OnOutputFormatChanged();
        RaiseStateProperties();
    }

    public void SetOutputFormat(string extension)
    {
        if (_isBusy || string.IsNullOrWhiteSpace(extension))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_outputPath))
        {
            return;
        }

        var currentExtension = Path.GetExtension(_outputPath);
        if (string.Equals(currentExtension, extension, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var withoutExtension = _outputPath[..^currentExtension.Length];
        _outputPath = $"{withoutExtension}{extension}";
        StatusMessage = extension.Equals(".mov", StringComparison.OrdinalIgnoreCase)
            ? "Output set to MOV ProRes editing master."
            : "Output set to MP4 10-bit HEVC Main 10 delivery.";
        OnPropertyChanged(nameof(OutputPathDisplay));
        OnOutputFormatChanged();
        RaiseStateProperties();
    }

    public async Task AnalyzeAsync()
    {
        if (_isBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_inputPath))
        {
            StatusMessage = "Choose a source video before analyzing.";
            return;
        }

        using var cancellationSource = new CancellationTokenSource();
        _operationCancellationSource = cancellationSource;
        OnPropertyChanged(nameof(CanCancelOperation));

        try
        {
            SetBusy(true, "Analyzing audio and detecting silence...");
            BeginAnalysisPresentation();
            PersistSettingsIfValid();
            var settings = BuildSettings();
            var progress = new Progress<AnalysisProgress>(OnAnalysisProgressReported);
            _analysisResult = await _workflowService.AnalyzeAsync(_inputPath, settings, cancellationSource.Token, progress);

            PopulateCuts(_analysisResult.CutCandidates);
            WarningsText = string.Join(Environment.NewLine, _analysisResult.Warnings);
            StatusMessage = $"Analysis complete. {_analysisResult.CutCandidates.Count} cut candidates detected.";
            RecalculateSummary();
            RecordAnalysisHistory();
            ShowView(WorkspaceView.Review);
            RaiseStateProperties();
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            StatusMessage = "Analysis canceled.";
        }
        catch (Exception exception)
        {
            WarningsText = exception.Message;
            StatusMessage = "Analysis failed.";
        }
        finally
        {
            _operationCancellationSource = null;
            OnPropertyChanged(nameof(CanCancelOperation));
            EndAnalysisPresentation();
            SetBusy(false, StatusMessage);
        }
    }

    public async Task ExportAsync()
    {
        if (_isBusy)
        {
            return;
        }

        if (_analysisResult is null || string.IsNullOrWhiteSpace(_inputPath) || string.IsNullOrWhiteSpace(_outputPath))
        {
            StatusMessage = "Analyze a video and choose an export path before exporting.";
            return;
        }

        using var cancellationSource = new CancellationTokenSource();
        _operationCancellationSource = cancellationSource;
        OnPropertyChanged(nameof(CanCancelOperation));

        try
        {
            SetBusy(true, "Rendering trimmed video with FFmpeg...");
            var request = BuildExportRequest();
            var requestFingerprint = ExportRequestFingerprint.Compute(request);
            _activeCheckpoint ??= _exportCheckpointStore.Find(requestFingerprint);
            var progress = new Progress<VideoExportProgress>(OnExportProgressReported);
            var outputDirectory = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            BeginExportPresentation(request);
            var executionOptions = new ExportExecutionOptions(
                _activeCheckpoint,
                checkpoint => OnCheckpointSaved(checkpoint));
            await _workflowService.ExportAsync(request, progress, cancellationSource.Token, executionOptions);
            if (_activeCheckpoint is not null)
            {
                _exportCheckpointStore.Delete(_activeCheckpoint);
                _activeCheckpoint = null;
            }

            RecordExportHistory(request);
            RefreshPausedExports();
            StatusMessage = $"Export complete: {_outputPath}";
            CompleteExportPresentation(request);
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            if (_pauseRequested && !_discardRequested)
            {
                StatusMessage = "Export paused. Resume it from Paused Jobs.";
            }
            else
            {
                DiscardActiveCheckpoint();
                StatusMessage = "Export canceled.";
            }
        }
        catch (Exception exception)
        {
            WarningsText = exception.Message;
            StatusMessage = "Export failed.";
        }
        finally
        {
            _operationCancellationSource = null;
            OnPropertyChanged(nameof(CanCancelOperation));
            _pauseRequested = false;
            _discardRequested = false;
            RefreshPausedExports();
            if (!IsExportCompleted)
            {
                EndExportPresentation();
            }

            SetBusy(false, StatusMessage);
        }
    }

    public void CancelOperation()
    {
        if (_operationCancellationSource is null || _operationCancellationSource.IsCancellationRequested)
        {
            return;
        }

        if (IsExporting)
        {
            _discardRequested = true;
            _pauseRequested = false;
        }

        StatusMessage = "Canceling current operation...";
        _operationCancellationSource.Cancel();
        OnPropertyChanged(nameof(CanCancelOperation));
    }

    public void PauseOperation()
    {
        if (!IsExporting || _operationCancellationSource is null || _operationCancellationSource.IsCancellationRequested)
        {
            return;
        }

        _pauseRequested = true;
        _discardRequested = false;
        StatusMessage = "Pausing export after the current batch...";
        _operationCancellationSource.Cancel();
        OnPropertyChanged(nameof(CanCancelOperation));
    }

    public async Task ResumeSelectedExport()
    {
        if (_isBusy || SelectedPausedExport is null)
        {
            return;
        }

        RestoreCheckpoint(SelectedPausedExport.Checkpoint);
        _activeCheckpoint = SelectedPausedExport.Checkpoint;
        ShowView(WorkspaceView.Review);
        await ExportAsync();
    }

    public void DiscardSelectedExport()
    {
        if (_isBusy || SelectedPausedExport is null)
        {
            return;
        }

        var checkpoint = SelectedPausedExport.Checkpoint;
        if (_exportCheckpointStore.Delete(checkpoint))
        {
            RefreshPausedExports();
            StatusMessage = "Paused export discarded.";
        }
    }

    public void LoadSelectedHistory()
    {
        if (_isBusy || SelectedHistoryEntry?.Entry.AnalysisResult is not { } analysis)
        {
            return;
        }

        _inputPath = SelectedHistoryEntry.Entry.InputPath;
        _outputPath = SelectedHistoryEntry.Entry.OutputPath ?? SuggestOutputPath(_inputPath);
        ApplySettingsToView(analysis.Settings);
        _analysisResult = analysis;
        PopulateCuts(analysis.CutCandidates);
        WarningsText = string.Join(Environment.NewLine, analysis.Warnings);
        RecalculateSummary();
        StatusMessage = "Saved analysis loaded. Review the cuts or export the restored plan.";
        OnPropertyChanged(nameof(InputPathDisplay));
        OnPropertyChanged(nameof(OutputPathDisplay));
        OnPropertyChanged(nameof(SuggestedOutputFileName));
        OnOutputFormatChanged();
        ShowView(WorkspaceView.Review);
        RaiseStateProperties();
    }

    public void SaveCustomPreset()
    {
        if (!CanSaveCustomPreset || !TryBuildPersistedPreferences(out var preferences))
        {
            return;
        }

        var name = CustomPresetNameText.Trim();
        var preset = new NamedPreset(
            name,
            preferences.MinSilenceMs,
            preferences.MinSpeechMs,
            preferences.PaddingBeforeMs,
            preferences.PaddingAfterMs,
            preferences.SpeechThreshold,
            preferences.ExportPreset);
        if (!_customPresetStore.Upsert(preset))
        {
            StatusMessage = "The custom preset could not be saved.";
            return;
        }

        RefreshCustomPresets();
        SelectedCustomPreset = CustomPresets.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        StatusMessage = $"Saved custom preset '{name}'.";
    }

    public void ApplySelectedCustomPreset()
    {
        if (_isBusy || SelectedCustomPreset is null)
        {
            return;
        }

        ApplySettingsToView(SelectedCustomPreset.Preset.ToAnalysisSettings());
        StatusMessage = $"Applied custom preset '{SelectedCustomPreset.Name}'.";
    }

    public void DeleteSelectedCustomPreset()
    {
        if (_isBusy || SelectedCustomPreset is null)
        {
            return;
        }

        var name = SelectedCustomPreset.Name;
        if (_customPresetStore.Delete(name))
        {
            RefreshCustomPresets();
            StatusMessage = $"Deleted custom preset '{name}'.";
        }
    }

    public void AddManualCut(TimeRange range)
    {
        if (_isBusy || _analysisResult is null || range.IsEmpty || range.Duration < TimeSpan.FromMilliseconds(100))
        {
            return;
        }

        var clampedStart = range.Start < TimeSpan.Zero ? TimeSpan.Zero : range.Start;
        var clampedEnd = range.End > _analysisResult.Duration ? _analysisResult.Duration : range.End;
        if (clampedEnd - clampedStart < TimeSpan.FromMilliseconds(100))
        {
            return;
        }

        var candidates = CutCandidates
            .Select(static candidate => candidate.ToModel())
            .Append(new CutCandidate(clampedStart, clampedEnd, "Manual cut"))
            .OrderBy(static candidate => candidate.Start)
            .ThenBy(static candidate => candidate.End)
            .ToArray();
        PopulateCuts(candidates);
        RecalculateSummary();
        StatusMessage = "Manual cut added to the timeline.";
    }

    public void ToggleCutAt(TimeSpan position)
    {
        if (_isBusy)
        {
            return;
        }

        var candidate = CutCandidates.FirstOrDefault(item => position >= item.Start && position <= item.End);
        if (candidate is not null)
        {
            candidate.IsEnabled = !candidate.IsEnabled;
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
        OnPropertyChanged(nameof(ShowExportOverlay));
        OnPropertyChanged(nameof(ShowExportTray));
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
        if (_isBusy)
        {
            return;
        }

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
        OnPropertyChanged(nameof(HasAnalysis));
        OnPropertyChanged(nameof(HasAnalysisWithoutCuts));
        OnPropertyChanged(nameof(CutSummaryText));
        OnPropertyChanged(nameof(WaveformPeaks));
        OnPropertyChanged(nameof(AnalysisDuration));
        OnPropertyChanged(nameof(TimelineCuts));
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

    private void OnOutputFormatChanged()
    {
        OnPropertyChanged(nameof(IsMovOutputFormat));
        OnPropertyChanged(nameof(IsMp4OutputFormat));
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

    private void PersistSettingsIfValid()
    {
        if (!TryBuildPersistedPreferences(out var preferences))
        {
            return;
        }

        if (!_settingsStore.Save(preferences))
        {
            StatusMessage = "Detection settings changed, but could not be saved to disk.";
        }

        RaisePresetStateProperties();
    }

    private bool TryBuildPersistedPreferences(out AnalysisSettingsPreferences preferences)
    {
        if (!TryParseInteger(_minSilenceMsText, out var minSilenceMs) || minSilenceMs < 0 ||
            !TryParseInteger(_minSpeechMsText, out var minSpeechMs) || minSpeechMs <= 0 ||
            !TryParseInteger(_paddingBeforeMsText, out var paddingBeforeMs) || paddingBeforeMs < 0 ||
            !TryParseInteger(_paddingAfterMsText, out var paddingAfterMs) || paddingAfterMs < 0 ||
            !TryParseFloat(_speechThresholdText, out var speechThreshold) || speechThreshold is < 0f or > 1f ||
            !float.IsFinite(speechThreshold) || !Enum.IsDefined(SelectedPreset))
        {
            preferences = AnalysisSettingsPreferences.Defaults;
            return false;
        }

        preferences = new AnalysisSettingsPreferences(
            minSilenceMs,
            minSpeechMs,
            paddingBeforeMs,
            paddingAfterMs,
            speechThreshold,
            SelectedPreset);
        return true;
    }

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
        OnPropertyChanged(nameof(HasAnalysisWithoutCuts));
        OnPropertyChanged(nameof(CutSummaryText));
        OnPropertyChanged(nameof(TimelineCuts));
    }

    private void OnCutCandidatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CutCandidateItemViewModel.IsEnabled))
        {
            RecalculateSummary();
            OnPropertyChanged(nameof(TimelineCuts));
            OnPropertyChanged(nameof(CutSummaryText));
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
        OnPropertyChanged(nameof(CanChangeSource));
        OnPropertyChanged(nameof(CanRevealSource));
        OnPropertyChanged(nameof(CanChooseOutput));
        OnPropertyChanged(nameof(CanResetReview));
        OnPropertyChanged(nameof(CanCancelOperation));
        OnPropertyChanged(nameof(CanPauseOperation));
        OnPropertyChanged(nameof(CanResumeSelectedExport));
        OnPropertyChanged(nameof(CanDiscardSelectedExport));
        OnPropertyChanged(nameof(CanLoadSelectedHistory));
        OnPropertyChanged(nameof(HasAnalysis));
        OnPropertyChanged(nameof(InputPathDisplay));
        OnPropertyChanged(nameof(OutputPathDisplay));
    }

    private void RaisePresetStateProperties()
    {
        OnPropertyChanged(nameof(CanSaveCustomPreset));
        OnPropertyChanged(nameof(CanApplySelectedCustomPreset));
        OnPropertyChanged(nameof(CanDeleteSelectedCustomPreset));
    }

    private void RecordAnalysisHistory()
    {
        if (_analysisResult is null || string.IsNullOrWhiteSpace(_inputPath))
        {
            return;
        }

        var snapshot = CreateCurrentAnalysisSnapshot();
        var request = BuildExportRequest();
        _historyStore.Append(new HistoryEntry(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            HistoryEntryKind.Analysis,
            HistoryEntryStatus.Completed,
            _inputPath,
            _outputPath,
            ExportRequestFingerprint.Compute(request),
            snapshot,
            null,
            "Analysis complete."));
        RefreshHistoryEntries();
    }

    private void RecordExportHistory(ExportRequest request)
    {
        if (_analysisResult is null)
        {
            return;
        }

        long? outputBytes = null;
        try
        {
            if (File.Exists(request.OutputPath))
            {
                outputBytes = new FileInfo(request.OutputPath).Length;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        _historyStore.Append(new HistoryEntry(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            HistoryEntryKind.Export,
            HistoryEntryStatus.Completed,
            request.InputPath,
            request.OutputPath,
            ExportRequestFingerprint.Compute(request),
            CreateCurrentAnalysisSnapshot(),
            outputBytes,
            "Export complete."));
        RefreshHistoryEntries();
    }

    private AnalysisResult CreateCurrentAnalysisSnapshot()
    {
        if (_analysisResult is null)
        {
            throw new InvalidOperationException("An analysis result is required for history.");
        }

        return _analysisResult with
        {
            Settings = BuildSettings(),
            CutCandidates = CutCandidates.Select(static candidate => candidate.ToModel()).ToArray()
        };
    }

    private void RefreshHistoryEntries()
    {
        var selectedId = SelectedHistoryEntry?.Entry.Id;
        HistoryEntries.Clear();
        foreach (var entry in _historyStore.Load())
        {
            HistoryEntries.Add(new HistoryEntryItemViewModel(entry));
        }

        SelectedHistoryEntry = HistoryEntries.FirstOrDefault(item => item.Entry.Id == selectedId);
        OnPropertyChanged(nameof(HasHistory));
    }

    private void RefreshCustomPresets()
    {
        var selectedName = SelectedCustomPreset?.Name;
        CustomPresets.Clear();
        foreach (var preset in _customPresetStore.Load())
        {
            CustomPresets.Add(new CustomPresetItemViewModel(preset));
        }

        SelectedCustomPreset = CustomPresets.FirstOrDefault(item => string.Equals(item.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        RaisePresetStateProperties();
    }

    private void RefreshPausedExports()
    {
        var selectedJobId = SelectedPausedExport?.Checkpoint.JobId;
        PausedExports.Clear();
        foreach (var checkpoint in _exportCheckpointStore.Load())
        {
            PausedExports.Add(new ExportCheckpointItemViewModel(checkpoint));
        }

        SelectedPausedExport = PausedExports.FirstOrDefault(item => item.Checkpoint.JobId == selectedJobId);
        OnPropertyChanged(nameof(CanResumeSelectedExport));
        OnPropertyChanged(nameof(CanDiscardSelectedExport));
        OnPropertyChanged(nameof(HasPausedExports));
        OnPropertyChanged(nameof(ShowExportTray));
    }

    private void OnCheckpointSaved(ExportCheckpoint checkpoint)
    {
        _activeCheckpoint = checkpoint;
        if (!_exportCheckpointStore.Save(checkpoint))
        {
            StatusMessage = "The export checkpoint could not be saved; completed batches will not be resumable after restart.";
        }

        RefreshPausedExports();
    }

    private void DiscardActiveCheckpoint()
    {
        if (_activeCheckpoint is not null)
        {
            _exportCheckpointStore.Delete(_activeCheckpoint);
            _activeCheckpoint = null;
        }

        RefreshPausedExports();
    }

    private void RestoreCheckpoint(ExportCheckpoint checkpoint)
    {
        _inputPath = checkpoint.Request.InputPath;
        _outputPath = checkpoint.Request.OutputPath;
        SelectedPreset = checkpoint.Request.Preset;
        _analysisResult = new AnalysisResult(
            checkpoint.Request.InputPath,
            checkpoint.Request.SourceDuration,
            BuildSettings(),
            checkpoint.Request.KeepSegments,
            checkpoint.Request.CutCandidates,
            new[] { "Restored from a paused export checkpoint." });
        PopulateCuts(checkpoint.Request.CutCandidates);
        WarningsText = string.Join(Environment.NewLine, _analysisResult.Warnings);
        RecalculateSummary();
        OnPropertyChanged(nameof(InputPathDisplay));
        OnPropertyChanged(nameof(OutputPathDisplay));
        OnPropertyChanged(nameof(SuggestedOutputFileName));
        OnOutputFormatChanged();
        RaiseStateProperties();
    }

    private void ApplySettingsToView(AnalysisSettings settings)
    {
        MinSilenceMsText = settings.MinSilenceMs.ToString(CultureInfo.InvariantCulture);
        MinSpeechMsText = settings.MinSpeechMs.ToString(CultureInfo.InvariantCulture);
        PaddingBeforeMsText = settings.PaddingBeforeMs.ToString(CultureInfo.InvariantCulture);
        PaddingAfterMsText = settings.PaddingAfterMs.ToString(CultureInfo.InvariantCulture);
        SpeechThresholdText = settings.SpeechThreshold.ToString("0.##", CultureInfo.InvariantCulture);
        SelectedPreset = settings.ExportPreset;
    }

    private void BeginAnalysisPresentation()
    {
        AnalysisStageText = "Preparing analysis...";
        _analysisStopwatch.Restart();
        UpdateAnalysisElapsedText();
        IsAnalyzing = true;
    }

    private void EndAnalysisPresentation()
    {
        _analysisStopwatch.Stop();
        UpdateAnalysisElapsedText();
        IsAnalyzing = false;
    }

    private void OnAnalysisProgressReported(AnalysisProgress progress)
    {
        AnalysisStageText = progress.Stage;
        StatusMessage = progress.Stage;
        UpdateAnalysisElapsedText();
    }

    private void UpdateAnalysisElapsedText() =>
        AnalysisElapsedText = $"Elapsed {_analysisStopwatch.Elapsed:hh\\:mm\\:ss}";

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
        _exportStopwatch.Restart();
        OnPropertyChanged(nameof(ShowExportOverlay));
        OnPropertyChanged(nameof(ShowExportTray));
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
        _exportStopwatch.Stop();
        ExportProgressDetailText = $"{request.OutputPath} · Elapsed {_exportStopwatch.Elapsed:hh\\:mm\\:ss}";
        ExportedOutputPath = request.OutputPath;
    }

    private void EndExportPresentation()
    {
        IsExporting = false;
        IsExportCompleted = false;
        _exportStopwatch.Stop();
        OnPropertyChanged(nameof(ShowExportOverlay));
        OnPropertyChanged(nameof(ShowExportTray));
    }

    private void OnExportProgressReported(VideoExportProgress progress)
    {
        ExportProgressValue = Math.Clamp(progress.FractionComplete * 100d, 0d, 100d);
        ExportProgressPercentText = $"{ExportProgressValue:0}%";
        ExportProgressStageText = progress.Stage;
        var detail = $"{progress.EncoderLabel} · {FormatDuration(progress.EncodedDuration)} / {FormatDuration(progress.TotalDuration)}";
        if (progress.FractionComplete > 0.01d && progress.FractionComplete < 1d)
        {
            var estimatedRemaining = TimeSpan.FromSeconds(
                _exportStopwatch.Elapsed.TotalSeconds * (1d - progress.FractionComplete) / progress.FractionComplete);
            detail += $" · ~{estimatedRemaining:hh\\:mm\\:ss} left";
        }
        else
        {
            detail += $" · Elapsed {_exportStopwatch.Elapsed:hh\\:mm\\:ss}";
        }

        ExportProgressDetailText = detail;
    }

    private static string ResolvePreferredOutputExtension(string inputPath)
    {
        return ".mp4";
    }

    private static int ParseInteger(string? text, int fallback) =>
        int.TryParse(text, out var parsed) ? parsed : fallback;

    private static bool TryParseInteger(string? text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static float ParseFloat(string? text, float fallback) =>
        float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static bool TryParseFloat(string? text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static string FormatDuration(TimeSpan value) => value.ToString(@"hh\:mm\:ss\.fff");

    private sealed class NullHistoryStore : IHistoryStore
    {
        public IReadOnlyList<HistoryEntry> Load() => Array.Empty<HistoryEntry>();

        public bool Append(HistoryEntry entry) => true;

        public bool Remove(string id) => false;
    }

    private sealed class NullCustomPresetStore : ICustomPresetStore
    {
        public IReadOnlyList<NamedPreset> Load() => Array.Empty<NamedPreset>();

        public bool Upsert(NamedPreset preset) => true;

        public bool Delete(string name) => false;
    }

    private sealed class NullExportCheckpointStore : IExportCheckpointStore
    {
        public IReadOnlyList<ExportCheckpoint> Load() => Array.Empty<ExportCheckpoint>();

        public ExportCheckpoint? Find(string requestFingerprint) => null;

        public bool Save(ExportCheckpoint checkpoint) => true;

        public bool Delete(ExportCheckpoint checkpoint) => true;
    }
}
