# MyOnOff Packaging and Release Requirements

## Purpose

This document defines additional packaging, execution, and release requirements.

`Plan.md` remains the original source of truth for architecture and MVP scope.

The current system is already functionally working.

The goal here is to remove development-only launch friction and create predictable build artifacts.

## Windows Desktop Controller Goal

Routine use must not require:

```powershell
dotnet run --project .\src\MyOnOff.DesktopController\MyOnOff.DesktopController.csproj -c Release
```

That command remains useful for development only.

Normal use should be:

```text
Double-click MyOnOff.DesktopController.exe
```

## Windows Desktop Framework-Dependent Publish

Support a standard Windows x64 framework-dependent publish for machines that already have the required .NET runtime.

Example target artifact structure:

```text
artifacts/
  desktop-controller/
    MyOnOff.DesktopController.exe
    ...
```

The exact required .NET runtime version must be documented.

## Windows Desktop Self-Contained Publish

Also support an optional Windows x64 self-contained Desktop Controller publish.

Purpose:

- Copy to another Windows PC without separately installing .NET.
- Match the deployment style already used successfully for Host Agent.

Example output:

```text
artifacts/
  desktop-controller-selfcontained/
    MyOnOff.DesktopController.exe
    ...
```

Do not require self-contained deployment for every machine.

Provide it as an explicit option.

## Host Agent Publish

Preserve the already proven Host Agent self-contained publish.

Expected style:

```powershell
dotnet publish .\src\MyOnOff.HostAgent\MyOnOff.HostAgent.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .\artifacts\host-agent-selfcontained
```

Do not regress the existing Host PC deployment model.

Host PC should not be assumed to have .NET installed.

## Windows Publish Script

Add a simple repository script, preferably:

```text
scripts\publish-windows.ps1
```

The script should build or publish the relevant Windows components into predictable artifact folders.

At minimum it should support:

- Host Agent self-contained publish
- Desktop Controller framework-dependent publish
- Desktop Controller self-contained publish, either by default or via a clear option

Keep the script simple.

Do not build a custom packaging framework.

## Artifact Layout

Use a predictable repository-local structure.

Suggested:

```text
artifacts/
  host-agent-selfcontained/
  desktop-controller/
  desktop-controller-selfcontained/
  android/
```

Generated artifacts should not be scattered across source directories.

Document whether `artifacts/` is ignored by Git.

## Desktop Shortcut and Launch Documentation

Document a simple real-user flow after publishing:

```text
1. Publish the Desktop Controller
2. Locate MyOnOff.DesktopController.exe
3. Run it directly
4. Optionally create a desktop shortcut
5. Optionally pin it to Start or taskbar
```

Do not require users to know the repository layout during normal use.

A dedicated installer is not required for the initial release unless it is very easy to add.

## Desktop Configuration Persistence

Existing controller settings should continue to persist normally after switching from `dotnet run` to published executable use.

Verify that publish mode does not break:

- Host IP
- Host MAC
- Broadcast IP
- WOL port
- Agent port
- SMB port
- SMB share
- Expected hostname
- Authentication token
- Easy Mode preference if added

Do not store secrets inside the published binary.

## Host Agent Update Documentation

Document the existing Host Agent update process.

At minimum describe:

```text
1. Publish a new self-contained Host Agent
2. Stop or disable the scheduled Host Agent task if required
3. Replace the deployment directory contents safely
4. Re-enable or restart the task
5. Verify /status
6. Verify smbReady
```

Do not silently overwrite a running Agent in a way that can corrupt deployment files.

## Android Debug Build

Document how to build the debug APK.

Known current state:

- Android Studio project import succeeds
- Android build succeeds
- `app-debug.apk` has been produced and installed on a real phone

Document the expected output path.

Example:

```text
android/app/build/outputs/apk/debug/app-debug.apk
```

If actual project output differs, document the real path.

## Android Release APK

Add or document a release APK build flow.

Goals:

- Produce a release APK suitable for normal personal installation.
- Keep signing secrets outside source control.
- Do not commit keystores, passwords, or private signing values.

Document:

- Where signing configuration should live
- How local signing values are supplied
- How to build the release APK
- Where the resulting APK is written

Do not add cloud signing or external release infrastructure.

## Android Signing Security

Explicitly ensure:

```text
Keystore files must not be committed.
Signing passwords must not be committed.
Private keys must not be committed.
```

Add appropriate `.gitignore` entries if needed.

Provide an example or template configuration without secret values if useful.

## Gradle Wrapper

The repository should have a normal, usable Gradle Wrapper if practical.

Goal:

```text
android\gradlew
android\gradlew.bat
android\gradle\wrapper\gradle-wrapper.properties
android\gradle\wrapper\gradle-wrapper.jar
```

This allows repeatable command-line builds without depending on a globally installed Gradle.

Normalize the wrapper only if it can be done cleanly with the current Android project.

Do not spend excessive time rebuilding the Android project around this requirement.

## Android Command-Line Build Documentation

After Gradle Wrapper normalization, document commands such as:

```powershell
cd android
.\gradlew.bat assembleDebug
```

and, once release signing is configured:

```powershell
.\gradlew.bat assembleRelease
```

Android Studio remains supported, but routine APK creation should not require navigating IDE menus.

## Android Installation Documentation

Document a simple personal-install flow:

```text
1. Build APK
2. Copy APK to phone or use ADB
3. Allow installation from the chosen source if Android requests it
4. Install APK
5. Connect phone to the same LAN Wi-Fi
6. Configure Host connection values
7. Verify ONLINE
```

Do not imply that internet access is required.

The application is intended to control the Host PC while the phone is on the same LAN.

## Version Information

If practical, expose a simple application version in both Windows Desktop and Android builds.

This may be shown in:

- About dialog
- Settings screen
- Build metadata

Keep versioning lightweight.

Do not introduce a complex release-management system.

## Build Verification

After packaging changes, perform available build checks.

Windows:

```powershell
dotnet build MyOnOff.sln -c Release
dotnet test tests\MyOnOff.Protocol.Tests\MyOnOff.Protocol.Tests.csproj -c Release
```

Verify published Desktop Controller starts successfully.

Verify published Host Agent still starts successfully.

Android:

- Gradle sync succeeds
- Debug APK builds
- Release APK builds once local signing is configured

Real-device regression should still be performed manually.

## Regression Requirements

Packaging work must not alter proven behavior.

Re-check after relevant changes:

```text
Desktop:
ONLINE/OFFLINE
ON
BOOTING
Sleep
Shutdown

Android:
ONLINE/OFFLINE
ON
Sleep
Shutdown

Host Agent:
Auto-start
/status
smbReady
/sleep
/shutdown
```

## Out of Scope

Do not turn this packaging task into:

- MSI installer project unless clearly justified later
- Microsoft Store packaging
- Google Play publication
- CI/CD release infrastructure
- Cloud artifact hosting
- Automatic online updates
- Code-signing service integration
- External package manager publication

The immediate goal is simple, repeatable local packaging and installation.

## Codex Implementation Guidance

Use this document together with:

```text
Plan.md
Validation_Status.md
UX_Requirements.md
```

Order of priorities:

```text
1. Preserve all already verified functionality.
2. Add Easy Mode and Desktop UI polish.
3. Make Desktop Controller runnable as a normal published executable.
4. Standardize Windows publish outputs.
5. Normalize Android build and APK release workflow.
6. Document installation and update steps.
```

Keep changes focused.

Do not re-architect the working power-control backend without a reproduced need.
