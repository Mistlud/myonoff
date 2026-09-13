# MyOnOff UX Requirements

## Purpose

This document defines additional UX requirements after successful MVP validation.

`Plan.md` remains the original source of truth for architecture and MVP behavior. These requirements are additive.

Main goals:

- Add an Easy Mode to both Windows Desktop and Android.
- Improve the normal-mode Windows Desktop UI.
- Preserve the current Android normal-mode UI.

## Core UX Principle

Normal mode is the management and diagnostic interface.

Easy Mode is a minimal one-purpose remote control.

Easy Mode should answer only:

```text
Is the Host PC off?
If yes, let me turn it on.
Is it turning on?
If yes, show that.
Is it on?
If yes, show that.
```

Easy Mode must not expose unnecessary technical information or extra power controls.

## Easy Mode Platforms

Easy Mode is required on:

- Windows Desktop Controller
- Android Controller

Both platforms should use the same behavioral rules.

Visual implementation may remain platform-appropriate.

Do not force both platforms into identical visual design.

## Easy Mode Entry

Normal mode should provide an obvious but non-intrusive way to enter Easy Mode.

Examples:

```text
Easy Mode
Simple Mode
Power Only
```

Exact label may be chosen during implementation.

Avoid adding a large navigation system only for this feature.

## Easy Mode Exit

Easy Mode must provide a small way to return to normal mode.

The exit control should be visually secondary.

Examples:

```text
Detailed Mode
Back to Details
Settings / Details icon
```

Do not place diagnostic information directly on the Easy Mode screen.

## Easy Mode OFFLINE State

When the Host PC is OFFLINE:

- Show one large primary ON button.
- The ON button should be the dominant element on screen.
- Do not show Sleep.
- Do not show Shutdown.
- Do not show IP address.
- Do not show port numbers.
- Do not show hostname.
- Do not show SMB status.
- Do not show latency.
- Do not show uptime.
- Do not show API details.

Conceptually:

```text
+---------------------------+
|                           |
|                           |
|          [ ON ]           |
|                           |
|                           |
+---------------------------+
```

The button must reuse the existing proven WOL implementation.

Do not create separate Easy Mode WOL logic.

## Easy Mode BOOTING State

Immediately after ON is pressed:

- Replace the OFFLINE presentation with a clear waking or booting indication.
- Prevent uncontrolled repeated ON clicks.
- Continue using the existing status model and polling logic.
- Do not expose technical probe detail.

Conceptually:

```text
+---------------------------+
|                           |
|         Turning on        |
|             ...           |
|                           |
+---------------------------+
```

The exact animation or indicator may be platform-native and simple.

## Easy Mode ONLINE State

When Host PC is ONLINE:

- Show a clear positive indication that the Host PC is on.
- Do not show an active ON button.
- Do not expose Sleep.
- Do not expose Shutdown.
- Do not expose technical information.

Conceptually:

```text
+---------------------------+
|                           |
|             ON            |
|        Host is ready      |
|                           |
+---------------------------+
```

The purpose is confirmation only.

Easy Mode is intentionally not a full power-management screen.

## Easy Mode UNKNOWN State

When state is UNKNOWN:

- Show a simple neutral status such as `Checking status`.
- Do not display raw network errors as the main UI.
- A small retry behavior is acceptable.
- Detailed diagnostics remain available in normal mode.

If the existing state model can safely allow ON during UNKNOWN, preserve current behavior.

Do not invent a second independent state machine.

## Easy Mode Startup Preference

If practical, add a preference:

```text
Start app in Easy Mode
```

This preference should be available on both platforms if implementation cost remains small.

The preference should persist between app launches.

If this would require unnecessary architecture changes, it may be implemented after the first Easy Mode version.

## Shared Logic Requirement

Easy Mode must reuse:

- Existing host configuration
- Existing WOL sender
- Existing Host Agent API client
- Existing status polling
- Existing OFFLINE / BOOTING / ONLINE / UNKNOWN semantics

Do not duplicate core power logic just to support a second screen.

Easy Mode should be a presentation layer over existing proven behavior.

## Windows Desktop Normal-Mode UI Cleanup

The current Windows Desktop normal-mode UI is functional but visually rough.

Improve the Windows Desktop UI while preserving its existing functionality.

Goals:

- Better information hierarchy
- Cleaner spacing
- Better grouping of host status
- Clearer primary and secondary actions
- More readable status presentation
- More polished button sizing and alignment
- More consistent typography
- Clearer separation between status, controls, and settings
- Reduce visual noise

Preserve access to current useful detailed information, including as appropriate:

- Host status
- Host IP
- Agent readiness
- SMB readiness
- Latency
- Power controls
- Settings

Do not remove working functionality merely to simplify the normal-mode screen.

## Windows Desktop Status Presentation

Status should remain understandable without relying only on color.

Use text plus visual indication.

Examples:

```text
ONLINE
OFFLINE
BOOTING
UNKNOWN
Going to sleep
Shutting down
```

The visual design may be improved, but state semantics must remain unchanged.

## Windows Desktop Power Controls

Normal mode should retain:

- ON
- Sleep
- Shutdown

Rules already established by the working implementation must remain:

- User actions must not be silently dropped by background polling.
- Repeated conflicting actions must be prevented while an action is running.
- BOOTING must not be overwritten by stale polling results.
- Existing WOL, Sleep, and Shutdown behavior must not regress.

## Android Normal UI

The current Android normal-mode UI is considered satisfactory.

Therefore:

```text
Android normal-mode UI redesign is explicitly out of scope.
```

Do not broadly restyle or restructure the existing Android screen.

Only make the minimum UI changes required to:

- Add Easy Mode entry
- Add Easy Mode screen
- Add Easy Mode exit
- Add optional Easy Mode startup preference

The existing normal Android visual design should otherwise remain unchanged.

## Cross-Platform Consistency

Desktop and Android do not need identical visual styling.

They should share:

- Same state meanings
- Same ON behavior
- Same Easy Mode behavior
- Same concept of OFFLINE
- Same concept of BOOTING
- Same concept of ONLINE
- Same concept of UNKNOWN

Platform-native presentation is preferred over forced visual uniformity.

## Out of Scope

Do not use this UX task to add:

- New cloud features
- Internet remote access
- New authentication systems
- New media playback features
- New SMB implementation
- New shutdown modes
- New multi-host architecture
- Android normal-mode redesign
- Large navigation frameworks
- A new cross-platform UI framework

This is a focused UX layer improvement over an already working MVP.

## Acceptance Criteria

### Desktop Easy Mode

Verified when:

```text
OFFLINE
-> large ON button visible
-> press ON
-> waking/booting indication appears immediately
-> Host wakes
-> screen reaches simple ON state
```

No Sleep or Shutdown controls appear in Easy Mode.

### Android Easy Mode

Verified with the same logical flow.

The existing Android normal screen remains materially unchanged.

### Desktop Normal Mode

Verified when:

- Existing functionality is preserved.
- Layout is visibly cleaner.
- Status remains clear.
- ON, Sleep, and Shutdown still work.
- No regression is introduced into polling or power-action concurrency.
