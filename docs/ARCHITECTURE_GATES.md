# MewNX Architecture Gates

This document defines the minimum gates for the cleanup phase. It is intentionally short: it is a decision record, not a progress diary.

## 1. Repository identity

- Repository: `MewSwitchManager`
- Product/application identity: `MewNX`
- All user-facing and project-level naming must converge on `MewNX` unless a legacy compatibility name is technically required.
- A rename must be explicit and tested; do not silently introduce a third identity.

## 2. Safe change flow

Changes to `main` should be protected by a feature branch and pull request once the repository's CI is stable enough to enforce that workflow.

Minimum PR expectations:

1. Focused change set.
2. Relevant tests added or updated.
3. Build/test matrix passes.
4. No generated status/progress documents unless they record a durable architectural decision.

## 3. CI discipline

CI should provide confidence, not substitute for design review.

- Avoid duplicate pipelines that execute the same validation.
- Keep fast feedback separate from expensive regression/integration validation.
- Prefer path filters and appropriate triggers where safe.
- Hardware-dependent validation must remain explicit and must not be represented as passing merely because a mock passed.

## 4. Persistent recovery contract

Any operation that can be interrupted must have an explicit recoverable state.

Required properties:

- State is persisted before a destructive step.
- Completion is committed only after verification succeeds.
- Interrupted work can be detected on startup.
- Invalid/corrupt state fails closed and is recoverable.
- A completed step is not repeated unless its validity can no longer be established.

## 5. Safety contract

For potentially destructive operations:

> uncertainty => stop => explain => require explicit confirmation

Never infer a target disk, device, partition, payload, or destructive action from ambiguous information.

## 6. Linux Gaming boundary

Linux Gaming must be usable as a standalone MewNX path for users who do not need Switch functionality.

It may consume generic infrastructure such as:

- download/resume
- integrity verification
- generic dependency management
- generic diagnostics

It must not have hard dependencies on Switch-specific code such as RCM, Hekate, console SD journaling, or Switch-specific workflows.

The module belongs visually at the bottom of the MewNX product flow, but its internal dependency boundary must remain independent.

## 7. Documentation rule

Durable documentation belongs in architecture/specification files. Temporary session state, repetitive progress logs, and AI-generated status prose should not accumulate in the repository.

The README should explain what the software is, how to build/use it, supported platforms, safety boundaries, and current limitations. It should not make claims that are not backed by tests or implementation.

## 8. Admission rule for future integrations

A new tool/integration should only be accepted when all are true:

- There is a clear user-facing purpose.
- Its source/release can be verified.
- Its lifecycle can be represented by the recovery model when interruption is possible.
- Its safety boundary is explicit.
- It does not create an unnecessary hard dependency between otherwise independent modules.

"Because it can be integrated" is not sufficient justification.
