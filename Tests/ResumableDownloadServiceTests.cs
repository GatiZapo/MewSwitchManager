using System.Net;
using System.Net.Http;
using System.Text;
using MewNX.Infrastructure;
using MewNX.Models;
using MewNX.Core;

namespace MewNX.Tests;

public sealed class ResumableDownloadServiceTests
{
    [Fact]
    public async Task ResumesOnlyWhenServerConfirmsRequestedRange()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var bytes = Encoding.UTF8.GetBytes("0123456789");
            await File.WriteAllBytesAsync(Path.Combine(root, "payload.bin.part"), Encoding.UTF8.GetBytes("0123"));
            using var client = new HttpClient(new RangeAwareHandler(bytes));
            var service = new ResumableDownloadService(client, new AppLogger(Path.Combine(root, "logs")));
            var job = new DownloadJob("job", "payload", "https://example.test/payload.bin", DownloadSourceKind.DirectUrl, root);

            var result = await service.DownloadAsync(job, null);

            Assert.Equal(bytes, await File.ReadAllBytesAsync(result));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsHashMismatchAndKeepsPartialForRecovery()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var client = new HttpClient(new FixedHandler(Encoding.UTF8.GetBytes("payload")));
            var service = new ResumableDownloadService(client, new AppLogger(Path.Combine(root, "logs")));
            var job = new DownloadJob("job", "payload", "https://example.test/payload.bin", DownloadSourceKind.DirectUrl, root, new string('0', 64));

            await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(job, null));
            Assert.False(File.Exists(Path.Combine(root, "payload.bin")));
            Assert.True(File.Exists(Path.Combine(root, "payload.bin.part")));
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class FixedHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });
    }

    private sealed class RangeAwareHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var start = request.Headers.Range?.Ranges.SingleOrDefault()?.From ?? 0;
            var response = new HttpResponseMessage(start > 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload[(int)start..])
            };
            if (start > 0)
                response.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(start, payload.Length - 1, payload.Length);
            return Task.FromResult(response);
        }
    }
}
