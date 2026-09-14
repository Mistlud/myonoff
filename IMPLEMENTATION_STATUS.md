# Implementation Status

Last updated: 2026-09-14

## Final finishing pass

`mds/MyOnOff_Final_Handoff_Additional_Requirements.md` is the current verified baseline and final scope document. The finishing implementation is limited to Android APK packaging/signing, approved icon integration and Windows Desktop Controller presentation.

- Added `scripts/build-android.ps1` for Debug and Release APK builds through the committed Gradle Wrapper. It copies successful output to `artifacts/android/` and reports the configuration, version and final path.
- Retained the existing ignored `android/signing.properties` workflow and expanded the first-time key generation, backup, debug-to-release transition and future update documentation.
- Added the approved `mds/assets/myonoff-icon-source.png` as the canonical icon source and a repeatable `scripts/generate-icons.ps1` derivation workflow.
- Embedded a seven-size ICO in the Desktop Controller executable and windows, and wired five conventional Android density resources into the manifest.
- Reworked only the Windows XAML presentation: compact status-first normal mode, differentiated power actions, secondary connection details and grouped settings. Controller/networking code and Android normal-mode UI code were not changed.

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

- `dotnet build MyOnOff.sln -c Release --no-restore` succeeds with 0 warnings and 0 errors after the final XAML and icon changes.
- `dotnet test MyOnOff.sln -c Release --no-restore` passes all 30 tests with 0 failures.
- `scripts/publish-windows.ps1 -ArtifactsDirectory '.validation\final-publish' -NoRestore` produces the Host Agent self-contained and both Desktop Controller publish variants. The alternate directory was used because the user's existing `artifacts/desktop-controller` executable was running and locking its DLL; that process was not stopped.
- The published Desktop Controller executable exposes an associated icon that can be extracted successfully from the EXE.
- `scripts/build-android.ps1 -Configuration Debug` succeeds through the committed Gradle Wrapper, reports version `0.1.0` and copies `artifacts/android/MyOnOff-0.1.0-debug.apk`.
- Android `testDebugUnitTest` passes all 6 tests with 0 failures.
- The generated ICO contains 16, 24, 32, 48, 64, 128 and 256-pixel entries. Android launcher PNGs have the expected 48, 72, 96, 144 and 192-pixel dimensions.
- With no local signing file, the one-click Release path stops before Gradle with actionable setup guidance and does not expose credentials.
- Release assembly is not coupled to Android Lint dependency download; Android Lint remains a separate validation command when its matching tooling is available.
- With the user-owned local signing configuration, `scripts/build-android.ps1 -Configuration Release` succeeds and copies `artifacts/android/MyOnOff-0.1.0-release.apk`. Android SDK `apksigner` verifies one signer using APK Signature Scheme v2, and the copied APK hash matches the Gradle output.
- `android/signing.properties` is ignored and untracked. The configured keystore exists outside the repository, no `.jks` or `.keystore` is tracked, and no signing secret was staged or printed during validation.
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

- The signed Release APK has not yet been installed on the Android device. The debug-to-release transition and a later same-key, higher-versionCode in-place update remain user checks.
- The final post-presentation real-LAN OFFLINE -> ON -> BOOTING -> ONLINE -> Sleep/Shutdown regression remains a manual acceptance check.
- Further Windows normal-mode visual refinement is explicitly deferred and does not block this final commit.
- The deferred Host Agent deployment-update flow and WAN-disconnected check remain out of the current critical path as specified by the final handoff.

These are verification gaps, not claimed passes. Follow the Final finishing-pass validation section in `MANUAL_VALIDATION.md` for the real-device rerun.

## Manual results

The proven MVP and post-MVP device results supplied by the user are recorded in `mds/Validation_Status.md`, `mds/MyOnOff_Final_Handoff_Additional_Requirements.md` and `mds/MyOnOff_Latest_Validation_Notes.txt`. The latest notes confirm the default Windows publish, published launch, settings persistence, Easy Mode, Windows icons, Android Debug install, Android launcher icon and existing Android behavior. The remaining finishing-pass checks are listed in `MANUAL_VALIDATION.md`.
