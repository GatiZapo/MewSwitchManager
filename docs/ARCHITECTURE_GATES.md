# MewNX Architecture Gates

Normative engineering rules for changes that affect installation, persistence, recovery, or destructive storage operations.

## Storage identity gate

A persisted checkpoint MUST NOT be treated as proof that the currently selected storage device is the same physical device used when the checkpoint was created.

### Identity strength

Storage identity is evaluated independently from filesystem state and from transient Windows addressing.

**Primary identity target:** USB/PnP hardware identity composed from the device's VID, PID, and hardware serial/instance identity when available and trustworthy.

The following are NOT sufficient as a root of trust on their own:

- drive letter;
- volume GUID or filesystem serial;
- MBR disk signature;
- GPT disk GUID;
- `\\.\\PhysicalDriveN` enumeration number.

These values may be retained as corroborating or locating information, but they MUST NOT authorize a destructive resume by themselves.

### Confidence states

Every identity comparison MUST be representable as one of:

- **Confirmed** — a sufficiently strong physical identity is available and matches the persisted target.
- **Unknown** — identity is absent, incomplete, empty, generic, duplicated/ambiguous, or otherwise not trustworthy enough to establish uniqueness.
- **Mismatch** — a previously confirmed identity does not match the current device.

For destructive operations:

- `Confirmed` + matching target MAY proceed to the next safety gate.
- `Unknown` MUST stop automatic destructive resume and require safe re-selection/revalidation.
- `Mismatch` MUST stop automatic destructive resume.

### Physical-state reconciliation

Hardware identity alone is insufficient to establish that an operation may safely resume.

Before a destructive resume, MewNX MUST also reconcile the current physical/storage state against the persisted expectations. This may include disk capacity, partition/layout expectations, filesystem state, expected files, and cryptographic integrity where applicable.

**Identity confirmed + unexpected physical state = STOP.**

### Recovery rule

A journal entry describing an interrupted operation expresses historical intent, not proof of the current physical state. `Running` or `Incomplete` MUST NOT be converted into `Completed` or automatically resumed without:

1. target identity verification;
2. physical-state reconciliation;
3. SafetyEngine approval;
4. an explicit recovery decision appropriate to the operation's transactional boundary.

### Implementation rule

Identity acquisition MUST be isolated behind a dedicated abstraction. Core recovery/safety logic MUST consume the abstraction rather than parsing Windows device strings directly. If Windows cannot provide a trustworthy unique hardware identity, the result MUST remain `Unknown` rather than being silently downgraded to a weaker identifier.
