using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using MewNX.Models;

namespace MewNX.Infrastructure;

public sealed class UpdateService
{
    private const string Repository = "GatiZapo/MewSwitchManager";
    private static readonly Uri ReleasesUri = new($"https://api.github.com/repos/{Repository}/releases/latest");
    private static readonly Uri MainCommitUri = new($"https://api.github.com/repos/{Repository}/commits/main");
    private static readonly Uri ActionsRunsUri = new($"https://api.github.com/repos/{Repository}/actions/runs?branch=main&status=success&per_page=10");
    private readonly HttpClient _http;
    private readonly AppLogger _logger;

    public UpdateService(AppLogger logger)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MewNX", GetCurrentVersion()));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public string GetCurrentVersion()
    {
        var info = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(info) ? "0.0.0" : info.Split('+')[0];
    }

    private static string? GetCurrentCommit() => Assembly.GetEntryAssembly()?.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(x => string.Equals(x.Key, "GitCommit", StringComparison.OrdinalIgnoreCase))?.Value;

    public async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        var current = GetCurrentVersion();
        var currentCommit = GetCurrentCommit();
        try
        {
            using var releaseResponse = await _http.GetAsync(ReleasesUri, ct);
            if (releaseResponse.IsSuccessStatusCode) return await ParseReleaseAsync(releaseResponse, current, ct);
            if (releaseResponse.StatusCode != HttpStatusCode.NotFound)
            {
                if (releaseResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    var remaining = releaseResponse.Headers.TryGetValues("X-RateLimit-Remaining", out var values) ? values.FirstOrDefault() : null;
                    var detail = string.Equals(remaining, "0", StringComparison.Ordinal) ? "GitHub API rate limit reached." : "GitHub denied the update request.";
                    _logger.Warn($"Update check: {detail}");
                    return UpdateInfo.Error(current, detail);
                }
                var detailHttp = $"GitHub returned HTTP {(int)releaseResponse.StatusCode} ({releaseResponse.ReasonPhrase}).";
                _logger.Warn($"Update check: {detailHttp}");
                return UpdateInfo.Error(current, detailHttp);
            }
            _logger.Info("No public release exists; checking the latest successful main CI build.");
            return await CheckDevelopmentBuildAsync(current, currentCommit, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (HttpRequestException ex) { _logger.Warn($"Update check failed: {ex.Message}"); return UpdateInfo.Error(current, "Unable to reach GitHub. Check your internet connection."); }
        catch (TaskCanceledException) { _logger.Warn("GitHub update check timed out."); return UpdateInfo.Error(current, "GitHub update check timed out."); }
        catch (JsonException ex) { _logger.Warn($"Update check failed: {ex.Message}"); return UpdateInfo.Error(current, "GitHub returned invalid update data."); }
        catch (Exception ex) { _logger.Error($"Update check failed: {ex.Message}"); return UpdateInfo.Error(current, "The update check failed unexpectedly. See the operation log for details."); }
    }

    private async Task<UpdateInfo> ParseReleaseAsync(HttpResponseMessage response, string current, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return UpdateInfo.Error(current, "GitHub returned an invalid release response.");
        var tag = GetString(root, "tag_name");
        if (string.IsNullOrWhiteSpace(tag)) return UpdateInfo.Error(current, "GitHub returned a release without a tag.");
        var latest = tag.TrimStart('v', 'V');
        var url = GetString(root, "html_url");
        var name = GetString(root, "name");
        var body = GetString(root, "body");
        var (assetUrl, assetName) = FindReleaseAsset(root);
        var available = IsNewer(latest, current);
        _logger.Info(available ? $"GitHub release update available: {current} -> {latest}." : $"GitHub release check complete: {current} is current against {latest}.");
        return new UpdateInfo(available, current, latest, tag, url, name, body, assetUrl, assetName);
    }

    private async Task<UpdateInfo> CheckDevelopmentBuildAsync(string current, string? currentCommit, CancellationToken ct)
    {
        using var commitResponse = await _http.GetAsync(MainCommitUri, ct);
        if (!commitResponse.IsSuccessStatusCode) return UpdateInfo.NoUpdate(current);
        await using var commitStream = await commitResponse.Content.ReadAsStreamAsync(ct);
        using var commitDoc = await JsonDocument.ParseAsync(commitStream, cancellationToken: ct);
        var root = commitDoc.RootElement;
        var sha = GetString(root, "sha");
        var url = GetString(root, "html_url");
        var message = root.TryGetProperty("commit", out var commit) ? GetString(commit, "message") : "";
        var shortSha = string.IsNullOrWhiteSpace(sha) ? "" : sha[..Math.Min(7, sha.Length)];
        if (string.IsNullOrWhiteSpace(sha)) return UpdateInfo.Error(current, "GitHub returned no main commit SHA.");
        if (string.IsNullOrWhiteSpace(currentCommit))
        {
            _logger.Info($"Development build detected, but installed build has no Git commit metadata. Main: {shortSha}.");
            return new UpdateInfo(false, current, current, "", url, "Development build", message, null, null, "This installation was built without Git commit metadata. Install a current CI build to enable development update detection.", sha, message, url, true);
        }
        var normalizedCurrent = currentCommit.Trim();
        var available = !string.Equals(normalizedCurrent, sha, StringComparison.OrdinalIgnoreCase);
        string? assetUrl = null; string? assetName = null;
        if (available) (assetUrl, assetName) = await FindLatestCiArtifactAsync(sha, ct);
        _logger.Info(available ? $"Development update available: {normalizedCurrent[..Math.Min(7, normalizedCurrent.Length)]} -> {shortSha}." : $"Development CI check complete: build {shortSha} is current.");
        return new UpdateInfo(available, current, available ? $"{current} (CI {shortSha})" : current, "", url, "Latest successful main CI build", message, assetUrl, assetName, available && string.IsNullOrWhiteSpace(assetUrl) ? "A newer main build exists, but its CI artifact is not currently available." : null, sha, message, url, true);
    }

    private async Task<(string? Url, string? Name)> FindLatestCiArtifactAsync(string targetSha, CancellationToken ct)
    {
        using var runsResponse = await _http.GetAsync(ActionsRunsUri, ct);
        if (!runsResponse.IsSuccessStatusCode) return (null, null);
        await using var runsStream = await runsResponse.Content.ReadAsStreamAsync(ct);
        using var runsDoc = await JsonDocument.ParseAsync(runsStream, cancellationToken: ct);
        var runs = runsDoc.RootElement.TryGetProperty("workflow_runs", out var workflowRuns) ? workflowRuns : default;
        if (runs.ValueKind != JsonValueKind.Array) return (null, null);
        foreach (var run in runs.EnumerateArray())
        {
            var runSha = GetString(run, "head_sha");
            if (!string.Equals(runSha, targetSha, StringComparison.OrdinalIgnoreCase)) continue;
            var runId = run.TryGetProperty("id", out var id) ? id.GetInt64() : 0;
            if (runId <= 0) continue;
            var artifactsUri = new Uri($"https://api.github.com/repos/{Repository}/actions/runs/{runId}/artifacts");
            using var artifactsResponse = await _http.GetAsync(artifactsUri, ct);
            if (!artifactsResponse.IsSuccessStatusCode) continue;
            await using var artifactsStream = await artifactsResponse.Content.ReadAsStreamAsync(ct);
            using var artifactsDoc = await JsonDocument.ParseAsync(artifactsStream, cancellationToken: ct);
            if (!artifactsDoc.RootElement.TryGetProperty("artifacts", out var artifacts) || artifacts.ValueKind != JsonValueKind.Array) continue;
            var preferred = $"MewNX-{GetPreferredArchitecture()}";
            foreach (var artifact in artifacts.EnumerateArray())
            {
                var name = GetString(artifact, "name");
                if (!string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase)) continue;
                var expired = artifact.TryGetProperty("expired", out var expiredValue) && expiredValue.ValueKind == JsonValueKind.True;
                if (expired) continue;
                var archive = GetString(artifact, "archive_download_url");
                if (!string.IsNullOrWhiteSpace(archive)) return (archive, name + ".zip");
            }
        }
        return (null, null);
    }

    public async Task<bool> DownloadAndInstallAsync(UpdateInfo update, CancellationToken ct = default)
    {
        if (!update.IsAvailable || string.IsNullOrWhiteSpace(update.AssetUrl)) return false;
        var tempRoot = Path.Combine(Path.GetTempPath(), "MewNX-update", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var downloadPath = Path.Combine(tempRoot, update.AssetName ?? "update.zip");
        var extractPath = Path.Combine(tempRoot, "package");
        try
        {
            _logger.Info($"Downloading MewNX {update.LatestVersion}...");
            using (var response = await _http.GetAsync(update.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(ct);
                await using var target = File.Create(downloadPath);
                await source.CopyToAsync(target, ct);
            }
            ZipFile.ExtractToDirectory(downloadPath, extractPath, true);
            var packageZip = Directory.EnumerateFiles(extractPath, "*.zip", SearchOption.AllDirectories).FirstOrDefault();
            if (packageZip is not null)
            {
                var inner = Path.Combine(tempRoot, "inner");
                ZipFile.ExtractToDirectory(packageZip, inner, true);
                extractPath = inner;
            }
            var newExe = Directory.EnumerateFiles(extractPath, "MewNX.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (newExe is null) throw new InvalidDataException("The update package does not contain MewNX.exe.");
            var currentExe = Environment.ProcessPath ?? throw new InvalidOperationException("Current executable path is unavailable.");
            var currentDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var script = Path.Combine(tempRoot, "apply-update.ps1");
            var pid = Environment.ProcessId;
            var sourceDir = Path.GetDirectoryName(newExe) ?? extractPath;
            var scriptText = $@"
$ErrorActionPreference = 'Stop'
$pid = {pid}
while (Get-Process -Id $pid -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 250 }}
$source = '{EscapePowerShell(sourceDir)}'
$target = '{EscapePowerShell(currentDir)}'
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force
Start-Process -FilePath '{EscapePowerShell(currentExe)}'
Remove-Item -LiteralPath '{EscapePowerShell(tempRoot)}' -Recurse -Force -ErrorAction SilentlyContinue
";
            await File.WriteAllTextAsync(script, scriptText, ct);
            Process.Start(new ProcessStartInfo { FileName = "powershell.exe", Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
            return true;
        }
        catch (Exception ex) { _logger.Error($"Update installation failed: {ex.Message}"); try { Directory.Delete(tempRoot, true); } catch { } return false; }
    }

    private static (string? Url, string? Name) FindReleaseAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return (null, null);
        var preferred = GetPreferredArchitecture();
        foreach (var asset in assets.EnumerateArray())
        {
            var name = GetString(asset, "name");
            var url = GetString(asset, "browser_download_url");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url) || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains(preferred, StringComparison.OrdinalIgnoreCase)) return (url, name);
        }
        return (null, null);
    }

    public static string GetPreferredArchitecture() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.Arm64 => "arm64",
        Architecture.X86 => "x86",
        _ => "x64"
    };

    private static bool IsNewer(string latest, string current)
    {
        var l = ParseVersion(latest); var c = ParseVersion(current);
        return l is not null && c is not null && l.Value.CompareTo(c.Value) > 0;
    }

    private static VersionKey? ParseVersion(string value)
    {
        var clean = value.Trim().TrimStart('v', 'V');
        var parts = clean.Split('-', 2, StringSplitOptions.TrimEntries);
        if (!Version.TryParse(parts[0], out var core)) return null;
        var prerelease = parts.Length == 1 ? Array.Empty<string>() : parts[1].Split('.', StringSplitOptions.RemoveEmptyEntries);
        return new VersionKey(core, prerelease);
    }

    private static string GetString(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    private static string EscapePowerShell(string value) => value.Replace("'", "''");

    private readonly record struct VersionKey(Version Core, string[] Prerelease) : IComparable<VersionKey>
    {
        public int CompareTo(VersionKey other)
        {
            var core = Core.CompareTo(other.Core); if (core != 0) return core;
            if (Prerelease.Length == 0 && other.Prerelease.Length == 0) return 0;
            if (Prerelease.Length == 0) return 1; if (other.Prerelease.Length == 0) return -1;
            var count = Math.Min(Prerelease.Length, other.Prerelease.Length);
            for (var i = 0; i < count; i++)
            {
                var a = Prerelease[i]; var b = other.Prerelease[i]; if (a == b) continue;
                var aNum = int.TryParse(a, out var ai); var bNum = int.TryParse(b, out var bi);
                if (aNum && bNum) return ai.CompareTo(bi); if (aNum) return -1; if (bNum) return 1;
                return string.CompareOrdinal(a, b);
            }
            return Prerelease.Length.CompareTo(other.Prerelease.Length);
        }
    }
}
