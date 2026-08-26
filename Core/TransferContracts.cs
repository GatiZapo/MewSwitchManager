namespace MewSwitchManager.Core;

public enum TransferTransport
{
    Usb,
    Network,
    Local
}

public sealed record TransferItem(string SourcePath, string DisplayName, long Size, string? Sha256 = null);
public sealed record TransferPreflight(bool CanProceed, long RequiredBytes, long AvailableBytes, IReadOnlyList<string> Warnings, IReadOnlyList<string> Errors);
public sealed record TransferProgress(long BytesTransferred, long TotalBytes, string Stage, bool CanResume);
public sealed record TransferResult(bool Success, string Message, long BytesTransferred, bool Resumable);

public interface ITransferAdapter
{
    string Id { get; }
    string DisplayName { get; }
    bool CanHandle(TransferTransport transport);
    Task<bool> DetectAsync(CancellationToken ct = default);
    Task<long?> GetAvailableSpaceAsync(CancellationToken ct = default);
    Task<TransferResult> TransferAsync(TransferItem item, IProgress<TransferProgress>? progress = null, CancellationToken ct = default);
}

public interface IGameSourceProvider
{
    string Id { get; }
    string DisplayName { get; }
    bool CanHandle(Uri source);
    Task<IReadOnlyList<TransferItem>> DiscoverAsync(Uri source, CancellationToken ct = default);
}
