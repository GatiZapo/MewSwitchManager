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
        var fileName = Path.GetFileName(new Uri(job.Source).AbsolutePath);
        if (string.IsNullOrWhiteSpace(fileName)) fileName = job.Id + ".download";
        var part = Path.Combine(job.WorkingDirectory, fileName + ".part");
        var final = Path.Combine(job.WorkingDirectory, fileName);
        var existing = File.Exists(part) ? new FileInfo(part).Length : 0L;
        using var request = new HttpRequestMessage(HttpMethod.Get, job.Source);
        if (existing > 0) request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existing, null);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (existing > 0 && response.StatusCode != HttpStatusCode.PartialContent)
        {
            existing = 0;
            response.Dispose();
            using var retry = new HttpRequestMessage(HttpMethod.Get, job.Source);
            response = await _http.SendAsync(retry, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength is long len ? len + existing : (long?)null;
        job.TotalBytes = total; job.BytesReceived = existing; job.State = DownloadJobState.Downloading; job.UpdatedAt = DateTimeOffset.UtcNow;
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(part, existing > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[1024 * 128]; int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct); job.BytesReceived += read; job.UpdatedAt = DateTimeOffset.UtcNow;
            progress?.Report(new DownloadProgress(job.BytesReceived, job.TotalBytes));
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
        job.State = DownloadJobState.Ready; job.UpdatedAt = DateTimeOffset.UtcNow;
        return final;
    }

    public static string? FindPreparedPayload(string root)
    {
        if (!Directory.Exists(root)) return null;
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".nro", ".nsp", ".nsz", ".nca", ".nso", ".kip", ".ovl", ".bin" };
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).FirstOrDefault(x => allowed.Contains(Path.GetExtension(x)));
    }
}
