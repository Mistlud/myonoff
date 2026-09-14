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

If a published executable is currently running and locking its output directory, leave it running and validate a separate destination instead:

```powershell
.\scripts\publish-windows.ps1 -ArtifactsDirectory '.validation\final-publish' -NoRestore
```

The default remains `artifacts/`. The script never stops a running application or deletes an existing directory.

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

## Application icon assets

The single approved visual source is:

```text
mds/assets/myonoff-icon-source.png
```

Do not redraw or replace it. Regenerate all derived assets from the repository root:

```powershell
.\scripts\generate-icons.ps1
```

The script writes:

- a 16, 24, 32, 48, 64, 128 and 256-pixel multi-resolution ICO to `src/MyOnOff.DesktopController/Assets/MyOnOff.ico`;
- conventional Android launcher PNGs to the `mipmap-mdpi` through `mipmap-xxxhdpi` resource directories.

The Desktop Controller project embeds the ICO in the executable and uses it for both WPF windows. A shortcut created from the published executable therefore uses the same icon. Android references `@mipmap/ic_launcher` for both its normal and round launcher icon declarations. A layered adaptive icon is intentionally omitted because separating the approved flattened artwork would require redesigning it.

## Android one-click APK build

The repository contains Gradle Wrapper 9.4.1. Use a compatible JDK; this project was verified with Android Studio's JBR 25. Android Studio does not need to be open and a global Gradle installation is not required.

Run either command from the repository root:

```powershell
.\scripts\build-android.ps1 -Configuration Debug
.\scripts\build-android.ps1 -Configuration Release
```

The helper resolves the repository root, invokes the committed `android/gradlew.bat`, stops on failure and copies the resulting APK to a stable path:

```text
artifacts/android/MyOnOff-<version>-debug.apk
artifacts/android/MyOnOff-<version>-release.apk
```

It prints the build type, `versionName` and final path after a successful build. To run Android unit tests separately:

```powershell
cd android
.\gradlew.bat testDebugUnitTest
```

Release assembly does not automatically download or run Android Lint. Run `\.\gradlew.bat lint` as a separate validation when the matching Android Lint dependencies are already available to Gradle.

## Create and preserve the Android release signing identity

Keystores, private keys and passwords must remain outside source control. The repository ignores `android/signing.properties`, `*.jks` and `*.keystore`.

1. Choose a private directory outside the repository. For example, create `%USERPROFILE%\.myonoff-signing` and ensure only your Windows account can access it.
2. Use `keytool` from the JDK already used by Android Studio or the command-line build to create the key:

```powershell
keytool -genkeypair -v `
  -keystore "$env:USERPROFILE\.myonoff-signing\myonoff-release.jks" `
  -alias myonoff `
  -keyalg RSA `
  -keysize 3072 `
  -validity 10000
```

3. Copy `android/signing.properties.example` to `android/signing.properties`.
4. Set `storeFile` to the absolute keystore path and fill `storePassword`, `keyAlias` and `keyPassword` locally. Use forward slashes in an absolute Windows path.
5. Verify the local configuration without printing credentials:

```powershell
cd android
.\gradlew.bat verifyReleaseSigningConfiguration
```

6. Return to the repository root and create the signed artifact:

```powershell
.\scripts\build-android.ps1 -Configuration Release
```

Back up the keystore, its store password, alias and key password in two secure locations. The same signing key must be retained for every future update. Losing it may prevent a future release from updating the installed application. Keep the application ID unchanged and increment `versionCode` before distributing a newer release.

Without a complete local signing file, the verification and release build stop with explicit guidance. Debug builds remain available.

## Move from a debug installation to release

An installed debug APK normally cannot be updated in place by a release APK because they use different signing identities.

1. Record the Host IP, MAC, ports, share name, expected hostname and Easy Mode preference that must be entered again. Handle the authentication token privately.
2. Uninstall the debug build.
3. Install `artifacts/android/MyOnOff-<version>-release.apk`.
4. Re-enter the local settings and run the normal ONLINE/WOL checks.
5. For later versions, sign with the same release key and use a higher `versionCode`; those builds can then update the existing release installation.

## Install and verify an Android APK personally

1. Build the debug or locally signed release APK.
2. Copy it to the phone or install it with `adb install -r <apk-path>`.
3. Allow installation from the selected source if Android requests it.
4. Confirm the approved icon is visible and not unacceptably cropped by the launcher.
5. Connect the phone to the same LAN Wi-Fi as the Host PC.
6. Configure the existing Host connection values.
7. Verify Easy Mode and the existing normal-mode ONLINE and WOL paths.

Internet access is not required for normal application use.
