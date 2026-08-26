using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

/// <summary>
/// Curated emulator/front-end catalog for the Nintendo Switch.
/// Distribution is intentionally explicit: MewNX never treats an arbitrary web page as a trusted source.
/// </summary>
public static class EmulatorCatalog
{
    public static IReadOnlyList<EmulatorDefinition> Definitions { get; } =
    [
        new("tico", "tico", "Multi-system frontend", "ticohq/tico", EmulatorDistribution.GitHubRelease, "", "switch/tico/tico.nro", "*.zip", true, "Controller-first emulation frontend with automatic library, metadata and core management.", "Tico cores are distributed separately and managed by Tico. MewNX must not bundle ROMs or BIOS files."),
        new("retroarch", "RetroArch", "Multi-system / libretro", "libretro/RetroArch", EmulatorDistribution.OfficialBuildbot, "", "switch/retroarch_switch.nro", "RetroArch.7z", true, "Official libretro frontend and core ecosystem for Switch.", "The official Switch bundle is distributed from the libretro buildbot rather than GitHub Releases. Keep the source URL explicit in the installer."),
        new("dolphin", "Dolphin (Switch port)", "GameCube / Wii", "NaGaa95/dolphin-nx", EmulatorDistribution.GitHubRelease, "", "switch/dolphin/dolphin.nro", "*.zip", true, "Native Switch Dolphin port with a dedicated launcher and game library.", "Compatibility and performance vary by title; do not promise desktop-Dolphin compatibility."),
        new("ppsspp", "PPSSPP (Switch Community Build)", "PSP", "SirSamael/ppsspp-switch-community-build", EmulatorDistribution.GitHubRelease, "", "switch/ppsspp/PPSSPP.nro", "*.zip", true, "Current community Switch build of PPSSPP with Switch-specific fixes and packaging.", "This is preferred over the older upstream legacy Switch build; runtime settings and compatibility remain build-specific."),
        new("drastic", "DraStic DS (Switch port)", "Nintendo DS", "NaGaa95/DrasticDS_nx", EmulatorDistribution.ManualOnly, "", "switch/DrasticDS.nro", "*.nro", false, "Native Switch port of DraStic DS.", "Manual-only until redistribution/licensing and release packaging are suitable for automated MewNX distribution. User-provided DS BIOS/firmware remain outside MewNX."),
        new("azahar", "Azahar (via tico)", "Nintendo 3DS", "ticohq/tico", EmulatorDistribution.ManualOnly, "", "tico/cores", "*.zip", true, "Nintendo 3DS emulation exposed through Tico's core ecosystem.", "MewNX installs/updates the Tico frontend; Tico manages its Azahar core. MewNX does not duplicate the core download mechanism."),
        new("flycast", "Flycast", "Dreamcast / Naomi", "libretro/flycast", EmulatorDistribution.ManualOnly, "", "retroarch/cores/flycast_libretro_libnx.nro", "*.nro", false, "Dreamcast and arcade emulation, normally consumed as a RetroArch core on Switch.", "Treat this as a RetroArch core rather than a standalone application. Core compatibility can differ across firmware/builds."),
        new("mgba", "mGBA (via RetroArch)", "Game Boy / Game Boy Color / Game Boy Advance", "libretro/mgba", EmulatorDistribution.ManualOnly, "", "retroarch/cores/mgba_libretro_libnx.nro", "*.nro", false, "Game Boy family emulation through the RetroArch/libretro ecosystem.", "Core distribution should be handled by the RetroArch core manager rather than duplicated by MewNX."),
        new("scummvm", "ScummVM", "Classic adventure games", "scummvm/scummvm", EmulatorDistribution.ManualOnly, "", "switch/scummvm/scummvm.nro", "*.zip", false, "Interpreter for supported classic adventure engines.", "Keep this cataloged separately from libretro cores; packaging varies by Switch build.")
    ];

    public static EmulatorDefinition? Find(string id)
        => Definitions.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
