# MyOnOff — Final Handoff & Additional Requirements

## 1. Purpose

This document continues from the state immediately after the latest Codex commit/push.

The core MyOnOff functionality is already implemented and has been validated on real hardware. The remaining work should be treated as a finishing pass, not as an opportunity to expand the product scope.

The final scope is limited to:

1. Android one-click APK build workflow
2. Android release APK + persistent signing workflow
3. Application icon asset generation and integration
4. Windows Desktop Controller visual redesign / cleanup

Do not add unrelated features.

---

## 2. Current Project State

### 2.1 Core architecture

The existing architecture remains unchanged:

- Windows Host Agent runs on the target host PC.
- Windows Desktop Controller controls the host from another Windows PC.
- Android Controller controls the same host from Android.
- Wake-on-LAN is used to power on the host.
- Host Agent API is used for Sleep and Shutdown.
- SMB remains responsible for media access.
- The app itself is not a media server.

The existing state model and control behavior must remain intact.

### 2.2 Previously validated backend behavior

The following were already validated on real hardware before the latest finishing pass:

- Host Agent starts automatically at Windows startup.
- Agent is reachable from another LAN machine.
- `/status` reports the host correctly.
- SMB readiness is detected.
- Authenticated Sleep works.
- Authenticated Shutdown works.
- Wake-on-LAN works from both Sleep and full Shutdown.
- Host Agent returns automatically after wake / reboot.
- Desktop Controller correctly transitions through OFFLINE → BOOTING → ONLINE.
- Desktop WOL race / concurrency bug was fixed and validated.
- Windows Desktop Controller core controls work.
- Android Controller core controls work.

Do not rework these systems unless a finishing change causes a regression.

---

## 3. Validation Completed After the Latest Commit / Push

The following validation occurred after the latest commit/push and should be considered the current verified baseline.

### 3.1 Windows build and tests

Release build completed successfully:

```powershell
dotnet build MyOnOff.sln -c Release
```

Result:

- 0 warnings
- 0 errors

Protocol / regression tests completed successfully:

- 30 passed
- 0 failed

The test count increased from the earlier 25-test baseline and includes the newer regression coverage.

### 3.2 Windows publish workflow

The existing Windows publish script was run successfully:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-windows.ps1
```

Expected publish artifacts were generated, including Desktop Controller outputs.

The published Desktop Controller executable was launched directly and worked without using `dotnet run`.

Saved settings were preserved correctly.

### 3.3 Android build environment

Android Studio was installed and the project under:

```text
android
```

was imported successfully.

Gradle project synchronization / import completed successfully.

A debug APK was built successfully and installed on a real Android device.

The Gradle Wrapper is also working from the command line:

```powershell
.\gradlew.bat assembleDebug
```

Therefore Android builds no longer depend on manually installed standalone Gradle.

### 3.4 Android real-device validation

The Android application was installed on a real phone and validated against the real host.

Verified:

- Host ONLINE detection
- Existing normal-mode controls
- Wake / control behavior
- Easy Mode
- Return from Easy Mode to normal mode

The Android normal-mode visual design is considered satisfactory and must not be redesigned as part of the remaining work.

### 3.5 Easy Mode validation

The following have now been manually verified and are considered complete:

- `Start app in Easy Mode` works correctly.
- The setting persists and launches the app into Easy Mode as expected.
- `UNKNOWN` is visually / behaviorally distinguished from confirmed `OFFLINE`.
- UNKNOWN does not incorrectly expose the large ON button as though the host were definitely offline.

These items do not require further feature work.

---

## 4. Remaining Final Scope

Only the items in this section should be treated as active finishing work.

---

# 5. Android One-Click APK Build

## 5.1 Goal

Provide a simple one-command or one-click Android build flow for future personal updates.

The user should not need to open Android Studio merely to produce an APK.

## 5.2 Required workflow

Add a repository-level build helper, preferably:

```text
scripts/build-android.ps1
```

A `.bat` wrapper may also be provided if useful, but PowerShell support is the primary requirement.

The script should:

1. Resolve the repository root reliably.
2. Enter or target the Android project directory.
3. Use the committed Gradle Wrapper.
4. Support at least:
   - debug APK
   - release APK
5. Fail clearly if the build fails.
6. Copy the final APK into a stable output directory:

```text
artifacts/android/
```

7. Print a concise success summary containing:
   - build type
   - application version
   - final APK path

Suggested usage:

```powershell
.\scripts\build-android.ps1 -Configuration Debug
.\scripts\build-android.ps1 -Configuration Release
```

Exact parameter naming may differ if there is a cleaner implementation.

## 5.3 Requirements

- Do not require Android Studio to be open.
- Do not require a global Gradle installation.
- Use `gradlew.bat`.
- Do not download or install unrelated tooling as part of the script.
- Keep the process predictable and repeatable.
- Preserve the existing Android project structure unless a change is necessary.

---

# 6. Android Release APK and Persistent Signing

## 6.1 Goal

Support a proper personal release APK so future versions can be installed as updates over the same release installation.

A stable signing identity should be established now rather than relying indefinitely on debug signing.

## 6.2 Signing requirements

Implement release signing support using a persistent user-owned keystore.

The following must never be committed:

- actual keystore file
- keystore password
- key password
- private signing credentials

Use one of the following clean approaches:

- ignored local properties file
- ignored dedicated signing properties file
- environment variables

Choose the approach that best fits the current Android project.

Provide a checked-in example / template file if useful, containing placeholders only.

Update `.gitignore` so signing secrets and local keystore files cannot be accidentally committed.

## 6.3 Documentation requirements

Document:

- how to generate the release keystore for the first time
- recommended location for storing it
- which values must be backed up
- how the build discovers the signing configuration
- how to build the release APK
- where the resulting APK is copied
- that the same signing key must be retained for future updates

Clearly warn that losing the release signing key may prevent future versions from updating the existing installed release build.

Do not commit actual passwords or signing keys into documentation.

## 6.4 Debug → Release transition

Document that an already-installed debug APK may not accept the release APK as an in-place update because the signing identities differ.

For the first transition, the user may need to:

1. note any settings that need to be re-entered
2. uninstall the debug build
3. install the release build
4. use release-signed builds for all subsequent updates

After that transition, future release APKs signed with the same key should be installable over the existing release installation, assuming the package ID remains unchanged and versioning is valid.

## 6.5 Release build acceptance

The final workflow should successfully produce a signed release APK using the user's local signing configuration.

The one-click Android build script should support this release path.

---

# 7. Application Icon Assets

## 7.1 Source design

A high-resolution square PNG icon design has been approved.

The design concept is:

- dark rounded-square app tile
- centered toggle switch
- switch shown in ON position
- bright contrasting switch
- restrained power-symbol motif
- no text

Do not redesign the icon.

Do not generate a different visual concept.

Use the provided approved PNG as the common visual source and derive platform assets from it.

## 7.2 Windows icon requirements

Generate a multi-resolution Windows icon file, for example:

```text
MyOnOff.ico
```

It should include appropriate sizes such as:

- 16×16
- 24×24
- 32×32
- 48×48
- 64×64
- 128×128
- 256×256

Use the same icon consistently where applicable:

- Desktop Controller executable icon
- Desktop Controller window icon
- Windows taskbar icon
- Start Menu shortcut
- Desktop shortcut if one is produced

The Host Agent does not need a separate newly designed brand identity unless the existing packaging specifically benefits from the same icon.

Avoid creating additional icon designs unless technically required.

## 7.3 Android icon requirements

Create Android launcher icon resources derived from the same approved source design.

At minimum, provide correctly scaled resources for the normal Android density buckets:

- mdpi
- hdpi
- xhdpi
- xxhdpi
- xxxhdpi

Place them in the appropriate Android resource directories and wire them into the application manifest / existing launcher configuration.

### Adaptive Icon

Use Android Adaptive Icon support if it can be implemented cleanly from the approved source without redesigning the artwork.

Preferred structure when practical:

```text
background
+
foreground symbol
```

If producing a proper foreground layer from the approved source would require inventing or significantly redesigning the image, do not expand the task unnecessarily. A correct conventional launcher icon implementation is preferable to scope creep.

A monochrome / themed icon may be added only if it can be derived cleanly and simply.

## 7.4 Asset generation workflow

Do not manually maintain many independent icon images if avoidable.

Prefer a repeatable asset-generation step or script that:

- takes the approved source PNG
- generates the required Windows sizes / ICO
- generates Android density assets
- writes them to known locations

The approved source asset should remain clearly identifiable as the canonical source.

Generated assets and source assets should not be confused.

## 7.5 Icon acceptance checks

Manually verify after build / install:

Windows:

- executable shows the icon
- running window shows the icon
- taskbar shows the icon
- published executable / shortcut uses the icon

Android:

- launcher shows the icon
- installed app uses the correct icon
- icon cropping / safe area is acceptable on the test device

---

# 8. Windows Desktop Controller Visual Redesign

## 8.1 Scope

The current Windows Desktop Controller is functionally correct but visually unsatisfactory.

This task is a presentation-layer redesign.

Do not change the underlying power behavior, WOL logic, polling model, state model, networking, authentication behavior, settings semantics, or concurrency rules merely for visual reasons.

Reuse the existing application behavior.

The Android normal-mode design is explicitly out of scope.

## 8.2 Overall design direction

The application should look like a compact, intentional Windows utility rather than:

- a developer test harness
- a generic enterprise dashboard
- a large settings form
- a collection of unrelated boxes

The visual language should be:

- compact
- calm
- modern
- readable
- restrained
- clearly hierarchical

Avoid decorative excess.

## 8.3 Window sizing and density

The main normal-mode window should be relatively compact.

It should not consume a large desktop area merely to display a few host controls.

Use spacing intentionally, but avoid oversized empty regions.

Prefer a vertically organized utility layout with a clear visual hierarchy.

The window should remain usable at normal Windows display scaling.

## 8.4 Information hierarchy

The most important information is the host's current state.

Recommended hierarchy:

1. Application / host identity
2. Current connection / power state
3. Primary available action
4. Secondary host information
5. Settings / advanced details

Do not give IP address, ports, SMB port, broadcast address, or similar technical data the same visual weight as the current host state.

Technical information should be available without dominating the interface.

## 8.5 Main status area

Create a visually strong but restrained status area.

The current state should be immediately understandable:

- ONLINE
- OFFLINE
- BOOTING
- UNKNOWN / CHECKING

State colors may be used, but keep the palette limited.

Do not fill the whole interface with saturated state colors.

Use state color primarily as an accent, indicator, badge, icon, or small status element.

The status area may include:

- state label
- brief human-readable supporting text
- optional host name

Avoid raw diagnostic text in the primary status area.

## 8.6 Power controls

Power actions must have clear hierarchy and risk distinction.

### When OFFLINE

ON / Wake should be the obvious primary action.

### When ONLINE

Sleep and Shutdown should be visually distinguishable.

Shutdown is the more destructive action and should not look identical to a harmless navigation button.

Do not make every button equally prominent.

Existing confirmation / safety behavior should be preserved.

## 8.7 Technical details

Normal Mode may continue to expose useful technical information, but organize it as secondary information.

Possible grouping:

- Host
  - hostname
  - IP
  - state
- Services
  - Agent
  - SMB
- Connection
  - Agent port
  - SMB port

Do not place every field inside its own heavy bordered card.

Prefer lightweight grouped rows or sections.

Do not turn the main window into a diagnostic dashboard unless diagnostic detail is explicitly opened.

## 8.8 Settings layout

Settings should be visually separated from everyday control.

Group related fields.

Suggested grouping:

### Host

- Host IP
- Expected hostname

### Wake-on-LAN

- MAC address
- Broadcast address
- WOL port

### Agent

- Agent port
- authentication token

### SMB

- SMB port
- share name

### Experience

- Start app in Easy Mode

Avoid one uninterrupted wall of labeled text boxes.

Existing values, validation behavior, and persistence must continue to work.

Do not expose the auth token more prominently than necessary.

## 8.9 Easy Mode

Easy Mode already works and its behavior is validated.

Do not redesign its behavior.

Visually, Easy Mode should remain intentionally sparse:

### UNKNOWN / CHECKING

- neutral checking state
- no ON button until OFFLINE is confirmed

### OFFLINE

- large, obvious ON control
- minimal supporting text

### BOOTING

- clear startup / booting indication
- subtle progress animation or activity indication is acceptable
- no unnecessary technical detail

### ONLINE

- clear confirmation that the host is ready
- no Sleep or Shutdown actions in Easy Mode

There should remain a small, unobtrusive way to return to Normal Mode.

Do not turn Easy Mode into another dashboard.

## 8.10 Typography

Use a small number of text levels:

- main state / key heading
- section heading
- normal value
- secondary / helper text

Avoid excessive font-size variation.

Avoid oversized title banners.

The interface should feel like a utility, not a marketing landing page.

## 8.11 Cards, borders, corners, and shadows

Use containers only where they improve grouping.

Avoid:

- a card around every row
- excessive rounded rectangles
- heavy drop shadows
- nested cards
- gradient-heavy surfaces
- glassmorphism
- decorative glow effects

Windows-native simplicity is preferable.

Rounded corners are acceptable but should not become the entire visual identity.

## 8.12 Icons

Use simple consistent icons only where they improve scanning.

Do not use emoji as interface icons.

Do not add decorative icons to every label.

The new MyOnOff application icon may be used for branding in the window if appropriate, but it should not consume excessive space.

## 8.13 Animation

Animation should be minimal.

Acceptable:

- subtle booting activity indicator
- restrained state transition

Avoid:

- bouncing
- pulsing large surfaces
- decorative transitions
- unnecessary motion

## 8.14 Color

Prefer a restrained neutral base with a small number of semantic accents.

For example:

- ONLINE: positive accent
- OFFLINE: muted / neutral
- BOOTING: active accent
- UNKNOWN: neutral checking state
- Shutdown: danger accent where appropriate

Do not create a rainbow state system.

The approved app icon does not require the entire UI to become neon green.

## 8.15 Functional preservation

The redesign must not regress:

- settings persistence
- Start app in Easy Mode
- UNKNOWN vs OFFLINE behavior
- WOL
- BOOTING state
- polling
- ONLINE readiness detection
- Sleep
- Shutdown
- authentication
- current concurrency protections
- logs
- existing tests

Prefer XAML / presentation changes over controller or networking changes.

If a code-behind or view-model change is needed purely to support presentation, keep it minimal and isolated.

---

# 9. Deferred / Lower-Priority Items

The following are intentionally not part of the immediate finishing pass.

## 9.1 Host Agent update-flow validation

A complete real-world validation of:

```text
new Host Agent build
→ deploy over existing install
→ restart scheduled task
→ preserve token / configuration
→ verify normal operation
```

may be performed later.

Do not block the current finish on this.

## 9.2 Full WAN-disconnected validation

The application is designed for local LAN operation and the relevant components have been tested locally.

A dedicated test with WAN / Internet connectivity physically or logically disabled may be performed later.

Do not block the current finish on this.

---

# 10. Explicit Non-Goals

Do not add any of the following during this finishing pass unless required to fix a regression:

- new media-server functionality
- remote Internet control
- cloud services
- user accounts
- push notifications
- new Android normal-mode redesign
- additional host power states
- new networking protocols
- automatic discovery systems
- installer redesign beyond what is necessary for current packaging
- unrelated refactors
- additional product features

The goal is to finish MyOnOff, not restart its design phase.

---

# 11. Recommended Implementation Order

Use this order unless the repository structure strongly suggests otherwise:

1. Preserve / record the current passing baseline.
2. Add Android release signing configuration support.
3. Add one-click Android build workflow for Debug and Release.
4. Add approved source icon asset to a clear canonical location.
5. Generate / integrate Windows icon assets.
6. Generate / integrate Android launcher icon assets.
7. Redesign Windows Desktop Controller presentation.
8. Run full .NET build and tests.
9. Run Windows publish script.
10. Build Android Debug APK.
11. Build signed Android Release APK.
12. Perform final real-device / real-host regression checks.

---

# 12. Final Acceptance Criteria

The finishing pass is complete when all of the following are true.

## Windows

- `dotnet build MyOnOff.sln -c Release` succeeds.
- Existing automated tests pass.
- `scripts/publish-windows.ps1` succeeds.
- Desktop Controller launches from its published executable.
- Existing settings still load correctly.
- WOL / ONLINE / Sleep / Shutdown behavior is unchanged.
- Easy Mode still behaves correctly.
- Start-in-Easy-Mode still works.
- UNKNOWN remains distinct from confirmed OFFLINE.
- New Windows visual design is applied without functional regression.
- Executable, window, and taskbar show the approved MyOnOff icon.

## Android

- Gradle Wrapper remains functional.
- Debug APK can be produced from the command line.
- Release APK can be produced from the command line using local signing configuration.
- One-click build script supports Debug and Release.
- Output APK is copied to `artifacts/android/`.
- Script reports version and output path.
- Signing secrets / keystore are not committed.
- Release signing setup is documented.
- Approved MyOnOff icon appears correctly in the launcher.
- Existing normal mode remains visually and functionally intact.
- Easy Mode remains functional.
- Release APK is installed and tested on a real device.
- After the initial debug-to-release transition, a subsequent release build can be used as the basis for future in-place updates using the same signing key.

## Repository

- No credentials or signing secrets are committed.
- Generated / source icon asset roles are clear.
- Documentation explains the final Windows and Android build flows.
- No unrelated feature expansion is introduced.

---

# 13. Final Regression Pass

After all finishing work is complete, perform one final end-to-end real-hardware regression.

At minimum:

```text
OFFLINE
→ ON / WOL
→ BOOTING
→ ONLINE
→ Sleep
→ ON / WOL
→ ONLINE
→ Shutdown
→ ON / WOL
→ ONLINE
```

Run this once from the Windows Desktop Controller.

Run the most important corresponding ON / ONLINE control path once from the Android release build.

If these checks pass and the build / packaging acceptance criteria above are satisfied, the project can be considered finished for the current scope.
