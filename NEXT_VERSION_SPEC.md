# MewSwitch Manager 0.3 — Manager Architecture

## Implemented in 0.3.0-alpha.1

- GitHub Releases based update detection.
- Automatic update check when the application starts.
- Manual `CHECK FOR UPDATES` action.
- `UPDATE CENTER` section in the navigation.
- Architecture-aware release asset selection (x64, x86, ARM64).
- Release notes display.
- Self-update flow using the official MewSwitch Manager GitHub release package.
- Windows elevated execution is preserved for replacing an installed executable.
- CI version aligned with the application version.

## Planned component manager

The Linux installer remains the first-class destructive workflow. Component management is deliberately separated from it so a failed component update cannot silently trigger a disk write.

### Components

1. Hekate / Nyx
2. Atmosphère
3. DBI
4. Linux / Switchroot
5. Supporting homebrew and tools

Each component should expose the same lifecycle:

- Detect installed state.
- Detect installed version where reliably possible.
- Query its official release channel.
- Compare installed and available versions.
- Download to a temporary cache.
- Verify archive/file integrity.
- Show exactly which files will change.
- Back up existing configuration before replacement.
- Apply the update atomically where possible.
- Re-scan the target.
- Persist the resulting state.

## Safety rules

- Never treat a downloaded archive as trusted merely because it downloaded successfully.
- Prefer official upstream repositories/releases.
- Never overwrite a Switch storage target without the existing SafetyEngine identity gates.
- Never delete user configuration merely to install a newer component.
- Component updates must be independent from destructive Linux USB preparation.
- Failed downloads and failed verification must leave the existing installation untouched.

## Planned UI

The current `UPDATE CENTER` is the first step toward a full manager layout:

- Overview
- Linux
- Hekate
- Atmosphère
- DBI
- Tools / Diagnostics
- Updates
- Safety / Settings

The existing Linux workflow will remain intact and become the Linux page rather than being rewritten as a generic component installer.

## Official upstream channels

- Atmosphère: `Atmosphere-NX/Atmosphere`
- Hekate: `CTCaer/hekate`
- DBI: `rashevskyv/dbi`

DBI needs special handling because its release history contains Russian-only releases; the manager must not blindly select the newest tag if doing so would silently change the language/channel selected by the user.
