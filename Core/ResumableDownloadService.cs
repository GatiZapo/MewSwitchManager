using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed class ResumableDownloadService
{
    private readonly HttpClient _http;
    private readonly AppLogger _logger;

    public ResumableDownloadService(HttpClient http, AppLogger logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string> DownloadAsync(DownloadJob job, IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        Directory.CreateDirectory(job.WorkingDirectory);
        var uri = new Uri(job.Source);
        var fileName = Path.GetFileName(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(fileName)) fileName = job.Id + ".download";
        var part = Path.Combine(job.WorkingDirectory, fileName + ".part");
        var final = Path.Combine(job.WorkingDirectory, fileName);
        var existing = File.Exists(part) ? new FileInfo(part).Length : 0L;

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (existing > 0) request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existing, null);
        using var response = await SendAsync(request, ct);
        if (existing > 0 && response.StatusCode != HttpStatusCode.PartialContent)
        {
            existing = 0;
            response.Dispose();
            using var restart = new HttpRequestMessage(HttpMethod.Get, uri);
            using var fullResponse = await SendAsync(restart, ct);
            return await ConsumeResponseAsync(job, fullResponse, part, final, existing, progress, ct);
        }

        return await ConsumeResponseAsync(job, response, part, final, existing, progress, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        try { response.EnsureSuccessStatusCode(); return response; }
        catch { response.Dispose(); throw; }
    }

    private static async Task<string> ConsumeResponseAsync(DownloadJob job, HttpResponseMessage response, string part, string final, long existing, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        using (response)
        {
            var total = response.Content.Headers.ContentLength is long len ? len + existing : (long?)null;
            job.TotalBytes = total;
            job.BytesReceived = existing;
            job.State = DownloadJobState.Downloading;
            job.UpdatedAt = DateTimeOffset.UtcNow;

            var stopwatch = Stopwatch.StartNew();
            var lastReportBytes = existing;
            var lastReportAt = stopwatch.Elapsed;
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = new FileStream(part, existing > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
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
                    var speed = elapsed.TotalSeconds > 0 ? (job.BytesReceived - lastReportBytes) / elapsed.TotalSeconds : 0d;
                    TimeSpan? eta = speed > 0 && total.HasValue
                        ? TimeSpan.FromSeconds(Math.Max(0, (total.Value - job.BytesReceived) / speed))
                        : null;
                    progress?.Report(new DownloadProgress(job.BytesReceived, job.TotalBytes, speed, eta, "Downloading", job.Name));
                    lastReportBytes = job.BytesReceived;
                    lastReportAt = stopwatch.Elapsed;
                }
            }
            await output.FlushAsync(ct);

            if (job.ExpectedSha256 is not null)
            {
                await using var hashInput = File.OpenRead(part);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(hashInput, ct));
                if (!hash.Equals(job.ExpectedSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Downloaded file SHA-256 does not match the expected hash.");
            }

            if (File.Exists(final)) File.Delete(final);
            File.Move(part, final);
            job.State = DownloadJobState.Ready;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            return final;
        }
    }

    public static string? FindPreparedPayload(string root)
    {
        if (!Directory.Exists(root)) return null;
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".nro", ".nsp", ".nsz", ".nca", ".nso", ".kip", ".ovl", ".bin" };
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).FirstOrDefault(x => allowed.Contains(Path.GetExtension(x)));
    }
}
