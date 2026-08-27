# MewNX Manager Completion Gates

This branch closes the current manager-hardening pass.

## Implemented

- dependency plans now block parent components when a required dependency is missing, cyclic or version-incompatible;
- transactional rollback now verifies restored files/directories before reporting success;
- component updates use verified rollback plus the existing user-visible backup as a second recovery layer;
- the component catalog is parsed and validated before release queries;
- the catalog is shipped with the published application;
- system diagnostics report catalog integrity as a health check;
- regression coverage includes missing/incompatible dependency blocking, cycle blocking, rollback verification and commit preservation.

## Release gate

The branch is not considered releasable until Windows CI and regression tests pass on the final commit. Hardware-dependent Switch operations still require physical validation on a Windows host with a real Switch/SD card.
