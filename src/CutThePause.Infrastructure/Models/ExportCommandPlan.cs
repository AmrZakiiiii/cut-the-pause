namespace CutThePause.Infrastructure.Models;

public sealed record ExportCommandPlan(
    string FilterGraph,
    IReadOnlyList<string> Arguments,
    bool UsesHardwareAcceleration);
