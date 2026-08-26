using System.Net.Http.Headers;
using System.Text.Json;
using MewSwitchManager.Models;

namespace MewSwitchManager.Infrastructure;

public sealed record GitHubAsset(string Name, string Url, long Size, string? Digest);
public sealed record GitHubRelease(string TagName, string Name, string HtmlUrl, bool Prerelease, IReadOnlyList<GitHubAsset> Assets);

public sealed class GitHubReleaseClient
{
    private readonly HttpClient _http;

    public GitHubReleaseClient(AppLogger logger)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MewSwitchManager", "0.4"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public Task<GitHubRelease> GetLatestAsync(string repository, CancellationToken ct = default)
        => GetAsync($"https://api.github.com/repos/{repository}/releases/latest", ct);

    public Task<GitHubRelease> GetTagAsync(string repository, string tag, CancellationToken ct = default)
        => GetAsync($"https://api.github.com/repos/{repository}/releases/tags/{Uri.EscapeDataString(tag)}", ct);

    private async Task<GitHubRelease> GetAsync(string uri, CancellationToken ct)
    {
        using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = json.RootElement;
        var assets = root.TryGetProperty("assets", out var rawAssets)
            ? rawAssets.EnumerateArray()
                .Select(a => new GitHubAsset(
                    a.GetProperty("name").GetString() ?? "",
                    a.GetProperty("browser_download_url").GetString() ?? "",
                    a.GetProperty("size").GetInt64(),
                    a.TryGetProperty("digest", out var d) ? d.GetString() : null))
                .Where(a => !string.IsNullOrWhiteSpace(a.Url))
                .ToArray()
            : [];
        return new GitHubRelease(
            root.GetProperty("tag_name").GetString() ?? "",
            root.GetProperty("name").GetString() ?? "",
            root.GetProperty("html_url").GetString() ?? "",
            root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean(),
            assets);
    }

    public async Task DownloadResumableAsync(string url, string destination, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var part = destination + ".part";
        var existing = File.Exists(part) ? new FileInfo(part).Length : 0L;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existing > 0) request.Headers.Range = new RangeHeaderValue(existing, null);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (existing > 0 && response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            response.Dispose();
            File.Delete(part);
            existing = 0;
            using var retry = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            retry.EnsureSuccessStatusCode();
            await CopyToPartAsync(retry, part, 0, progress, ct);
        }
        else
        {
            response.EnsureSuccessStatusCode();
            var append = existing > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent;
            if (!append) existing = 0;
            await CopyToPartAsync(response, part, existing, progress, ct);
        }

        // Deliberately keep the .part file in place. The caller verifies the
        // complete payload before promoting it, so a bad download never
        // replaces a previously valid cached asset and interrupted downloads
        // remain resumable.
    }

    public void PromotePart(string destination)
    {
        var part = destination + ".part";
        if (!File.Exists(part)) throw new FileNotFoundException("Completed download part is missing.", part);

        if (!File.Exists(destination))
        {
            File.Move(part, destination);
            return;
        }

        try
        {
            File.Replace(part, destination, null, ignoreMetadataErrors: true);
        }
        catch (PlatformNotSupportedException)
        {
            File.Move(part, destination, overwrite: true);
        }
    }

    private static async Task CopyToPartAsync(HttpResponseMessage response, string part, long existing, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        var length = response.Content.Headers.ContentLength;
        long? total = length.HasValue ? existing + length.Value : null;
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(part, existing > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024, useAsync: true);
        var buffer = new byte[1024 * 1024];
        long received = existing;
        var started = DateTime.UtcNow;
        while (true)
        {
            var read = await input.ReadAsync(buffer, ct);
            if (read == 0) break;
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;
            var elapsed = Math.Max(0.001, (DateTime.UtcNow - started).TotalSeconds);
            progress?.Report(new DownloadProgress(received, total, received / elapsed, null, "DOWNLOADING COMPONENT", null));
        }
        await output.FlushAsync(ct);
    }
}
