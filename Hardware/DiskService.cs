using System.Text.Json;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Hardware;

public sealed class DiskService(ProcessRunner runner, AppLogger logger)
{
    public async Task<IReadOnlyList<DiskInfo>> ScanAsync(CancellationToken ct = default)
    {
        const string command = @"
$os = Get-CimInstance Win32_OperatingSystem
$systemDrive = $os.SystemDrive.TrimEnd(':')
$protected = New-Object 'System.Collections.Generic.HashSet[int]'

function Add-DiskNumber([object]$partition) {
    if ($null -ne $partition -and $null -ne $partition.DiskNumber) { [void]$protected.Add([int]$partition.DiskNumber) }
}

try { Add-DiskNumber (Get-Partition -DriveLetter $systemDrive -ErrorAction Stop) } catch {}
try { Get-Partition | Where-Object { $_.IsBoot -or $_.Type -match 'System|EFI|Recovery' } | ForEach-Object { Add-DiskNumber $_ } } catch {}
try {
    Get-CimInstance Win32_PageFileUsage | ForEach-Object {
        $drive = ($_.Name -split ':')[0]
        if ($drive) { try { Add-DiskNumber (Get-Partition -DriveLetter $drive -ErrorAction Stop) } catch {} }
    }
} catch {}

Get-Disk | ForEach-Object {
    $d = $_
    $isProtectedByWindows = $d.IsBoot -or $d.IsSystem -or $protected.Contains([int]$d.Number)
    $reason = ''
    if ($isProtectedByWindows) { $reason = 'Windows boot/system/pagefile/recovery disk' }
    elseif ($d.IsReadOnly) { $reason = 'Read-only disk' }
    elseif ($d.IsOffline) { $reason = 'Offline disk' }
    elseif ([string]$d.BusType -ne 'USB') { $reason = 'Non-USB disk' }

    [pscustomobject]@{
        Number = [int]$d.Number
        FriendlyName = [string]$d.FriendlyName
        Size = [int64]$d.Size
        BusType = [string]$d.BusType
        IsBoot = [bool]$d.IsBoot
        IsSystem = [bool]$d.IsSystem
        IsReadOnly = [bool]$d.IsReadOnly
        IsOffline = [bool]$d.IsOffline
        UniqueId = [string]$d.UniqueId
        SafeCandidate = [bool](-not $isProtectedByWindows -and -not $d.IsReadOnly -and -not $d.IsOffline -and [string]$d.BusType -eq 'USB')
        ProtectionReason = $reason
    }
} | ConvertTo-Json -Compress";

        try
        {
            var result = await runner.RunAsync("powershell.exe", $"-NoProfile -Command {Quote(command)}", ct);
            if (result.ExitCode != 0) { logger.Warn($"Disk scan returned exit code {result.ExitCode}: {result.StdErr.Trim()}"); return []; }
            if (string.IsNullOrWhiteSpace(result.StdOut)) return [];

            using var document = JsonDocument.Parse(result.StdOut);
            var items = new List<JsonElement>();
            if (document.RootElement.ValueKind == JsonValueKind.Array)
                items.AddRange(document.RootElement.EnumerateArray());
            else
                items.Add(document.RootElement);

            var disks = new List<DiskInfo>();
            foreach (var item in items)
            {
                var number = GetString(item, "Number");
                var model = GetString(item, "FriendlyName").Trim();
                var sizeGb = GetInt64(item, "Size") / 1_000_000_000d;
                var bus = GetString(item, "BusType", "Unknown");
                var boot = GetBool(item, "IsBoot");
                var system = GetBool(item, "IsSystem");
                var readOnly = GetBool(item, "IsReadOnly");
                var offline = GetBool(item, "IsOffline");
                var safe = GetBool(item, "SafeCandidate");
                var reason = GetString(item, "ProtectionReason");
                var uniqueId = GetString(item, "UniqueId");
                disks.Add(new DiskInfo(number, string.IsNullOrWhiteSpace(model) ? "Mass Storage Device" : model, sizeGb, bus, boot, system, readOnly, offline, safe, reason, uniqueId));
            }

            foreach (var blocked in disks.Where(d => !d.SafeCandidate))
                logger.Info($"Disk {blocked.Number} protected/hidden: {blocked.DisplayName} — {blocked.ProtectionReason}");

            return disks.OrderBy(d => int.TryParse(d.Number, out var n) ? n : int.MaxValue).ToList();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { logger.Error("Disk scan failed", ex); return []; }
    }

    public async Task<DiskInfo?> GetDiskAsync(string number, CancellationToken ct = default) =>
        (await ScanAsync(ct)).FirstOrDefault(d => d.Number == number);

    private static string GetString(JsonElement item, string property, string fallback = "") => item.TryGetProperty(property, out var value) ? value.ToString() : fallback;
    private static long GetInt64(JsonElement item, string property) => item.TryGetProperty(property, out var value) && value.TryGetInt64(out var result) ? result : 0;
    private static bool GetBool(JsonElement item, string property) => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
