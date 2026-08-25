using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using MewSwitchManager.Models;

namespace MewSwitchManager.Infrastructure;

public sealed class UpdateService
{
    private const string Repository = "GatiZapo/MewSwitchManager";
    private static readonly Uri ReleasesUri = new($"https://api.github.com/repos/{Repository}/releases/latest");
    private readonly HttpClient _http;
    private readonly AppLogger _logger;

    public UpdateService(AppLogger logger)
    {
        _logger = logger;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MewSwitchManager", GetCurrentVersion()));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public string GetCurrentVersion()
    {
        var info = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(info) ? "0.0.0" : info.Split('+')[0];
    }

    public async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        var current = GetCurrentVersion();

        try
        {
            using var response = await _http.GetAsync(ReleasesUri, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.Info("GitHub update check: no public release exists.");
                return UpdateInfo.NoUpdate(current);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                var remaining = response.Headers.TryGetValues("X-RateLimit-Remaining", out var values)
                    ? values.FirstOrDefault()
                    : null;
                var detail = string.Equals(remaining, "0", StringComparison.Ordinal)
                    ? "GitHub API rate limit reached."
                    : "GitHub denied the update request.";
                _logger.Warn($"Update check: {detail}");
                return UpdateInfo.Error(current, detail);
            }

            if (!response.IsSuccessStatusCode)
            {
                var detail = $"GitHub returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";
                _logger.Warn($"Update check: {detail}");
                return UpdateInfo.Error(current, detail);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return UpdateInfo.Error(current, "GitHub returned an invalid release response.");

            var tag = GetString(root, "tag_name");
            if (string.IsNullOrWhiteSpace(tag))
                return UpdateInfo.Error(current, "GitHub returned a release without a tag.");

            var latest = tag.TrimStart('v', 'V');
            var url = GetString(root, "html_url");
            var name = GetString(root, "name");
            var body = GetString(root, "body");

            string? assetUrl = null;
            string? assetName = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                var preferred = GetPreferredArchitecture();
                foreach (var asset in assets.EnumerateArray())
                {
                    var n = GetString(asset, "name");
                    var u = GetString(asset, "browser_download_url");
                    if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(u) ||
                        !n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (n.Contains(preferred, StringComparison.OrdinalIgnoreCase))
                    {
                        assetName = n;
                        assetUrl = u;
                        break;
                    }
                }
            }

            var available = IsNewer(latest, current);
            _logger.Info(available
                ? $"GitHub update available: {current} -> {latest} ({GetPreferredArchitecture()})."
                : $"GitHub update check complete: {current} is current against {latest}.");

            return new UpdateInfo(available, current, latest, tag, url, name, body, assetUrl, assetName);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            var message = "Unable to reach GitHub. Check your internet connection.";
            _logger.Warn($"Update check failed: {ex.Message}");
            return UpdateInfo.Error(current, message);
        }
        catch (TaskCanceledException)
        {
            var message = "GitHub update check timed out.";
            _logger.Warn(message);
            return UpdateInfo.Error(current, message);
        }
        catch (JsonException ex)
        {
            var message = "GitHub returned invalid release data.";
            _logger.Warn($"Update check failed: {ex.Message}");
            return UpdateInfo.Error(current, message);
        }
        catch (Exception ex)
        {
            _logger.Error($"Update check failed: {ex.Message}");
            return UpdateInfo.Error(current, "The update check failed unexpectedly. See the operation log for details.");
        }
    }

    public async Task<bool> DownloadAndInstallAsync(UpdateInfo update, CancellationToken ct = default)
    {
        if (!update.IsAvailable || string.IsNullOrWhiteSpace(update.AssetUrl)) return false;

        var tempRoot = Path.Combine(Path.GetTempPath(), "MewSwitchManager-update", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var zipPath = Path.Combine(tempRoot, update.AssetName ?? "update.zip");
        var extractPath = Path.Combine(tempRoot, "package");

        try
        {
            _logger.Info($"Downloading MewSwitch Manager {update.LatestVersion}...");
            using (var response = await _http.GetAsync(update.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(ct);
                await using var target = File.Create(zipPath);
                await source.CopyToAsync(target, ct);
            }

            ZipFile.ExtractToDirectory(zipPath, extractPath, true);
            var newExe = Path.Combine(extractPath, "MewSwitch Manager.exe");
            if (!File.Exists(newExe)) throw new InvalidDataException("The update package does not contain MewSwitch Manager.exe.");

            var currentExe = Environment.ProcessPath ?? throw new InvalidOperationException("Current executable path is unavailable.");
            var currentDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var script = Path.Combine(tempRoot, "apply-update.ps1");
            var pid = Environment.ProcessId;
            var scriptText = $@"
$ErrorActionPreference = 'Stop'
$pid = {pid}
while (Get-Process -Id $pid -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 250 }}
$source = '{EscapePowerShell(extractPath)}'
$target = '{EscapePowerShell(currentDir)}'
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force
Start-Process -FilePath '{EscapePowerShell(currentExe)}'
Remove-Item -LiteralPath '{EscapePowerShell(tempRoot)}' -Recurse -Force -ErrorAction SilentlyContinue
";
            await File.WriteAllTextAsync(script, scriptText, ct);

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Update installation failed: {ex.Message}");
            try { Directory.Delete(tempRoot, true); } catch { }
            return false;
        }
    }

    public static string GetPreferredArchitecture() =>
        RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "x64"
        };

    private static bool IsNewer(string latest, string current)
    {
        var l = ParseVersion(latest);
        var c = ParseVersion(current);
        if (l is null || c is null) return false;
        return l.Value.CompareTo(c.Value) > 0;
    }

    private static VersionKey? ParseVersion(string value)
    {
        var clean = value.Trim().TrimStart('v', 'V');
        var parts = clean.Split('-', 2, StringSplitOptions.TrimEntries);
        if (!Version.TryParse(parts[0], out var core)) return null;

        var prerelease = parts.Length == 1 ? Array.Empty<string>() : parts[1].Split('.', StringSplitOptions.RemoveEmptyEntries);
        return new VersionKey(core, prerelease);
    }

    private static string GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string EscapePowerShell(string value) => value.Replace("'", "''");

    private readonly record struct VersionKey(Version Core, string[] Prerelease) : IComparable<VersionKey>
    {
        public int CompareTo(VersionKey other)
        {
            var core = Core.CompareTo(other.Core);
            if (core != 0) return core;

            if (Prerelease.Length == 0 && other.Prerelease.Length == 0) return 0;
            if (Prerelease.Length == 0) return 1;
            if (other.Prerelease.Length == 0) return -1;

            var count = Math.Min(Prerelease.Length, other.Prerelease.Length);
            for (var i = 0; i < count; i++)
            {
                var a = Prerelease[i];
                var b = other.Prerelease[i];
                if (a == b) continue;

                var aNum = int.TryParse(a, out var ai);
                var bNum = int.TryParse(b, out var bi);
                if (aNum && bNum) return ai.CompareTo(bi);
                if (aNum) return -1;
                if (bNum) return 1;
                return string.CompareOrdinal(a, b);
            }

            return Prerelease.Length.CompareTo(other.Prerelease.Length);
        }
    }
}
