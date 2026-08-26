# MewNX Emulation Center

MewNX now treats emulation as a **complete installable software stack**, not a list of links. One button can install the tico frontend, the full official RetroArch Switch bundle, and every currently released Tico core used by the stock Tico systems.

## What gets installed

### tico

The tico frontend is installed to `sdmc:/switch/tico/tico.nro`.

tico intentionally stopped bundling emulator cores in 0.6.0. Cores are independent packages and are normally installed from its Core Download Manager. MewNX reproduces that practical result from the PC side: it downloads the official Tico core releases and puts them in `sdmc:/tico/cores/` so the user does not have to open Tico's updater one core at a time.

The currently managed Tico core set is:

| Core | Systems |
|---|---|
| tico FCEUmm | NES / Famicom |
| tico Snes9x | SNES / Super Famicom |
| tico Mupen64Plus-Next | Nintendo 64 |
| tico Dolphin | GameCube / Wii |
| tico Gambatte | Game Boy / Game Boy Color |
| tico mGBA | Game Boy Advance |
| tico Azahar | Nintendo 3DS |
| tico Genesis Plus GX | Master System / Game Gear / Genesis / Sega CD |
| tico YabaSanshiro | Sega Saturn |
| tico Flycast | Dreamcast / Naomi / Atomiswave |
| tico FBNeo | Arcade / FinalBurn Neo |
| tico DuckStation | PlayStation |
| tico PPSSPP | PSP |

This set matches the stock Tico systems/core layout documented by the community configuration example and the Tico release history. Tico cores are kept as Tico cores rather than being placed in RetroArch: their integration includes Tico-specific overlays and chain-loading behaviour.

### RetroArch

MewNX downloads the **official Libretro Switch `RetroArch.7z` bundle** and extracts it to the SD-card root. Libretro documents this bundle as containing RetroArch, all cores and the required assets; the Switch bundle's cores live under `retroarch/cores`.

The current implementation uses the official nightly bundle endpoint so the package follows the current Switch build instead of pinning MewNX to an obsolete release. The archive is validated before any SD-card merge.

MewNX preserves the user's:

- `retroarch.cfg`
- `retroarch/config/`
- `retroarch/saves/`
- `retroarch/states/`
- `retroarch/playlists/`
- `retroarch/thumbnails/`
- `retroarch/system/` (BIOS/system files)

The rest of the official bundle can be replaced during an update so cores/assets stay synchronized with the RetroArch executable.

## Why there are two systems

This is deliberate:

- **tico** is the polished controller-first frontend with its own Switch-specific cores and integration.
- **RetroArch** is the universal libretro environment with a very large core catalogue and its own ecosystem.

They overlap in systems, but they are not redundant from MewNX's point of view. Installing both gives the user a full frontend plus a full general-purpose libretro stack.

## Dependencies, drivers and BIOS

There is no generic Windows-style "emulator driver" that MewNX needs to install onto the Switch. The important runtime dependencies are the emulator/core binaries, their bundled assets/configuration, and—where required—system/BIOS data.

MewNX automatically installs everything it can legally redistribute:

- frontend binaries
- emulator cores
- RetroArch core bundle
- RetroArch assets
- Tico core packages
- required SD-card directories
- matching package versions from their official release channels

MewNX **does not download BIOS dumps, ROMs, keys, console firmware or other user-owned copyrighted content**. If a core requires one of those files, a future Switch Health/Emulation diagnostic should report the exact missing file and destination rather than silently downloading it from an untrusted source.

## Installer behaviour

`INSTALL EVERYTHING` performs:

1. SD-card detection.
2. Free-space preflight (4 GB recommended for the full stack).
3. Automatic checkpoint.
4. Resumable downloads.
5. GitHub SHA-256 verification for GitHub release assets.
6. Archive path-traversal validation for RetroArch.
7. Package staging.
8. User-data preservation.
9. Installation/merge.
10. Expected-file validation.
11. Automatic rollback if a component fails.
12. Final Switch Health rescan.

Individual components can also be retried from the same screen.

The installer never writes NAND, boot0/boot1, emuMMC partitions or Linux partitions.

## Sources investigated

The source list was checked against the Tico GitHub organization and current release assets. Tico's latest frontend release is distributed as `tico.nro`; its current release notes explicitly point to independent core repositories such as Tico Dolphin and Tico DuckStation. The Tico core repositories expose release assets with SHA-256 digests, which MewNX verifies before installation.

RetroArch's official Switch documentation recommends the bundle installation method and states that the bundle includes all cores and assets.
