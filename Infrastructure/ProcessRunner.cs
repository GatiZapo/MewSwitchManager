using System.Diagnostics;
using System.ComponentModel;

namespace MewSwitchManager.Infrastructure;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

public sealed class ProcessRunner(AppLogger? logger = null)
{
    public async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default,
        bool allowMissingExecutable = false)
    {
        logger?.Debug($"Process start: {fileName} {arguments}");
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
        catch (Win32Exception ex) when (allowMissingExecutable)
        {
            logger?.Warn($"Process missing: {fileName}: {ex.Message}");
            return new ProcessResult(-1, string.Empty, $"Executable not found: {fileName}");
        }
        catch (Exception ex)
        {
            logger?.Error($"Process failed to start: {fileName}", ex);
            throw;
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(cancellationToken);

        var result = new ProcessResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);

        logger?.Debug($"Process exit {result.ExitCode}: {fileName}{Environment.NewLine}STDOUT: {TrimForLog(result.StdOut)}{Environment.NewLine}STDERR: {TrimForLog(result.StdErr)}");
        return result;
    }

    private static string TrimForLog(string text)
    {
        const int max = 8000;
        var normalized = text.Trim();
        return normalized.Length <= max ? normalized : normalized[..max] + "… [truncated]";
    }
}
