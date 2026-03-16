namespace CutThePause.Core.Models;

public sealed record VideoExportProgress(
    double FractionComplete,
    TimeSpan EncodedDuration,
    TimeSpan TotalDuration,
    string Stage,
    string EncoderLabel);
