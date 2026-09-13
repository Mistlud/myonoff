# Implementation Status

Last updated: 2026-09-13

## Implemented

- Shared versioned API contracts, deterministic state classifier, standard 102-byte WOL packet builder and IPv4 CIDR matcher.
- Windows Host Agent with `GET /status`, authenticated `POST /sleep`, authenticated `POST /shutdown`, local SMB readiness probe, finite timeouts, LAN subnet middleware, fixed-time token comparison, bounded one-command power queue and file logging.
- WPF desktop controller with periodic multi-signal status, OFFLINE/BOOTING/ONLINE/UNKNOWN and transitional states, WOL burst, authenticated Sleep/Shutdown, duplicate-action suppression, shutdown confirmation, local settings and diagnostics.
- Android Compose controller with direct WOL, Agent/SMB polling, matching state semantics, three primary power controls, local settings, Android 17 local-network runtime permission handling and visible LAN errors.
- .NET and Android unit-test sources for WOL/state/CIDR logic.
- Host firewall and startup-task scripts with `-WhatIf` support.
- Build, security and all 13 real-LAN Acceptance Criteria procedures in `MANUAL_VALIDATION.md`.

## Verified in the current environment

- `scripts\static-check.ps1` passes: committed JSON, XML/XAML, PowerShell syntax, solution header, empty secret placeholder, required files and MVP source contracts.
- Existing Visual Studio Build Tools Roslyn `csi.exe` parsed all 29 C# source and validation-helper files with zero syntax errors via `scripts\roslyn-syntax-check.csx`; this is syntax evidence, not a referenced .NET SDK build.
- `scripts\semantic-check.ps1` compiled Protocol and WPF C# against the installed .NET 8 runtime assemblies. WPF generated-field stubs were used because XAML build targets are SDK-owned.
- The compiled Protocol assembly executed 26 WOL, status-classification, CIDR and private-address assertions successfully.
- `git diff --check` passes with no whitespace errors.
- `Plan.md` is byte-for-byte unchanged from commit `d1435a4` (Git object `d2fe8d1291852f6529aac1dff5d041dfac710f05`).
- Source scan found no cloud, OAuth, media-server, transcoding, UPnP or Dynamic DNS implementation markers.
- Manual source audit confirmed finite network timeouts, private-IPv4 target validation, Android permission re-check on resume, one-at-a-time power actions and bounded transition polling.
- No SDK or build tool was installed, per user direction.

## Not verified here

- .NET restore/build/test/publish: .NET SDK is absent.
- Host Agent semantic compilation: ASP.NET Core runtime/reference assemblies are absent; its C# syntax is verified but SDK compilation remains required.
- Android Gradle sync/test/APK build: Android SDK and Gradle are absent.
- WPF visual layout and interaction: not launched.
- Android UI/runtime permission behavior: no Android device/emulator.
- Host Agent binding, firewall and boot-start task: not run on Host PC.
- Real WOL, sleep, shutdown, SMB readiness and WAN-disconnected operation: require the designated LAN and devices.

These are verification gaps, not claimed passes. Follow `MANUAL_VALIDATION.md` and append the results below.

## Manual results

No user-run results recorded yet.
