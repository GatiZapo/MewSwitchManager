# MewSwitch Manager 0.4 ALPHA

MewSwitch Manager is a Windows-first, safety-focused Nintendo Switch utility. It combines the guarded Switchroot/Linux preparation workflow with a separate component manager for maintaining Switch storage.

## What changed in 0.4

- Persistent workflow reconciliation when a new Manager version starts.
- Resumable component downloads using `.part` files as well as the existing Linux image resume flow.
- Automatic removable/fixed-drive detection for Switch SD cards with Switch-folder signatures.
- New **SWITCH MANAGER** section for Hekate / Nyx, Atmosphère and DBI.
- Official GitHub release channels are queried directly.
- Component archives are staged before installation and protected against archive path traversal.
- GitHub release SHA-256 asset digests are verified when published.
- Existing Hekate, Atmosphère and DBI data is backed up before an update.
- Component merges preserve existing configuration instead of deleting the component directory first.
- Failed component updates attempt automatic rollback from the pre-update backup.
- DBI is deliberately pinned to the official English release channel instead of blindly selecting the newest Russian-only release.
- Added an RCM helper that detects the RCM USB device and explains the physical entry procedure.
- Linux preparation remains isolated behind the existing SafetyEngine and destructive confirmations.
- Manager self-update remains available through UPDATE CENTER.

## Switch component manager

The manager currently handles:

1. **Hekate / Nyx** — detects `bootloader/update.bin`, checks the official latest release, backs up `bootloader`, then merges the new release.
2. **Atmosphère** — detects `atmosphere/package3`, checks the official latest release, backs up `atmosphere`, then merges the new release.
3. **DBI** — uses the official English release channel and installs `switch/DBI/DBI.nro` with a backup of the existing DBI directory.

Linux/Switchroot remains a dedicated workflow because it may involve destructive disk operations. Supporting tools are represented in the manager architecture but are not yet blindly auto-installed.

## RCM

MewSwitch Manager can detect a Nintendo Switch already exposing the RCM USB device. It cannot safely force a normal retail Switch from Horizon into RCM over USB: the console must already be in RCM before a payload can be injected. The manager therefore provides an RCM guide rather than pretending a USB reset can enter RCM.

AutoRCM is intentionally not modified automatically. It changes boot-related storage and should remain a separate, explicitly controlled operation.

## Safety model

MewSwitch Manager does not expose Windows boot/system disks as selectable destructive USB targets. The Linux workflow rejects disks that are not safe USB candidates, re-checks target identity before destructive operations, and requires explicit confirmation.

Component updates are deliberately independent from the destructive Linux workflow. A failed component download or verification never starts a disk write.

## Supported host

Windows 10 1809+ / Windows 11 on x64, ARM64 or x86, subject to the hardware and driver requirements of the connected Switch and USB controller.

## Build

```text
build.cmd
```

GitHub Actions builds self-contained Windows packages for **x64, ARM64 and x86** and generates SHA-256 checksums.

## Project structure

```text
Core/             workflow, safety and Switch component management
Hardware/         Windows disks, USB, storage and RCM detection
Infrastructure/   persistence, logging, processes and GitHub releases
Linux/            Linux image download, resume and verification
Models/           persistent workflow/component state and configuration
UI/               responsive WinForms interface
.github/          CI and release workflows
```

## Important

This is still **ALPHA**. Never use the destructive USB workflow against a device containing data you cannot restore. Verify the selected disk, model and capacity before confirming the final write. Component updates should also be performed with the SD card mounted from the Switch fully powered down.
