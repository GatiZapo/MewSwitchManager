using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

public static class SwitchToolCatalog
{
    public static IReadOnlyList<SwitchToolDefinition> Definitions { get; } =
    [
        new("tegraexplorer", "TegraExplorer", "suchmememanyskill/TegraExplorer", SwitchToolKind.Payload, "bootloader/payloads/TegraExplorer.bin", "*.bin", "Payload file manager and low-level maintenance tools."),
        new("lockpick-rcm", "Lockpick_RCM", "shchmue/Lockpick_RCM", SwitchToolKind.Payload, "bootloader/payloads/Lockpick_RCM.bin", "*.bin", "Boot-time key derivation utility; use only for legitimate recovery/maintenance."),
        new("sphaira", "Sphaira", "porepore/Sphaira", SwitchToolKind.Homebrew, "switch/Sphaira.nro", "*.nro", "Homebrew launcher/file manager style application."),
        new("jksv", "JKSV", "J-D-K/JKSV", SwitchToolKind.Homebrew, "switch/JKSV/JKSV.nro", "*.nro", "Save-data management utility."),
        new("checkpoint", "Checkpoint", "FlagBrew/Checkpoint", SwitchToolKind.Homebrew, "switch/Checkpoint/Checkpoint.nro", "*.nro", "Save-data and extra-data management utility."),
        new("goldleaf", "Goldleaf", "XorTroll/Goldleaf", SwitchToolKind.Homebrew, "switch/Goldleaf.nro", "*.nro", "General Switch file/title utility."),
        new("dbi", "DBI", "rashevskyv/dbi", SwitchToolKind.Homebrew, "switch/DBI/DBI.nro", "*.nro", "Title/file/save manager with MTP and USB installation workflows."),
        new("awoo-installer", "Awoo Installer", "Huntereb/Awoo-Installer", SwitchToolKind.Homebrew, "switch/Awoo-Installer/Awoo-Installer.nro", "*.zip", "Installer utility for user-provided content; MewNX never supplies games or keys."),
        new("nx-shell", "NX-Shell", "joel16/NX-Shell", SwitchToolKind.Homebrew, "switch/NX-Shell/NX-Shell.nro", "*.nro", "Lightweight SD file manager."),
        new("daybreak", "Daybreak", "Atmosphere-NX/Atmosphere", SwitchToolKind.Homebrew, "switch/daybreak.nro", "*.nro", "Firmware update utility distributed with Atmosphere."),
        new("tesla-loader", "nx-ovlloader", "WerWolv/nx-ovlloader", SwitchToolKind.Overlay, "atmosphere/contents/420000000007E51A/exefs.nsp", "*.zip", "Tesla overlay loader."),
        new("tesla-menu", "Tesla Menu", "WerWolv/Tesla-Menu", SwitchToolKind.Overlay, "switch/.overlays/ovlmenu.ovl", "*.zip", "Tesla overlay menu."),
        new("sys-clk", "sys-clk", "retronx-team/sys-clk", SwitchToolKind.Overlay, "switch/.overlays/sys-clk-overlay.ovl", "*.zip", "Per-title CPU/GPU/memory clock management."),
        new("status-monitor", "Status Monitor Overlay", "masagrator/Status-Monitor-Overlay", SwitchToolKind.Overlay, "switch/.overlays/Status-Monitor-Overlay.ovl", "*.zip", "System monitoring overlay."),
        new("mission-control", "MissionControl", "ndeadly/MissionControl", SwitchToolKind.Overlay, "atmosphere/contents/010000000000bd00/exefs.nsp", "*.zip", "Bluetooth controller support."),
        new("fpslocker", "FPSLocker", "masagrator/FPSLocker", SwitchToolKind.Overlay, "switch/.overlays/FPSLocker.ovl", "*.zip", "Frame-rate and performance overlay."),
        new("ultrahand", "Ultrahand Overlay", "ppkantorski/Ultrahand-Overlay", SwitchToolKind.Overlay, "switch/.overlays/Ultrahand.ovl", "*.zip", "Advanced overlay/action launcher.")
    ];

    public static IReadOnlyList<SwitchToolPack> Packs { get; } =
    [
        new("essentials", "Essentials", "Launcher, file management and save-management basics.", ["sphaira", "jksv", "nx-shell"]),
        new("maintenance", "Maintenance", "Recovery and maintenance utilities.", ["tegraexplorer", "lockpick-rcm", "sphaira", "jksv", "checkpoint", "nx-shell"]),
        new("overlays", "Performance / Overlays", "Tesla stack plus performance and monitoring overlays.", ["tesla-loader", "tesla-menu", "sys-clk", "status-monitor", "fpslocker", "ultrahand"]),
        new("controllers", "Controller Support", "Bluetooth controller and overlay essentials.", ["mission-control", "tesla-loader", "tesla-menu"]),
        new("full", "HATS-style Full Toolkit", "A broad all-in-one toolkit preset. It installs only the selected open-source tools and never includes games, keys or user content.", ["tegraexplorer", "sphaira", "jksv", "checkpoint", "goldleaf", "dbi", "awoo-installer", "nx-shell", "daybreak", "tesla-loader", "tesla-menu", "sys-clk", "status-monitor", "mission-control", "fpslocker", "ultrahand"])
    ];
}
