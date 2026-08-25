# MewSwitch Manager 0.3 ALPHA

MewSwitch Manager is a Windows-first, safety-focused assistant for preparing a USB Linux target for Nintendo Switch / Switchroot L4T.

## What changed in 0.3

- Added persistent installation checkpoints with automatic resume-state detection.
- Completed stages are remembered across application restarts and application upgrades.
- The UI identifies the exact next required checkpoint instead of asking the user to repeat completed work.
- Added atomic state writes, backup recovery and schema migration handling.
- Added cached Linux-image fingerprint metadata so ordinary startup checks do not need to hash a multi-gigabyte image every time.
- The cached Linux image is still fully SHA-1 re-verified immediately before any destructive USB write.
- A remembered USB target is never silently replaced by another drive if the original device is missing or its identity changes.
- Added automatic Hekate/SD checkpoint detection when the relevant SD contents are mounted in Windows.
- Added explicit persistent checkpoints for Hekate/SD, Switch configuration and the final Mewroot handoff.
- Added GitHub release update detection and visible update status.
- Added stronger hacker/terminal-style workflow language and a visible resume engine.
- Updated version metadata and CI validation to 0.3.0-alpha.

## 0.2 foundations retained

- Responsive, DPI-aware WinForms dashboard with compact layout for small Windows screens and tablets.
- Keyboard shortcuts: **F5** refresh, **Esc** cancel an active operation.
- Single-instance protection so two MewSwitch processes cannot touch the same USB.
- Native Windows volume writing instead of writing to a physical disk offset.
- Final disk identity check after partition creation and immediately before the write.
- In-process 7z extraction using SharpCompress; no 7-Zip installation is required.
- Resumable Linux image downloads using a persistent `.part` file.
- SHA-1 verification before any destructive USB operation.
- Native Windows builds for **x64, ARM64 and x86**.
- Missing optional components are reported instead of silently changing Windows.

## Supported host

Windows 10 1809+ / Windows 11 on x64, ARM64 or x86, subject to the hardware and driver requirements of the connected Switch and USB controller.

The application requests administrator privileges because DiskPart and direct volume access are destructive system-level operations.

## Safety model

MewSwitch Manager does not expose Windows boot/system disks as selectable USB targets. It rejects disks that are:

- not USB
- Windows boot/system/recovery/pagefile related
- offline
- read-only
- otherwise marked unsafe by Windows

The target identity is captured and checked again immediately before `clean`. It is checked again after the new partition is created, before the image is opened for writing.

A destructive operation also requires two explicit confirmations, including typing `WRITE DISK N`.

A persisted USB selection is identity-bound. If the remembered device is disconnected or its unique identity changes, MewSwitch clears the selection instead of guessing which replacement drive the user intended.

## Resume model

The workflow is represented as persistent checkpoints:

1. Environment preflight
2. Linux image
3. USB / storage preparation
4. Hekate / SD
5. Switch configuration
6. Mewroot handoff

The manager stores the state locally and restores it on the next launch. It can also reconcile state after an application version change. Physical checkpoints remain explicit, while detectable Hekate/SD state can be recognized automatically when the SD card is mounted in Windows.

## Switchroot USB method

The USB workflow follows the Switchroot USB/eMMC documentation: create a partition on the USB device and write the Linux raw image to the **partition**, not the Windows physical disk as a whole.

The current Noble release is based on Ubuntu 24.04 and the Switchroot documentation lists Hekate 6.0.6+ as required for the 5.1.2 release.

MewSwitch Manager prepares the USB portion and then keeps the user at the appropriate physical/configuration hand-off checkpoint rather than pretending those Switch-side actions can be performed safely from Windows.

## Build

```text
build.cmd
```

The script publishes:

```text
dist/win-x64/
dist/win-arm64/
dist/win-x86/
```

GitHub Actions produces the same three architectures and packages each as a separate ZIP with SHA-256 checksums.

## Project structure

```text
Core/             workflow and safety orchestration
Hardware/         Windows disks, USB and native volume writer
Infrastructure/   persistence, logging, processes and dependencies
Linux/            Linux image download, resume and verification
Models/           persistent state and configuration
UI/               responsive WinForms interface and resume engine
.github/          CI and release workflows
```

## Important

This is still **ALPHA**. Never use the destructive USB write against a device containing data you cannot restore. Verify the selected disk, its model and its capacity before confirming the final write.
