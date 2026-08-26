using MewSwitchManager.Models;

namespace MewSwitchManager.Core;

/// <summary>
/// Complete, distributable emulation stack for Switch.
/// ROMs, BIOS dumps, keys and console firmware are intentionally excluded.
/// </summary>
public static class EmulatorCatalog
{
    private static readonly string[] RetroArchPreserve =
    ["retroarch/retroarch.cfg", "retroarch/config", "retroarch/saves", "retroarch/states", "retroarch/playlists", "retroarch/thumbnails", "retroarch/system"];

    public static IReadOnlyList<EmulationPackageDefinition> Definitions { get; } =
    [
        new("tico", "tico frontend", "Multi-system launcher", EmulationSourceKind.GitHubRelease, "ticohq/tico", "tico.nro", "switch/tico/tico.nro", EmulationInstallMode.DirectFile, true, "Controller-first frontend; its external cores are installed below."),
        new("retroarch", "RetroArch + full core/asset bundle", "Multi-system / libretro", EmulationSourceKind.OfficialBundle, "", "RetroArch.7z", "", EmulationInstallMode.ArchiveToRoot, true, "Official Switch bundle containing RetroArch, all bundled cores and assets. User configuration, saves, states, playlists, thumbnails and BIOS/system files are preserved.", RetroArchPreserve),
        new("tico-fceumm", "tico FCEUmm", "NES / Famicom", EmulationSourceKind.GitHubRelease, "ticohq/tico-fceumm", "tico-fceumm.nro", "tico/cores/tico-fceumm.nro", EmulationInstallMode.DirectFile, true, "Tico NES/Famicom core."),
        new("tico-snes9x", "tico Snes9x", "SNES / Super Famicom", EmulationSourceKind.GitHubRelease, "ticohq/tico-snes9x", "tico-snes9x.nro", "tico/cores/tico-snes9x.nro", EmulationInstallMode.DirectFile, true, "Tico SNES core."),
        new("tico-mupen64", "tico Mupen64Plus-Next", "Nintendo 64", EmulationSourceKind.GitHubRelease, "ticohq/tico-mupen64plus", "tico-mupen64plus.nro", "tico/cores/tico-mupen64plus.nro", EmulationInstallMode.DirectFile, true, "Tico N64 core."),
        new("tico-dolphin", "tico Dolphin", "GameCube / Wii", EmulationSourceKind.GitHubRelease, "ticohq/tico-dolphin", "tico-dolphin.nro", "tico/cores/tico-dolphin.nro", EmulationInstallMode.DirectFile, true, "Tico GameCube/Wii core; one NRO serves both systems."),
        new("tico-gambatte", "tico Gambatte", "Game Boy / Game Boy Color", EmulationSourceKind.GitHubRelease, "ticohq/tico-gambatte", "tico-gambatte.nro", "tico/cores/tico-gambatte.nro", EmulationInstallMode.DirectFile, true, "Tico GB/GBC core."),
        new("tico-mgba", "tico mGBA", "Game Boy Advance", EmulationSourceKind.GitHubRelease, "ticohq/tico-mgba", "tico-mgba.nro", "tico/cores/tico-mgba.nro", EmulationInstallMode.DirectFile, true, "Tico GBA core."),
        new("tico-azahar", "tico Azahar", "Nintendo 3DS", EmulationSourceKind.GitHubRelease, "ticohq/tico-azahar", "tico-azahar.nro", "tico/cores/tico-azahar.nro", EmulationInstallMode.DirectFile, true, "Tico 3DS core."),
        new("tico-genesis", "tico Genesis Plus GX", "Master System / Game Gear / Genesis / Sega CD", EmulationSourceKind.GitHubRelease, "ticohq/tico-genesisplusgx", "tico-genesisplusgx.nro", "tico/cores/tico-genesisplusgx.nro", EmulationInstallMode.DirectFile, true, "Tico Sega 8/16-bit and Sega CD core."),
        new("tico-yabause", "tico YabaSanshiro", "Sega Saturn", EmulationSourceKind.GitHubRelease, "ticohq/tico-yabasanshiro", "tico-yabasanshiro.nro", "tico/cores/tico-yabasanshiro.nro", EmulationInstallMode.DirectFile, true, "Tico Saturn core."),
        new("tico-flycast", "tico Flycast", "Dreamcast / Naomi / Atomiswave", EmulationSourceKind.GitHubRelease, "ticohq/tico-flycast", "tico-flycast.nro", "tico/cores/tico-flycast.nro", EmulationInstallMode.DirectFile, true, "Tico Dreamcast/Naomi/Atomiswave core."),
        new("tico-fbneo", "tico FBNeo", "Arcade / FinalBurn Neo", EmulationSourceKind.GitHubRelease, "ticohq/tico-fbneo", "tico-fbneo.nro", "tico/cores/tico-fbneo.nro", EmulationInstallMode.DirectFile, true, "Tico FinalBurn Neo arcade core."),
        new("tico-duckstation", "tico DuckStation", "PlayStation", EmulationSourceKind.GitHubRelease, "ticohq/tico-duckstation", "tico-duckstation.nro", "tico/cores/tico-duckstation.nro", EmulationInstallMode.DirectFile, true, "Tico PlayStation core."),
        new("tico-ppsspp", "tico PPSSPP", "PSP", EmulationSourceKind.GitHubRelease, "ticohq/tico-ppsspp", "tico-ppsspp.nro", "tico/cores/tico-ppsspp.nro", EmulationInstallMode.DirectFile, true, "Tico PSP core.")
    ];

    public static IReadOnlyList<EmulationPackageDefinition> FullStack => Definitions.Where(x => x.RequiredForFullStack).ToArray();

    public static EmulationPackageDefinition? Find(string id)
        => Definitions.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
