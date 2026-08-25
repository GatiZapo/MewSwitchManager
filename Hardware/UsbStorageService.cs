using System.Diagnostics;
using System.Text.Json;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Hardware;

public sealed class UsbStorageService(ProcessRunner runner, AppLogger logger, MewSwitchManager.Core.SafetyEngine safety)
{
    private readonly NativeVolumeWriter _writer = new();

    public async Task PrepareAndFlashAsync(
        DiskInfo target,
        string archivePath,
        string workDirectory,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("USB preparation requires Windows.");
        safety.DemandSafeTarget(target);
        if (!int.TryParse(target.Number, out var diskNumber))
            throw new InvalidOperationException("Invalid target disk number.");
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Linux image archive not found.", archivePath);

        Directory.CreateDirectory(workDirectory);
        var extractDir = Path.Combine(workDirectory, "linux-extracted");
        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, recursive: true);
        Directory.CreateDirectory(extractDir);

        logger.Warn($"USB write requested for Disk {target.Number}: {target.Model} ({target.SizeGb:0.0} GB). All data on the target will be destroyed.");

        await ExtractArchiveAsync(archivePath, extractDir, progress, ct);
        var rawPath = await ResolveRawImageAsync(extractDir, progress, ct);
        var rawSize = new FileInfo(rawPath).Length;
        if (rawSize <= 0) throw new InvalidDataException("The extracted Linux image is empty.");
        if (target.SizeGb * 1_000_000_000d < rawSize)
            throw new InvalidOperationException($"The selected USB is too small. It has about {target.SizeGb:0.0} GB, while the Linux image needs {rawSize / 1_000_000_000d:0.00} GB.");

        progress?.Report(new DownloadProgress(0, 1, 0, null, "VERIFYING TARGET"));

        var beforeClean = await new DiskService(runner, logger).GetDiskAsync(target.Number, ct);
        safety.DemandStableIdentity(target, beforeClean);

        progress?.Report(new DownloadProgress(0, 1, 0, null, "PARTITIONING USB"));
        await RepartitionTargetAsync(diskNumber, ct);
        var partition = await GetFirstPartitionVolumeAsync(diskNumber, ct);
        if (partition is null || string.IsNullOrWhiteSpace(partition.Value.VolumePath))
            throw new InvalidOperationException("Windows created the USB partition but did not expose a writable volume path. The image was not written.");
        if (partition.Value.Size < rawSize)
            throw new InvalidOperationException($"The USB partition ({partition.Value.Size:N0} bytes) is smaller than the Linux image ({rawSize:N0} bytes).");

        await RemoveDriveLetterAsync(diskNumber, partition.Value.PartitionNumber, partition.Value.DriveLetter, ct);

        var afterPartition = await new DiskService(runner, logger).GetDiskAsync(target.Number, ct);
        safety.DemandStableIdentity(target, afterPartition);

        logger.Info($"Flashing ubuntu.raw ({rawSize:N0} bytes) to USB partition {partition.Value.PartitionNumber} at {partition.Value.VolumePath}.");
        progress?.Report(new DownloadProgress(0, rawSize, 0, null, "FLASHING USB"));
        await _writer.WriteAsync(partition.Value.VolumePath!, rawPath, progress, ct);
        logger.Info("USB Linux image write completed successfully.");

        progress?.Report(new DownloadProgress(1, 1, 0, TimeSpan.Zero, "USB FLASH COMPLETE"));

        try
        {
            Directory.Delete(extractDir, recursive: true);
            logger.Info("Temporary extracted Linux files removed.");
        }
        catch (Exception cleanupError)
        {
            logger.Warn($"Temporary extraction cleanup failed: {cleanupError.Message}");
        }
    }

    private async Task<string> ResolveRawImageAsync(string root, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        progress?.Report(new DownloadProgress(0, 1, 0, null, "BUILDING LINUX IMAGE"));
        var raw = Directory.EnumerateFiles(root, "ubuntu.raw", SearchOption.AllDirectories).FirstOrDefault();
        if (raw is not null) return raw;

        var parts = Directory.EnumerateFiles(root, "l4t.*", SearchOption.AllDirectories)
            .Where(p => Path.GetFileName(p).StartsWith("l4t.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => ExtractPartNumber(Path.GetFileName(p)))
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (parts.Length == 0)
            throw new InvalidDataException("The Linux archive does not contain ubuntu.raw or l4t split image files.");

        var output = Path.Combine(root, "ubuntu.raw");
        await MergePartsAsync(parts, output, progress, ct);
        return output;
    }

    private async Task ExtractArchiveAsync(string archivePath, string destination, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        logger.Info("Extracting Linux archive...");
        await Task.Run(() => ExtractArchiveWithProgress(archivePath, destination, progress, ct), ct);
        logger.Info("Linux archive extracted successfully.");
    }

    private void ExtractArchiveWithProgress(string archivePath, string destination, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        var root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var archive = SevenZipArchive.OpenArchive(archivePath);
        var entries = archive.Entries.ToArray();
        var totalEntries = Math.Max(1, entries.Length);
        var totalBytes = entries.Where(e => !e.IsDirectory).Sum(e => Math.Max(0L, e.Size));
        var useByteProgress = totalBytes > 0;
        var completedEntries = 0;
        long completedBytes = 0;

        progress?.Report(new DownloadProgress(0, useByteProgress ? totalBytes : totalEntries, 0, null, "EXTRACTING LINUX IMAGE"));

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            var key = entry.Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                completedEntries++;
                continue;
            }

            var relative = key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var outputPath = Path.GetFullPath(Path.Combine(destination, relative));
            if (!outputPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"The Linux archive contains an unsafe path: {key}");

            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(outputPath);
            }
            else
            {
                var parent = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);

                var tempPath = outputPath + $".{Guid.NewGuid():N}.mewtmp";
                try
                {
                    WriteEntryToTempWithRetry(entry, tempPath, completedBytes, totalBytes, completedEntries, totalEntries, progress, ct);
                    MoveExtractedFileWithRetry(tempPath, outputPath, ct);
                    completedBytes += Math.Max(0L, entry.Size);
                }
                finally
                {
                    TryDeleteFile(tempPath);
                }
            }

            completedEntries++;
            var current = useByteProgress ? Math.Min(totalBytes, completedBytes) : completedEntries;
            progress?.Report(new DownloadProgress(current, useByteProgress ? totalBytes : totalEntries, 0, null, "EXTRACTING LINUX IMAGE"));
        }

        progress?.Report(new DownloadProgress(useByteProgress ? totalBytes : totalEntries, useByteProgress ? totalBytes : totalEntries, 0, TimeSpan.Zero, "EXTRACTING LINUX IMAGE"));
    }

    private void WriteEntryToTempWithRetry(
        IArchiveEntry entry,
        string tempPath,
        long completedBytes,
        long totalBytes,
        int completedEntries,
        int totalEntries,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        const int attempts = 8;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var output = new ProgressFileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete,
                    1024 * 1024,
                    FileOptions.SequentialScan,
                    written =>
                    {
                        var current = completedBytes + written;
                        if (totalBytes > 0)
                            progress?.Report(new DownloadProgress(Math.Min(current, totalBytes), totalBytes, 0, null, "EXTRACTING LINUX IMAGE"));
                        else
                            progress?.Report(new DownloadProgress(completedEntries, totalEntries, 0, null, "EXTRACTING LINUX IMAGE"));
                    });

                entry.WriteTo(output, new ExtractionOptions
                {
                    ExtractFullPath = false,
                    Overwrite = false,
                    CheckCrc = true
                });
                output.Flush(flushToDisk: true);
                return;
            }
            catch (IOException) when (attempt < attempts)
            {
                TryDeleteFile(tempPath);
                Thread.Sleep(150 * attempt);
            }
        }
    }

    private void MoveExtractedFileWithRetry(string tempPath, string outputPath, CancellationToken ct)
    {
        const int attempts = 20;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                File.Move(tempPath, outputPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < attempts)
            {
                Thread.Sleep(250 * attempt);
            }
        }

        throw new IOException($"Could not finalize extracted file '{outputPath}'. Another process may be holding it open.");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }

    private async Task MergePartsAsync(string[] parts, string output, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        var totalBytes = parts.Sum(p => new FileInfo(p).Length);
        long copiedBytes = 0;
        await using var dest = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4 * 1024 * 1024, FileOptions.SequentialScan | FileOptions.Asynchronous);
        for (var i = 0; i < parts.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            logger.Info($"Merging {Path.GetFileName(parts[i])}...");
            await using var src = new FileStream(parts[i], FileMode.Open, FileAccess.Read, FileShare.Read, 4 * 1024 * 1024, FileOptions.SequentialScan | FileOptions.Asynchronous);
            var buffer = new byte[4 * 1024 * 1024];
            int read;
            while ((read = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, read), ct);
                copiedBytes += read;
                progress?.Report(new DownloadProgress(copiedBytes, totalBytes, 0, null, "BUILDING LINUX IMAGE"));
            }
        }
        await dest.FlushAsync(ct);
        progress?.Report(new DownloadProgress(totalBytes, totalBytes, 0, TimeSpan.Zero, "BUILDING LINUX IMAGE"));
        logger.Info($"Merged {parts.Length} Linux image parts into ubuntu.raw.");
    }

    private async Task RepartitionTargetAsync(int diskNumber, CancellationToken ct)
    {
        var script = $"select disk {diskNumber}\r\nclean\r\ncreate partition primary\r\nexit\r\n";
        var temp = Path.Combine(Path.GetTempPath(), $"mewswitch-diskpart-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(temp, script, ct);
        try
        {
            var result = await runner.RunAsync("diskpart.exe", $"/s {Quote(temp)}", ct);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Disk partitioning failed: {result.StdOut}\n{result.StdErr}");
        }
        finally
        {
            try { File.Delete(temp); } catch { }
        }
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

        var command = string.Format(commandTemplate, diskNumber);
        var result = await runner.RunAsync("powershell.exe", $"-NoProfile -Command {Quote(command)}", ct);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut)) return null;

        using var doc = JsonDocument.Parse(result.StdOut);
        var root = doc.RootElement;
        var partitionNumber = root.GetProperty("PartitionNumber").GetInt32();
        var size = root.GetProperty("Size").GetInt64();
        var path = root.TryGetProperty("VolumePath", out var pathElement) ? pathElement.GetString() : null;
        var drive = root.TryGetProperty("DriveLetter", out var driveElement) ? driveElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(path)) return (partitionNumber, size, null, drive);
        logger.Info($"Target partition exposed by Windows as {path}.");
        return (partitionNumber, size, path, drive);
    }

    private async Task RemoveDriveLetterAsync(int diskNumber, int partitionNumber, string? driveLetter, CancellationToken ct)
    {
        var command = $"$p=Get-Partition -DiskNumber {diskNumber} -PartitionNumber {partitionNumber}; if($p.DriveLetter){{Remove-PartitionAccessPath -DiskNumber {diskNumber} -PartitionNumber {partitionNumber} -AccessPath ($p.DriveLetter+':\\') -ErrorAction SilentlyContinue}}";
        var result = await runner.RunAsync("powershell.exe", $"-NoProfile -Command {Quote(command)}", ct);
        if (result.ExitCode != 0)
            logger.Warn($"Could not remove temporary drive letter {driveLetter ?? ""}; continuing because the volume path was already captured.");
    }

    private static int ExtractPartNumber(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot >= 0 && int.TryParse(fileName[(dot + 1)..], out var n) ? n : int.MaxValue;
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private sealed class ProgressFileStream(
        string path,
        FileMode mode,
        FileAccess access,
        FileShare share,
        int bufferSize,
        FileOptions options,
        Action<long> onProgress) : FileStream(path, mode, access, share, bufferSize, options)
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastReported;

        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            ReportIfNeeded(count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            base.Write(buffer);
            ReportIfNeeded(buffer.Length);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await base.WriteAsync(buffer, cancellationToken);
            ReportIfNeeded(buffer.Length);
        }

        private void ReportIfNeeded(int written)
        {
            _lastReported += written;
            if (_clock.ElapsedMilliseconds >= 120)
            {
                onProgress(_lastReported);
                _clock.Restart();
            }
        }
    }
}
