# MewNX 0.4 ALPHA

**MewNX — Advanced Nintendo Switch Toolkit** is a Windows-first, safety-focused Nintendo Switch utility. It combines Switchroot/Linux preparation with a broader toolkit for maintaining Switch storage, CFW components, homebrew, overlays, RCM, backups and a complete emulation stack.

## What changed in 0.4

- Persistent workflow reconciliation when a new MewNX version starts.
- Resumable component downloads using `.part` files as well as the existing Linux image resume flow.
- Automatic removable/fixed-drive detection for Switch SD cards with Switch-folder signatures.
- **SWITCH HEALTH** dashboard for Hekate / Nyx, Atmosphère, emuMMC, tools and configuration.
- **SWITCH MANAGER** for Hekate / Nyx, Atmosphère and DBI.
- Official GitHub release channels are queried directly.
- Component archives are staged before installation and protected against archive path traversal.
- GitHub release SHA-256 asset digests are verified when published.
- Existing Hekate, Atmosphère and DBI data is backed up before an update.
- Component merges preserve existing configuration instead of deleting the component directory first.
- Failed component updates attempt automatic rollback from the pre-update backup.
- DBI is deliberately pinned to the official English release channel instead of blindly selecting the newest Russian-only release.
- AIO tools include Checkpoint, JKSV, Sphaira, Goldleaf, NX-Shell, Daybreak, Tesla, nx-ovlloader, sys-clk, Status Monitor, MissionControl, FPSLocker, Ultrahand, TegraExplorer and Lockpick_RCM.
- Added checkpoints and recommended-setup preparation before managed tool changes.
- Added an RCM helper that detects the RCM USB device and explains the physical entry procedure.
- Added a **one-click Emulation Center** that installs tico, the official RetroArch Switch bundle and all currently released Tico cores used by the stock Tico systems.
- RetroArch user configuration, saves, states, playlists, thumbnails and BIOS/system files are preserved during bundle updates.
- Linux preparation remains isolated behind the existing SafetyEngine and destructive confirmations.
- Manager self-update remains available through UPDATE CENTER.

## Toolkit areas

- **System:** Hekate / Nyx, Atmosphère, DBI, emuMMC and configuration.
- **Tools:** Homebrew, payloads, save management and overlays.
- **Emulation:** tico + its complete released core set, plus RetroArch + its official Switch core/asset bundle. See `EMULATION_CENTER.md`.
- **RCM:** RCM detection, payload workflow and safety guidance.
- **Backups:** checkpoints, configuration and NAND/emuMMC safety workflows.
- **Linux:** Switchroot preparation remains a dedicated, safety-gated workflow.

## Emulation Center

The default **INSTALL EVERYTHING** action installs the whole emulation software stack without requiring the user to browse download pages or manually install cores.

It installs:

- tico frontend
- tico FCEUmm — NES/Famicom
- tico Snes9x — SNES/Super Famicom
- tico Mupen64Plus-Next — Nintendo 64
- tico Dolphin — GameCube/Wii
- tico Gambatte — Game Boy/Game Boy Color
- tico mGBA — Game Boy Advance
- tico Azahar — Nintendo 3DS
- tico Genesis Plus GX — Master System/Game Gear/Genesis/Sega CD
- tico YabaSanshiro — Sega Saturn
- tico Flycast — Dreamcast/Naomi/Atomiswave
- tico FBNeo — arcade
- tico DuckStation — PlayStation
- tico PPSSPP — PSP
- RetroArch + its official Switch bundle with its large core and asset catalogue

MewNX downloads these packages from their official GitHub releases or the official Libretro buildbot, verifies GitHub SHA-256 release digests when available, stages them, validates the result and rolls back failed writes.

There is no generic Switch "driver pack" to install separately. The required runtime software is the frontend/core/asset stack itself. ROMs, BIOS dumps, keys and console firmware are intentionally outside MewNX and must be supplied by the user.

## RCM

MewNX can detect a Nintendo Switch already exposing the RCM USB device. It cannot safely force a normal retail Switch from Horizon into RCM over USB: the console must already be in RCM before a payload can be injected. The toolkit therefore provides RCM guidance rather than pretending a USB reset can enter RCM.

AutoRCM is intentionally not modified automatically. It changes boot-related storage and should remain a separate, explicitly controlled operation.

## Safety model

MewNX does not expose Windows boot/system disks as selectable destructive USB targets. The Linux workflow rejects disks that are not safe USB candidates, re-checks target identity before destructive operations, and requires explicit confirmation.

Component updates are deliberately independent from the destructive Linux workflow. A failed component download or verification never starts a disk write.

Emulator installation follows the same principle: download → verify → stage → validate → checkpoint/backup → merge. Emulator updates never touch NAND, boot0/boot1, emuMMC partitions or Linux partitions.

## Supported host

Windows 10 1809+ / Windows 11 on x64, ARM64 or x86, subject to the hardware and driver requirements of the connected Switch and USB controller.

## Build

```text
build.cmd
```

GitHub Actions builds self-contained Windows packages for **x64, ARM64 and x86** and generates SHA-256 checksums.

## Project structure

```text
Core/             workflow, safety and Switch toolkit management
Hardware/         Windows disks, USB, storage and RCM detection
Infrastructure/   persistence, logging, processes and GitHub releases
Linux/            Linux image download, resume and verification
Models/           persistent workflow/component/emulation state and configuration
UI/               responsive WinForms interface
.github/          CI and release workflows
```

## Important

This is still **ALPHA**. Never use the destructive USB workflow against a device containing data you cannot restore. Verify the selected disk, model and capacity before confirming the final write. Component and emulation updates should be performed with the SD card mounted from the Switch fully powered down.
