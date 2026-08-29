using MewNX.Models;
using Xunit;

namespace MewNX.Tests;

public sealed class DiskIdentityProviderTests
{
    [Theory]
    [InlineData("", DiskIdentitySourceStatus.DeviceUnavailable)]
    [InlineData(" ", DiskIdentitySourceStatus.DeviceUnavailable)]
    public void EmptyDiskNumberIsUnavailable(string diskNumber, DiskIdentitySourceStatus expected)
    {
        var identity = new DiskIdentity(diskNumber, "", "", "", "", "", DiskIdentityConfidence.Unknown, expected);
        Assert.Equal(DiskIdentityConfidence.Unknown, identity.Confidence);
        Assert.Equal(expected, identity.SourceStatus);
    }

    [Fact]
    public void UnreliableHardwareIdentityCannotBeConfirmed()
    {
        var identity = new DiskIdentity("7", "1234", "5678", "", "USBSTOR", "", DiskIdentityConfidence.Unknown,
            DiskIdentitySourceStatus.NoReliableHardwareIdentity);

        Assert.Equal(DiskIdentityConfidence.Unknown, identity.Confidence);
        Assert.Empty(identity.CanonicalFingerprint);
    }

    [Fact]
    public void QueryFailureRemainsDistinguishableFromMissingSerial()
    {
        var failed = new DiskIdentity("7", "", "", "", "", "", DiskIdentityConfidence.Unknown,
            DiskIdentitySourceStatus.QueryFailed);
        var noSerial = new DiskIdentity("7", "1234", "5678", "", "USB\\VID_1234&PID_5678", "", DiskIdentityConfidence.Unknown,
            DiskIdentitySourceStatus.NoReliableHardwareIdentity);

        Assert.NotEqual(failed.SourceStatus, noSerial.SourceStatus);
        Assert.Equal(DiskIdentityConfidence.Unknown, failed.Confidence);
        Assert.Equal(DiskIdentityConfidence.Unknown, noSerial.Confidence);
    }
}
