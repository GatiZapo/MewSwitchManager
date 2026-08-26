using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using MewSwitchManager.Infrastructure;
using MewSwitchManager.Models;

namespace MewSwitchManager.Linux;

public sealed class LinuxImageService
{
    private readonly HttpClient _http;
    private readonly AppLogger _logger;
    private readonly AppConfig _config;

    public LinuxImageService(HttpClient http, AppLogger logger, AppConfig config)
    {
        _http = http;
        _logger = logger;
        _config = config;
        _http.Timeout = Timeout.InfiniteTimeSpan;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MewSwitch-Manager/0.4");
    }

    public string FinalPath(string cacheDirectory) => Path.Combine(cacheDirectory, _config.LinuxImage.FileName);
    public string PartPath(string cacheDirectory) => FinalPath(cacheDirectory) + ".part";

    public async Task<bool> VerifyExistingAsync(string cacheDirectory, CancellationToken ct = default)
    {
        var path = FinalPath(cacheDirectory);
        if (!File.Exists(path)) return false;
        if (_config.LinuxImage.ExpectedSizeBytes > 0 && new FileInfo(path).Length != _config.LinuxImage.ExpectedSizeBytes) return false;
        if (!string.IsNullOrWhiteSpace(_config.LinuxImage.Sha1))
            return await VerifySha1Async(path, ct);

        if (_config.LinuxImage.ExpectedSizeBytes > 0)
        {
            var actualSize = new FileInfo(path).Length;
            var ok = actualSize == _config.LinuxImage.ExpectedSizeBytes;
            _logger.Warn(ok
                ? $"No SHA-1 configured; verified image by expected size only ({actualSize:N0} bytes)."
                : $"No SHA-1 configured and size mismatch. Expected {_config.LinuxImage.ExpectedSizeBytes:N0}, got {actualSize:N0}.");
            return ok;
        }

        _logger.Warn("No SHA-1 or expected size configured; image cannot be verified.");
        return false;
    }

    public async Task DownloadAsync(string cacheDirectory, IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(_config.LinuxImage.Url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Linux image URL is not configured.");

        Directory.CreateDirectory(cacheDirectory);
        var finalPath = FinalPath(cacheDirectory);
        var partPath = PartPath(cacheDirectory);

        if (File.Exists(finalPath) && await VerifyExistingAsync(cacheDirectory, ct))
        {
            _logger.Info("Linux image already exists and is verified.");
            progress?.Report(new DownloadProgress(new FileInfo(finalPath).Length, new FileInfo(finalPath).Length, 0, TimeSpan.Zero, "IMAGE VERIFIED"));
            return;
        }

        var existing = File.Exists(partPath) ? new FileInfo(partPath).Length : 0L;
        var restart = false;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                if (existing > 0 && !restart)
                    request.Headers.Range = new RangeHeaderValue(existing, null);

                _logger.Info(existing > 0 && !restart ? $"Resuming at {existing:N0} bytes." : "Starting Linux image download.");
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (existing > 0 && !restart && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    _logger.Warn("Server rejected the resume range (HTTP 416). Discarding partial file and restarting safely.");
                    DeleteIfExists(partPath);
                    existing = 0;
                    restart = true;
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var append = existing > 0 && response.StatusCode == HttpStatusCode.PartialContent;
                if (!append) existing = 0;

                var total = response.Content.Headers.ContentLength;
                if (total.HasValue && append) total += existing;

                await using var source = await response.Content.ReadAsStreamAsync(ct);
                await using (var target = new FileStream(partPath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var buffer = new byte[1024 * 1024];
                    var received = existing;
                    var watch = Stopwatch.StartNew();
                    var lastBytes = received;
                    var lastTicks = watch.ElapsedTicks;

                    while (true)
                    {
                        var read = await source.ReadAsync(buffer, ct);
                        if (read == 0) break;
                        await target.WriteAsync(buffer.AsMemory(0, read), ct);
                        received += read;

                        var elapsed = watch.Elapsed.TotalSeconds;
                        var speed = elapsed > 0 ? (received - lastBytes) / Math.Max(0.001, (watch.ElapsedTicks - lastTicks) / (double)Stopwatch.Frequency) : 0;
                        if (watch.ElapsedMilliseconds >= 250)
                        {
                            TimeSpan? eta = total.HasValue && speed > 1
                                ? (TimeSpan?)TimeSpan.FromSeconds(Math.Max(0, (total.Value - received) / speed))
                                : null;
                            progress?.Report(new DownloadProgress(received, total, speed, eta, "DOWNLOADING LINUX IMAGE"));
                            lastBytes = received;
                            lastTicks = watch.ElapsedTicks;
                            watch.Restart();
                        }
                    }
                    await target.FlushAsync(ct);
                }

                // Critical: the FileStream is closed before rename/hash verification.
                ReplaceAtomically(partPath, finalPath);
                _logger.Info($"Download complete: {new FileInfo(finalPath).Length:N0} bytes.");
                progress?.Report(new DownloadProgress(new FileInfo(finalPath).Length, total, 0, TimeSpan.Zero, "VERIFYING IMAGE"));
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.Error("Linux image download failed", ex);
                throw;
            }
        }
    }

    public async Task<bool> VerifySha1Async(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return false;
        if (string.IsNullOrWhiteSpace(_config.LinuxImage.Sha1))
        {
            if (_config.LinuxImage.ExpectedSizeBytes > 0)
            {
                var actualSize = new FileInfo(path).Length;
                var sizeOk = actualSize == _config.LinuxImage.ExpectedSizeBytes;
                _logger.Warn(sizeOk
                    ? $"No SHA-1 configured; verified image by expected size only ({actualSize:N0} bytes)."
                    : $"No SHA-1 configured and size mismatch. Expected {_config.LinuxImage.ExpectedSizeBytes:N0}, got {actualSize:N0}.");
                return sizeOk;
            }
            _logger.Warn("No expected SHA-1 or expected size configured; image cannot be verified.");
            return false;
        }

        if (_config.LinuxImage.ExpectedSizeBytes > 0)
        {
            var actualSize = new FileInfo(path).Length;
            if (actualSize != _config.LinuxImage.ExpectedSizeBytes)
            {
                _logger.Warn($"Image size mismatch before SHA-1 verification. Expected {_config.LinuxImage.ExpectedSizeBytes:N0}, got {actualSize:N0}.");
                return false;
            }
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha = SHA1.Create();
        var hash = await sha.ComputeHashAsync(stream, ct);
        var actual = Convert.ToHexString(hash);
        var expected = _config.LinuxImage.Sha1.Replace(" ", "").Trim().ToUpperInvariant();
        var ok = CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(actual), System.Text.Encoding.ASCII.GetBytes(expected));
        _logger.Info(ok ? $"Linux image verified successfully. Size: {new FileInfo(path).Length:N0} bytes. SHA-1: {actual}" : $"SHA-1 mismatch. Expected {expected}, got {actual}.");
        return ok;
    }

    private static void ReplaceAtomically(string source, string destination)
    {
        if (!File.Exists(destination))
        {
            File.Move(source, destination);
            return;
        }

        try
        {
            // Keep the previously verified image intact until the replacement succeeds.
            File.Replace(source, destination, null, ignoreMetadataErrors: true);
        }
        catch (PlatformNotSupportedException)
        {
            // The manager currently runs on Windows, but keep a safe fallback for test environments.
            File.Move(source, destination, overwrite: true);
        }
    }

    private static void DeleteIfExists(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
