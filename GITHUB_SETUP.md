# GitHub setup

1. Create or use the repository `MewSwitchManager`.
2. Upload the contents of this project to the `main` branch.
3. Open **Actions**.
4. The workflow is `.github/workflows/windows-build.yml`.
5. A normal push builds x64, ARM64 and x86 artifacts.
6. A tag such as `v0.2.0-alpha.1` additionally creates a GitHub Release.

No manual `.csproj` discovery is required: the project file is at the repository root.
