# CI Build

GitHub Actions publishes three self-contained Windows variants:

- `win-x64`
- `win-arm64`
- `win-x86`

Each job:

1. cleans `bin`, `obj` and `dist`
2. restores NuGet packages
3. builds Release
4. publishes a self-contained single-file executable
5. verifies the EXE and configuration exist
6. calculates SHA-256
7. packages a ZIP
8. uploads the architecture-specific artifact

A `v*` tag creates a GitHub Release containing all three ZIPs.
