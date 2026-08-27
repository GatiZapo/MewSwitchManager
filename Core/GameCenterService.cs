using System.Security.Cryptography;
using MewSwitchManager.Infrastructure;

namespace MewSwitchManager.Core;

/// <summary>Handles user-provided content: verify, stage atomically, and preserve the source.</summary>
public sealed class GameCenterService(AppLogger logger)
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".nsp", ".nsz", ".xci", ".xcz", ".nro", ".nca", ".nso", ".kip", ".ovl", ".zip", ".7z", ".tar", ".gz" };

    public async Task<GameContentInfo> InspectAsync(string sourcePath, CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Game Center source file was not found.", sourcePath);
        var info = new FileInfo(sourcePath);
        if (!SupportedExtensions.Contains(info.Extension)) throw new InvalidDataException($"Unsupported Game Center file type: {info.Extension}");
        await using var stream = File.OpenRead(sourcePath);
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(await sha.ComputeHashAsync(stream, ct));
        return new GameContentInfo(sourcePath, info.Name, info.Length, hash, info.Extension.ToLowerInvariant());
    }

    public async Task<GameStageResult> StageAsync(GameContentInfo content, string destinationRoot, CancellationToken ct = default)
    {
        if (!Directory.Exists(destinationRoot)) throw new DirectoryNotFoundException(destinationRoot);
        if (!File.Exists(content.SourcePath)) throw new FileNotFoundException("The indexed source file no longer exists.", content.SourcePath);
        var sourceInfo = new FileInfo(content.SourcePath);
        if (sourceInfo.Length != content.SizeBytes) throw new IOException("The source changed after indexing. Re-run preflight before staging.");
        var incoming = Path.Combine(destinationRoot, "MewNX", "Incoming"); Directory.CreateDirectory(incoming);
        var safeName = SanitizeFileName(content.DisplayName); var finalPath = Path.Combine(incoming, safeName); var partPath = finalPath + ".part";
        var root = Path.GetPathRoot(Path.GetFullPath(destinationRoot));
        if (string.IsNullOrWhiteSpace(root)) throw new IOException("Could not determine destination volume.");
        var available = new DriveInfo(root).AvailableFreeSpace;
        if (available < content.SizeBytes) throw new IOException($"Not enough free space for staging. Required: {content.SizeBytes:N0} bytes; available: {available:N0} bytes.");
        logger.Info($"Game Center staging {content.DisplayName} ({content.SizeBytes:N0} bytes) to {finalPath}.");
        try
        {
            await CopyAtomicAsync(content.SourcePath, partPath, ct);
            var stagedHash = await ComputeSha256Async(partPath, ct);
            if (!string.Equals(stagedHash, content.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 verification failed after staging. The staged file was discarded.");
            if (File.Exists(finalPath)) File.Delete(finalPath); File.Move(partPath, finalPath);
            return new GameStageResult(true, finalPath, content.SizeBytes, stagedHash, "Content staged and SHA-256 verified. Ready for a supported installer adapter.");
        }
        catch { TryDelete(partPath); throw; }
    }

    private static async Task CopyAtomicAsync(string source, string destination, CancellationToken ct)
    {
        await using var input = File.OpenRead(source); await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true); await input.CopyToAsync(output, 1024 * 1024, ct); await output.FlushAsync(ct);
    }
    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct) { await using var stream = File.OpenRead(path); using var sha = SHA256.Create(); return Convert.ToHexString(await sha.ComputeHashAsync(stream, ct)); }
    private static string SanitizeFileName(string value) { foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_'); return value; }
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}

public sealed record GameContentInfo(string SourcePath, string DisplayName, long SizeBytes, string Sha256, string Extension);
public sealed record GameStageResult(bool Success, string DestinationPath, long BytesStaged, string Sha256, string Message);
