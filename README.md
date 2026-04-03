# ChromaLink

[![.NET 9](https://img.shields.io/badge/.NET-9-5C2D91?logo=dotnet)](https://dotnet.microsoft.com/)
[![Lua 5.1](https://img.shields.io/badge/Lua-5.1-2C2D72?logo=lua)](https://www.lua.org/)
[![RIFT MMO](https://img.shields.io/badge/RIFT-MMO-FF7A2F)](https://www.riftgame.com/)
[![License MIT](https://img.shields.io/badge/License-MIT-97CA00)](#)

ChromaLink is a reliability-first optical telemetry bridge for RIFT.

A Lua addon renders a structured color strip inside the game client. A `.NET 9` desktop stack captures that strip from the RIFT window, decodes it, validates it, and publishes a normalized live telemetry snapshot for other tools and apps.

The core design constraint is intentional:

> the game-to-desktop bridge is the on-screen strip itself.

If capture, decode, freshness, or synchronization fail, those failures are visible and debuggable in pixels rather than hidden behind an opaque API.

## Current Project Position

ChromaLink is now in a **telemetry-first, low-drift** phase.

What that means today:
- the strip/decoder pipeline remains the product center
- player basics stay the hot path
- Rift Meter is now **actually integrated** for compact combat telemetry
- desktop consumers now get a normalized combat view that merges native combat state with Rift Meter combat state
- monitor and CLI are proof/diagnostic tools, not the main product abstraction

## Current Implemented Architecture

```text
[RIFT Client @ 640x360 client area]
   ↓
[ChromaLink Lua Addon]
   ├─ gathers native player telemetry
   ├─ optionally samples Rift Meter combat state
   ├─ builds 24-byte transport payloads
   └─ renders a 640x24 strip using the P360C profile
         ↓ (pixels only)
[Desktop Reader]
   ├─ captures the client window
   ├─ locates strip geometry
   ├─ decodes symbols and validates CRCs
   ├─ aggregates live frame observations
   └─ publishes a rolling JSON contract
         ↓
[CLI / Monitor / HTTP Bridge / other local apps]
```

## Proven Baseline

### Window/profile
- profile: `P360C`
- RIFT client area: `640x360`
- strip: `640x24`
- segments: `80`
- segment size: `8x24`
- payload symbol range: segments `9-72`
- control markers: segments `1-8` and `73-80`
- alphabet: fixed `8-color`, `3 bits` per segment

### Desktop stack
- capture backends supported:
  - `PrintWindow`
  - `DesktopDuplication`
  - `ScreenBitBlt`
- current live workflow prefers `PrintWindow`
- build, smoke, replay, validate, and desktop solution build are working

## Active Transport Contract

Each strip frame carries:
- `24` transport bytes
- `12` payload bytes
- CRC-protected header and payload validation

### Active hot-path frames
The current baseline emphasizes:
- `coreStatus`
- `playerVitals`
- `playerResources`
- `playerCombat`
- `riftMeterCombat`

### Additional supported frames
These remain supported in the codebase and reader:
- `playerPosition`
- `playerCast`
- `targetPosition`
- `targetVitals`
- `targetResources`
- `followUnitStatus`
- `auxUnitCast`
- `auraPage`
- `textPage`
- `abilityWatch`

They are no longer the center of the project, but they remain useful for compatibility, diagnostics, and controlled expansion.

## Rift Meter Integration: Current State

Rift Meter is no longer just planned; it is partially integrated end-to-end.

### Implemented now
- addon-side Rift Meter adapter exists
- adapter safely inspects public/global Rift Meter data
- addon builds a dedicated `riftMeterCombat` frame
- `riftMeterCombat` is transported on the strip
- desktop reader decodes it
- CLI and monitor show it
- rolling JSON includes both:
  - raw `aggregate.riftMeterCombat`
  - normalized merged `aggregate.combat`

### Current `riftMeterCombat` purpose
It is a compact combat/health-of-source frame intended to prove:
- Rift Meter is present
- Rift Meter is readable
- active combat is visible with low latency
- desktop consumers can merge native combat state with Rift Meter combat state

### Current `riftMeterCombat` payload content
The compact payload currently carries:
- loaded / available / active flags
- overall totals present flag
- active duration present flag
- overall duration present flag
- degraded snapshot bit
- stable snapshot bit
- combat count
- active combat duration
- active combat player count
- active combat hostile count
- overall duration
- overall player count
- overall hostile count
- compact overall damage in K
- compact overall healing in K

### Important limitation
This is still a **compact proof-oriented combat bridge**, not a full-fidelity combat export.

Current limitations:
- damage/healing are lossy (`K` units)
- there is no rich encounter metadata yet
- there is no player-by-player or ability-by-ability combat contract yet
- source-health is better than before, but still compact

## Current Downstream Contract

The rolling machine-readable snapshot lives at:
- `%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out\chromalink-live-telemetry.json`

Current contract header:
- `contract.name = chromalink-live-telemetry`
- `contract.schemaVersion = 2`

### Important aggregate sections
- `aggregate.coreStatus`
- `aggregate.playerVitals`
- `aggregate.playerResources`
- `aggregate.playerCombat`
- `aggregate.riftMeterCombat`
- `aggregate.combat` ← preferred merged combat view
- aggregate freshness/health fields

### `aggregate.combat`
This normalized desktop-side combat view merges native `playerCombat` with `riftMeterCombat` and currently exposes:
- Rift Meter present / loaded / available / active
- Rift Meter degraded / stable snapshot bits
- native and Rift Meter sequences
- sequence delta
- observation skew ms
- combo / charge / planar / absorb
- combat count
- active combat duration
- overall duration
- compact overall damage/healing
- active/overall player and hostile counts

This is now the preferred downstream combat surface.

## Rotation And Priority

### Default baseline
The active baseline is telemetry-first and intentionally narrow.

### Combat-aware override
When Rift Meter combat is active and publishing is enabled, runtime rotation increases priority for:
- `playerCombat`
- `riftMeterCombat`
- player vitals/resources/core state

So the strip spends more bandwidth on combat-state freshness only when that matters.

## In-Game Commands

Current ChromaLink slash commands include the normal addon diagnostics plus Rift Meter diagnostics.

Important ones:
- `/cl status`
- `/cl build`
- `/cl diag`
- `/cl rotation`
- `/cl refresh`
- `/cl riftmeter`
- `/cl riftmeter status`
- `/cl riftmeter dump`

Rift Meter commands are intended for live verification of adapter shape and combat-time behavior.

## Desktop Tools

### CLI
`ChromaLink.Cli` currently supports:
- `smoke`
- `replay <bmpPath>`
- `live [sampleCount] [sleepMs]`
- `watch [durationSeconds] [sleepMs]`
- `bench`
- `validate`
- `capture-dump`
- `prepare-window [left] [top]`

Useful commands:

```powershell
dotnet build .\DesktopDotNet\ChromaLink.sln
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- validate
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- prepare-window 32 32
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 5 100
```

### Monitor
The monitor is now a proof/diagnostic surface showing:
- contract/freshness/readiness
- player vitals/resources
- player combat basics
- Rift Meter combat summary
- merged combat cues where available

### HTTP Bridge
The HTTP bridge exposes the rolling snapshot over localhost and is the intended app-facing direction.

Endpoints:
- `/latest-snapshot`
- `/snapshot`
- `/health`
- `/ready`

Default base URL:
- `http://127.0.0.1:7337/`

## Design Rules

1. **Keep the strip as the product center.**
   Avoid replacing the visible transport with hidden direct app integration.

2. **Prefer low drift over reinvention.**
   Preserve strip geometry, protocol framing, reader assumptions, and proven tooling unless there is a concrete need to change them.

3. **Player basics stay authoritative in native gather.**
   HP, resources, and basic state should remain available even if Rift Meter is absent or degraded.

4. **Rift Meter is enrichment, not ownership of the bridge.**
   It improves combat context, but should not become a hidden hard dependency for the whole pipeline.

5. **Normalize downstream contracts on desktop.**
   The desktop side is where multi-frame correlation and app-facing contracts should stabilize.

## Proposed Roadmap

### Near-term roadmap
1. **Live-verify the new source-health bits**
   - confirm degraded/stable semantics during idle, combat, and post-combat conditions

2. **Strengthen the normalized combat contract**
   - define it as the preferred HTTP/app-facing combat surface
   - document fields and expected semantics

3. **Tune combat-time rotation using freshness evidence**
   - shift from simple combat override to freshness/error-aware prioritization

### Mid-term roadmap
4. **Add a richer secondary combat frame**
   - keep `riftMeterCombat` compact for the hot path
   - add a lower-frequency richer frame for less-lossy totals and encounter metadata

5. **Promote HTTP bridge as the official downstream interface**
   - treat monitor/CLI as diagnostics
   - treat HTTP/JSON as the stable consumer surface

6. **Tighten the minimal live proof product**
   - focus the UI on HP, resources, combat, freshness, and source health

### Longer-term roadmap
7. **Expand carefully beyond combat basics**
   - richer target telemetry
   - encounter metadata
   - more precise combat summaries
   - optional specialized frames only when they do not harm reliability

## What Is Not The Focus Right Now

Not current priorities:
- large dashboard/product UX work
- broad feature expansion just because the codebase supports it
- replacing the strip with a hidden integration path
- deleting all old frame types immediately

The current strategy is to **preserve working context, narrow priorities, and expand only where reliability is already proven**.

## Key Docs

- `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\notes\refocus-plan-2026-04-03-riftmeter.md`
- `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\notes\current-stage-roadmap-2026-04-03.md`
- `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.HttpBridge\README.txt`

## License

MIT
