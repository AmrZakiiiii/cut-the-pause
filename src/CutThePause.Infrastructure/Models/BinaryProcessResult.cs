namespace CutThePause.Infrastructure.Models;

public sealed record BinaryProcessResult(int ExitCode, ReadOnlyMemory<byte> StandardOutput, string StandardError);
