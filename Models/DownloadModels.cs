namespace MewNX.Models;

public enum DownloadJobState { Queued, Downloading, Processing, Ready, Installing, Verifying, Completed, Failed, Cancelled }

public enum DownloadSourceKind { DirectUrl, LocalFile }

public sealed record DownloadJob(
    string Id,
    string Name,
    string Source,
    DownloadSourceKind SourceKind,
    string WorkingDirectory,
    string? ExpectedSha256 = null)
{
    public DownloadJobState State { get; set; } = DownloadJobState.Queued;
    public long BytesReceived { get; set; }
    public long? TotalBytes { get; set; }
    public string? PreparedPath { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record PreparedContent(string Path, string Kind, long SizeBytes);
