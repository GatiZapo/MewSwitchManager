using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
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
        _http = new HttpClient();
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
        try
        {
            using var response = await _http.GetAsync(ReleasesUri, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return UpdateInfo.NoUpdate(GetCurrentVersion());

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
            var latest = tag.TrimStart('v');
            var current = GetCurrentVersion();
            var url = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "";
            var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? tag : tag;
            var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";

            string? assetUrl = null;
            string? assetName = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                var preferred = GetPreferredArchitecture();
                foreach (var asset in assets.EnumerateArray())
                {
                    var n = asset.TryGetProperty("name", out var nProp) ? nProp.GetString() : null;
                    var u = asset.TryGetProperty("browser_download_url", out var uProp) ? uProp.GetString() : null;
                    if (n is null || u is null || !n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                    if (n.Contains(preferred, StringComparison.OrdinalIgnoreCase))
                    {
                        assetName = n;
                        assetUrl = u;
                        break;
                    }
                }
            }

            var available = IsNewer(latest, current);
            return new UpdateInfo(available, current, latest, tag, url, name, body, assetUrl, assetName);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Update check failed: {ex.Message}");
            return UpdateInfo.NoUpdate(GetCurrentVersion());
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
            var scriptText = $"""
$ErrorActionPreference = 'Stop'
$pid = {pid}
while (Get-Process -Id $pid -ErrorAction SilentlyContinue) {{ Start-Sleep -Milliseconds 250 }}
$source = '{EscapePowerShell(extractPath)}'
$target = '{EscapePowerShell(currentDir)}'
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force
Start-Process -FilePath '{EscapePowerShell(currentExe)}'
Remove-Item -LiteralPath '{EscapePowerShell(tempRoot)}' -Recurse -Force -ErrorAction SilentlyContinue
""";
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
            Architecture.Arm64 => "ARM64",
            Architecture.X86 => "x86",
            _ => "x64"
        };

    private static bool IsNewer(string latest, string current)
    {
        if (!Version.TryParse(NormalizeVersion(latest), out var l)) return false;
        if (!Version.TryParse(NormalizeVersion(current), out var c)) return false;
        return l > c;
    }

    private static string NormalizeVersion(string value)
    {
        var clean = value.Trim().TrimStart('v');
        var dash = clean.IndexOf('-');
        if (dash >= 0) clean = clean[..dash];
        return clean;
    }

    private static string EscapePowerShell(string value) => value.Replace("'", "''");
}
