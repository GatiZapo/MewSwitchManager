using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public sealed record SwitchSdReport(
    string Root,
    long TotalBytes,
    long FreeBytes,
    bool LooksLikeSwitchSd,
    bool HasHekate,
    bool HasAtmosphere,
    bool HasNintendo,
    bool HasEmummc,
    bool HasBootloaderConfig,
    IReadOnlyList<string> Warnings);

public sealed class SwitchSdInspector
{
    public SwitchSdReport Inspect(string root)
    {
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(root))!);
        var warnings = new List<string>();
        var hasHekate = File.Exists(Path.Combine(root, "bootloader", "update.bin"));
        var hasAtmosphere = File.Exists(Path.Combine(root, "atmosphere", "package3"));
        var hasNintendo = Directory.Exists(Path.Combine(root, "Nintendo"));
        var hasEmummc = Directory.Exists(Path.Combine(root, "emuMMC"));
        var hasConfig = File.Exists(Path.Combine(root, "bootloader", "hekate_ipl.ini"));
        if (!hasHekate) warnings.Add("Hekate payload not detected (bootloader/update.bin).");
        if (!hasAtmosphere) warnings.Add("Atmosphère package3 not detected.");
        if (!hasConfig) warnings.Add("bootloader/hekate_ipl.ini not detected.");
        if (hasEmummc && !File.Exists(Path.Combine(root, "emuMMC", "emummc.ini"))) warnings.Add("emuMMC directory exists but emummc.ini was not detected.");
        return new SwitchSdReport(root, drive.TotalSize, drive.AvailableFreeSpace, hasHekate || hasAtmosphere || hasNintendo || hasEmummc, hasHekate, hasAtmosphere, hasNintendo, hasEmummc, hasConfig, warnings);
    }
}
