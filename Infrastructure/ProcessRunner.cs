using System.Diagnostics;

namespace MewSwitchManager.Infrastructure;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

public sealed class ProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default,
        bool allowMissingExecutable = false)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception) when (allowMissingExecutable)
        {
            return new ProcessResult(-1, string.Empty, $"Executable not found: {fileName}");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }
}
