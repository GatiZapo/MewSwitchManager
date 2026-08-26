# MewNX Design System

## Direction

MewNX is an AIO control surface, not a generic Windows utility. The interface should feel like a compact technical console: dense information, deliberate spacing, strong borders, monospace telemetry and a nearly-black canvas.

The visual direction takes inspiration from the *language* of technical terminal/web interfaces such as the reference screenshots supplied during the MewNX redesign. It does not copy their artwork, layout, wording, branding or content.

## Core palette

- **Void** — `#05060A` — application background.
- **Surface** — `#0B0D14` — primary panels.
- **Surface 2** — `#11131C` — active controls and secondary panels.
- **Surface 3** — `#161822` — hover/focus states.
- **Border** — `#272B39` — quiet separators.
- **Border strong** — `#3D4152` — technical frames and active outlines.
- **Text** — `#F4F5FA` — primary information.
- **Muted** — `#8B91A5` — secondary information.
- **Subtle** — `#5E6478` — metadata.
- **Neon pink** — `#FF00D4` — primary MewNX action accent.
- **Electric blue** — `#00D2FF` — navigation and secondary actions.
- **Lime** — `#00FFA4` — success/verified/ready.
- **Amber** — `#FFBE2D` — warning/attention/in progress.
- **Red** — `#FF4169` — failure/destructive state.

Pink and blue are identity accents. Green, amber and red are semantic state colours and must not be used decoratively.

## Typography

- UI labels and long-form text: Segoe UI.
- Technical values, logs, IDs, paths and operation status: Consolas.
- Section labels: uppercase monospace with deliberate letter spacing.
- Avoid oversized decorative text that pushes functional content below the fold.

## Panels

Prefer square or almost-square technical frames over rounded cards. Borders should carry the hierarchy instead of shadows.

A standard technical panel contains:

1. section label;
2. one-line state or description;
3. dense but readable content;
4. explicit action row;
5. optional operation log.

Do not create a new visual container for every line of information. Group related information into a single frame.

## Operation language

Use concise technical verbs internally while keeping user-facing explanations understandable:

- FETCH — obtain a package.
- VERIFY — validate size/hash/source.
- STAGE — prepare without touching the final target.
- DEPLOY — write/install to the selected destination.
- SYNC — reconcile versions/configuration.
- RECOVER — restore a checkpoint.
- READY / RUNNING / BLOCKED / FAILED / COMPLETE — standard state vocabulary.

Every destructive action must also include a plain-language explanation before confirmation.

## Session log

Important workflows should expose a compact log such as:

```text
> INITIALIZING MewNX CORE ............... OK
> DETECTING TARGET ...................... OK
> VERIFYING IDENTITY .................... OK
> CHECKING DEPENDENCIES ................. OK
> PREPARING PACKAGE ..................... OK
> DEPLOYMENT ............................ OK
> OPERATION COMPLETE
```

The compact log is a UI summary; the full structured log remains available in Diagnostics and the application log file.

## Navigation

The AIO shell uses stable numbered sections:

1. HOME
2. INSTALL
3. SWITCH TOOLS
4. EMULATION
5. GAME CENTER
6. RECOVERY
7. DIAGNOSTICS
8. UPDATES

Pages must not initialize expensive services merely because the user opened MewNX. Services are created on first use and shared afterwards.

## Accessibility and performance

- Never rely on colour alone to communicate state; include text/icon labels.
- Keep animations optional and short.
- Avoid continuous timers unless a page is visible or an operation is active.
- Avoid repeated hardware scans while the UI is idle.
- Reuse HTTP clients and cached release metadata.
- Keep large downloads off the UI thread.
- Never load ROM/game artwork into memory unless the Game Center/Emulation page needs it.
- Preserve user data during updates and rollback.

## Safety visual language

Safety gates should be visible before destructive operations:

```text
SAFETY ENGINE
TARGET ............... VERIFIED
IDENTITY ............. STABLE
BACKUP ............... READY
DEPENDENCIES ......... READY
FREE SPACE ........... 184 GB
DESTRUCTIVE .......... NO

[ PROCEED ]
```

The same state machine must drive the backend and UI. The UI must never display a green state for an operation that the backend has not verified.
