using System.Diagnostics;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Hardware;

/// <summary>
/// Extracts the Linux distribution archive and resolves the raw image used by the USB writer.
/// Keeps archive-specific logic out of the destructive USB workflow.
/// </summary>
public sealed class LinuxArchiveService(AppLogger logger)
{
    private const int FileWriteAttempts = 8;
    private const int MoveAttempts = 20;

    public async Task<string> PrepareRawImageAsync(
        string archivePath,
        string workDirectory,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct = default)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Linux image archive not found.", archivePath);

        Directory.CreateDirectory(workDirectory);
        var extractDirectory = Path.Combine(workDirectory, "linux-extracted");
        RecreateDirectory(extractDirectory);

        try
        {
            await ExtractAsync(archivePath, extractDirectory, progress, ct);
            return await ResolveRawImageAsync(extractDirectory, progress, ct);
        }
        catch
        {
            TryDeleteDirectory(extractDirectory);
            throw;
        }
    }

    public void Cleanup(string workDirectory)
    {
        var extractDirectory = Path.Combine(workDirectory, "linux-extracted");
        TryDeleteDirectory(extractDirectory);
    }

    private async Task ExtractAsync(
        string archivePath,
        string destination,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        logger.Info($"Extracting Linux archive: {Path.GetFileName(archivePath)}");
        await Task.Run(() => ExtractSynchronously(archivePath, destination, progress, ct), ct);
        logger.Info("Linux archive extraction completed.");
    }

    // SharpCompress documents synchronous sequential extraction as the preferred path
    // for solid 7z/LZMA archives because async extraction can be less efficient.
    private void ExtractSynchronously(
        string archivePath,
        string destination,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        var root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var archive = SevenZipArchive.OpenArchive(archivePath);
        var entries = archive.Entries.ToArray();
        var totalBytes = entries.Where(e => !e.IsDirectory).Sum(e => Math.Max(0L, e.Size));
        var totalEntries = Math.Max(1, entries.Length);
        var useByteProgress = totalBytes > 0;
        long completedBytes = 0;
        var completedEntries = 0;

        Report(progress, 0, useByteProgress ? totalBytes : totalEntries, "EXTRACTING LINUX IMAGE", "Opening archive...");

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            var key = entry.Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                completedEntries++;
                continue;
            }

            var outputPath = GetSafeOutputPath(destination, root, key);
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(outputPath);
            }
            else
            {
                var parent = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);

                var temporaryPath = outputPath + $".{Guid.NewGuid():N}.mewtmp";
                try
                {
                    WriteEntryWithRetry(
                        entry,
                        temporaryPath,
                        completedBytes,
                        totalBytes,
                        completedEntries,
                        totalEntries,
                        useByteProgress,
                        progress,
                        ct);

                    MoveWithRetry(temporaryPath, outputPath, ct);
                    completedBytes += Math.Max(0L, entry.Size);
                }
                finally
                {
                    TryDeleteFile(temporaryPath);
                }
            }

            completedEntries++;
            var current = useByteProgress ? Math.Min(completedBytes, totalBytes) : completedEntries;
            Report(progress, current, useByteProgress ? totalBytes : totalEntries, "EXTRACTING LINUX IMAGE", Path.GetFileName(key));
        }

        Report(progress, useByteProgress ? totalBytes : totalEntries, useByteProgress ? totalBytes : totalEntries, "FINALIZING LINUX IMAGE", "Extraction complete. Finalizing files...");
    }

    private void WriteEntryWithRetry(
        IArchiveEntry entry,
        string temporaryPath,
        long completedBytes,
        long totalBytes,
        int completedEntries,
        int totalEntries,
        bool useByteProgress,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= FileWriteAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var output = new ProgressFileStream(
                    temporaryPath,
                    written =>
                    {
                        var current = completedBytes + written;
                        Report(
                            progress,
                            useByteProgress ? Math.Min(current, totalBytes) : completedEntries,
                            useByteProgress ? totalBytes : totalEntries,
                            "EXTRACTING LINUX IMAGE",
                            $"Extracting {Path.GetFileName(entry.Key)}");
                    });

                entry.WriteTo(output, new ExtractionOptions
                {
                    ExtractFullPath = false,
                    Overwrite = false,
                    CheckCrc = true
                });

                // Closing the stream is enough to flush buffered data. Avoid forcing
                // a physical disk flush for every archive entry; that can make large
                // 7z extraction appear frozen at the end of the operation.
                return;
            }
            catch (IOException) when (attempt < FileWriteAttempts)
            {
                TryDeleteFile(temporaryPath);
                Thread.Sleep(150 * attempt);
            }
        }
    }

    private static void MoveWithRetry(string source, string destination, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MoveAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < MoveAttempts)
            {
                Thread.Sleep(250 * attempt);
            }
        }

        throw new IOException($"Could not finalize extracted file '{destination}'. Another process may be holding it open.");
    }

    private async Task<string> ResolveRawImageAsync(
        string root,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        Report(progress, 0, 1, "BUILDING LINUX IMAGE", "Locating Linux raw image...");

        var raw = Directory.EnumerateFiles(root, "ubuntu.raw", SearchOption.AllDirectories).FirstOrDefault();
        if (raw is not null)
        {
            Report(progress, 1, 1, "BUILDING LINUX IMAGE", "ubuntu.raw found.");
            return raw;
        }

        var parts = Directory.EnumerateFiles(root, "l4t.*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).StartsWith("l4t.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => ExtractPartNumber(Path.GetFileName(path)))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (parts.Length == 0)
            throw new InvalidDataException("The Linux archive does not contain ubuntu.raw or l4t split image files.");

        var output = Path.Combine(root, "ubuntu.raw");
        await MergePartsAsync(parts, output, progress, ct);
        return output;
    }

    private async Task MergePartsAsync(
        string[] parts,
        string output,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        var totalBytes = parts.Sum(path => new FileInfo(path).Length);
        long copiedBytes = 0;

        await using var destination = new FileStream(
            output,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4 * 1024 * 1024,
            FileOptions.SequentialScan | FileOptions.Asynchronous);

        foreach (var part in parts)
        {
            ct.ThrowIfCancellationRequested();
            logger.Info($"Merging {Path.GetFileName(part)}...");

            await using var source = new FileStream(
                part,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4 * 1024 * 1024,
                FileOptions.SequentialScan | FileOptions.Asynchronous);

            var buffer = new byte[4 * 1024 * 1024];
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), ct);
                copiedBytes += read;
                Report(progress, copiedBytes, totalBytes, "BUILDING LINUX IMAGE", $"Merging {Path.GetFileName(part)}");
            }
        }

        await destination.FlushAsync(ct);
        Report(progress, totalBytes, totalBytes, "FINALIZING LINUX IMAGE", "Raw image assembled successfully.");
        logger.Info($"Merged {parts.Length} Linux image parts into ubuntu.raw.");
    }

    private static string GetSafeOutputPath(string destination, string root, string key)
    {
        var relative = key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var outputPath = Path.GetFullPath(Path.Combine(destination, relative));
        if (!outputPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The Linux archive contains an unsafe path: {key}");
        return outputPath;
    }

    private static int ExtractPartNumber(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot >= 0 && int.TryParse(fileName[(dot + 1)..], out var number) ? number : int.MaxValue;
    }

    private static void RecreateDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
        Directory.CreateDirectory(path);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static void Report(
        IProgress<DownloadProgress>? progress,
        long bytes,
        long? total,
        string phase,
        string detail)
    {
        progress?.Report(new DownloadProgress(bytes, total, 0, null, phase, detail));
    }

    private sealed class ProgressFileStream(string path, Action<long> onProgress) : FileStream(
        path,
        FileMode.CreateNew,
        FileAccess.Write,
        FileShare.ReadWrite | FileShare.Delete,
        1024 * 1024,
        FileOptions.SequentialScan)
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _written;

        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            Report(count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            base.Write(buffer);
            Report(buffer.Length);
        }

        private void Report(int count)
        {
            _written += count;
            if (_clock.ElapsedMilliseconds < 120)
                return;

            onProgress(_written);
            _clock.Restart();
        }
    }
}
