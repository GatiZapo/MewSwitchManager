using System.Net.Http.Headers;
using System.Text.Json;
using MewSwitchManager.Models;

namespace MewSwitchManager.Infrastructure;

public sealed class GitHubUpdateService
{
    private readonly HttpClient _http;
    private readonly AppLogger _logger;
    private readonly AppConfig _config;

    public GitHubUpdateService(HttpClient http, AppLogger logger, AppConfig config)
    {
        _http = http;
        _logger = logger;
        _config = config;
        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MewSwitchManager", _config.AppVersion));
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        var current = NormalizeVersion(_config.AppVersion);
        if (!_config.Updates.Enabled || string.IsNullOrWhiteSpace(_config.Updates.Repository))
            return new UpdateInfo(false, current, current, null, null, null);

        try
        {
            var url = $"https://api.github.com/repos/{_config.Updates.Repository.Trim()}/releases/latest";
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                var message = $"GitHub update check returned {(int)response.StatusCode} ({response.ReasonPhrase}).";
                _logger.Warn(message);
                return new UpdateInfo(false, current, current, null, null, null, message);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            var releaseUrl = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;
            DateTimeOffset? published = null;
            if (root.TryGetProperty("published_at", out var publishedElement) && publishedElement.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(publishedElement.GetString(), out var parsed))
                published = parsed;

            var latest = NormalizeVersion(tag ?? name ?? current);
            var available = CompareVersions(latest, current) > 0;
            if (available)
                _logger.Info($"Update available: {current} -> {latest}.");
            else
                _logger.Info($"MewSwitch Manager is up to date ({current}).");

            return new UpdateInfo(available, current, latest, releaseUrl, name, published);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warn($"GitHub update check failed: {ex.Message}");
            return new UpdateInfo(false, current, current, null, null, null, ex.Message);
        }
    }

    private static string NormalizeVersion(string value)
    {
        value = (value ?? "0.0.0").Trim();
        if (value.StartsWith('v')) value = value[1..];
        var dash = value.IndexOf('-');
        return dash >= 0 ? value[..dash] : value;
    }

    private static int CompareVersions(string left, string right)
    {
        if (Version.TryParse(left, out var l) && Version.TryParse(right, out var r))
            return l.CompareTo(r);
        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
