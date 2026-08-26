using System.Text.Json;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Hardware;

public sealed class UsbStorageService(
    ProcessRunner runner,
    AppLogger logger,
    MewSwitchManager.Core.SafetyEngine safety)
{
    private readonly NativeVolumeWriter _writer = new();
    private readonly DiskService _disks = new(runner, logger);
    private readonly LinuxArchiveServiceV2 _archive = new(logger);

    public async Task PrepareAndFlashAsync(DiskInfo target, string archivePath, string workDirectory, IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        ValidateTarget(target, archivePath);
        Directory.CreateDirectory(workDirectory);
        logger.Warn($"USB write requested for Disk {target.Number}: {target.Model} ({target.SizeGb:0.0} GB). All data on the target will be destroyed.");
        try
        {
            var rawPath = await _archive.PrepareRawImageAsync(archivePath, workDirectory, progress, ct);
            await FlashAsync(target, rawPath, progress, ct);
        }
        finally { _archive.Cleanup(workDirectory); }
    }

    private async Task FlashAsync(DiskInfo target, string rawPath, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        var rawSize = new FileInfo(rawPath).Length;
        if (rawSize <= 0) throw new InvalidDataException("The extracted Linux image is empty.");
        if (target.SizeGb * 1_000_000_000d < rawSize)
            throw new InvalidOperationException($"The selected USB is too small. It has about {target.SizeGb:0.0} GB, while the Linux image needs {rawSize / 1_000_000_000d:0.00} GB.");
        if (!int.TryParse(target.Number, out var diskNumber)) throw new InvalidOperationException("Invalid target disk number.");

        progress?.Report(new DownloadProgress(0, 1, 0, null, "VERIFYING TARGET", "Re-checking USB identity before the destructive operation."));
        safety.DemandStableIdentity(target, await _disks.GetDiskAsync(target.Number, ct));
        progress?.Report(new DownloadProgress(0, 1, 0, null, "PARTITIONING USB", "Creating the target partition. The USB is being modified now."));
        await RepartitionTargetAsync(diskNumber, ct);

        var partition = await GetFirstPartitionVolumeAsync(diskNumber, ct);
        ValidatePartition(partition, rawSize);
        await RemoveDriveLetterAsync(diskNumber, partition!.Value.PartitionNumber, partition.Value.DriveLetter, ct);
        safety.DemandStableIdentity(target, await _disks.GetDiskAsync(target.Number, ct));

        logger.Info($"Flashing ubuntu.raw ({rawSize:N0} bytes) to USB partition {partition.Value.PartitionNumber} at {partition.Value.VolumePath}.");
        progress?.Report(new DownloadProgress(0, rawSize, 0, null, "FLASHING USB", "Writing Linux image to USB..."));
        await _writer.WriteAsync(partition.Value.VolumePath!, rawPath, progress, ct);
        logger.Info("USB Linux image write completed successfully.");
        progress?.Report(new DownloadProgress(1, 1, 0, TimeSpan.Zero, "USB FLASH COMPLETE", "Linux image written successfully."));
    }

    private void ValidateTarget(DiskInfo target, string archivePath)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("USB preparation requires Windows.");
        safety.DemandSafeTarget(target);
        if (!File.Exists(archivePath)) throw new FileNotFoundException("Linux image archive not found.", archivePath);
    }

    private static void ValidatePartition((int PartitionNumber, long Size, string? VolumePath, string? DriveLetter)? partition, long rawSize)
    {
        if (partition is null || string.IsNullOrWhiteSpace(partition.Value.VolumePath))
            throw new InvalidOperationException("Windows created the USB partition but did not expose a writable volume path. The image was not written.");
        if (partition.Value.Size < rawSize)
            throw new InvalidOperationException($"The USB partition ({partition.Value.Size:N0} bytes) is smaller than the Linux image ({rawSize:N0} bytes).");
    }

    private async Task RepartitionTargetAsync(int diskNumber, CancellationToken ct)
    {
        var script = $"select disk {diskNumber}\r\nclean\r\ncreate partition primary\r\nexit\r\n";
        var temp = Path.Combine(Path.GetTempPath(), $"mewswitch-diskpart-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(temp, script, ct);
        try
        {
            var result = await runner.RunAsync("diskpart.exe", $"/s {Quote(temp)}", ct);
            if (result.ExitCode != 0) throw new InvalidOperationException($"Disk partitioning failed: {result.StdOut}\n{result.StdErr}");
        }
        finally { TryDelete(temp); }
        logger.Info($"Disk {diskNumber} repartitioned with one primary partition.");
    }

    private async Task<(int PartitionNumber, long Size, string? VolumePath, string? DriveLetter)?> GetFirstPartitionVolumeAsync(int diskNumber, CancellationToken ct)
    {
        const string commandTemplate = @"
$p = Get-Partition -DiskNumber {0} | Sort-Object PartitionNumber | Select-Object -First 1
if ($null -eq $p) {{ exit 2 }}
$v = Get-Volume -Partition $p -ErrorAction SilentlyContinue
if ($null -eq $v) {{
    $used = @(Get-Volume | Where-Object {{ $null -ne $_.DriveLetter }} | ForEach-Object {{ [string]$_.DriveLetter }})
    $candidate = ('R','S','T','U','V','W','X','Y','Z') | Where-Object {{ $used -notcontains $_ }} | Select-Object -First 1
    if ($candidate) {{
        Set-Partition -DiskNumber {0} -PartitionNumber $p.PartitionNumber -NewDriveLetter $candidate -ErrorAction Stop
        $v = Get-Volume -DriveLetter $candidate -ErrorAction Stop
    }}
}}
[pscustomobject]@{{
    PartitionNumber = [int]$p.PartitionNumber
    Size = [int64]$p.Size
    VolumePath = if ($v) {{ [string]$v.Path }} else {{ '' }}
    DriveLetter = if ($v -and $v.DriveLetter) {{ [string]$v.DriveLetter }} else {{ '' }}
}} | ConvertTo-Json -Compress";
        var result = await runner.RunAsync("powershell.exe", $"-NoProfile -Command {Quote(string.Format(commandTemplate, diskNumber))}", ct);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut)) return null;
        using var document = JsonDocument.Parse(result.StdOut);
        var root = document.RootElement;
        return (root.GetProperty("PartitionNumber").GetInt32(), root.GetProperty("Size").GetInt64(), root.TryGetProperty("VolumePath", out var path) ? path.GetString() : null, root.TryGetProperty("DriveLetter", out var drive) ? drive.GetString() : null);
    }

    private async Task RemoveDriveLetterAsync(int diskNumber, int partitionNumber, string? driveLetter, CancellationToken ct)
    {
        var command = $"$p=Get-Partition -DiskNumber {diskNumber} -PartitionNumber {partitionNumber}; if($p.DriveLetter){{Remove-PartitionAccessPath -DiskNumber {diskNumber} -PartitionNumber {partitionNumber} -AccessPath ($p.DriveLetter+':\\') -ErrorAction SilentlyContinue}}";
        var result = await runner.RunAsync("powershell.exe", $"-NoProfile -Command {Quote(command)}", ct);
        if (result.ExitCode != 0) logger.Warn($"Could not remove temporary drive letter {driveLetter ?? ""}; continuing because the volume path was already captured.");
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
