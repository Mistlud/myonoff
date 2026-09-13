# MyOnOff Validation & WOL Bug Handoff

## Purpose

This document records the real-device validation work completed after the initial Codex implementation, the small source fixes required to make the Windows solution build, the current verified system state, and the next bug-fix task.

Use this document together with `Plan.md`.

`Plan.md` remains the source of truth for overall MVP scope.
This document is the source of truth for what has actually been tested on real hardware and for the currently reproduced Desktop Controller WOL bug.

---

# 1. Current Environment

## Host PC

The controlled machine is PC #2.

- OS: Windows
- Hostname: `DESKTOP-VHU9KUS`
- Connection: Wired Ethernet
- Reserved IPv4: `192.168.219.104`
- DHCP reservation: configured and verified
- SMB share: `domination`
- SMB port: `445`
- Agent port: `5055`
- LAN subnet: `192.168.219.0/24`
- Broadcast address used for WOL: `192.168.219.255`
- WOL UDP port: `9`
- External HDD is attached to this PC and used as the media storage location.
- Host MAC address is configured in the Desktop Controller but is intentionally not recorded in this document.
- Authentication token is configured and verified but is intentionally not recorded in this document.

## Controller PC

PC #1 is being used for Windows Desktop Controller validation.

- IPv4 observed during testing: `192.168.219.101`
- Connection: Wi-Fi
- Same local subnet as Host PC
- Direct SMB access to Host PC has been verified.

---

# 2. Network and SMB Validation Completed Before App Testing

The following were manually verified before application-level testing.

## Host reachability

From PC #1:

```powershell
Test-NetConnection 192.168.219.104 -Port 445
```

Result:

```text
TcpTestSucceeded : True
```

Therefore:

- PC #1 can reach PC #2.
- SMB TCP port 445 is reachable.
- Router isolation is not blocking the two PCs.

## SMB share

The `domination` SMB share on PC #2 was successfully accessed from PC #1.

A dedicated local Windows account was created for SMB access and granted both:

- Share permission
- NTFS filesystem permission

This resolved the earlier Windows SMB authorization error.

The SMB share is now usable from PC #1.

---

# 3. Wake-on-LAN Hardware / Windows Validation

Wake-on-LAN was manually configured and tested before application testing.

## NIC configuration verified

On Host PC:

- `Wake on LAN Shutdown` is enabled.
- `Wake on Magic Packet` is enabled.
- The NIC is allowed to wake the computer.
- `Only allow a magic packet to wake the computer` was enabled.

## Windows Fast Startup

Windows Fast Startup was disabled to improve shutdown-state WOL reliability.

## BIOS / UEFI

No explicit Wake-on-LAN option was found in BIOS/UEFI.

Despite that, WOL works correctly in real hardware testing.

## Manual WOL validation

A PowerShell magic-packet sender was used from PC #1.

Equivalent logic:

```powershell
$mac = "<HOST_WIRED_MAC>"
$b = "192.168.219.255"

$m = $mac -split '[:-]' | ForEach-Object { [Convert]::ToByte($_,16) }
$p = [byte[]]((0xFF,0xFF,0xFF,0xFF,0xFF,0xFF) + ($m * 16))

$u = [System.Net.Sockets.UdpClient]::new()
$u.EnableBroadcast = $true
$u.Send($p, $p.Length, $b, 9)
$u.Close()
```

Verified:

- WOL from Windows sleep works.
- WOL from full Windows shutdown works.
- Host PC wakes quickly in both cases.

Therefore the Host PC hardware, router/LAN, broadcast address, MAC address, and WOL port are already proven working.

Any Desktop Controller ON failure should be treated primarily as an application bug, not as an unverified WOL environment issue.

---

# 4. .NET Environment and Windows Build Validation

The development PC initially did not have the .NET SDK.

Installed SDK:

```text
8.0.425 [C:\Program Files\dotnet\sdk]
```

The solution was then restored and built.

## Initial build issue 1

The first compile failure was:

```text
CS0246: 'HttpClient' type or namespace name could not be found
```

File:

```text
src\MyOnOff.DesktopController\HostControllerClient.cs
```

Fix applied:

```csharp
using System.Net.Http;
```

## Initial build issue 2

After the first fix, build produced multiple errors for:

```text
Path
File
Directory
```

Files:

```text
src\MyOnOff.DesktopController\AppLog.cs
src\MyOnOff.DesktopController\SettingsStore.cs
```

Fix applied to both files:

```csharp
using System.IO;
```

## Final Windows build result

After those fixes:

```powershell
dotnet build MyOnOff.sln -c Release --no-restore
```

completed successfully.

## Test result

Executed:

```powershell
dotnet test tests\MyOnOff.Protocol.Tests\MyOnOff.Protocol.Tests.csproj -c Release --no-build
```

Result:

```text
Passed: 25
Failed: 0
Skipped: 0
```

This is a real local build/test result, not a static-code assumption.

---

# 5. Host Agent Publish and Deployment

A framework-dependent publish was first produced, but Host PC did not have .NET installed.

Instead of installing .NET on Host PC, a self-contained publish was created.

Command used:

```powershell
dotnet publish .\src\MyOnOff.HostAgent\MyOnOff.HostAgent.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .\artifacts\host-agent-selfcontained
```

The complete `host-agent-selfcontained` directory was copied to Host PC.

Current Host Agent location:

```text
C:\myonoff\host-agent-selfcontained
```

This approach is intentional.

Do not assume Host PC has a separate .NET runtime installed.

---

# 6. Host Agent Runtime Validation

Host Agent was manually launched on Host PC:

```powershell
.\MyOnOff.HostAgent.exe
```

Observed successful startup:

```text
MyOnOff Host Agent starting on http://0.0.0.0:5055
Now listening on: http://0.0.0.0:5055
Hosting environment: Production
Content root path: C:\myonoff\host-agent-selfcontained
```

## Local `/status` validation

From Host PC:

```powershell
Invoke-RestMethod http://127.0.0.1:5055/status
```

Observed:

```text
status        : online
hostname      : DESKTOP-VHU9KUS
ip            : 192.168.219.104
uptimeSeconds : <value>
smbReady      : True
apiVersion    : 1
```

## Remote `/status` validation

From PC #1:

```powershell
Invoke-RestMethod http://192.168.219.104:5055/status
```

The same valid response was received.

Therefore:

- Agent listens correctly.
- Agent is reachable over LAN.
- Port 5055 is usable.
- `/status` works remotely.
- SMB readiness detection works.
- Hostname and Host IP reporting are correct.

---

# 7. Host Agent Authentication Token

A permanent machine-level Agent token was created on Host PC.

The first attempt failed because PowerShell was not elevated:

```text
SecurityException:
The requested registry access is not allowed.
```

The operation was repeated from an Administrator PowerShell and succeeded.

The token is stored as the machine environment variable:

```text
MYONOFF_AGENT_TOKEN
```

The value was read back successfully and verified.

Important:

- Do not replace the current authentication model unless required by a bug.
- Do not commit the token.
- Do not print or log the token.
- This document intentionally does not contain the token.

---

# 8. Host Agent Automatic Startup

Host Agent was configured to run automatically at system startup using a Windows Scheduled Task.

The task runs:

- As `SYSTEM`
- At startup
- With highest privileges
- Using the Host Agent executable
- With the Host Agent directory as working directory

The Agent was first launched manually through the scheduled task.

Remote `/status` from PC #1 returned:

```text
status   : online
smbReady : True
```

Then Host PC was rebooted.

After reboot, without manually launching the Agent, PC #1 again received:

```text
status   : online
smbReady : True
```

Therefore Host Agent automatic startup is verified working on the real Host PC.

---

# 9. Real Power-Control Validation

The Host Agent API was manually tested before trusting the GUI.

## Sleep request

From PC #1, authenticated request:

```powershell
Invoke-RestMethod `
    -Method Post `
    -Uri "http://192.168.219.104:5055/sleep" `
    -Headers $headers
```

Observed response:

```text
action   accepted message
------   -------- -------
sleep    True     Power action accepted.
```

Host PC successfully entered sleep.

Then a manually generated WOL packet woke the Host PC.

After waking, `/status` again returned:

```text
status   : online
smbReady : True
```

Therefore this full loop is verified:

```text
ONLINE
-> remote Sleep API
-> SLEEP
-> WOL
-> ONLINE
-> Agent available
-> SMB ready
```

## Shutdown request

From PC #1, authenticated request:

```powershell
Invoke-RestMethod `
    -Method Post `
    -Uri "http://192.168.219.104:5055/shutdown" `
    -Headers $headers
```

Observed response:

```text
action    accepted message
------    -------- -------
shutdown  True     Power action accepted.
```

Host PC shut down normally.

Then a manually generated WOL packet woke Host PC from full shutdown.

After boot:

```text
status   : online
smbReady : True
```

Therefore this full loop is verified:

```text
ONLINE
-> remote Shutdown API
-> fully powered off
-> WOL
-> Windows boot
-> Host Agent automatic startup
-> ONLINE
-> SMB ready
```

This is the most important verified backend flow.

---

# 10. Desktop Controller Validation

The Windows Desktop Controller now builds and launches successfully.

Settings were filled with the correct values, including:

```text
Host IP:        192.168.219.104
Host MAC:       verified wired Host MAC
Broadcast IP:   192.168.219.255
WOL Port:       9
Agent Port:     5055
SMB Port:       445
SMB Share:      domination
Expected Host:  DESKTOP-VHU9KUS
Auth Token:     configured correctly
```

The Desktop Controller correctly detects:

```text
ONLINE
```

when Host PC is available.

## Sleep via Desktop Controller

Sleep button was tested.

Observed:

- UI correctly displayed `Going to sleep`.
- Host PC actually entered sleep.

Therefore Desktop Controller -> Agent sleep flow works.

## Offline detection

After Host PC shutdown, Desktop Controller correctly detected:

```text
OFFLINE
```

Therefore offline-state detection works.

---

# 11. Current Reproduced Bug: Desktop Controller ON Does Nothing

This is the current priority bug.

## Reproduction

With Host PC in OFFLINE state:

1. Launch Desktop Controller.
2. Confirm Host PC is shown as OFFLINE.
3. Press `ON`.

Observed behavior:

- No visible response.
- Host PC does not wake.
- UI does not transition into a useful WOL/BOOTING indication.
- No WOL attempt appears in controller log.

This was reproduced both:

- after sleep/offline state
- after full shutdown/offline state

Manual PowerShell WOL from the same PC still wakes Host PC correctly.

Therefore:

- Host MAC is correct.
- Broadcast IP is correct.
- WOL UDP port 9 is correct.
- Router/LAN is correct.
- Host WOL hardware configuration is correct.
- The failure is in the Desktop Controller path before or during the user action flow.

## Controller log

After pressing ON, the relevant log contained only earlier actions:

```text
2026-09-13T20:55:40.2253910+09:00 Controller settings updated.
2026-09-13T20:56:15.8564950+09:00 Sleep request accepted; waiting for the host to become unreachable.
```

There was no WOL log entry.

This strongly suggests that the ON user action is being discarded before the WOL sender executes.

---

# 12. Suspected Root Cause

Inspect the Desktop Controller action/polling synchronization.

Current suspected issue:

- `RefreshAsync()` and `RunActionAsync()` share `_operationGate`.
- Both use non-blocking `WaitAsync(0)` behavior.
- When status polling owns `_operationGate`, a user button action can immediately return instead of waiting.
- In OFFLINE state, network probing can occupy much of each poll interval.
- This can create a race where ON button clicks are silently discarded.

The suspected problematic pattern is equivalent to:

```csharp
if (!await _operationGate.WaitAsync(0))
{
    return;
}
```

Do not assume this diagnosis is correct without inspecting the current source.

However, the observed lack of both UI response and WOL log entry is consistent with an early return before the WOL sender is reached.

---

# 13. Required Fix

Fix the Desktop Controller bug where ON can be silently ignored while the Host is OFFLINE.

Requirements:

1. OFFLINE-state ON clicks must not be lost because background status polling is in progress.
2. User-triggered power actions must take priority over routine status polling.
3. Repeated user clicks while a power action is already running must still be prevented.
4. ON should provide immediate visible feedback.
5. ON should transition the UI into an appropriate state such as:
   - sending WOL
   - waking
   - booting
6. WOL send success must be logged.
7. WOL send failure must be logged.
8. After WOL send, the controller should continue monitoring until:
   - Host becomes reachable
   - Agent becomes ready
   - SMB becomes ready
   - final state becomes ONLINE
9. Existing Sleep behavior must remain working.
10. Existing Shutdown behavior must remain working.
11. Existing ONLINE/OFFLINE detection must remain working.
12. Avoid broad refactors.
13. Do not introduce a new state-management framework for this bug.
14. Prefer a minimal synchronization fix.
15. If practical, add a regression test or isolate enough controller logic to test that a user action cannot be silently dropped by status polling.
16. Build the Windows solution after the change.
17. Run the existing Protocol tests after the change.
18. Report:
    - root cause
    - modified files
    - exact behavior change
    - build result
    - test result
    - any remaining manual validation required

---

# 14. Important Constraint for Codex

Do not spend time installing Android SDK, Gradle, .NET SDK, or other missing development environments inside the Codex environment just to prove every build.

For this task:

- Modify the source.
- Run build/tests that are already possible in the available environment.
- If a required local tool is unavailable, document what must be run manually.
- Do not turn this bug fix into an environment-provisioning task.

The real Windows build and real-device validation are being performed manually.

---

# 15. Current Verified State Summary

## Verified working

- DHCP reservation for Host PC
- Host IP `192.168.219.104`
- SMB access to `domination`
- SMB TCP port 445
- WOL from sleep
- WOL from full shutdown
- Host Agent self-contained deployment
- Agent `/status` locally
- Agent `/status` remotely
- `smbReady = True`
- Authenticated `/sleep`
- Authenticated `/shutdown`
- Permanent machine-level Agent token
- Agent automatic startup as SYSTEM
- Agent startup after reboot
- Agent startup after WOL from shutdown
- Desktop Controller build
- Protocol tests: 25 passed, 0 failed, 0 skipped
- Desktop Controller ONLINE detection
- Desktop Controller OFFLINE detection
- Desktop Controller Sleep button
- Desktop Controller `Going to sleep` transitional UI

## Currently broken

- Desktop Controller ON button while Host is OFFLINE
- Host does not wake from Desktop Controller ON
- ON action leaves no WOL-attempt log entry

## Not yet validated

- Desktop Controller ON after bug fix
- Full Desktop Controller loop:
  - ON
  - Sleep
  - ON
  - Shutdown
  - ON
- Android Controller real build
- Android Controller real-device behavior
- Android ON/Sleep/Shutdown
- Android state transitions

---

# 16. Next Validation After Fix

After Codex modifies the Desktop Controller, manually run:

```powershell
dotnet build MyOnOff.sln -c Release --no-restore
```

Then:

```powershell
dotnet test tests\MyOnOff.Protocol.Tests\MyOnOff.Protocol.Tests.csproj -c Release --no-build
```

Then launch the Desktop Controller.

Test in this order:

```text
1. Host ONLINE
2. Press Sleep
3. Confirm Host enters sleep
4. Confirm Controller reaches OFFLINE
5. Press ON
6. Confirm immediate WOL/BOOTING feedback
7. Confirm Host wakes
8. Confirm Controller reaches ONLINE
9. Confirm SMB becomes Ready
10. Press Shutdown
11. Confirm Host fully shuts down
12. Confirm Controller reaches OFFLINE
13. Press ON
14. Confirm Host boots from WOL
15. Confirm Agent starts automatically
16. Confirm Controller reaches ONLINE
17. Confirm SMB is Ready
```

If all of the above passes, the Windows MVP power-control loop can be considered functionally verified on real hardware.

---

# 17. Codex Task

Use `Plan.md` and this document together.

Priority:

```text
Fix the Desktop Controller OFFLINE -> ON silent failure.
```

Do not expand scope.

Do not redesign the project.

Do not work on Android until this Windows WOL path is corrected and the Windows solution still builds.

The backend is already proven on real hardware.

The immediate goal is to make the Desktop Controller reliably exercise that already-working backend and WOL path.
