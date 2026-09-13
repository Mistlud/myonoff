# Implementation Status

Last updated: 2026-09-13

## Post-MVP UX and packaging work

- Added Windows and Android Easy Mode as presentation layers over the existing polling, state and WOL paths.
- UNKNOWN remains a neutral checking presentation; the large Easy Mode ON button appears only for a classified OFFLINE state.
- Added a persisted Start app in Easy Mode preference on both platforms.
- Refined the Windows normal-mode visual hierarchy without removing status details or power controls.
- Added standard Windows publish targets, a Gradle 9.4.1 Wrapper, local Android release-signing configuration and packaging documentation.
- Source/build validation results and remaining real-device checks are recorded below; this section does not replace the proven MVP evidence in `mds/Validation_Status.md`.

## Implemented

- Shared versioned API contracts, deterministic state classifier, standard 102-byte WOL packet builder and IPv4 CIDR matcher.
- Windows Host Agent with `GET /status`, authenticated `POST /sleep`, authenticated `POST /shutdown`, local SMB readiness probe, finite timeouts, LAN subnet middleware, fixed-time token comparison, bounded one-command power queue and file logging.
- WPF desktop controller with periodic multi-signal status, OFFLINE/BOOTING/ONLINE/UNKNOWN and transitional states, WOL burst, authenticated Sleep/Shutdown, duplicate-action suppression, shutdown confirmation, local settings and diagnostics.
- Desktop user actions and routine polling now use separate coordination gates. A user action can begin while an older poll is in flight, and that poll's result or error is suppressed while the action owns the UI state.
- Android Compose controller with direct WOL, Agent/SMB polling, matching state semantics, three primary power controls, local settings, Android 17 local-network runtime permission handling and visible LAN errors.
- .NET and Android unit-test sources for WOL/state/CIDR logic.
- Host firewall and startup-task scripts with `-WhatIf` support.
- Build, security and all 13 real-LAN Acceptance Criteria procedures in `MANUAL_VALIDATION.md`.

## Verified in the current environment

- `scripts\static-check.ps1` passes: JSON, XML/XAML, PowerShell syntax, solution header, empty secret placeholder, required files and source contracts.
- `scripts\semantic-check.ps1` compiles Protocol, Host Agent and WPF C# and executes 26 Protocol assertions.
- An isolated-output Release build of `MyOnOff.sln` builds Protocol, Host Agent, Desktop Controller and tests with 0 warnings and 0 errors.
- `dotnet test tests\MyOnOff.Protocol.Tests\MyOnOff.Protocol.Tests.csproj -c Release --no-restore` passes 30 tests with 0 failures. Existing action/polling coordination tests still pass, and three new tests fix the Easy Mode UNKNOWN/OFFLINE/BOOTING presentation contract.
- Android `testDebugUnitTest assembleDebug` succeeds with the existing Gradle 9.4.1 and Android Studio JBR. All 6 Android unit tests pass, and the debug APK is produced at `android/app/build/outputs/apk/debug/app-debug.apk`.
- The checked-in Gradle Wrapper 9.4.1 runs successfully. With no local signing file, `verifyReleaseSigningConfiguration` fails as designed with actionable setup guidance and does not expose a secret.
- `scripts\publish-windows.ps1` successfully produces Host Agent self-contained, Desktop framework-dependent and Desktop self-contained executables in the documented artifact directories.
- `git diff --check` passes with no whitespace errors.
- `Plan.md` is byte-for-byte unchanged from commit `d1435a4` (Git object `d2fe8d1291852f6529aac1dff5d041dfac710f05`).
- Existing Host Agent, network clients, WOL senders, status classifiers and action/polling coordination code were not replaced by the UX work.
- No SDK, Android SDK, Gradle or other development tool was installed during this work.

## Not verified here

- Desktop normal-mode visual layout and Easy Mode state transitions have not been inspected interactively in the new build.
- Desktop and Android real-LAN OFFLINE -> ON -> BOOTING -> ONLINE, Sleep and Shutdown regressions must be rerun after the presentation changes.
- Android Easy Mode and its startup preference have not been installed and checked on a real phone.
- A real signed release APK was intentionally not built because no private local signing configuration was supplied.
- Published Desktop executables have not been manually launched, and the documented Host Agent update procedure has not been exercised against the deployed Scheduled Task.

These are verification gaps, not claimed passes. Follow the Post-MVP section in `MANUAL_VALIDATION.md` for the real-device rerun.

## Manual results

Proven MVP real-device results are recorded in `mds/Validation_Status.md`. Post-MVP UX and packaging results are not yet recorded.
