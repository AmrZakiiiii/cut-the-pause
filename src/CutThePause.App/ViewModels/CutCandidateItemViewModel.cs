using CutThePause.Core.Models;

namespace CutThePause.App.ViewModels;

public sealed class CutCandidateItemViewModel : ViewModelBase
{
    private bool _isEnabled;

    public CutCandidateItemViewModel(CutCandidate cutCandidate)
    {
        Start = cutCandidate.Start;
        End = cutCandidate.End;
        Reason = cutCandidate.Reason;
        _isEnabled = cutCandidate.IsEnabled;
    }

    public TimeSpan Start { get; }

    public TimeSpan End { get; }

    public string Reason { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public string Title => $"{Format(Start)} to {Format(End)}";

    public string Subtitle => IsEnabled ? "This silence range will be removed." : "This range will be kept in the final export.";

    public string DurationText => Format(End - Start);

    public CutCandidate ToModel() => new(Start, End, Reason, IsEnabled);

    private static string Format(TimeSpan value) => value.ToString(@"hh\:mm\:ss\.fff");
}
