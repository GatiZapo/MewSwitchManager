using System.Security.Cryptography;
using SharpCompress.Archives;
using SharpCompress.Common;
using MewNX.Infrastructure;
using MewNX.Models;

namespace MewNX.Core;

public sealed record SwitchToolInstallResult(SwitchToolDefinition Definition, string Version, string BackupPath, string Message);

public sealed class SwitchToolInstaller
{
    private readonly AppPaths _paths;
    private readonly AppLogger _logger;
    private readonly GitHubReleaseClient _releases;

    public SwitchToolInstaller(AppPaths paths, AppLogger logger)
    {
        _paths = paths;
        _logger = logger;
        _releases = new GitHubReleaseClient(logger);
    }

    public async Task<SwitchToolInstallResult> InstallOrUpdateAsync(SwitchToolDefinition definition, string targetRoot, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(targetRoot)) throw new DirectoryNotFoundException(targetRoot);
        var release = await _releases.GetLatestAsync(definition.Repository, ct);
        var asset = SelectAsset(definition, release);
        if (asset is null) throw new InvalidOperationException($"No compatible asset was found for {definition.Name} in {release.TagName}.");

        var cache = Path.Combine(_paths.CacheDirectory, "tools", definition.Id);
        Directory.CreateDirectory(cache);
        var file = Path.Combine(cache, Sanitize(asset.Name));
        await _releases.DownloadResumableAsync(asset.Url, file, progress, ct);
        var part = file + ".part";
        await VerifyAsync(part, asset.Digest, ct);
        _releases.PromotePart(file);

        var backup = BackupDestination(targetRoot, definition.Destination);
        try
        {
            if (IsArchive(file))
            {
                var stage = Path.Combine(cache, "stage", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stage);
                try
                {
                    ExtractSafe(file, stage);
                    var sourceRoot = FindRoot(stage, definition.Destination);
                    Merge(sourceRoot, targetRoot);
                }
                finally { TryDelete(stage); }
            }
            else
            {
                var destination = Path.Combine(targetRoot, definition.Destination);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, true);
            }
            if (!File.Exists(Path.Combine(targetRoot, definition.Destination)))
                throw new InvalidDataException($"{definition.Name} did not produce the expected file: {definition.Destination}");
        }
        catch
        {
            try { Restore(targetRoot, backup, definition.Destination); }
            catch (Exception ex) { _logger.Error($"Rollback failed for {definition.Name}", ex); }
            throw;
        }

        return new SwitchToolInstallResult(definition, release.TagName, backup, $"{definition.Name} updated to {release.TagName}.");
    }

    public async Task<IReadOnlyList<SwitchToolInstallResult>> InstallAllSafeAsync(string targetRoot, IEnumerable<SwitchToolDefinition> definitions, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        var results = new List<SwitchToolInstallResult>();
        foreach (var definition in definitions)
        {
            ct.ThrowIfCancellationRequested();
            try { results.Add(await InstallOrUpdateAsync(definition, targetRoot, progress, ct)); }
            catch (Exception ex) { _logger.Warn($"Skipping {definition.Name}: {ex.Message}"); }
        }
        return results;
    }

    private static GitHubAsset? SelectAsset(SwitchToolDefinition definition, GitHubRelease release)
    {
        var pattern = definition.AssetPattern.Replace("*", "", StringComparison.Ordinal);
        var candidates = release.Assets.Where(a => a.Size > 0 && !a.Name.Contains("source", StringComparison.OrdinalIgnoreCase) && !a.Name.Contains("src", StringComparison.OrdinalIgnoreCase));
        if (definition.AssetPattern.EndsWith(".nro", StringComparison.OrdinalIgnoreCase))
            return candidates.FirstOrDefault(a => a.Name.EndsWith(".nro", StringComparison.OrdinalIgnoreCase));
        if (definition.AssetPattern.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
            return candidates.FirstOrDefault(a => a.Name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase));
        return candidates.FirstOrDefault(a => a.Name.EndsWith(pattern, StringComparison.OrdinalIgnoreCase) || a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task VerifyAsync(string path, string? digest, CancellationToken ct)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new InvalidDataException("Downloaded tool is empty or missing.");
        if (string.IsNullOrWhiteSpace(digest)) return;
        if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Unsupported digest: {digest}");
        await using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var actual = Convert.ToHexString(await sha.ComputeHashAsync(stream, ct));
        var expected = digest[7..].Trim().ToUpperInvariant();
        if (!CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(actual), System.Text.Encoding.ASCII.GetBytes(expected)))
            throw new InvalidDataException("SHA-256 verification failed.");
    }

    private static bool IsArchive(string path) => Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".7z", StringComparison.OrdinalIgnoreCase);

    private static void ExtractSafe(string archivePath, string destination)
    {
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            var key = entry.Key;
            if (string.IsNullOrWhiteSpace(key)) throw new InvalidDataException("Archive contains an entry without a path.");
            var relative = key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(destination, relative));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive contains an unsafe path.");
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            entry.WriteToFile(full, new ExtractionOptions { ExtractFullPath = false, Overwrite = true });
        }
    }

    private static string FindRoot(string stage, string destination)
    {
        if (File.Exists(Path.Combine(stage, destination))) return stage;
        foreach (var directory in Directory.EnumerateDirectories(stage, "*", SearchOption.AllDirectories))
            if (File.Exists(Path.Combine(directory, destination))) return directory;
        var fileName = Path.GetFileName(destination);
        var matches = Directory.EnumerateFiles(stage, fileName, SearchOption.AllDirectories).ToArray();
        if (matches.Length == 1) return Path.GetDirectoryName(matches[0])!;
        throw new InvalidDataException($"Archive does not contain expected tool path: {destination}");
    }

    private static string BackupDestination(string targetRoot, string relative)
    {
        var source = Path.Combine(targetRoot, relative);
        if (!File.Exists(source) && !Directory.Exists(source)) return "";
        var backup = Path.Combine(targetRoot, "_mewswitch-backups", "tools", $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Sanitize(relative)}");
        if (File.Exists(source)) { Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(backup, relative))!); File.Copy(source, Path.Combine(backup, relative), true); }
        else CopyDirectory(source, Path.Combine(backup, relative));
        return backup;
    }

    private static void Restore(string targetRoot, string backup, string relative)
    {
        var destination = Path.Combine(targetRoot, relative);
        if (string.IsNullOrWhiteSpace(backup) || !Directory.Exists(backup))
        {
            if (File.Exists(destination)) File.Delete(destination);
            else if (Directory.Exists(destination)) Directory.Delete(destination, true);
            return;
        }
        if (File.Exists(destination)) File.Delete(destination);
        else if (Directory.Exists(destination)) Directory.Delete(destination, true);
        var backupPath = Path.Combine(backup, relative);
        if (File.Exists(backupPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(backupPath, destination, true);
        }
        else if (Directory.Exists(backupPath)) CopyDirectory(backupPath, destination);
    }

    private static void Merge(string source, string destination)
    {
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static string Sanitize(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
    }

    private static void TryDelete(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
}
