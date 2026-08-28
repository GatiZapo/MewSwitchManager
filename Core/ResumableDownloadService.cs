using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using MewNX.Infrastructure;
using MewNX.Models;

namespace MewSwitchManager.Core;

public sealed class ResumableDownloadService
{
    private readonly HttpClient _http;
    private readonly AppLogger _logger;

    public ResumableDownloadService(HttpClient http, AppLogger logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> DownloadAsync(DownloadJob job, IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ct.ThrowIfCancellationRequested();

        if (job.SourceKind != DownloadSourceKind.DirectUrl)
            throw new NotSupportedException($"Download source kind '{job.SourceKind}' is not supported by the HTTP download service.");

        if (!Uri.TryCreate(job.Source, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("Download source must be an absolute HTTP(S) URL.", nameof(job));

        if (string.IsNullOrWhiteSpace(job.Id))
            throw new ArgumentException("Download job ID is required.", nameof(job));

        if (!string.IsNullOrWhiteSpace(job.ExpectedSha256) && !IsSha256(job.ExpectedSha256))
            throw new ArgumentException("ExpectedSha256 must contain exactly 64 hexadecimal characters.", nameof(job));

        Directory.CreateDirectory(job.WorkingDirectory);
        var fileName = Path.GetFileName(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(fileName) || fileName is "." or "..")
            fileName = job.Id + ".download";

        var part = Path.Combine(job.WorkingDirectory, fileName + ".part");
        var final = Path.Combine(job.WorkingDirectory, fileName);
        var existing = File.Exists(part) ? new FileInfo(part).Length : 0L;

        if (existing > 0)
        {
            using var rangedRequest = new HttpRequestMessage(HttpMethod.Get, uri);
            rangedRequest.Headers.Range = new RangeHeaderValue(existing, null);
            using var rangedResponse = await SendAsync(rangedRequest, ct);

            if (rangedResponse.StatusCode == HttpStatusCode.PartialContent)
            {
                var contentRange = rangedResponse.Content.Headers.ContentRange;
                if (contentRange?.From != existing)
                {
                    _logger.Warn($"Ignoring stale/inconsistent partial response for {job.Name}; restarting download.");
                    existing = 0;
                }
                else
                {
                    return await ConsumeResponseAsync(job, rangedResponse, part, final, existing, progress, ct);
                }
            }
            else
            {
                _logger.Warn($"Server did not honor resume for {job.Name}; restarting download from zero.");
                existing = 0;
            }
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await SendAsync(request, ct);
        return await ConsumeResponseAsync(job, response, part, final, 0, progress, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        try
        {
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static async Task<string> ConsumeResponseAsync(
        DownloadJob job,
        HttpResponseMessage response,
        string part,
        string final,
        long existing,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        using (response)
        {
            var contentLength = response.Content.Headers.ContentLength;
            var total = contentLength is long len && len >= 0 ? len + existing : (long?)null;
            job.TotalBytes = total;
            job.BytesReceived = existing;
            job.State = DownloadJobState.Downloading;
            job.Error = null;
            job.UpdatedAt = DateTimeOffset.UtcNow;

            var stopwatch = Stopwatch.StartNew();
            var lastReportBytes = existing;
            var lastReportAt = stopwatch.Elapsed;

            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = new FileStream(
                part,
                existing > 0 ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 128,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var buffer = new byte[1024 * 128];
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(), ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                job.BytesReceived += read;
                job.UpdatedAt = DateTimeOffset.UtcNow;

                var elapsed = stopwatch.Elapsed - lastReportAt;
                if (elapsed.TotalMilliseconds >= 250 || (total.HasValue && job.BytesReceived >= total.Value))
                {
                    var speed = elapsed.TotalSeconds > 0
                        ? (job.BytesReceived - lastReportBytes) / elapsed.TotalSeconds
                        : 0d;
                    TimeSpan? eta = speed > 0 && total.HasValue
                        ? TimeSpan.FromSeconds(Math.Max(0, (total.Value - job.BytesReceived) / speed))
                        : null;
                    progress?.Report(new DownloadProgress(job.BytesReceived, job.TotalBytes, speed, eta, "Downloading", job.Name));
                    lastReportBytes = job.BytesReceived;
                    lastReportAt = stopwatch.Elapsed;
                }
            }

            await output.FlushAsync(ct);
        }

        if (job.ExpectedSha256 is not null)
        {
            await using var hashInput = File.OpenRead(part);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(hashInput, ct));
            if (!hash.Equals(job.ExpectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                job.State = DownloadJobState.Failed;
                job.Error = "Downloaded file SHA-256 does not match the expected hash.";
                job.UpdatedAt = DateTimeOffset.UtcNow;
                throw new InvalidDataException(job.Error);
            }
        }

        ct.ThrowIfCancellationRequested();
        File.Move(part, final, true);
        job.State = DownloadJobState.Ready;
        job.BytesReceived = new FileInfo(final).Length;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        return final;
    }

    private static bool IsSha256(string value)
    {
        value = value.Trim();
        return value.Length == 64 && value.All(Uri.IsHexDigit);
    }

    public static string? FindPreparedPayload(string root)
    {
        if (!Directory.Exists(root)) return null;
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".nro", ".nsp", ".nsz", ".nca", ".nso", ".kip", ".ovl", ".bin"
        };

        var candidates = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(x => allowed.Contains(Path.GetExtension(x)))
            .Take(2)
            .ToArray();

        return candidates.Length == 1 ? candidates[0] : null;
    }
}
