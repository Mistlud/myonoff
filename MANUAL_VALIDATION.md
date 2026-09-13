# Manual Build and Acceptance Validation

`Plan.md` remains the source of truth. This checklist covers the build and real-device evidence that cannot be produced in the current authoring environment.

## 1. Build gate

On a Windows development PC with .NET 8 SDK:

```powershell
dotnet restore MyOnOff.sln
dotnet build MyOnOff.sln -c Release --no-restore
dotnet test tests\MyOnOff.Protocol.Tests\MyOnOff.Protocol.Tests.csproj -c Release --no-build
dotnet publish src\MyOnOff.HostAgent\MyOnOff.HostAgent.csproj -c Release -r win-x64 --self-contained false -o artifacts\host-agent
dotnet publish src\MyOnOff.DesktopController\MyOnOff.DesktopController.csproj -c Release -r win-x64 --self-contained false -o artifacts\desktop-controller
```

Pass condition: zero build errors and all protocol tests pass.

On a machine with Android Studio, JDK 17, Android SDK 37 and Gradle 9.4.1:

```powershell
cd android
gradle test assembleDebug
```

Pass condition: Kotlin unit tests pass and `app\build\outputs\apk\debug\app-debug.apk` is created. Install that APK on the test phone.

## 2. Host Agent setup

1. Place the published Agent in a stable folder on Host PC.
2. Set a long random machine-level `MYONOFF_AGENT_TOKEN`, or create a protected `appsettings.Local.json` next to the Agent.
3. Run `scripts\configure-host-firewall.ps1 -WhatIf`, review the LAN subnet and port, then run it elevated without `-WhatIf`.
4. Run `scripts\install-host-startup.ps1 -PublishedAgentPath '<full exe path>' -WhatIf`, review it, then register the task.
5. Start the scheduled task and run `scripts\check-agent-status.ps1` locally and from another LAN PC.
6. Reboot Host PC without logging in. Verify TCP 5055 and `GET /status` become available; this proves startup independence.
7. Inspect `%ProgramData%\MyOnOff\host-agent.log` for startup/listening entries. Confirm no authentication token is present.

## 3. Security negative tests

Perform these before allowing power commands:

1. Send `POST /sleep` without an Authorization header. Expected: HTTP 401 and no sleep.
2. Send it with an incorrect Bearer token. Expected: HTTP 401 and no sleep.
3. Temporarily clear the Agent token and restart it. Expected: HTTP 503 for control commands.
4. From an address outside `192.168.219.0/24`, if a safely isolated test route exists, call `/status`. Expected: HTTP 403. Do not open a router port to perform this test.
5. Press Sleep or Shutdown repeatedly in each controller. Expected: disabled/busy UI and no duplicate accepted commands.

## 4. Desktop and Android configuration

On both controllers, configure the actual wired MAC address, `192.168.219.104`, `192.168.219.255`, Agent port 5055, SMB port 445, share `domination`, the shared token, and the actual Host PC hostname.

On Android 17 or later, grant the Local network permission. Deny it once first: expected result is a visible permission message with LAN actions disabled, not a crash.

## 5. Acceptance Criteria evidence

Record timestamps or short notes in `IMPLEMENTATION_STATUS.md` for each row.

| # | Test | Pass condition |
|---|---|---|
| 1 | Reboot Host PC without opening a controller | Agent starts through the startup task and `/status` responds. |
| 2 | Stop Agent and SMB, then restore them while observing desktop | Desktop distinguishes OFFLINE/BOOTING/ONLINE according to signals. |
| 3 | Fully shut down Host PC, press desktop ON | WOL boots Host PC without internet. |
| 4 | Watch desktop during cold boot | State progresses OFFLINE → BOOTING → ONLINE; ONLINE waits for Agent and SMB. |
| 5 | Make Agent reachable while blocking/stopping SMB | State remains BOOTING, never ONLINE. |
| 6 | Press desktop Sleep | Authenticated request is accepted and the PC sleeps normally. |
| 7 | Wake from sleep, then press desktop Shutdown | PC wakes and later performs a normal, non-forced shutdown. |
| 8 | Repeat ON, Sleep and Shutdown from Android | All three actions work on the same LAN. |
| 9 | Repeat partial-signal and unavailable-network cases on Android | Status logic matches desktop semantics. |
| 10 | Disconnect WAN/internet but keep router/LAN active | Status, WOL, Sleep and Shutdown continue to work. |
| 11 | After ONLINE, open `\\192.168.219.104\domination` | Existing media share remains usable. |
| 12 | Inspect traffic/configuration and repeat with WAN disconnected | No cloud service is required. |
| 13 | Review installed components | Only existing PCs, phone, router and storage are used; no new hardware. |

## 6. Failure and transition cases

Also verify the Plan-required routine failures:

- ON while already awake does not crash or start duplicate work.
- Shutdown while already offline is disabled because Agent is unavailable.
- WOL timeout remains BOOTING/OFFLINE with a useful detail instead of hanging.
- Host reachable while Agent fails to start remains BOOTING.
- Agent ready while SMB is delayed remains BOOTING.
- Wrong Wi-Fi/no LAN becomes UNKNOWN with a useful message.
- Wrong token is reported and does not perform a power action.
- A deliberately incorrect expected hostname becomes UNKNOWN.
- Sleep/Shutdown transitional labels appear immediately and eventually settle after reachability changes.

## 7. Result recording template

```text
Date/time:
Windows build/test:
Android build/test:
Host Agent startup:
Desktop AC 2-7:
Android AC 8-9:
Offline-internet AC 10-12:
SMB AC 11:
Failures checked:
Issues found:
```
