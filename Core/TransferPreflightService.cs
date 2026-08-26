namespace MewSwitchManager.Core;

public sealed class TransferPreflightService
{
    public async Task<TransferPreflight> CheckAsync(TransferItem item, long? availableBytes, Func<CancellationToken, Task<bool>>? destinationCheck = null, CancellationToken ct = default)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        if (!File.Exists(item.SourcePath)) errors.Add("Source file does not exist.");
        else if (new FileInfo(item.SourcePath).Length != item.Size) errors.Add("Source size changed since it was indexed.");
        if (availableBytes.HasValue && availableBytes.Value < item.Size) errors.Add($"Insufficient destination space: {availableBytes.Value:N0} bytes available, {item.Size:N0} required.");
        if (destinationCheck is not null && !await destinationCheck(ct)) errors.Add("Destination/installer is not ready.");
        if (string.IsNullOrWhiteSpace(item.Sha256)) warnings.Add("No SHA-256 was supplied; integrity cannot be cryptographically verified against a known digest.");
        return new(errors.Count == 0, item.Size, availableBytes ?? -1, warnings, errors);
    }
}
