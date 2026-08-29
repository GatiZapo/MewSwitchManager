namespace MewNX.Models;

public enum DiskIdentityConfidence
{
    Unknown,
    Confirmed
}

public enum DiskIdentitySourceStatus
{
    Confirmed,
    NoReliableHardwareIdentity,
    QueryFailed,
    DeviceUnavailable
}

public sealed record DiskIdentity(
    string DiskNumber,
    string Vid,
    string Pid,
    string HardwareSerial,
    string InstanceId,
    string CanonicalFingerprint,
    DiskIdentityConfidence Confidence,
    DiskIdentitySourceStatus SourceStatus,
    string? Diagnostic = null);
