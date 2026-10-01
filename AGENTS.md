# Repository guidance

2FAGuard is a Windows TOTP authenticator. This fork includes MTG changes while retaining upstream conventions. Read `README.md`, `CONTRIBUTING.md`, and `SECURITY.md` before implementation; discuss breaking changes or new features through the upstream contribution process where applicable.

## Code map

The solution is `2fa-guard.sln`. `Guard.WPF/` is the desktop UI, `Guard.CLI/` is the command-line app, and `Guard.Test/` holds tests. Translation contributions belong in `Guard.WPF/Resources/`. Inspect the relevant project and shared code before changing token storage, import/export, Windows Hello, or policy behavior. Advanced administrative settings are documented in `docs/advanced.md`.

## Verification

Use Windows and the .NET 10 SDK, matching `.github/workflows/test.yml`. From the repository root:

```sh
dotnet test Guard.Test
dotnet build Guard.CLI --configuration Release
dotnet build Guard.WPF --configuration Release
```

CodeQL also builds `Guard.WPF` on Windows. These source-defined checks do not prove interactive Windows Hello or installer behavior; report which manual flows were exercised when changing them.

## Sensitive state

Use disposable test tokens and app-data directories. Never inspect, export, reset, or delete an operator's real authenticator data as a development shortcut. Preserve encrypted backup compatibility, token confidentiality, and configured export/password restrictions when changing these surfaces. Treat app-data deletion described in the README as a destructive reset, not a troubleshooting default.

The opt-in CLI desktop bridge allows same-user processes to request codes from an unlocked desktop session. Preserve its default-disabled policy and audit process identity/time without logging generated codes. Never share an app-data path between users.

Retain upstream attribution, license, translations, and security reporting conventions. Keep MTG-specific behavior explicit in the diff and documentation; avoid unrelated upstream rewrites.
