# Security / Safety Model

MewSwitch Manager performs destructive storage operations. Safety is therefore a product feature, not an optional mode.

## Required gates

1. Windows-only execution for storage operations.
2. Administrator elevation.
3. USB bus-type requirement.
4. Windows boot/system/pagefile/recovery protection.
5. Offline/read-only protection.
6. Selected disk identity capture.
7. Identity re-check immediately before `clean`.
8. Identity re-check after partition creation.
9. Explicit destructive confirmation.
10. Typed confirmation: `WRITE DISK N`.
11. SHA-1 verification of the downloaded Linux archive before flashing.

## No Safe Mode bypass

There is deliberately no button that disables the Safety Engine.

## Download safety

Partial downloads are retained as `.part` files so an interrupted download can resume. A final archive is only accepted after size/hash verification.

## Reporting

Operational events are written to the application log under the user's roaming application-data directory.
