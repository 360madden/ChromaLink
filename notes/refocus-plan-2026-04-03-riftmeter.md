# ChromaLink refocus plan (telemetry-first, low-drift)

## Goal
Refocus ChromaLink around the existing strip transport and decoder pipeline while minimizing drift from the current codebase.

Primary objective:
- maximize reliable low-latency telemetry from RIFT into the ChromaLink decoder
- make decoded telemetry available to other apps
- keep only a minimal live proof surface for player basics

Secondary objective:
- integrate Rift Meter as an optional upstream enrichment source without making ChromaLink depend on it for core player vitals
- defer live Rift Meter integration work until the RIFT client is available again

## Architectural stance
ChromaLink remains the owner of:
- strip layout and rendering
- transport protocol and framing
- decoder compatibility
- normalized desktop telemetry publishing

Rift Meter becomes an optional source for:
- combat/encounter context
- aggregate combat state
- prioritization hints
- compact combat summaries where stable public fields exist

Native ChromaLink gathering remains authoritative for:
- current HP / max HP / HP%
- current resource / max resource / resource%
- basic player state flags
- basic cast state
- transport sequence / timing

## Preserve first
Do not redesign these first:
- `Core/Protocol.lua` symbol/byte framing
- `RIFT/Render.lua` strip rendering model
- `DesktopDotNet/ChromaLink.Reader` capture/decode foundations
- `P360C` transport profile
- current control markers / palette / geometry

## Refocus by file

### 1) `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\Core\Config.lua`
Purpose in refocus:
- keep the current protocol/profile ids stable
- narrow active telemetry rotation without deleting old concepts

Changes:
- shorten `frameRotation` so the default schedule prioritizes:
  - `coreStatus`
  - `playerVitals`
  - `playerResources`
  - `playerCombat` (or compact combat summary if repurposed)
- de-prioritize by default, but do not remove:
  - `targetVitals`
  - `targetResources`
  - `targetPosition`
  - `followUnitStatus`
  - `auxUnitCast`
  - `auraPage`
  - `textPage`
  - `abilityWatch`
- add feature flags for optional Rift Meter enrichment

Recommended near-term default rotation:
1. `coreStatus`
2. `playerVitals`
3. `coreStatus`
4. `playerResources`
5. `coreStatus`
6. `playerCombat`

Optional later:
- inject one low-frequency optional frame every N cycles

### 2) `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\Core\Gather.lua`
Purpose in refocus:
- remain the central assembly point for addon-side telemetry
- merge native data with optional Rift Meter enrichment

Changes:
- keep existing safe native wrappers intact
- add a merge step for a Rift Meter adapter snapshot
- make native player basics the authoritative source for always-hot values
- add a compact merged snapshot shape that clearly separates:
  - native basics
  - optional Rift Meter enrichment
  - source availability flags

Fields to prioritize in gathered snapshot:
- playerHealthCurrent
- playerHealthMax
- playerHealthPct
- playerResourceKind
- playerResourceCurrent
- playerResourceMax
- playerResourcePct
- playerAlive
- playerCombat
- playerCasting
- sequence / timestamp hints
- riftMeterLoaded
- riftMeterCombatActive
- riftMeterCombatDurationMs
- riftMeterOverallDamage (optional)
- riftMeterOverallHealing (optional)

### 3) `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\Core\RiftMeterAdapter.lua` (new)
Purpose in refocus:
- isolate all Rift Meter integration logic in one place
- prevent tight coupling between ChromaLink gather logic and Rift Meter internals

Responsibilities:
- detect whether `RiftMeter` global exists
- safely inspect public tables like `RM.combats` and `RM.overall`
- expose a small normalized adapter result
- fail safely when Rift Meter is absent or incompatible

Initial adapter output:
- `loaded`
- `available`
- `inCombat`
- `combatCount`
- `activeCombatDurationMs` if safely derivable
- `overallDamage` if safely derivable
- `overallHealing` if safely derivable
- `warnings` / `shapeVersion` as diagnostics

Rule:
- do not read or depend on Rift Meter UI frames
- prefer public globals and stable table summaries only

### 4) `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\Core\Protocol.lua`
Purpose in refocus:
- preserve transport encoding and decoder compatibility as much as possible

Changes:
- keep current framing and CRC logic intact
- keep existing frame ids if practical
- keep current `coreStatus`, `playerVitals`, `playerResources`, `playerCombat` paths as the main active transport
- if needed, repurpose spare bytes in `playerCombat` for compact Rift Meter enrichment before adding a new frame type

Priority:
- avoid introducing new frame types until the narrowed transport is proven insufficient

### 5) `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\RIFT\Bootstrap.lua`
Purpose in refocus:
- control the runtime rotation and priority policy with minimal structural drift

Changes:
- simplify live rotation to the new narrow default
- gate optional pages behind low-frequency or disabled-by-default scheduling
- prepare a small hook point where combat-active state can raise the priority of `playerCombat`
- keep existing scheduling helpers unless they directly interfere with the narrow baseline

Later enhancement:
- event-driven burst policy after significant changes:
  - large HP/resource changes
  - combat enter/exit
  - cast start/stop

### 6) `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\RIFT\Commands.lua`
Purpose in refocus:
- add visibility into the new source model without bloating scope

Changes:
- keep existing commands unless they become misleading
- add or update diagnostics so `/cl status` or `/cl diag` can show:
  - native telemetry status
  - Rift Meter detected: yes/no
  - Rift Meter readable: yes/no
  - active narrow rotation

### 7) `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.Reader`
Purpose in refocus:
- keep decoder behavior stable while narrowing what is treated as first-class output

Changes:
- retain support for current decoded frame set initially
- define a normalized telemetry model centered on:
  - transport freshness
  - player vitals
  - player resources
  - combat state
  - optional Rift Meter enrichment
- maintain diagnostics for rejected/accepted frames and freshness

### 8) `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.HttpBridge`
Purpose in refocus:
- become the primary app-facing surface

Changes:
- publish a stable current snapshot contract
- expose health/freshness metadata
- avoid making monitor/inspector the main product abstraction

Suggested outward contract sections:
- `contract`
- `transport`
- `player`
- `riftMeter`
- `health`

### 9) `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.Monitor`
Purpose in refocus:
- become a minimal proof surface only

Changes:
- show only:
  - HP current/max/%
  - resource current/max/%
  - combat/casting
  - freshness/latency
  - Rift Meter loaded/combat active
- de-emphasize broader dashboard behavior

### 10) `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\README.md`
Purpose in refocus:
- align documentation with the telemetry-first mission while preserving current context

Changes:
- describe ChromaLink as:
  - strip transport
  - decoder
  - app-facing telemetry bridge
- describe Rift Meter as optional enrichment
- reduce emphasis on broad product/dashboard framing
- clearly distinguish:
  - proven transport baseline
  - minimal proof UI
  - optional internal tools

## Order of implementation

### Milestone 1: scope narrowing without protocol churn
1. update docs/notes
2. narrow default frame rotation in `Core/Config.lua`
3. adjust runtime rotation logic in `RIFT/Bootstrap.lua`
4. keep decoder compatibility intact

Success check:
- current decoder still works
- player basics are fresher and more frequent

### Milestone 2: Rift Meter adapter
1. add `Core/RiftMeterAdapter.lua`
2. merge adapter snapshot into `Core/Gather.lua`
3. expose adapter status via addon diagnostics

Success check:
- ChromaLink runs with and without Rift Meter installed
- no addon load failures
- adapter state is visible in diagnostics

Offline note:
- while RIFT is offline, do not rely on runtime verification of Rift Meter structures
- limit current work to adapter boundaries, config changes, and documentation unless a non-live test path is available

### Milestone 3: compact combat enrichment
1. map stable Rift Meter fields into existing combat-oriented payload
2. prefer existing frame ids before adding new ones
3. update desktop normalization to surface optional Rift Meter data

Success check:
- downstream snapshot includes optional `riftMeter` section
- no regression in player basics decode

### Milestone 4: app-facing bridge cleanup
1. make HTTP bridge the official outward-facing surface
2. shrink monitor to proof-of-life stats
3. leave richer tools as secondary/internal

Success check:
- another local app can consume one stable snapshot
- proof UI shows low-latency basics plus bridge health

## Risks
- Rift Meter public structures may not be a stable API
- active combat details may require runtime shape verification
- over-packing combat enrichment too early could create protocol churn
- removing old telemetry pages too soon would increase drift and reduce test coverage

## Guardrails
- preserve current transport geometry and decoder assumptions first
- add Rift Meter through one adapter module only
- prefer de-prioritizing old telemetry over deleting it immediately
- keep native player basics as the low-latency guaranteed baseline
- make each step live-verifiable before pruning more

## Immediate next coding step
Implement Milestone 1 first:
- tighten `frameRotation` in `Core/Config.lua`
- adjust `RIFT/Bootstrap.lua` only as needed to honor the narrowed rotation cleanly
- leave protocol ids and decoder logic untouched
