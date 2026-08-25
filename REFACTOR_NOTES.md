# 0.2 Refactor Notes

## Architecture

The project now has one source tree and one project file. The accidental duplicate minimal project from the initial CI recovery is removed.

The destructive storage path is split into:

- `SafetyEngine` — policy and identity checks
- `DiskService` — Windows disk discovery
- `UsbStorageService` — archive extraction, partition creation and target resolution
- `NativeVolumeWriter` — locked Windows volume streaming

## Storage correctness

The previous prototype wrote through `PhysicalDriveN` with a partition offset. The 0.2 implementation instead resolves the Windows volume created for the USB partition and writes the raw image to that volume. This matches the Switchroot USB procedure and avoids confusing a disk-level target with a partition-level target.

## UX

- Per-monitor DPI support
- responsive compact layout
- touch-friendly 40px action buttons
- keyboard cancellation
- explicit operation state
- no automatic WSL installation by default
- single-instance guard

## Download behavior

The `.part` file is preserved after cancellation or connection failure. If the server supports HTTP Range, the next attempt resumes from the existing byte count. If the server refuses the range, the service safely restarts from zero.
