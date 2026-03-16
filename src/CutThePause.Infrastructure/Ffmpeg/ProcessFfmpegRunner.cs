using System.Diagnostics;
using System.Text;
using CutThePause.Infrastructure.Abstractions;
using CutThePause.Infrastructure.Models;

namespace CutThePause.Infrastructure.Ffmpeg;

public sealed class ProcessFfmpegRunner : IFfmpegRunner
{
    public async Task<ProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        await RunInternalAsync(executablePath, arguments, null, cancellationToken).ConfigureAwait(false);

    public async Task<ProcessResult> RunWithProgressAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        Action<string> onStandardOutputLine,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onStandardOutputLine);

        return await RunInternalAsync(executablePath, arguments, onStandardOutputLine, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BinaryProcessResult> RunBinaryAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = CreateProcess(executablePath, arguments);
        using var output = new MemoryStream();

        process.Start();

        var copyTask = process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(copyTask, process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        return new BinaryProcessResult(process.ExitCode, output.ToArray(), stderr);
    }

    private static async Task<ProcessResult> RunInternalAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        Action<string>? onStandardOutputLine,
        CancellationToken cancellationToken)
    {
        using var process = CreateProcess(executablePath, arguments);
        var standardOutput = new StringBuilder();

        process.Start();

        var stdoutTask = Task.Run(async () =>
        {
            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                standardOutput.AppendLine(line);
                onStandardOutputLine?.Invoke(line);
            }
        }, cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(stdoutTask, process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        return new ProcessResult(process.ExitCode, standardOutput.ToString(), stderr);
    }

    private static Process CreateProcess(string executablePath, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new Process { StartInfo = startInfo };
    }
}
