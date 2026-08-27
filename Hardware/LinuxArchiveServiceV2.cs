using System.Diagnostics;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using MewNX.Infrastructure;
using MewNX.Models;

namespace MewNX.Hardware;

/// <summary>
/// Extracts the Linux distribution archive and resolves the raw image used by the USB writer.
/// All filesystem-heavy post-extraction discovery is kept off the WinForms UI thread.
/// </summary>
public sealed class LinuxArchiveServiceV2(AppLogger logger)
{
    private const int MoveAttempts = 12;

    public async Task<string> PrepareRawImageAsync(string archivePath, string workDirectory, IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        if (!File.Exists(archivePath)) throw new FileNotFoundException("Linux image archive not found.", archivePath);
        Directory.CreateDirectory(workDirectory);
        var extractDirectory = Path.Combine(workDirectory, "linux-extracted");
        RecreateDirectory(extractDirectory);
        try
        {
            await Task.Run(() => ExtractArchive(archivePath, extractDirectory, progress, ct), ct);
            progress?.Report(new DownloadProgress(0, 1, 0, null, "BUILDING LINUX IMAGE", "Scanning extracted files for the Linux image..."));
            return await Task.Run(() => ResolveRawImageAsync(extractDirectory, progress, ct), ct);
        }
        catch { TryDeleteDirectory(extractDirectory); throw; }
    }

    public void Cleanup(string workDirectory) => TryDeleteDirectory(Path.Combine(workDirectory, "linux-extracted"));

    private void ExtractArchive(string archivePath, string destination, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        logger.Info($"Extracting Linux archive: {Path.GetFileName(archivePath)}");
        using var archive = SevenZipArchive.OpenArchive(archivePath);
        var entries = archive.Entries.ToArray();
        var files = entries.Where(x => !x.IsDirectory).ToArray();
        var totalBytes = files.Sum(x => Math.Max(0L, x.Size));
        var totalEntries = Math.Max(1, files.Length);
        long completedBytes = 0;
        var completedEntries = 0;
        var root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Report(progress, 0, totalBytes > 0 ? totalBytes : totalEntries, "EXTRACTING LINUX IMAGE", "Opening archive...");
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            var key = entry.Key ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key)) continue;
            var outputPath = SafeOutputPath(destination, root, key);
            if (entry.IsDirectory) { Directory.CreateDirectory(outputPath); continue; }
            var parent = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
            var temporaryPath = outputPath + $".{Guid.NewGuid():N}.mewtmp";
            try
            {
                using var output = new ProgressFileStream(temporaryPath, written =>
                {
                    var current = completedBytes + written;
                    Report(progress, totalBytes > 0 ? Math.Min(current, totalBytes) : completedEntries, totalBytes > 0 ? totalBytes : totalEntries, "EXTRACTING LINUX IMAGE", $"Extracting {Path.GetFileName(key)}");
                });
                entry.WriteTo(output);
                output.Flush();
                MoveWithRetry(temporaryPath, outputPath, ct);
                completedBytes += Math.Max(0L, entry.Size);
                completedEntries++;
                Report(progress, totalBytes > 0 ? completedBytes : completedEntries, totalBytes > 0 ? totalBytes : totalEntries, "EXTRACTING LINUX IMAGE", Path.GetFileName(key));
            }
            finally { TryDeleteFile(temporaryPath); }
        }
        Report(progress, totalBytes > 0 ? totalBytes : totalEntries, totalBytes > 0 ? totalBytes : totalEntries, "FINALIZING LINUX IMAGE", "Extraction complete. Finalizing files...");
        logger.Info("Linux archive extraction completed.");
    }

    private static void MoveWithRetry(string source, string destination, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MoveAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { File.Move(source, destination, overwrite: true); return; }
            catch (IOException) when (attempt < MoveAttempts) { Thread.Sleep(200 * attempt); }
        }
        throw new IOException($"Could not finalize extracted file '{destination}'. Another process may be holding it open.");
    }

    private string ResolveRawImageAsync(string root, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Report(progress, 0, 1, "BUILDING LINUX IMAGE", "Locating Linux raw image...");
        var raw = Directory.EnumerateFiles(root, "ubuntu.raw", SearchOption.AllDirectories).FirstOrDefault();
        if (raw is not null) { Report(progress, 1, 1, "BUILDING LINUX IMAGE", "ubuntu.raw found."); logger.Info($"Resolved Linux raw image: {raw}"); return raw; }
        var parts = Directory.EnumerateFiles(root, "l4t.*", SearchOption.AllDirectories).OrderBy(path => ExtractPartNumber(Path.GetFileName(path))).ThenBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        if (parts.Length == 0) throw new InvalidDataException("The Linux archive does not contain ubuntu.raw or l4t split image files.");
        var output = Path.Combine(root, "ubuntu.raw");
        MergePartsAsync(parts, output, progress, ct).GetAwaiter().GetResult();
        return output;
    }

    private static async Task MergePartsAsync(string[] parts, string output, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        var totalBytes = parts.Sum(path => new FileInfo(path).Length);
        long copiedBytes = 0;
        await using var destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4 * 1024 * 1024, FileOptions.SequentialScan | FileOptions.Asynchronous);
        var buffer = new byte[4 * 1024 * 1024];
        foreach (var part in parts)
        {
            ct.ThrowIfCancellationRequested();
            await using var source = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, FileOptions.SequentialScan | FileOptions.Asynchronous);
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), ct);
                copiedBytes += read;
                Report(progress, copiedBytes, totalBytes, "BUILDING LINUX IMAGE", $"Merging {Path.GetFileName(part)}");
            }
        }
        await destination.FlushAsync(ct);
        Report(progress, totalBytes, totalBytes, "FINALIZING LINUX IMAGE", "Raw image assembled successfully.");
    }

    private static string SafeOutputPath(string destination, string root, string key)
    {
        var relative = key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var outputPath = Path.GetFullPath(Path.Combine(destination, relative));
        if (!outputPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"The Linux archive contains an unsafe path: {key}");
        return outputPath;
    }

    private static int ExtractPartNumber(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot >= 0 && int.TryParse(fileName[(dot + 1)..], out var number) ? number : int.MaxValue;
    }

    private static void RecreateDirectory(string path) { TryDeleteDirectory(path); Directory.CreateDirectory(path); }
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
    private static void TryDeleteFile(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private static void Report(IProgress<DownloadProgress>? progress, long bytes, long? total, string phase, string detail) => progress?.Report(new DownloadProgress(bytes, total, 0, null, phase, detail));

    private sealed class ProgressFileStream(string path, Action<long> onProgress) : FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, 1024 * 1024, FileOptions.SequentialScan)
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _written;
        public override void Write(byte[] buffer, int offset, int count) { base.Write(buffer, offset, count); Report(count); }
        public override void Write(ReadOnlySpan<byte> buffer) { base.Write(buffer); Report(buffer.Length); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); Write(buffer, offset, count); return Task.CompletedTask; }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); Write(buffer.Span); return ValueTask.CompletedTask; }
        private void Report(int count) { _written += count; if (_clock.ElapsedMilliseconds < 100) return; onProgress(_written); _clock.Restart(); }
    }
}
