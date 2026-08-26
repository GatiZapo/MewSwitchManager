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
                if (!drive.IsReady) continue;
                if (drive.DriveType != DriveType.Removable && !LooksLikeSwitchStorage(drive.RootDirectory.FullName)) continue;
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

    private static bool LooksLikeSwitchStorage(string root)
    {
        try
        {
            return Directory.Exists(Path.Combine(root, "atmosphere"))
                || Directory.Exists(Path.Combine(root, "bootloader"))
                || Directory.Exists(Path.Combine(root, "switch"))
                || File.Exists(Path.Combine(root, "hbmenu.nro"));
        }
        catch { return false; }
    }
}
