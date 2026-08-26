using System.Security.Cryptography;
using SharpCompress.Archives;
using SharpCompress.Common;
using MewSwitchManager.Hardware;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed class SwitchComponentManager
{
    private static readonly ComponentDefinition[] Definitions =
    [
        new(SwitchComponent.Hekate, "Hekate / Nyx", "CTCaer/hekate", "bootloader/update.bin", "Bootloader and Nyx", ".zip"),
        new(SwitchComponent.Atmosphere, "Atmosphère", "Atmosphere-NX/Atmosphere", "atmosphere/package3", "CFW core", ".zip"),
        new(SwitchComponent.Dbi, "DBI", "rashevskyv/dbi", "switch/DBI.nro", "Homebrew installer / file manager", ".zip"),
        new(SwitchComponent.Linux, "Switchroot Linux", "", "bootloader/hekate_ipl.ini", "Linux stack managed by the Linux workflow", "", true),
        new(SwitchComponent.Tools, "Supporting tools", "", "switch", "Additional homebrew and utilities", ".zip")
    ];

    private readonly AppLogger _logger;
    private readonly GitHubReleaseClient _releases;
    private readonly AppPaths _paths;
    private readonly RemovableDriveService _drives = new();
    private readonly string _stateFile;
    private ComponentManagerState _state;

    public SwitchComponentManager(AppPaths paths, AppLogger logger)
    {
        _paths = paths;
        _logger = logger;
        _releases = new GitHubReleaseClient(logger);
        _stateFile = Path.Combine(paths.DataDirectory, "components.json");
        _state = new JsonStore<ComponentManagerState>(_stateFile).LoadOrCreate();
    }

    public IReadOnlyList<ComponentDefinition> Components => Definitions;
    public IReadOnlyList<RemovableDrive> ScanTargets() => _drives.Scan();

    public async Task<IReadOnlyList<ComponentStatus>> ScanAsync(string targetRoot, CancellationToken ct = default)
    {
        if (!Directory.Exists(targetRoot)) throw new DirectoryNotFoundException(targetRoot);
        var result = new List<ComponentStatus>();
        foreach (var definition in Definitions)
        {
            ct.ThrowIfCancellationRequested();
            var installed = File.Exists(Path.Combine(targetRoot, definition.DetectionPath));
            var installedVersion = installed && _state.LastKnownVersions.TryGetValue(definition.Id.ToString(), out var v) ? v : installed ? "Detected" : "Not installed";
            if (string.IsNullOrWhiteSpace(definition.Repository))
            {
                result.Add(new ComponentStatus(definition, installed, installedVersion, "Managed by Linux workflow", null, false, definition.Description));
                continue;
            }

            try
            {
                var release = await _releases.GetLatestAsync(definition.Repository, ct);
                result.Add(new ComponentStatus(definition, installed, installedVersion, release.TagName, release.HtmlUrl, installed && installedVersion != "Detected" && CompareVersions(installedVersion, release.TagName) < 0, release.Name));
            }
            catch (Exception ex)
            {
                _logger.Warn($"Could not query {definition.Name}: {ex.Message}");
                result.Add(new ComponentStatus(definition, installed, installedVersion, "Unavailable", null, false, "Release check failed; existing files were not touched."));
            }
        }

        _state.LastTargetRoot = targetRoot;
        _state.UpdatedAt = DateTimeOffset.UtcNow;
        new JsonStore<ComponentManagerState>(_stateFile).Save(_state);
        return result;
    }

    public async Task<ComponentStatus> InstallOrUpdateAsync(SwitchComponent component, string targetRoot, IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        var definition = Definitions.Single(x => x.Id == component);
        if (component is SwitchComponent.Linux or SwitchComponent.Tools)
            throw new InvalidOperationException($"{definition.Name} is not yet an automatic release component.");
        if (!Directory.Exists(targetRoot)) throw new DirectoryNotFoundException(targetRoot);

        var release = await _releases.GetLatestAsync(definition.Repository, ct);
        var asset = SelectAsset(definition, release);
        if (asset is null) throw new InvalidOperationException($"No suitable release archive was found for {definition.Name} in {release.TagName}.");

        var componentCache = Path.Combine(_paths.CacheDirectory, "components", component.ToString());
        Directory.CreateDirectory(componentCache);
        var archive = Path.Combine(componentCache, SanitizeFileName(asset.Name));
        await _releases.DownloadResumableAsync(asset.Url, archive, progress, ct);
        VerifyArchiveFile(archive);

        var stage = Path.Combine(componentCache, "staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            ExtractSafe(archive, stage);
            var sourceRoot = FindPayloadRoot(stage, definition.DetectionPath);
            BackupBeforeUpdate(targetRoot, definition, release.TagName);
            MergeDirectory(sourceRoot, targetRoot);
            if (!File.Exists(Path.Combine(targetRoot, definition.DetectionPath)))
                throw new InvalidDataException($"The downloaded archive did not produce the expected {definition.DetectionPath}.");

            _state.LastTargetRoot = targetRoot;
            _state.LastKnownVersions[component.ToString()] = release.TagName;
            _state.UpdatedAt = DateTimeOffset.UtcNow;
            new JsonStore<ComponentManagerState>(_stateFile).Save(_state);
            _logger.Info($"{definition.Name} updated to {release.TagName} on {targetRoot}.");
            return new ComponentStatus(definition, true, release.TagName, release.TagName, release.HtmlUrl, false, "Updated successfully; existing configuration was preserved where files overlapped.");
        }
        finally
        {
            TryDeleteDirectory(stage);
        }
    }

    private static GitHubAsset? SelectAsset(ComponentDefinition definition, GitHubRelease release)
    {
        var assets = release.Assets.Where(a => a.Size > 0).ToList();
        if (definition.Id == SwitchComponent.Dbi)
        {
            // Prefer an archive containing DBI rather than a source archive or a language-only package.
            return assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && a.Name.Contains("DBI", StringComparison.OrdinalIgnoreCase))
                ?? assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        }

        return assets.FirstOrDefault(a => a.Name.EndsWith(definition.ArchiveHint, StringComparison.OrdinalIgnoreCase)
                                          && !a.Name.Contains("source", StringComparison.OrdinalIgnoreCase)
                                          && !a.Name.Contains("src", StringComparison.OrdinalIgnoreCase));
    }

    private static int CompareVersions(string installed, string available)
    {
        var a = ExtractNumbers(installed);
        var b = ExtractNumbers(available);
        for (var i = 0; i < Math.Max(a.Count, b.Count); i++)
        {
            var av = i < a.Count ? a[i] : 0;
            var bv = i < b.Count ? b[i] : 0;
            if (av != bv) return av.CompareTo(bv);
        }
        return string.Equals(installed, available, StringComparison.OrdinalIgnoreCase) ? 0 : -1;
    }

    private static List<int> ExtractNumbers(string value) =>
        System.Text.RegularExpressions.Regex.Matches(value, @"\d+")
            .Select(m => int.TryParse(m.Value, out var n) ? n : 0).ToList();

    private static void VerifyArchiveFile(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new InvalidDataException("Component archive is empty or missing.");
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        _ = sha.ComputeHash(stream);
    }

    private static void ExtractSafe(string archivePath, string destination)
    {
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using var archive = ArchiveFactory.Open(archivePath);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            var relative = entry.Key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(destination, relative));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive contains an unsafe path.");
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            entry.WriteToFile(full, new ExtractionOptions { ExtractFullPath = false, Overwrite = true });
        }
    }

    private static string FindPayloadRoot(string stage, string detectionPath)
    {
        if (File.Exists(Path.Combine(stage, detectionPath))) return stage;
        foreach (var directory in Directory.EnumerateDirectories(stage, "*", SearchOption.AllDirectories))
            if (File.Exists(Path.Combine(directory, detectionPath))) return directory;
        throw new InvalidDataException($"Archive does not contain expected component path: {detectionPath}");
    }

    private static void BackupBeforeUpdate(string targetRoot, ComponentDefinition definition, string version)
    {
        var candidates = definition.Id switch
        {
            SwitchComponent.Hekate => new[] { "bootloader" },
            SwitchComponent.Atmosphere => new[] { "atmosphere" },
            SwitchComponent.Dbi => new[] { Path.Combine("switch", "DBI.nro") },
            _ => Array.Empty<string>()
        };
        if (candidates.Length == 0) return;
        var backupRoot = Path.Combine(targetRoot, "_mewswitch-backups", $"{definition.Id}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{SanitizeFileName(version)}");
        foreach (var relative in candidates)
        {
            var source = Path.Combine(targetRoot, relative);
            if (!Directory.Exists(source) && !File.Exists(source)) continue;
            var destination = Path.Combine(backupRoot, relative);
            if (Directory.Exists(source)) CopyDirectory(source, destination);
            else { Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination, true); }
        }
    }

    private static void MergeDirectory(string source, string destination)
    {
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
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
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return string.IsNullOrWhiteSpace(value) ? "component.bin" : value;
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
