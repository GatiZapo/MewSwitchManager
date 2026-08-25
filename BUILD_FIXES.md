# Build and architecture fixes — 0.2 ALPHA

The recovered prototype had two separate project trees. One of them was a minimal CI experiment and did not contain a `.csproj` at repository root, which caused the GitHub Action error:

`MSB1003: Specify a project or solution file.`

0.2 removes that duplicate tree and keeps one canonical `MewSwitchManager.csproj` at the repository root.

Additional fixes:

- GitHub Actions now publishes x64, ARM64 and x86.
- The project is self-contained and single-file.
- SharpCompress is bundled so 7z extraction does not depend on Windows `tar.exe`.
- USB writing targets the created Windows volume, matching the Switchroot USB procedure.
- Two target identity checks surround the destructive storage transition.
- Partial Linux downloads are preserved and resumed where HTTP Range is supported.
- The UI is DPI-aware and responsive instead of relying on a fixed desktop geometry.
