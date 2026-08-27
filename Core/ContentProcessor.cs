using System.IO.Compression;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace MewSwitchManager.Core;

public sealed class ContentProcessor
{
    private readonly AppLogger _logger;
    public ContentProcessor(AppLogger logger) => _logger = logger;

    public async Task<PreparedContent> PrepareAsync(string input, string workDirectory, CancellationToken ct = default)
    {
        if (!File.Exists(input)) throw new FileNotFoundException("Content file not found.", input);
        Directory.CreateDirectory(workDirectory);
        var extension = Path.GetExtension(input);
        if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) || extension.Equals(".7z", StringComparison.OrdinalIgnoreCase) || extension.Equals(".tar", StringComparison.OrdinalIgnoreCase) || extension.Equals(".gz", StringComparison.OrdinalIgnoreCase))
        {
            var extract = Path.Combine(workDirectory, "extracted");
            if (Directory.Exists(extract)) Directory.Delete(extract, true);
            Directory.CreateDirectory(extract);
            await Task.Run(() => ExtractArchive(input, extract), ct);
            var payload = ResumableDownloadService.FindPreparedPayload(extract);
            if (payload is null) throw new InvalidDataException("Archive did not contain a supported Switch payload/content file.");
            var info = new FileInfo(payload);
            _logger.Info($"Game Center extracted {Path.GetFileName(input)} to {payload}.");
            return new PreparedContent(payload, Path.GetExtension(payload).TrimStart('.').ToUpperInvariant(), info.Length);
        }
        var file = new FileInfo(input);
        return await Task.FromResult(new PreparedContent(input, extension.TrimStart('.').ToUpperInvariant(), file.Length));
    }

    private static void ExtractArchive(string input, string destination)
    {
        if (Path.GetExtension(input).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(input, destination, overwriteFiles: true);
            return;
        }
        using var archive = ArchiveFactory.OpenArchive(input);
        archive.WriteToDirectory(destination, new ExtractionOptions { ExtractFullPath = true, Overwrite = true, CheckCrc = true });
    }

    public static void Cleanup(string workingDirectory, IEnumerable<string> keepPaths)
    {
        if (!Directory.Exists(workingDirectory)) return;
        var keep = keepPaths.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(workingDirectory, "*", SearchOption.AllDirectories))
            if (!keep.Contains(Path.GetFullPath(file))) TryDelete(file);
    }

    private static void TryDelete(string path) { try { File.Delete(path); } catch { } }
}
