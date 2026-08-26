# MewNX Emulation Center

MewNX now has a curated emulator/front-end catalog. The catalog deliberately separates **trusted automatic sources** from **manual-only entries** so a changing third-party download page can never silently become a trusted installation source.

## Curated lineup

| Entry | Systems | Distribution | Role |
|---|---|---|---|
| tico | Multi-system | GitHub Releases | Recommended frontend |
| RetroArch | Multi-system / libretro | Official libretro buildbot | Recommended frontend |
| Dolphin | GameCube / Wii | GitHub Releases | Recommended standalone emulator |
| PPSSPP Switch Community Build | PSP | GitHub Releases | Recommended standalone emulator |
| DraStic DS | Nintendo DS | Manual only | Advanced/experimental |
| Azahar via tico | Nintendo 3DS | Managed by tico | Recommended through tico |
| Flycast | Dreamcast / Naomi | Manual core | RetroArch core |
| mGBA | GB / GBC / GBA | Manual core | RetroArch core |
| ScummVM | Classic adventures | Manual only | Standalone emulator |

## tico

tico has been explicitly investigated rather than treated as a generic homebrew app. The current project describes itself as a controller-first emulation frontend for Nintendo Switch, supporting libretro cores and custom emulator cores, automatic game organization, metadata/assets, save states and RetroAchievements. Its core architecture means MewNX should install/update the frontend and then let tico manage its own emulator cores instead of duplicating that core-management system.

MewNX must never download or distribute ROMs, BIOS files, system firmware or other copyrighted game content. The user supplies legally obtained content.

## Installation policy

### GitHub Releases

MewNX may query the official repository's latest release, select a known compatible asset, download it resumably, verify the published SHA-256 digest when available, stage it, validate the expected SD-card path and then merge it into the target.

### Official buildbot

RetroArch's Switch bundle is not a GitHub release asset. Its official libretro buildbot URL must therefore be represented explicitly by the installer rather than guessed from a repository release.

### Manual only

DraStic, individual RetroArch cores and other entries remain cataloged but are not automatically redistributed until their packaging/licensing/source conditions are verified. This prevents MewNX from becoming a blind scraper or redistributor.

## Safety requirements

Every automatic emulator update must use the existing MewNX download, SHA-256 verification, staging, checkpoint and rollback infrastructure. A failed download or verification must never modify the Switch SD card.

Before a destructive or broad replacement, MewNX should create a checkpoint and preserve existing saves/configuration. Emulator installation must never touch NAND, boot0/boot1 or emuMMC partitions.
