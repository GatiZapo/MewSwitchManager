using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MewNX.Infrastructure;
using MewNX.Models;

namespace MewNX.Hardware;

/// <summary>Obtains stable USB/PnP identity evidence without treating transient disk numbering as identity.</summary>
public sealed class DiskIdentityProvider(ProcessRunner runner, AppLogger logger)
{
    private const string Query = @"
Get-CimInstance Win32_DiskDrive | ForEach-Object {
    [pscustomobject]@{
        Index = [int]$_.Index
        InterfaceType = [string]$_.InterfaceType
        PnpDeviceId = [string]$_.PNPDeviceID
        SerialNumber = [string]$_.SerialNumber
    }
} | ConvertTo-Json -Compress";

    public async Task<DiskIdentity> GetIdentityAsync(string diskNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(diskNumber))
            return Unknown(diskNumber, DiskIdentitySourceStatus.DeviceUnavailable, "Disk number is empty.");

        try
        {
            var result = await runner.RunAsync("powershell.exe", $"-NoProfile -Command {Quote(Query)}", ct).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                logger.Warn($"Disk identity query failed with exit code {result.ExitCode}: {result.StdErr.Trim()}");
                return Unknown(diskNumber, DiskIdentitySourceStatus.QueryFailed, "Windows PnP identity query failed.");
            }

            if (string.IsNullOrWhiteSpace(result.StdOut))
                return Unknown(diskNumber, DiskIdentitySourceStatus.DeviceUnavailable, "Windows returned no disk identity data.");

            using var document = JsonDocument.Parse(result.StdOut);
            var items = document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray().ToArray()
                : [document.RootElement];
            var item = items.FirstOrDefault(x => GetString(x, "Index") == diskNumber);
            if (item.ValueKind == JsonValueKind.Undefined)
                return Unknown(diskNumber, DiskIdentitySourceStatus.DeviceUnavailable, "Selected physical disk is no longer present.");

            var pnp = GetString(item, "PnpDeviceId").Trim();
            var serial = NormalizeSerial(GetString(item, "SerialNumber"));
            if (!pnp.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) && !pnp.StartsWith("USBSTOR\\", StringComparison.OrdinalIgnoreCase))
                return Unknown(diskNumber, DiskIdentitySourceStatus.NoReliableHardwareIdentity, "Target is not reporting a USB PnP identity.");

            var vid = ExtractToken(pnp, "VID_");
            var pid = ExtractToken(pnp, "PID_");
            var instanceId = ExtractInstanceId(pnp);

            // A hardware serial is required for a unique destructive-resume root of trust.
            // VID/PID identify a device family, not an individual physical unit.
            if (string.IsNullOrWhiteSpace(vid) || string.IsNullOrWhiteSpace(pid) || string.IsNullOrWhiteSpace(serial) || IsGenericSerial(serial))
                return new(diskNumber, vid, pid, serial, instanceId, "", DiskIdentityConfidence.Unknown,
                    DiskIdentitySourceStatus.NoReliableHardwareIdentity,
                    "USB identity is incomplete or reports a non-unique serial.");

            var canonical = Fingerprint($"USB|VID={vid}|PID={pid}|SERIAL={serial}");
            return new(diskNumber, vid, pid, serial, instanceId, canonical, DiskIdentityConfidence.Confirmed,
                DiskIdentitySourceStatus.Confirmed);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.Error("Disk identity query failed", ex);
            return Unknown(diskNumber, DiskIdentitySourceStatus.QueryFailed, ex.GetType().Name);
        }
    }

    private static DiskIdentity Unknown(string diskNumber, DiskIdentitySourceStatus status, string diagnostic) =>
        new(diskNumber ?? "", "", "", "", "", "", DiskIdentityConfidence.Unknown, status, diagnostic);

    private static string GetString(JsonElement item, string property) => item.TryGetProperty(property, out var value) ? value.ToString() : "";

    private static string ExtractToken(string value, string prefix)
    {
        var start = value.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return "";
        start += prefix.Length;
        var end = value.IndexOf('&', start);
        if (end < 0) end = value.IndexOf('\\', start);
        if (end < 0) end = value.Length;
        return value[start..end].Trim();
    }

    private static string ExtractInstanceId(string pnp)
    {
        var slash = pnp.LastIndexOf('\\');
        return slash >= 0 && slash + 1 < pnp.Length ? pnp[(slash + 1)..].Trim() : "";
    }

    private static string NormalizeSerial(string serial) => serial.Trim().Replace(" ", "", StringComparison.Ordinal);

    private static bool IsGenericSerial(string serial) =>
        serial.Length < 4 || serial.All(c => c == '0') ||
        serial.Equals("123456789", StringComparison.OrdinalIgnoreCase) ||
        serial.Equals("0123456789", StringComparison.OrdinalIgnoreCase) ||
        serial.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase);

    private static string Fingerprint(string canonical)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
