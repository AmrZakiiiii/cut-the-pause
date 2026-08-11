using System.Diagnostics;
using CutThePause.Infrastructure.Ffmpeg;

namespace CutThePause.Infrastructure.Tests;

public sealed class ProcessFfmpegRunnerTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"cut-the-pause-runner-{Guid.NewGuid():N}");

    public ProcessFfmpegRunnerTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task RunAsync_CancellationKillsChildProcessTree()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var scriptPath = Path.Combine(_tempDirectory, "long-running.sh");
        var pidPath = Path.Combine(_tempDirectory, "child.pid");
        File.WriteAllText(scriptPath, "#!/bin/sh\nprintf '%s' \"$$\" > \"$1\"\ntrap 'exit 143' TERM INT\nwhile true; do sleep 1; done\n");
        File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        using var cancellation = new CancellationTokenSource();
        var runner = new ProcessFfmpegRunner();
        var runTask = runner.RunAsync(scriptPath, new[] { pidPath }, cancellation.Token);
        var processId = await WaitForProcessIdAsync(pidPath);

        try
        {
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
            await WaitForConditionAsync(() => !IsProcessRunning(processId));
        }
        finally
        {
            if (IsProcessRunning(processId))
            {
                using var process = Process.GetProcessById(processId);
                process.Kill(entireProcessTree: true);
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static async Task<int> WaitForProcessIdAsync(string path)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path) && int.TryParse(await File.ReadAllTextAsync(path), out var processId))
            {
                return processId;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("The child process did not publish its PID.");
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
