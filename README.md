# MewNX 0.4 ALPHA

**MewNX — Advanced Nintendo Switch Toolkit** is a Windows-first, safety-focused Nintendo Switch AIO utility. It combines Switchroot/Linux preparation with Switch storage management, CFW components, homebrew tools, RCM helpers, checkpoints, recovery, emulation and controlled user-content transfer workflows.

## AIO experience

- Separate **Home, Install, Switch Tools, Emulation, Game Center, Recovery, Diagnostics, Auto Mode and Updates** sections instead of one long dashboard.
- **Auto Mode** builds a persisted operation plan, reconciles it with the real installation state and automatically runs only the safe, resumable steps.
- Auto Mode stops at destructive or hardware-dependent checkpoints and never bypasses the existing safety confirmations.
- The active operation stays pinned above every section, so downloads, verification, extraction and USB writes remain visible while navigating.
- Technical state is presented with compact monospace telemetry, strong borders and MewNX neon pink/blue accents on a near-black canvas.
- Persistent operation/error logs are written to the MewNX application data directory for post-mortem troubleshooting.
- System Diagnostics can validate Windows, storage, WSL, RCM, Hekate/SD detection, the cached Linux image and persisted workflow state.
- MewNX keeps resumable downloads and reconciles persisted workflow state when a new build starts.
- The application, installer, taskbar/window identity and published artifacts use the MewNX brand.

See `docs/MEWNX_DESIGN_SYSTEM.md` for the complete visual and UX rules.

## Linux / USB safety

- USB-only target selection with system/protected disks blocked.
- Target identity is re-checked immediately before destructive preparation and again after the disk is cleaned.
- Verified `.raw` Linux images are written directly to the physical USB disk; MewNX does not create a Windows partition first.
- The raw-image writer refreshes Windows storage properties after flashing.
- Destructive actions require explicit confirmation and a typed disk identity.
- Linux archive extraction is staged, path-traversal protected and kept off the WinForms UI thread.

## Switch tools

- **SWITCH TOOLS** manages Hekate/Nyx, Atmosphère, DBI, Awoo Installer and the broader AIO tool catalog.
- Component downloads are resumable, verified and staged before installation.
- Existing managed data is backed up before replacement and rollback is available when a transaction fails.
- Recommended setup and Update Everything create checkpoints before modifying Switch storage.

## Emulation Center

- One-click managed emulator setup with the current stable tico frontend and its released Tico cores, plus the official RetroArch Switch stable bundle.
- Tico cores are treated as independent packages so one core can be updated or rolled back without replacing the frontend.
- Current Tico releases are resolved from the official `ticohq` repositories. Source-only projects are documented but are not falsely presented as installable packages.
- Existing RetroArch user data, saves, states, playlists, thumbnails and BIOS/system files are preserved.
- Transactional installation and rollback prevent partially installed managed stacks.
- MewNX never downloads game ROMs, console keys, firmware or user BIOS dumps.

## Game Center

Game Center is a **content-management and staging layer**, not a game-piracy catalogue.

- Index and verify files supplied by the user.
- Calculate SHA-256 before and after staging.
- Check destination space before copying.
- Stage content atomically into `MewNX/Incoming` on the selected SD card.
- Keep the source file untouched.
- Provide a clean hand-off point for supported installers such as DBI/Awoo.
- Preserve a clear distinction between MewNX-managed software and user-provided content.

MewNX does not ship warez-site lists, scrape unauthorised game-dump pages or automatically download copyrighted game dumps from unofficial sources. Legitimate, user-controlled servers and official homebrew sources remain compatible with the architecture.

## Architecture principles

- **Lazy services:** expensive centers and hardware/network work should be created on first use rather than during application startup.
- **Shared infrastructure:** release metadata, HTTP, logging, checkpoints and dependency resolution are reused instead of recreated per page.
- **Transactional deployment:** resolve → resumable fetch → verify → stage → validate → deploy → checkpoint commit.
- **Safety Engine:** backend state is authoritative; the UI never shows a green state before the corresponding check succeeds.
- **Dependency graph:** components declare dependencies as data and are installed in dependency order.
- **Persisted planning:** Auto Mode stores its checkpoint in application state so a new build can resume from the actual completed stage instead of blindly repeating work.

See `docs/MEWNX_AIO_ARCHITECTURE.md` for the complete architecture and source policy.

## What changed in 0.4

- Persistent workflow reconciliation when a new MewNX version starts.
- Persisted **Auto Mode** operation planning with explicit user/hardware safety gates.
- Added System Diagnostics and surfaced its results in the main UI.
- Resumable component downloads using `.part` files as well as the existing Linux image resume flow.
- Automatic removable/fixed-drive detection for Switch SD cards with Switch-folder signatures.
- **SWITCH HEALTH** dashboard for Hekate / Nyx, Atmosphère, emuMMC, tools and configuration.
- Official GitHub release channels are queried directly.
- Component archives are staged before installation and protected against archive path traversal.
- GitHub release SHA-256 asset digests are verified when published.
- Existing component data is backed up before an update.
- Component merges preserve existing configuration instead of deleting the component directory first.
- Failed component updates attempt automatic rollback from the pre-update backup.
- AIO tools include Checkpoint, JKSV, Sphaira, Goldleaf, NX-Shell, Daybreak, Tesla, nx-ovlloader, sys-clk, Status Monitor, MissionControl, FPSLocker, Ultrahand, TegraExplorer, Lockpick_RCM, DBI and Awoo Installer.
- Added checkpoints and recommended-setup preparation before managed tool changes.
- Added Game Center preflight/staging with post-copy SHA-256 verification.
- Refined the visual system toward a technical terminal aesthetic without copying third-party branding or layouts.

## Logs

MewNX keeps a persistent log at the application data directory shown in the UI. It records session startup, external process execution, standard output/error summaries, safety checks, downloads, installations, cancellations and full exception details. Logging failures never interrupt the operation being observed.

## Windows distribution

The Windows CI produces self-contained single-file x64, ARM64 and x86 builds plus an x64 Inno Setup installer. Published artifacts include SHA-256 checksums.

The CI uses current Node 24-compatible GitHub Actions runtimes for checkout, .NET setup and artifact upload, avoiding the Node 20 deprecation warnings emitted by the older action versions.

For Smart App Control / antivirus reputation, the release pipeline is prepared for Authenticode signing when the project's Windows signing certificate is configured in GitHub Actions secrets. A certificate is required to obtain trusted Windows reputation; no application can honestly guarantee that Smart App Control will accept an unsigned executable.
