# GOAL: Local PC Power Controller and Media Host Helper

## 1. Project Goal

Build a small local-network control system for a designated Windows PC that acts as a media storage host.

The system must allow other Windows PCs and Android phones on the same home LAN to:

- See whether the target PC is offline, booting, online, or in an unknown/error state.
- Wake the target PC using Wake-on-LAN.
- Put the target PC into sleep mode remotely.
- Shut the target PC down remotely.
- Confirm whether the SMB media share is ready for use.

This project is not a media streaming server. The existing Windows SMB share remains responsible for file access.

The primary target PC is referred to as `Host PC` in this document.

---

## 2. Current Environment

### Host PC

- OS: Windows
- Network connection: Wired Ethernet
- Reserved IPv4 address: `192.168.219.104`
- DHCP reservation: Already configured and verified
- Wake-on-LAN:
  - Wake from sleep: Verified working
  - Wake from full Windows shutdown: Verified working
- SMB share name: `domination`
- Media storage: External HDD connected directly to Host PC
- SMB TCP port: `445`
- Existing SMB access from another PC: Verified working

### LAN

- Router subnet: `192.168.219.0/24`
- Router address: `192.168.219.1`
- Internet access must not be required for normal operation.
- All control features are intended for use only inside the local network.

### Host MAC address

The Host PC wired Ethernet MAC address must be configurable rather than hard-coded in source code.

---

## 3. Required Components

The project consists of three logical components.

### 3.1 Host Agent

A lightweight background application running on the Host PC.

Responsibilities:

- Expose a small local control API.
- Report host status.
- Report SMB readiness.
- Accept authenticated sleep requests.
- Accept authenticated shutdown requests.
- Start automatically after Windows login or system boot.
- Run without requiring the main desktop controller to be open.

The Host Agent does not implement Wake-on-LAN because the Host PC cannot process application-level requests while powered off.

### 3.2 Windows Desktop Controller

A desktop application intended for other Windows PCs on the same LAN.

Responsibilities:

- Display detailed Host PC status.
- Provide exactly three primary power buttons:
  - ON
  - Sleep
  - Shutdown
- Send a Wake-on-LAN magic packet for ON.
- Call the Host Agent API for Sleep and Shutdown.
- Periodically refresh Host PC status.
- Clearly show transitional and failure states.

### 3.3 Android Controller

A simple Android application for phones on the same LAN.

Responsibilities:

- Show a large and immediately understandable status indicator.
- Provide exactly three primary power buttons:
  - ON
  - Sleep
  - Shutdown
- Send Wake-on-LAN directly from the phone for ON.
- Call the Host Agent API for Sleep and Shutdown.
- Periodically refresh state while the app is open.
- Remain visually simple and mobile-friendly.

---

## 4. Functional Requirements

### 4.1 ON

The ON action must:

1. Read the configured Host PC MAC address.
2. Create a standard Wake-on-LAN magic packet.
3. Send the packet as a UDP broadcast on the local subnet.
4. Prefer the subnet broadcast address `192.168.219.255`.
5. Use a conventional WOL port such as UDP 9.
6. Immediately move the UI into a waking or booting state.
7. Poll until the Host PC becomes reachable.
8. Continue polling until the Host Agent and SMB service are ready.
9. End in ONLINE when the Host PC is usable.

Sending the packet multiple times in a short burst is acceptable if it improves reliability.

The implementation must not depend on internet connectivity.

### 4.2 Sleep

The Sleep action must:

1. Be available only when the Host Agent is reachable.
2. Ask the Host Agent to put Windows into sleep mode.
3. Give the UI immediate feedback that the sleep request was accepted.
4. Continue status checks until the host becomes unreachable or enters the expected sleeping state.
5. Handle the case where the machine is already sleeping or unavailable without crashing.

### 4.3 Shutdown

The Shutdown action must:

1. Be available only when the Host Agent is reachable.
2. Ask the Host Agent to perform a normal Windows shutdown.
3. Avoid forced power-off unless explicitly added in a future version.
4. Give the UI immediate feedback that shutdown was accepted.
5. Continue status checks until the host becomes unreachable.
6. Handle the case where the machine is already offline without crashing.

### 4.4 Status

Do not treat a successful ping as the only definition of ONLINE.

The controller should evaluate multiple signals where practical:

- ICMP ping or equivalent host reachability
- Host Agent API reachability
- TCP port 445 reachability for SMB
- Optional response latency

The minimum user-facing states are:

#### OFFLINE

The Host PC does not respond to normal reachability checks and the Host Agent is unavailable.

#### BOOTING

The Host PC has started responding to the network, but the Host Agent or SMB service is not fully ready yet.

Typical example:

- Ping works
- Agent or TCP 445 is not ready

#### ONLINE

The Host Agent responds successfully and SMB TCP port 445 is reachable.

This means the machine is ready for normal `domination` share use.

#### UNKNOWN

The controller cannot confidently classify the state.

Examples:

- Local network disconnected
- Network permission denied
- Agent returns malformed data
- Status timeout
- Conflicting readiness signals

A distinct transitional state such as `GOING_TO_SLEEP` or `SHUTTING_DOWN` may be implemented internally or displayed if useful.

---

## 5. Host Agent API

Keep the API deliberately small.

Suggested initial endpoints:

```text
GET  /status
POST /sleep
POST /shutdown
```

Suggested `/status` response:

```json
{
  "status": "online",
  "hostname": "HOSTNAME",
  "ip": "192.168.219.104",
  "uptimeSeconds": 12345,
  "smbReady": true
}
```

The exact schema may be adjusted during implementation, but it must remain small, versionable, and documented.

The agent should expose a lightweight health endpoint or use `/status` as the health endpoint.

---

## 6. Security Requirements

This is a local-network tool, but destructive commands still require protection.

Minimum requirements:

- Do not expose the API intentionally to the public internet.
- Bind the Host Agent to the Host PC LAN interface or otherwise restrict it to LAN access.
- Restrict the Windows Firewall rule to the local subnet where practical.
- Require a pre-shared authentication secret for Sleep and Shutdown.
- Do not hard-code the secret in source control.
- Store the secret in local configuration.
- Do not log the secret.
- Do not include any cloud login, remote account system, or external authentication provider in the MVP.

A simple bearer-style local token is acceptable for the MVP when combined with LAN-only exposure and firewall subnet restriction.

If the implementation introduces a stronger local authentication design without significantly increasing complexity, that is acceptable.

---

## 7. Configuration

Do not hard-code environment-specific values in application logic.

At minimum, the controllers must support configuration for:

```text
Host IP:        192.168.219.104
Host MAC:       configurable
Broadcast IP:   192.168.219.255
Agent port:     configurable
SMB port:       445
SMB share:      domination
Auth secret:    configurable
```

Reasonable defaults may be supplied.

Configuration format may differ by platform, but configuration should be easy to edit or expose in a small Settings screen.

The Host PC IP is currently stable because DHCP reservation is already configured.

---

## 8. Windows Desktop UI Requirements

The desktop application should prioritize information density without becoming visually noisy.

Minimum main-window content:

```text
Host PC

Status:      ONLINE
IP:          192.168.219.104
Agent:       Ready
SMB:         Ready
Latency:     5 ms

[ ON ]    [ Sleep ]    [ Shutdown ]
```

Required behavior:

- Status indicator must use both text and visual state.
- Do not rely on color alone.
- Buttons must clearly indicate when an action is unavailable.
- Sleep and Shutdown should be disabled when the Agent is unreachable.
- ON may remain usable while the state is OFFLINE or UNKNOWN.
- Repeated clicks must not create uncontrolled duplicate requests.
- Destructive actions should not accidentally trigger due to double-click or UI race conditions.

A lightweight confirmation for Shutdown is acceptable, but avoid modal-dialog overload.

The desktop app should remain small and fast to launch.

---

## 9. Android UI Requirements

The Android app should be optimized for quick one-handed use.

Minimum main screen:

```text
       Host PC

       ONLINE

     [   ON   ]
     [ Sleep  ]
     [Shutdown]
```

Additional compact information may include:

- `192.168.219.104`
- Agent availability
- SMB readiness
- Last refresh time

Requirements:

- Large status indicator
- Three clearly separated primary buttons
- No unnecessary navigation for basic control
- Local-network permissions handled correctly
- Show a useful message when Wi-Fi or LAN connectivity is unavailable
- Do not imply that internet connectivity is required

---

## 10. Suggested Technology Direction

The exact implementation language may be chosen by Codex after inspecting the repository, but prefer simple platform-native or low-overhead technologies.

Reasonable directions:

### Host Agent

Preferred:

- C# on modern .NET
- Minimal local HTTP API
- Windows-specific sleep and shutdown implementation

The Agent may initially run as a background startup application if implementing a true Windows Service would slow down the MVP significantly.

A Windows Service may be adopted when it provides a clear reliability benefit.

### Windows Desktop Controller

Preferred options:

- C# with WPF or WinUI
- Another lightweight Windows desktop stack only if the repository already favors it

Avoid introducing a browser runtime solely for this small utility unless there is a strong repository-specific reason.

### Android Controller

Preferred:

- Kotlin
- Jetpack Compose

Keep Android dependencies modest.

---

## 11. Error Handling

All components must fail visibly and safely.

Expected cases include:

- Host already offline when Shutdown is pressed
- Host already awake when ON is pressed
- WOL packet sent but Host PC does not boot
- Host boots but Agent fails to start
- Host boots but SMB port 445 is not ready
- LAN connection disappears
- Controller is on the wrong Wi-Fi or network
- Authentication secret mismatch
- Agent request timeout
- Sleep request succeeds but host takes time to disappear
- Shutdown request succeeds but SMB remains reachable briefly
- Host IP reachable but response belongs to an unexpected machine

Do not crash on routine network failures.

All network operations must have finite timeouts.

---

## 12. Logging

Logging should be lightweight and useful for debugging.

Host Agent should log:

- Startup
- Listening address and port
- Successful authenticated control request
- Rejected authentication
- Sleep request
- Shutdown request
- Internal errors

Do not log authentication secrets.

Controllers should log or expose enough diagnostic information to understand:

- Last status check
- Last WOL attempt
- Last Agent error
- Last timeout

Do not build a heavy logging subsystem for the MVP.

---

## 13. MVP Scope

The first usable version is complete when all of the following work on the real LAN:

1. Host Agent starts on the Host PC.
2. Desktop Controller can identify OFFLINE versus ONLINE.
3. Desktop Controller ON wakes the fully shut down Host PC.
4. Status transitions from OFFLINE to BOOTING to ONLINE.
5. ONLINE requires the Agent and SMB readiness checks.
6. Desktop Controller Sleep successfully puts the Host PC to sleep.
7. Desktop Controller Shutdown successfully shuts down the Host PC.
8. Android Controller performs the same three power actions.
9. Android status indication reflects the same logical states.
10. All normal use works without internet access.
11. The existing `domination` SMB share remains usable after the Host PC is awake.
12. No cloud service is required.
13. No new hardware is required.

---

## 14. Explicitly Out of Scope for MVP

Do not implement these unless required to complete the core system:

- Cloud server
- Remote access over the public internet
- User accounts
- OAuth
- External database
- VPN management
- New media streaming protocol
- Video transcoding
- Jellyfin or Plex replacement
- Media library indexing
- Thumbnail generation
- Subtitle management
- File synchronization
- SMB server implementation
- Router configuration automation
- Dynamic DNS
- UPnP port forwarding
- Public web dashboard
- Multi-host fleet management

The project controls one designated Host PC first.

Avoid scope expansion.

---

## 15. Future Enhancements

These may be considered only after the MVP is stable.

### Desktop convenience

- Button to open `\\192.168.219.104\domination`
- Automatic wake then open `domination`
- Remember window position
- System tray control
- Native notifications

### Host information

- External HDD free space
- Host uptime
- CPU usage
- RAM usage
- Network throughput
- SMB active session count

### Power automation

- Automatic sleep after configurable idle time
- Prevent sleep while SMB media is actively being read
- Grace period after last SMB activity
- Optional scheduled sleep
- Optional safe cancellation of a pending sleep

### Android convenience

- Home-screen widget
- Quick Settings tile
- Notification action buttons
- Optional biometric confirmation for Shutdown

### Multiple hosts

Only consider after the single-host design is stable.

---

## 16. Implementation Principles

Codex should follow these principles while building the project:

- Prefer the smallest architecture that satisfies the requirements.
- Do not add infrastructure merely because it is conventional.
- Keep LAN control logic understandable.
- Keep platform-specific power operations isolated behind clear interfaces.
- Share protocol definitions where practical.
- Keep status classification deterministic.
- Avoid hidden retries that make the UI appear frozen.
- Prefer explicit state transitions.
- Do not silently ignore failures.
- Do not require administrator rights in the controller applications unless technically necessary.
- Limit administrator privileges on the Host Agent to only what is required.
- Keep configuration outside source code.
- Preserve the existing SMB setup.
- Do not modify router configuration automatically.
- Do not change the Host PC static/DHCP setup.
- Do not introduce cloud dependencies.

---

## 17. Suggested Development Order

Implement incrementally and verify each stage before expanding scope.

### Phase 1: Protocol and Host Agent

- Define configuration model.
- Implement `/status`.
- Implement authentication.
- Implement `/sleep`.
- Implement `/shutdown`.
- Confirm the Agent can start automatically.
- Configure or document required Windows Firewall scope.

### Phase 2: Desktop Controller Core

- Implement status polling.
- Implement state classification.
- Implement Wake-on-LAN.
- Implement Sleep.
- Implement Shutdown.
- Test against the real Host PC.

### Phase 3: Desktop UI Polish

- Add status details.
- Add disabled/loading button states.
- Add useful error messages.
- Add settings handling.

### Phase 4: Android Controller

- Implement configuration.
- Implement status polling.
- Implement Wake-on-LAN.
- Implement Sleep and Shutdown.
- Implement mobile status UI.
- Test on the same LAN.

### Phase 5: Integration Testing

Test real transitions:

```text
OFFLINE -> ON -> BOOTING -> ONLINE
ONLINE -> SLEEP -> OFFLINE/SLEEPING
SLEEPING -> ON -> BOOTING -> ONLINE
ONLINE -> SHUTDOWN -> OFFLINE
OFFLINE -> ON -> BOOTING -> ONLINE -> SMB READY
```

Also test with internet disconnected while the router and LAN remain operational.

---

## 18. Acceptance Criteria

The project is acceptable when a user can perform the following without opening PowerShell, Device Manager, or router settings:

### From Windows

1. Launch the controller.
2. See whether Host PC is usable.
3. Press ON while Host PC is fully shut down.
4. Watch status progress until ONLINE.
5. Access the existing `domination` SMB share.
6. Press Sleep and see the machine become unavailable.
7. Wake it again.
8. Press Shutdown and see the machine shut down.

### From Android

1. Open the app while connected to the home LAN.
2. Immediately understand Host PC state.
3. Wake the Host PC.
4. Confirm when the Host PC and SMB service are ready.
5. Put the Host PC to sleep.
6. Shut the Host PC down.

All of the above must work without internet connectivity.

---

## 19. Repository Guidance for Codex

Before implementation:

1. Inspect the repository structure and existing technology choices.
2. Reuse an appropriate existing stack if one already exists.
3. If the repository is empty, create a clear multi-project structure for:
   - Host Agent
   - Windows Desktop Controller
   - Android Controller
   - Shared protocol/documentation as appropriate
4. Add a concise root README with:
   - Build instructions
   - Run instructions
   - Configuration instructions
   - Firewall notes
   - Host Agent startup setup
   - Android LAN permission notes
5. Keep this `Plan.md` as the source of truth for MVP scope.
6. If a requirement is ambiguous, prefer the simpler implementation that preserves the stated architecture and local-only security model.
7. Do not expand scope without an explicit reason documented in the repository.

---

## 20. Known Working Facts

These facts have already been manually verified and should not be re-investigated as speculative requirements:

- Host PC IP reservation at `192.168.219.104` works.
- Host PC is connected by Ethernet.
- Wake-on-LAN from sleep works.
- Wake-on-LAN from full shutdown works.
- `Wake on Magic Packet` is enabled on the Host PC NIC.
- Wake-on-LAN on shutdown is enabled on the Host PC NIC.
- Windows Fast Startup has been disabled for WOL reliability.
- SMB TCP port 445 is reachable when Host PC is running.
- The `domination` share is reachable from another Windows PC with valid credentials.
- The external HDD is already connected and provides the media storage space.
- Internet connectivity is not required for the intended usage.

The remaining work is application-level control, monitoring, usability, and safe local command handling.
