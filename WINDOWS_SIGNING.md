# MewNX Windows signing

MewNX is prepared for Authenticode signing, but the repository does not contain a private signing certificate.

## Required GitHub Actions secrets

Configure these repository secrets before a release:

- `WINDOWS_SIGNING_CERT_BASE64` — base64 of the `.pfx` certificate.
- `WINDOWS_SIGNING_CERT_PASSWORD` — password for the PFX.

The Windows CI signs both `MewNX.exe` and `MewNX-Setup-x64.exe` with SHA-256 and a trusted RFC 3161 timestamp when both secrets are present. If the secrets are absent, CI intentionally publishes unsigned artifacts rather than failing development builds.

## Windows Smart App Control

Smart App Control and antivirus reputation cannot be disabled or guaranteed by application code. A real code-signing certificate issued by a trusted CA is required for a signed release to build reputation. Signing is therefore part of the release/distribution process, not a runtime workaround.

For the public release, use a certificate whose publisher identity matches the MewNX release identity and keep the private key only in GitHub Actions secrets or an equivalent secure signing service.
