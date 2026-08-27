using MewNX.Core;
using MewNX.Infrastructure;
using MewNX.Models;

namespace MewNX.Hardware;

public sealed class UsbStorageService(
    ProcessRunner runner,
    AppLogger logger,
    SafetyEngine safety)
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
        var before = await _disks.GetDiskAsync(target.Number, ct);
        safety.DemandStableIdentity(target, before);

        progress?.Report(new DownloadProgress(0, 1, 0, null, "PREPARING USB", "Cleaning the selected USB disk before writing the raw image."));
        await CleanTargetAsync(diskNumber, ct);
        await RescanStorageAsync(ct);

        var afterClean = await _disks.GetDiskAsync(target.Number, ct);
        safety.DemandStableIdentity(target, afterClean);

        logger.Info($"Flashing ubuntu.raw ({rawSize:N0} bytes) directly to PhysicalDrive{diskNumber}.");
        progress?.Report(new DownloadProgress(0, rawSize, 0, null, "FLASHING USB", "Writing the verified Linux disk image to the USB..."));
        await _writer.WritePhysicalDiskAsync(diskNumber, rawPath, progress, ct);
        await RescanStorageAsync(ct);
        logger.Info("USB Linux disk image write completed successfully.");
        progress?.Report(new DownloadProgress(rawSize, rawSize, 0, TimeSpan.Zero, "USB FLASH COMPLETE", "Linux image written successfully."));
    }

    private void ValidateTarget(DiskInfo target, string archivePath)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("USB preparation requires Windows.");
        safety.DemandSafeTarget(target);
        if (!File.Exists(archivePath)) throw new FileNotFoundException("Linux image archive not found.", archivePath);
    }

    private async Task CleanTargetAsync(int diskNumber, CancellationToken ct)
    {
        var script = $"select disk {diskNumber}\r\nattributes disk clear readonly\r\nclean\r\nexit\r\n";
        var temp = Path.Combine(Path.GetTempPath(), $"mewnx-diskpart-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(temp, script, ct);
        try
        {
            var result = await runner.RunAsync("diskpart.exe", $"/s {Quote(temp)}", ct);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"USB preparation failed while cleaning Disk {diskNumber}: {result.StdOut}\n{result.StdErr}");
            logger.Info($"Disk {diskNumber} cleaned successfully and is ready for raw-image writing.");
        }
        finally { TryDelete(temp); }
    }

    private async Task RescanStorageAsync(CancellationToken ct)
    {
        try
        {
            var result = await runner.RunAsync("powershell.exe", "-NoProfile -Command \"Update-HostStorageCache -ErrorAction SilentlyContinue\"", ct);
            if (result.ExitCode != 0)
                logger.Warn($"Storage rescan returned exit code {result.ExitCode}: {result.StdErr.Trim()}");
        }
        catch (Exception ex) { logger.Warn($"Storage rescan was unavailable: {ex.Message}"); }
        await Task.Delay(750, ct);
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
