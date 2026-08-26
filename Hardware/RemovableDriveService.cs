namespace MewSwitchManager.Hardware;

public sealed record RemovableDrive(string Root, string VolumeLabel, long FreeBytes, long TotalBytes)
{
    public override string ToString() => $"{Root}  {VolumeLabel}  ({TotalBytes / 1_000_000_000d:0.0} GB)";
}

public sealed class RemovableDriveService
{
    public IReadOnlyList<RemovableDrive> Scan()
    {
        var result = new List<RemovableDrive>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Removable) continue;
                result.Add(new RemovableDrive(
                    drive.RootDirectory.FullName,
                    drive.VolumeLabel,
                    drive.AvailableFreeSpace,
                    drive.TotalSize));
            }
            catch
            {
                // A card can disappear while Windows is enumerating it.
            }
        }
        return result;
    }
}
