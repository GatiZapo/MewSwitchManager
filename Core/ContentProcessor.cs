using System.IO.Compression;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

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
        if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var extract = Path.Combine(workDirectory, "extracted");
            if (Directory.Exists(extract)) Directory.Delete(extract, true);
            Directory.CreateDirectory(extract);
            ZipFile.ExtractToDirectory(input, extract, overwriteFiles: true);
            var payload = ResumableDownloadService.FindPreparedPayload(extract);
            if (payload is null) throw new InvalidDataException("Archive did not contain a supported Switch payload/content file.");
            var info = new FileInfo(payload);
            return await Task.FromResult(new PreparedContent(payload, Path.GetExtension(payload).TrimStart('.').ToUpperInvariant(), info.Length));
        }
        var file = new FileInfo(input);
        return await Task.FromResult(new PreparedContent(input, extension.TrimStart('.').ToUpperInvariant(), file.Length));
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
