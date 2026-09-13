# MyOnOff Validation Status

## Purpose

This document records the current real-device validation state of MyOnOff.

`Plan.md` remains the original source of truth for project architecture and MVP scope. This document is additive and should be used to avoid re-investigating already verified behavior.

No authentication token, MAC address, or other secret is included here.

## Current Host Environment

Target Host PC:

- Windows
- Hostname: `DESKTOP-VHU9KUS`
- Wired Ethernet
- Reserved IPv4: `192.168.219.104`
- LAN subnet: `192.168.219.0/24`
- WOL broadcast: `192.168.219.255`
- WOL UDP port: `9`
- Agent port: `5055`
- SMB port: `445`
- SMB share: `domination`
- External HDD used as media storage

DHCP reservation has been configured and verified.

## SMB Validation

The `domination` SMB share is reachable from another Windows PC on the same LAN.

Verified:

- TCP 445 reachable
- SMB authentication works
- Share permission works
- NTFS permission works
- Files can be accessed from another PC

A dedicated local Windows account is used for SMB access.

## Wake-on-LAN Validation

WOL has been verified manually from another PC on the same LAN.

Verified working:

- Wake from Windows sleep
- Wake from full Windows shutdown

Relevant Host PC configuration already verified:

- Wake on LAN Shutdown enabled
- Wake on Magic Packet enabled
- NIC allowed to wake the computer
- Magic-packet-only wake enabled
- Windows Fast Startup disabled

No explicit WOL BIOS option was found, but real hardware testing confirms WOL works.

Manual WOL using the configured Host MAC, broadcast address `192.168.219.255`, and UDP port `9` wakes the Host PC correctly.

Therefore WOL hardware, LAN broadcast, and Host NIC configuration should be considered proven working.

## Windows Build Validation

Development PC has:

```text
.NET SDK 8.0.425
```

Initial source fixes required before the Windows solution could build:

### HostControllerClient.cs

Added:

```csharp
using System.Net.Http;
```

### AppLog.cs

Added:

```csharp
using System.IO;
```

### SettingsStore.cs

Added:

```csharp
using System.IO;
```

After those fixes:

```powershell
dotnet build MyOnOff.sln -c Release --no-restore
```

completed successfully.

## Protocol Test Validation

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

## Host Agent Deployment

Host PC does not rely on a separately installed .NET runtime.

A self-contained Windows x64 Host Agent publish was created and deployed to:

```text
C:\myonoff\host-agent-selfcontained
```

This deployment model is intentional.

## Host Agent Runtime Validation

Host Agent successfully listens on:

```text
http://0.0.0.0:5055
```

Local and remote `/status` requests work.

Verified response includes:

```text
status        : online
hostname      : DESKTOP-VHU9KUS
ip            : 192.168.219.104
smbReady      : True
apiVersion    : 1
```

## Authentication and Host Agent Auto-Start

A permanent machine-level authentication token is configured in:

```text
MYONOFF_AGENT_TOKEN
```

The actual token value must never be committed or logged.

Host Agent auto-start is configured through a Windows Scheduled Task.

Verified characteristics:

- Runs as `SYSTEM`
- Starts at system startup
- Runs with highest privileges
- Uses the Host Agent deployment directory as working directory

After reboot, without manually launching the Agent:

```text
status   : online
smbReady : True
```

was confirmed remotely.

## Real Power-Control API Validation

### Sleep

Verified full loop:

```text
ONLINE
-> remote Sleep API
-> Host enters sleep
-> manual WOL
-> Host wakes
-> Agent becomes reachable
-> smbReady = True
```

### Shutdown

Verified full loop:

```text
ONLINE
-> remote Shutdown API
-> Host fully shuts down
-> manual WOL
-> Windows boots
-> Host Agent starts automatically
-> Agent becomes reachable
-> smbReady = True
```

The Host Agent backend power-control path is proven on real hardware.

## Desktop Controller Validation

The Windows Desktop Controller builds and launches successfully.

Configured values include:

```text
Host IP:       192.168.219.104
Broadcast IP:  192.168.219.255
WOL Port:      9
Agent Port:    5055
SMB Port:      445
SMB Share:     domination
Expected Host: DESKTOP-VHU9KUS
```

The actual Host MAC and authentication token are configured locally and must not be committed.

Verified in the real Desktop Controller:

- ONLINE detection
- OFFLINE detection
- BOOTING indication
- ON button
- Sleep button
- Shutdown button
- `Going to sleep` transitional UI
- WOL from OFFLINE state
- OFFLINE -> BOOTING -> ONLINE transition

A previous concurrency bug caused ON clicks to be silently dropped while status polling held the same gate.

Codex fixed this bug, and the corrected build has been manually verified on real hardware.

## Android Build Validation

Android development environment was prepared on the development PC.

Verified:

- Android 17
- API 37
- Required build tools available
- Gradle project import successful
- Android build completed successfully
- `app-debug.apk` produced

Observed:

```text
BUILD SUCCESSFUL
```

## Android Real-Device Validation

The debug APK was installed on a real Android phone connected to the same LAN through Wi-Fi.

Verified:

- ONLINE detection
- OFFLINE detection
- ON button
- Sleep button
- Shutdown button
- WOL works
- Host sleep works
- Host shutdown works
- Host wakes and returns to ONLINE

The Android Controller core power-control loop is proven working on real hardware.

The current normal Android UI is considered satisfactory and should not be broadly redesigned.

## Current Functional Status

The core MVP power-control system is functionally working.

Verified end-to-end:

```text
Windows Desktop Controller
        |
        +-> WOL
        +-> Host Agent status
        +-> Sleep
        +-> Shutdown
        |
Host PC + External HDD + SMB
        |
Android Controller
        |
        +-> WOL
        +-> Host Agent status
        +-> Sleep
        +-> Shutdown
```

Internet access is not required for normal operation.

## Remaining Work

The remaining work is primarily usability, packaging, and release polish.

Still required:

- Desktop normal-mode UI cleanup
- Easy Mode for Desktop
- Easy Mode for Android
- Easy Mode entry and exit flow
- Windows Desktop Controller publish flow
- Avoid requiring routine `dotnet run`
- Standardized Windows artifact output
- Windows publish helper script
- Android release build flow
- Android release signing documentation
- Gradle Wrapper normalization if practical
- User-facing install and update documentation
- Final regression pass after UX and packaging changes

## Regression Rule

Future changes must not break already verified behavior.

At minimum, after relevant modifications re-check:

```text
Desktop:
OFFLINE -> ON -> BOOTING -> ONLINE
ONLINE -> Sleep -> OFFLINE
OFFLINE -> ON -> ONLINE
ONLINE -> Shutdown -> OFFLINE
OFFLINE -> ON -> ONLINE

Android:
OFFLINE -> ON -> ONLINE
ONLINE -> Sleep -> OFFLINE
OFFLINE -> ON -> ONLINE
ONLINE -> Shutdown -> OFFLINE
OFFLINE -> ON -> ONLINE
```

The current power-control backend should be treated as stable unless a specific regression is reproduced.
