# Packaging and Local Release

`Plan.md` remains the source of truth for the original MVP. This guide covers local packaging after the MVP was validated.

Generated output is written below `artifacts/`, which is ignored by Git.

## Windows prerequisites

- Build machine: Windows with .NET SDK 8.0.x.
- Framework-dependent Desktop Controller target: Windows x64 with the .NET 8 Desktop Runtime x64.
- Self-contained targets: Windows x64; no separately installed .NET runtime is required.

## Publish Windows applications

From the repository root, publish every supported Windows artifact:

```powershell
.\scripts\publish-windows.ps1
```

Publish one target when iterating:

```powershell
.\scripts\publish-windows.ps1 -Target HostAgent
.\scripts\publish-windows.ps1 -Target DesktopFrameworkDependent
.\scripts\publish-windows.ps1 -Target DesktopSelfContained
```

Use `-NoRestore` only after the required `win-x64` runtime packs have already been restored.

Expected output:

```text
artifacts/
  host-agent-selfcontained/MyOnOff.HostAgent.exe
  desktop-controller/MyOnOff.DesktopController.exe
  desktop-controller-selfcontained/MyOnOff.DesktopController.exe
```

The script does not delete an existing artifact directory. Remove or archive an old output directory before creating a clean release bundle.

## Run the Desktop Controller

1. Choose `desktop-controller` when the target PC has the .NET 8 Desktop Runtime x64.
2. Otherwise choose `desktop-controller-selfcontained`.
3. Copy the complete selected directory to its permanent location.
4. Double-click `MyOnOff.DesktopController.exe`.
5. Optionally right-click the executable to create a desktop shortcut or pin it to Start/taskbar.

Settings remain in `%LocalAppData%\MyOnOff\controller-settings.json`, independent of the publish directory. The authentication token is not embedded in the executable or publish output. Confirm that Host IP, MAC, broadcast address, ports, share, hostname, token and the Easy Mode startup preference remain intact after switching from a development build.

## Update the Host Agent safely

The Host PC uses the self-contained `host-agent-selfcontained` output and must not be assumed to have .NET installed.

1. Publish a new Host Agent directory on the development PC.
2. On the Host PC, stop or disable the MyOnOff Scheduled Task.
3. Confirm that `MyOnOff.HostAgent.exe` is no longer running.
4. Back up the current deployment directory or rename it for rollback.
5. Copy the complete new publish directory into the configured deployment location.
6. Preserve the machine-level `MYONOFF_AGENT_TOKEN` and any local `appsettings.Local.json`; never copy a real token into source control.
7. Re-enable and start the Scheduled Task.
8. Verify `/status`, the expected hostname and `smbReady = true` from another LAN PC.

Do not overwrite binaries while the Agent is running.

## Android Gradle Wrapper

The repository contains Gradle Wrapper 9.4.1. Use a compatible JDK; this project was verified with Android Studio's JBR 25.

```powershell
cd android
.\gradlew.bat testDebugUnitTest assembleDebug
```

The debug APK is written to:

```text
android/app/build/outputs/apk/debug/app-debug.apk
```

## Configure Android release signing

Keystores, private keys and passwords must remain outside source control.

1. Copy `android/signing.properties.example` to `android/signing.properties`.
2. Store the keystore in a private location outside the repository when practical.
3. Fill `storeFile`, `storePassword`, `keyAlias` and `keyPassword` locally.
4. Verify the local configuration:

```powershell
cd android
.\gradlew.bat verifyReleaseSigningConfiguration
```

5. Build the signed release APK:

```powershell
.\gradlew.bat assembleRelease
```

The release APK is expected at:

```text
android/app/build/outputs/apk/release/app-release.apk
```

Without a complete local signing file, the verification and release build stop with an explicit message. Debug builds remain available.

## Install an Android APK personally

1. Build the debug or locally signed release APK.
2. Copy it to the phone or install it with `adb install -r <apk-path>`.
3. Allow installation from the selected source if Android requests it.
4. Connect the phone to the same LAN Wi-Fi as the Host PC.
5. Configure the existing Host connection values.
6. Verify ONLINE and the complete power-control regression sequence.

Internet access is not required for normal application use.
