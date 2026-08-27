using MewNX.Models;

namespace MewNX.Infrastructure;

public sealed class DependencyService(ProcessRunner runner, AppLogger logger)
{
    public IReadOnlyList<DependencyInfo> Detect()
    {
        if (!OperatingSystem.IsWindows())
            return [new("Windows", "windows", false, true, "MewNX requires Windows 10 1809 or newer.")];

        return
        [
            CheckCommand("Windows PowerShell", "powershell.exe", true),
            CheckCommand("DiskPart", "diskpart.exe", true),
            CheckCommand("WSL", "wsl.exe", false)
        ];
    }

    public async Task<IReadOnlyList<DependencyInfo>> EnsureAsync(bool installOptional, CancellationToken ct = default)
    {
        var dependencies = Detect().ToList();
        var requiredMissing = dependencies.Where(x => x.Required && !x.Installed).ToList();
        if (requiredMissing.Count > 0)
            throw new InvalidOperationException("Required Windows components are missing: " + string.Join(", ", requiredMissing.Select(x => x.Name)));

        var wsl = dependencies.FirstOrDefault(x => x.Name == "WSL");
        if (installOptional && wsl is { Installed: false })
        {
            logger.Warn("WSL is not installed. Starting the official Windows WSL installation command.");
            var result = await runner.RunAsync("wsl.exe", "--install --no-distribution", ct, allowMissingExecutable: true);
            if (result.ExitCode != 0)
                logger.Warn($"WSL installation command returned {result.ExitCode}: {result.StdErr.Trim()}");
            else
                logger.Info("WSL installation command completed. Windows may require a restart before WSL is ready.");
        }

        return Detect();
    }

    private static DependencyInfo CheckCommand(string name, string command, bool required)
    {
        var installed = File.Exists(command) || FindOnPath(command);
        return new DependencyInfo(name, command, installed, required, installed ? "Available" : "Missing");
    }

    private static bool FindOnPath(string command)
    {
        if (Path.IsPathRooted(command)) return File.Exists(command);
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => Path.Combine(p, command))
            .Any(File.Exists);
    }
}
