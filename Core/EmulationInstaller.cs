using System.Security.Cryptography;
using SharpCompress.Archives;
using SharpCompress.Common;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed class EmulationInstaller
{
    private const string RetroArchBundleUrl = "https://buildbot.libretro.com/stable/1.22.2/nintendo/switch/libnx/RetroArch.7z";
    private readonly AppPaths _paths;
    private readonly AppLogger _logger;
    private readonly GitHubReleaseClient _releases;

    public EmulationInstaller(AppPaths paths, AppLogger logger) { _paths = paths; _logger = logger; _releases = new GitHubReleaseClient(logger); }

    public static long RecommendedFreeBytes => 4L * 1024 * 1024 * 1024;

    public async Task<IReadOnlyList<EmulationInstallResult>> InstallFullStackAsync(string targetRoot, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        ValidateTarget(targetRoot);
        EnsureFreeSpace(targetRoot);

        var results = new List<EmulationInstallResult>();
        var failures = new List<Exception>();

        foreach (var definition in EmulatorCatalog.FullStack)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                results.Add(await InstallOrUpdateAsync(definition, targetRoot, progress, ct));
            }
            catch (Exception ex)
            {
                failures.Add(new InvalidOperationException($"{definition.Name}: {ex.Message}", ex));
                _logger.Error($"Full emulation stack component failed: {definition.Name}", ex);
                break;
            }
        }

        if (failures.Count > 0)
        {
            for (var i = results.Count - 1; i >= 0; i--)
            {
                try { RestoreBackup(targetRoot, results[i].BackupPath, results[i].Definition); }
                catch (Exception ex) { _logger.Error($"Full emulation rollback failed for {results[i].Definition.Name}", ex); }
            }

            throw new AggregateException("The complete emulation stack could not be installed. No partial emulation installation was left on the SD card.", failures);
        }

        CreateTicoLibraryDirectories(targetRoot);
        return results;
    }

    public async Task<EmulationInstallResult> InstallOrUpdateAsync(EmulationPackageDefinition definition, string targetRoot, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        ValidateTarget(targetRoot);
        var cache = Path.Combine(_paths.CacheDirectory, "emulation", definition.Id);
        Directory.CreateDirectory(cache);
        var (version, url, digest, assetName) = await ResolveSourceAsync(definition, ct);
        var downloadPath = Path.Combine(cache, Sanitize(assetName));
        await _releases.DownloadResumableAsync(url, downloadPath, progress, ct);

        // Keep the completed payload in .part until its digest/content has been
        // verified. This preserves resumability and prevents a bad download
        // from replacing a previously valid cached component.
        var partPath = downloadPath + ".part";
        await VerifyDownloadedFileAsync(partPath, digest, ct);
        _releases.PromotePart(downloadPath);

        var backup = BackupDestination(targetRoot, definition);
        try
        {
            if (definition.InstallMode == EmulationInstallMode.DirectFile)
            {
                var destination = Path.Combine(targetRoot, definition.Destination);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(downloadPath, destination, true);
                if (!File.Exists(destination)) throw new IOException($"{definition.Name} was not written to {definition.Destination}.");
            }
            else
            {
                var stage = Path.Combine(cache, "stage", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stage);
                try
                {
                    ExtractSafe(downloadPath, stage);
                    ValidateRetroArchBundle(stage);
                    MergePreservingUserData(stage, targetRoot, definition.PreserveRelativePaths ?? Array.Empty<string>());
                    if (!File.Exists(Path.Combine(targetRoot, "switch", "retroarch.nro")) || !Directory.Exists(Path.Combine(targetRoot, "retroarch", "cores")))
                        throw new InvalidDataException("RetroArch bundle is incomplete: switch/retroarch.nro or retroarch/cores is missing.");
                }
                finally { TryDeleteDirectory(stage); }
            }
        }
        catch
        {
            try { RestoreBackup(targetRoot, backup, definition); }
            catch (Exception ex) { _logger.Error($"Rollback failed for {definition.Name}", ex); }
            throw;
        }

        return new EmulationInstallResult(definition, version, backup, $"{definition.Name} installed/updated to {version}.");
    }

    private async Task<(string Version, string Url, string? Digest, string AssetName)> ResolveSourceAsync(EmulationPackageDefinition definition, CancellationToken ct)
    {
        if (definition.SourceKind == EmulationSourceKind.OfficialBundle) return ("1.22.2", RetroArchBundleUrl, null, definition.AssetName);
        var release = await _releases.GetLatestAsync(definition.Repository, ct);
        var asset = release.Assets.FirstOrDefault(a => string.Equals(a.Name, definition.AssetName, StringComparison.OrdinalIgnoreCase));
        if (asset is null) throw new InvalidOperationException($"No {definition.AssetName} asset was found in {definition.Repository} release {release.TagName}.");
        return (release.TagName, asset.Url, asset.Digest, asset.Name);
    }

    private static void ValidateTarget(string targetRoot)
    {
        if (!Directory.Exists(targetRoot)) throw new DirectoryNotFoundException(targetRoot);
    }

    private static void EnsureFreeSpace(string targetRoot)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(targetRoot));
        if (string.IsNullOrWhiteSpace(root)) return;
        var drive = new DriveInfo(root);
        if (drive.AvailableFreeSpace < RecommendedFreeBytes)
            throw new IOException($"The Switch SD card needs at least 4 GB of free space for the full emulation stack. Available: {drive.AvailableFreeSpace / 1024d / 1024d / 1024d:F1} GB.");
    }

    private static void CreateTicoLibraryDirectories(string targetRoot)
    {
        var names = new[] { "nes", "snes", "n64", "gc", "wii", "gb", "gbc", "gba", "3ds", "master-system", "game-gear", "genesis", "sega-cd", "saturn", "dc", "naomi", "atomiswave", "fbneo", "psx", "psp" };
        foreach (var name in names) Directory.CreateDirectory(Path.Combine(targetRoot, "tico", "roms", name));
    }

    private static string BackupDestination(string targetRoot, EmulationPackageDefinition definition)
    {
        var backupRoot = Path.Combine(targetRoot, "_mewswitch-backups", "emulation", $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Sanitize(definition.Id)}");
        var copied = false;

        if (definition.InstallMode == EmulationInstallMode.DirectFile)
        {
            var source = Path.Combine(targetRoot, definition.Destination);
            if (File.Exists(source))
            {
                var destination = Path.Combine(backupRoot, definition.Destination);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, true);
                copied = true;
            }
        }
        else
        {
            var retroArchRoot = Path.Combine(targetRoot, "retroarch");
            if (Directory.Exists(retroArchRoot)) { CopyDirectory(retroArchRoot, Path.Combine(backupRoot, "retroarch")); copied = true; }
            var nro = Path.Combine(targetRoot, "switch", "retroarch.nro");
            if (File.Exists(nro))
            {
                var destination = Path.Combine(backupRoot, "switch", "retroarch.nro");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(nro, destination, true);
                copied = true;
            }
        }

        return copied ? backupRoot : "";
    }

    private static void RestoreBackup(string targetRoot, string backupRoot, EmulationPackageDefinition definition)
    {
        if (definition.InstallMode == EmulationInstallMode.ArchiveToRoot)
        {
            var targetRetro = Path.Combine(targetRoot, "retroarch");
            var backupRetro = string.IsNullOrWhiteSpace(backupRoot) ? "" : Path.Combine(backupRoot, "retroarch");
            if (Directory.Exists(targetRetro)) Directory.Delete(targetRetro, true);
            if (Directory.Exists(backupRetro)) CopyDirectory(backupRetro, targetRetro);

            var targetNro = Path.Combine(targetRoot, "switch", "retroarch.nro");
            var backupNro = string.IsNullOrWhiteSpace(backupRoot) ? "" : Path.Combine(backupRoot, "switch", "retroarch.nro");
            if (File.Exists(targetNro)) File.Delete(targetNro);
            if (File.Exists(backupNro))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetNro)!);
                File.Copy(backupNro, targetNro, true);
            }
            return;
        }

        var destination = Path.Combine(targetRoot, definition.Destination);
        if (string.IsNullOrWhiteSpace(backupRoot) || !Directory.Exists(backupRoot))
        {
            if (File.Exists(destination)) File.Delete(destination);
            return;
        }

        MergeDirectory(backupRoot, targetRoot);
    }

    private static void ExtractSafe(string archivePath, string destination)
    {
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using var archive = ArchiveFactory.OpenArchive(archivePath);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            var relative = entry.Key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(destination, relative));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive contains an unsafe path.");
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            entry.WriteToFile(full, new ExtractionOptions { ExtractFullPath = false, Overwrite = true });
        }
    }

    private static void ValidateRetroArchBundle(string stage)
    {
        var nro = Directory.GetFiles(stage, "retroarch.nro", SearchOption.AllDirectories).FirstOrDefault();
        var cores = Directory.GetDirectories(stage, "cores", SearchOption.AllDirectories).FirstOrDefault();
        if (nro is null || cores is null) throw new InvalidDataException("The downloaded RetroArch archive is not a valid Switch bundle.");
    }

    private static void MergePreservingUserData(string source, string destination, IReadOnlyList<string> preserved)
    {
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            if (!IsPreserved(relative, preserved)) Directory.CreateDirectory(Path.Combine(destination, relative));
        }
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (IsPreserved(relative, preserved)) continue;
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static bool IsPreserved(string relative, IReadOnlyList<string> preserved)
    {
        var normalized = relative.Replace('\\', '/');
        return preserved.Any(p => normalized.Equals(p.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) || normalized.StartsWith(p.Replace('\\', '/') + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static void MergeDirectory(string source, string destination)
    {
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
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
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static async Task VerifyDownloadedFileAsync(string path, string? digest, CancellationToken ct)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new InvalidDataException("Downloaded emulation package is empty or missing.");
        if (string.IsNullOrWhiteSpace(digest)) return;
        if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Unsupported digest format: {digest}");
        await using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var actual = Convert.ToHexString(await sha.ComputeHashAsync(stream, ct));
        var expected = digest[7..].Trim().ToUpperInvariant();
        if (!CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(actual), System.Text.Encoding.ASCII.GetBytes(expected)))
            throw new InvalidDataException($"SHA-256 verification failed for emulation package. Expected {expected}, got {actual}.");
    }

    private static string Sanitize(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value.Replace('/', '_').Replace('\\', '_');
    }

    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
}
