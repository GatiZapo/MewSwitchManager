# MewNX 0.4 ALPHA

**MewNX — Advanced Nintendo Switch Toolkit** is a Windows-first, safety-focused Nintendo Switch AIO utility. It combines Switchroot/Linux preparation with Switch storage management, CFW components, homebrew tools, RCM helpers, checkpoints, recovery and a managed emulation stack.

## AIO experience

- Separate **Home, Install, Switch Tools, Emulation, Recovery, Diagnostics and Updates** sections instead of one long dashboard.
- The active operation stays pinned above every section, so downloads, verification, extraction and USB writes remain visible while navigating.
- Persistent operation/error logs are written to the MewNX application data directory for post-mortem troubleshooting.
- MewNX keeps resumable downloads and reconciles persisted workflow state when a new build starts.
- The application, installer, taskbar/window identity and published artifacts use the MewNX brand.

## Linux / USB safety

- USB-only target selection with system/protected disks blocked.
- Target identity is re-checked immediately before destructive preparation and again after the disk is cleaned.
- Verified `.raw` Linux images are written directly to the physical USB disk; MewNX does not create a Windows partition first.
- The raw-image writer refreshes Windows storage properties after flashing.
- Destructive actions require explicit confirmation and a typed disk identity.
- Linux archive extraction is staged, path-traversal protected and kept off the WinForms UI thread.

## Switch tools

- **SWITCH TOOLS** manages Hekate/Nyx, Atmosphère and DBI plus the broader AIO tool catalog.
- Component downloads are resumable, verified and staged before installation.
- Existing managed data is backed up before replacement and rollback is available when a transaction fails.
- Recommended setup and Update Everything create checkpoints before modifying Switch storage.

## Emulation Center

- One-click managed emulator setup with RetroArch and the configured Tico frontend/core stack.
- Dependencies are treated as managed packages instead of leaving the user to hunt for separate installers.
- Existing RetroArch user data, saves, states, playlists, thumbnails and BIOS/system files are preserved.
- Transactional installation and rollback prevent partially installed managed stacks.
- MewNX never downloads game ROMs, console keys, firmware or user BIOS dumps.

## What changed in 0.4

- Persistent workflow reconciliation when a new MewNX version starts.
- Resumable component downloads using `.part` files as well as the existing Linux image resume flow.
- Automatic removable/fixed-drive detection for Switch SD cards with Switch-folder signatures.
- **SWITCH HEALTH** dashboard for Hekate / Nyx, Atmosphère, emuMMC, tools and configuration.
- Official GitHub release channels are queried directly.
- Component archives are staged before installation and protected against archive path traversal.
- GitHub release SHA-256 asset digests are verified when published.
- Existing Hekate, Atmosphère and DBI data is backed up before an update.
- Component merges preserve existing configuration instead of deleting the component directory first.
- Failed component updates attempt automatic rollback from the pre-update backup.
- DBI is deliberately pinned to the official English release channel instead of blindly selecting the newest Russian-only release.
- AIO tools include Checkpoint, JKSV, Sphaira, Goldleaf, NX-Shell, Daybreak, Tesla, nx-ovlloader, sys-clk, Status Monitor, MissionControl, FPSLocker, Ultrahand, TegraExplorer and Lockpick_RCM.
- Added checkpoints and recommended-setup preparation before managed tool changes.

## Logs

MewNX keeps a persistent log at the application data directory shown in the UI. It records session startup, external process execution, standard output/error summaries, safety checks, downloads, installations, cancellations and full exception details. Logging failures never interrupt the operation being observed.

## Windows distribution

The Windows CI produces self-contained single-file x64, ARM64 and x86 builds plus an x64 Inno Setup installer. Published artifacts include SHA-256 checksums.

For Smart App Control / antivirus reputation, the release pipeline is prepared for Authenticode signing when the project's Windows signing certificate is configured in GitHub Actions secrets. A certificate is required to obtain trusted Windows reputation; no application can honestly guarantee that Smart App Control will accept an unsigned executable.
