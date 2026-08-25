namespace MewSwitchManager.Models;

public sealed record DownloadProgress(
    long BytesReceived,
    long? TotalBytes,
    double SpeedBytesPerSecond,
    TimeSpan? Eta,
    string Phase);
