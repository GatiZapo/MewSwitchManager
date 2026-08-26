using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MewSwitchManager.Models;

namespace MewSwitchManager.Infrastructure;

public sealed class GitHubReleaseClient
{
    private readonly HttpClient _http;
    private readonly AppLogger _logger;

    public GitHubReleaseClient(AppLogger logger)
    {
        _logger = logger;
        _http = new HttpClient { BaseAddress = new Uri("https://api.github.com/") };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MewSwitchManager/0.4");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<GitHubRelease> GetLatestAsync(string repository, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"repos/{repository}/releases/latest", ct);
        response.EnsureSuccessStatusCode();
        return await ParseReleaseAsync(response, ct);
    }

    public async Task<GitHubRelease> GetTagAsync(string repository, string tag, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"repos/{repository}/releases/tags/{Uri.EscapeDataString(tag)}", ct);
        response.EnsureSuccessStatusCode();
        return await ParseReleaseAsync(response, ct);
    }

    private static async Task<GitHubRelease> ParseReleaseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = document.RootElement;
        var assets = root.GetProperty("assets").EnumerateArray().Select(x => new GitHubAsset(
            x.GetProperty("name").GetString() ?? "",
            x.GetProperty("browser_download_url").GetString() ?? "",
            x.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
            x.TryGetProperty("digest", out var digest) ? digest.GetString() : null)).ToArray();
        return new GitHubRelease(
            root.GetProperty("tag_name").GetString() ?? "",
            root.GetProperty("name").GetString() ?? "",
            root.GetProperty("html_url").GetString() ?? "",
            assets);
    }

    public async Task DownloadResumableAsync(string url, string destination, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var part = destination + ".part";
        long existing = File.Exists(part) ? new FileInfo(part).Length : 0;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existing > 0) request.Headers.Range = new RangeHeaderValue(existing, null);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (existing > 0 && response.StatusCode == HttpStatusCode.OK)
        {
            File.Delete(part);
            existing = 0;
            using var retry = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            retry.EnsureSuccessStatusCode();
            await CopyToPartAsync(retry, part, 0, progress, ct);
        }
        else
        {
            response.EnsureSuccessStatusCode();
            var append = existing > 0 && response.StatusCode == HttpStatusCode.PartialContent;
            if (!append) existing = 0;
            await CopyToPartAsync(response, part, existing, progress, ct);
        }
        File.Move(part, destination, true);
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
            var elapsed = Math.Max((DateTime.UtcNow - started).TotalSeconds, 0.001);
            progress?.Report(new DownloadProgress(received, total, received / elapsed));
        }
    }
}
