# Implementation Status

Last updated: 2026-09-13

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

- `scripts\static-check.ps1` passes: committed JSON, XML/XAML, PowerShell syntax, solution header, empty secret placeholder, required files and MVP source contracts.
- `scripts\semantic-check.ps1` compiled Protocol, Host Agent and WPF C# against the installed .NET 8 runtime assemblies. WPF generated-field stubs were used in this supplemental check because XAML build targets are SDK-owned.
- The compiled Protocol assembly executed 26 WOL, status-classification, CIDR and private-address assertions successfully.
- `dotnet build MyOnOff.sln -c Release --no-restore -p:OutputPath=C:\ccy\myonoff\.validation\sdk-build\solution\` builds Protocol, Host Agent, Desktop Controller and tests with 0 warnings and 0 errors, including normal Desktop Controller apphost generation.
- `dotnet test tests\MyOnOff.Protocol.Tests\MyOnOff.Protocol.Tests.csproj -c Release --no-build` passes 27 tests with 0 failures. Two tests specifically verify that an in-flight poll cannot discard a user action, cannot overwrite the action state, and cannot admit a duplicate action.
- `git diff --check` passes with no whitespace errors.
- `Plan.md` is byte-for-byte unchanged from commit `d1435a4` (Git object `d2fe8d1291852f6529aac1dff5d041dfac710f05`).
- Source scan found no cloud, OAuth, media-server, transcoding, UPnP or Dynamic DNS implementation markers.
- Manual source audit confirmed finite network timeouts, private-IPv4 target validation, Android permission re-check on resume, one-at-a-time power actions, user-action priority over routine polling and bounded transition polling.
- No SDK or build tool was installed during this fix.

## Not verified here

- A normal in-place `dotnet build MyOnOff.sln -c Release --no-restore` could not replace the Desktop Controller EXE/DLL because an existing Desktop Controller process was running from that output directory. The full solution passed using the isolated output path documented above. Close the running controller and repeat the normal in-place build before deployment.
- Android Gradle sync/test/APK build: Android SDK and Gradle are absent.
- The post-fix Desktop Controller OFFLINE-to-ON path has not been exercised on the real LAN. Immediate WOL/BOOTING feedback, the success/failure log entry, wake from sleep, wake from full shutdown, Agent recovery, SMB readiness and final ONLINE state still require manual verification.
- Existing Sleep, Shutdown and ONLINE/OFFLINE behavior must be rechecked once after the synchronization change.
- Android build and device behavior remain outside this Windows bug-fix scope.

These are verification gaps, not claimed passes. Follow `MyOnOff_Validation_Handoff.md` section 16 and `MANUAL_VALIDATION.md` for the real-device rerun.

## Manual results

Pre-fix real-device results are recorded in `MyOnOff_Validation_Handoff.md`. Post-fix Desktop Controller results are not yet recorded.
