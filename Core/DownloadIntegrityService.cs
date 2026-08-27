using System.Security.Cryptography;

namespace MewSwitchManager.Core;

public sealed class DownloadIntegrityService
{
    public async Task<bool> VerifySha256Async(string path, string expected, CancellationToken ct = default)
    {
        if (!File.Exists(path) || string.IsNullOrWhiteSpace(expected)) return false;
        await using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
        return string.Equals(actual, expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
