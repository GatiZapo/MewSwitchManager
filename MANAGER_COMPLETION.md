# MewNX Manager Completion Gates

This pass hardens the manager core before release.

- Dependency plans block parents when required dependencies are missing, cyclic or incompatible.
- Component catalog validation rejects duplicate/unknown dependency references and blocks unsafe update plans.
- Managed component updates use a transaction journal and verify the restored state after rollback.
- System diagnostics validate catalog integrity in addition to platform, storage, WSL, RCM, SD, Hekate, Atmosphere, Linux cache and persisted state.
- Regression tests cover dependency blocking, cycles, version constraints, rollback and catalog planning.

The release gate remains: Windows CI, vNext validation and regression tests must all be green on the final commit. Physical Switch/SD/RCM behaviour still requires a real Windows host and hardware.
