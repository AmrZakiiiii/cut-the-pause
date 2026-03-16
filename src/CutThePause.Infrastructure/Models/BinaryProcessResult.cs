namespace CutThePause.Infrastructure.Models;

public sealed record BinaryProcessResult(int ExitCode, byte[] StandardOutput, string StandardError);
