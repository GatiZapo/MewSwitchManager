using System.Text.Json;
using MewNX.Infrastructure;

namespace MewNX.Hardware;

public sealed class SystemProbe(ProcessRunner runner, AppLogger logger)
{
    public async Task<bool> IsWslReadyAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await runner.RunAsync("wsl.exe", "--status", ct);
            var text = (r.StdOut + "\n" + r.StdErr).ToLowerInvariant();
            return r.ExitCode == 0 || text.Contains("default distribution") || text.Contains("wsl version");
        }
        catch (Exception ex) { logger.Warn($"WSL probe failed: {ex.Message}"); return false; }
    }

    public async Task<bool> IsRcmConnectedAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await runner.RunAsync("powershell.exe", "-NoProfile -Command \"Get-PnpDevice -PresentOnly | Select-Object -ExpandProperty InstanceId | ConvertTo-Json -Compress\"", ct);
            return r.StdOut.Contains("VID_0955&PID_7321", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) { logger.Warn($"RCM probe failed: {ex.Message}"); return false; }
    }

    public async Task<bool> IsHekateDetectedAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await runner.RunAsync("powershell.exe", "-NoProfile -Command \"Get-CimInstance Win32_LogicalDisk | Select-Object -ExpandProperty DeviceID\"", ct);
            foreach (var drive in r.StdOut.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var root = drive.EndsWith(":") ? drive + "\\" : drive;
                if (File.Exists(Path.Combine(root, "bootloader", "hekate_ipl.ini"))) return true;
            }
            return false;
        }
        catch (Exception ex) { logger.Warn($"Hekate probe failed: {ex.Message}"); return false; }
    }
}
