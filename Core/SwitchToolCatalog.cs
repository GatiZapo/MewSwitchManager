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
        new("goldleaf", "Goldleaf", "XorTroll/Goldleaf", SwitchToolKind.Homebrew, "switch/Goldleaf.nro", "*.nro", "General Switch file/title utility."),
        new("tesla-loader", "nx-ovlloader", "WerWolv/nx-ovlloader", SwitchToolKind.Overlay, "atmosphere/contents/420000000007E51A/exefs.nsp", "*.zip", "Tesla overlay loader."),
        new("tesla-menu", "Tesla Menu", "WerWolv/Tesla-Menu", SwitchToolKind.Overlay, "switch/.overlays/ovlmenu.ovl", "*.zip", "Tesla overlay menu."),
        new("sys-clk", "sys-clk", "retronx-team/sys-clk", SwitchToolKind.Overlay, "switch/.overlays/sys-clk-overlay.ovl", "*.zip", "Per-title CPU/GPU/memory clock management; optional."),
        new("status-monitor", "Status Monitor Overlay", "masagrator/Status-Monitor-Overlay", SwitchToolKind.Overlay, "switch/.overlays/Status-Monitor-Overlay.ovl", "*.zip", "System monitoring overlay; optional.")
    ];
}
