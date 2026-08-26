# MewNX AIO Architecture

## Goal

MewNX should orchestrate trusted tools rather than reimplement mature Switch software. The application owns the workflow, safety, dependency resolution, caching, checkpoints, verification and UX. Specialist projects remain responsible for their specialist behaviour.

## Runtime rules

### 1. Lazy services

Do not initialize every center during application startup. The shell and cheap metadata load immediately. Expensive services are created when the corresponding page is first opened or an operation explicitly needs them.

Shared services should be single instances per application lifetime:

- HTTP/release client;
- logger;
- dependency catalog;
- checkpoint store;
- download/cache service.

### 2. Download pipeline

All managed downloads follow:

`resolve -> cache -> resumable fetch -> integrity verify -> stage -> validate -> deploy -> checkpoint commit`

Interrupted downloads retain `.part` data and resume when the server supports byte ranges. A completed package is not promoted into the cache until validation succeeds.

### 3. Dependency graph

Dependencies are data, not scattered conditionals. A component declares:

- ID;
- source;
- channel;
- minimum version when applicable;
- dependencies;
- target paths;
- preservation rules;
- license/source notes.

The dependency manager resolves a topological install order and skips already-satisfied components.

### 4. Transactional changes

Operations use a staging directory and a checkpoint. The target is modified only after source validation. A failed multi-component operation restores the checkpoint/backup instead of leaving a partial installation.

Rollback is best-effort but must report a failure as a failure; the UI must never claim success after a rollback error.

## Emulation Center

### tico

The managed tico frontend is resolved from the official `ticohq/tico` GitHub release. As of the current design pass, the latest stable release is 0.7.9. MewNX downloads only the frontend binary and other redistributable software supplied by the project.

### Tico cores

Tico treats emulator cores as independent components. MewNX therefore models each core separately so an individual core can be updated or rolled back without replacing the entire frontend.

Current public Tico repositories include:

- FCEUmm — NES/Famicom
- Snes9x — SNES
- Mupen64Plus — Nintendo 64
- Dolphin — GameCube/Wii
- Gambatte — Game Boy/Game Boy Color
- mGBA — Game Boy Advance
- Azahar/TresDeEsse — Nintendo 3DS
- Genesis Plus GX — Sega 8/16-bit and Sega CD
- YabaSanshiro — Sega Saturn
- Flycast — Dreamcast/Naomi/Atomiswave
- FinalBurn Neo — arcade
- DuckStation — PlayStation
- PPSSPP — PSP
- melonDS — Nintendo DS (source available; release packaging must be validated before enabling automatic deployment)

MewNX must not invent release assets for repositories that do not publish a usable Switch release. A source-only repository remains documented but is not marked installable until a compatible release asset exists.

### RetroArch

The stable Switch bundle is the preferred default because the official Libretro distribution includes RetroArch, cores and assets in a single validated package. Current stable is 1.22.2.

User data is preserved across updates, including configuration, saves, states, playlists, thumbnails and system/BIOS data. MewNX never downloads BIOS dumps or ROMs.

If a future Switch/Atmosphere combination requires a nightly build, that should be an explicit compatibility decision rather than silently replacing the stable channel.

## Game Center

Game Center is a **content-management and transfer layer**, not a piracy catalogue.

Allowed responsibilities:

- index files already owned/provided by the user;
- calculate and display SHA-256;
- verify file size and available destination space;
- stage content into a user-selected destination;
- integrate with legitimate, user-controlled installer workflows;
- remember completed stages/checkpoints;
- preserve resumable transfer state.

MewNX must not ship a list of piracy sites, scrape warez pages, bypass access controls, or automatically fetch copyrighted game dumps from unauthorised sources. Users can configure legitimate sources and local servers for content they are entitled to use.

### Installer adapters

The architecture supports adapters for:

- DBI / DBI backend;
- Awoo Installer + NS-USBloader-compatible workflows;
- Goldleaf;
- plain SD staging for manual installation.

An adapter must expose detection, preflight, transfer progress and a clear failure state. The Game Center must not assume that a Switch is ready merely because a USB device exists.

## Source trust

Sources are classified as:

1. **Official** — project-owned GitHub release or official project site.
2. **User configured** — a URL/server explicitly supplied by the user.
3. **Untrusted** — anything discovered through generic web scraping.

Only Official and User configured sources may enter the managed download pipeline. Untrusted sources are never silently scraped.

## UX principle

The user should normally see:

`SELECT -> PREFLIGHT -> VERIFY -> DEPLOY -> COMPLETE`

while the technical detail remains available through the operation log and Diagnostics page.
