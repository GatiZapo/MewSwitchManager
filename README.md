# MewSwitch Manager 0.2 ALPHA

MewSwitch Manager is a Windows-first, safety-focused assistant for preparing a USB Linux target for Nintendo Switch / Switchroot L4T.

## What changed in 0.2

- Refactored the UI into a responsive, DPI-aware dashboard.
- Added compact layout for small Windows screens and Windows tablets.
- Added keyboard shortcuts: **F5** refresh, **Esc** cancel an active operation.
- Added single-instance protection so two MewSwitch processes cannot touch the same USB.
- Added native Windows volume writing instead of writing to a physical disk offset.
- Added a final disk identity check after partition creation and immediately before the write.
- Added in-process 7z extraction using SharpCompress; no 7-Zip installation is required.
- Kept resumable Linux image downloads using a persistent `.part` file.
- Added SHA-1 verification before any destructive USB operation.
- Added native Windows builds for **x64, ARM64 and x86**.
- Removed automatic WSL installation by default. Missing optional components are reported instead of silently changing Windows.

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

## Switchroot USB method

The USB workflow follows the Switchroot USB/eMMC documentation: create a partition on the USB device and write the Linux raw image to the **partition**, not the Windows physical disk as a whole.

The current Noble release is based on Ubuntu 24.04 and the Switchroot documentation lists Hekate 6.0.6+ as required for the 5.1.2 release.

MewSwitch Manager does not replace the Hekate/SD configuration steps. It prepares the USB portion and then leaves the user at the appropriate physical/configuration hand-off stage.

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
UI/               responsive WinForms interface
.github/          CI and release workflows
```

## Important

This is still **ALPHA**. Never use the destructive USB write against a device containing data you cannot restore. Verify the selected disk, its model and its capacity before confirming the final write.
