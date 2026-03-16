using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Abstractions;

public interface IFfmpegRunner
{
    Task<ProcessResult> RunAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken cancellationToken);

    Task<ProcessResult> RunWithProgressAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        Action<string> onStandardOutputLine,
        CancellationToken cancellationToken);

    Task<BinaryProcessResult> RunBinaryAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
