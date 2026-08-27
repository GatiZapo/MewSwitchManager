namespace MewNX.Models;

public sealed record DownloadProgress(
    long BytesReceived,
    long? TotalBytes,
    double SpeedBytesPerSecond,
    TimeSpan? Eta,
    string Phase,
    string? Detail = null);
