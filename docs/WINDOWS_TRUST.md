# Windows trust and Smart App Control

MewNX is currently distributed as a self-contained Windows executable and installer. Unsigned development builds can trigger Windows Smart App Control/SmartScreen on systems that enforce application control.

## Release policy

For public releases, MewNX should use a **trusted RSA code-signing identity** and sign every executable that users can execute, including:

- `MewNX.exe`
- the Inno Setup installer
- the uninstaller/bootstrap executables produced by the installer
- any additional native executable shipped by MewNX

Signing must happen **after publishing/building and before packaging/release**. Nothing may modify a signed binary afterwards.

Microsoft currently recommends Artifact Signing (formerly Trusted Signing) for non-Store distribution. Smart App Control accepts RSA-based signatures from certificates in the Microsoft Trusted Root Program. A new signed application can still show a reputation warning until Microsoft has enough reputation for the publisher/hash; signing is necessary but cannot guarantee that every fresh binary will immediately have zero SmartScreen prompts. See Microsoft's current guidance before each release.

## CI integration

The repository deliberately does not contain a private certificate, password, Azure credential, or signing token. Those are secrets and must never be committed.

Once a trusted signing service/certificate is provisioned, the release workflow must insert a signing stage after `dotnet publish` and before ZIP/installer creation. The release job should then verify the Authenticode signature and publisher identity before publishing artifacts.

A self-signed certificate is **not** an acceptable production workaround for Smart App Control.
