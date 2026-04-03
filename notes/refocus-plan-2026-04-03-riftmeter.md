# ChromaLink refocus plan (updated 2026-04-03)

## Status summary
This plan started as a refocus note. It is now also a status note.

The original refocus direction has largely been implemented:
- ChromaLink remained centered on the existing strip/render/protocol/decode pipeline
- default transport priority was narrowed around player basics
- Rift Meter was integrated as an optional enrichment source rather than replacing native gather
- a dedicated compact `riftMeterCombat` transport frame was added
- desktop consumers now receive a normalized merged combat view

So this document now tracks:
- what is done
- what remains in progress
- what should happen next

## What is implemented

### Addon side
Implemented in the current codebase:
- narrowed default telemetry-first rotation
- combat-aware runtime priority boost for `riftMeterCombat`
- `Core/RiftMeterAdapter.lua`
- safe public/global Rift Meter inspection
- `BuildRiftMeterCombatSnapshot()` in `Core/Gather.lua`
- dedicated `riftMeterCombat` frame encoding in `Core/Protocol.lua`
- in-game diagnostics:
  - `/cl riftmeter`
  - `/cl riftmeter status`
  - `/cl riftmeter dump`

### Desktop side
Implemented in the current codebase:
- reader support for frame type `15` / `RiftMeterCombat`
- aggregate storage of Rift Meter combat frames
- normalized combat model that merges native `playerCombat` with `riftMeterCombat`
- rolling JSON contract with:
  - raw `aggregate.riftMeterCombat`
  - preferred normalized `aggregate.combat`
- monitor support for Rift Meter combat
- CLI summary support for Rift Meter combat

## Architectural stance

### ChromaLink owns
- strip layout and rendering
- protocol framing
- decoder compatibility
- desktop normalization
- app-facing contract evolution

### Native gather remains authoritative for
- player HP / max HP
- player resources
- basic player state flags
- basic cast state
- always-hot low-latency proof data

### Rift Meter currently provides
- compact combat presence/state
- combat counts and durations
- compact overall totals
- source-health cues through compact flags
- the strongest candidate set for next live-rate telemetry: DPS / HPS / DTPS

This remains the right split.

## Current design

### Transport baseline
Keep stable unless there is strong evidence to change it:
- `P360C`
- `640x360` client baseline
- `640x24` strip
- existing strip geometry / control markers / palette
- CRC/header model

### Active transport priorities
Current priority emphasis:
- `coreStatus`
- `playerVitals`
- `playerResources`
- `playerCombat`
- `riftMeterCombat`

### Downstream contract direction
Preferred downstream surface is now:
- desktop normalized JSON
- especially `aggregate.combat`

Raw frame sections still matter for debugging, but downstream apps should increasingly consume normalized sections instead of piecing raw frames together themselves.

## Current Rift Meter frame semantics

`riftMeterCombat` is intentionally compact.

Current meaning of major flags:
- loaded
- available
- active
- has overall totals
- has active duration
- has overall duration
- degraded snapshot
- stable snapshot

Current payload family:
- combat count
- active combat duration
- overall duration
- active/overall player counts
- active/overall hostile counts
- compact overall damage in K
- compact overall healing in K

This is good enough for low-latency proof and source-health signaling, but not yet good enough for rich combat analytics.

## Remaining gaps

### 1) Live semantic verification
Still needed:
- verify `degraded` vs `stableSnapshot` behavior through real combat and post-combat cycles
- confirm that the compact flags stay meaningful across different combat states

### 2) Better downstream documentation
Still needed:
- formally document `aggregate.combat`
- define which fields downstream apps should treat as authoritative
- make the HTTP bridge docs point to the normalized combat section directly
- document DPS/HPS/DTPS as send-now live combat metrics

### 3) Richer combat evolution path
Still needed:
- preserve `riftMeterCombat` as the hot-path frame
- promote DPS/HPS/DTPS into the immediate live telemetry set
- add a lower-frequency richer combat frame only if the compact frame proves insufficient

## Recommended next roadmap

### Phase A: validate and stabilize
1. live-verify the current compact frame flags
2. freeze `aggregate.combat` semantics once verified
3. document preferred downstream usage
4. lock DPS/HPS/DTPS into the send-now live contract plan

### Phase B: improve app-facing contract
1. make HTTP bridge docs and outputs emphasize normalized combat
2. keep CLI and monitor aligned with the same contract
3. avoid forcing downstream apps to decode raw frame semantics
4. treat DPS/HPS/DTPS as first-class live combat outputs

### Phase C: richer combat only if justified
1. add a secondary lower-frequency frame for richer totals or encounter metadata
2. keep the hot path focused on compact combat state plus DPS/HPS/DTPS
3. do not break current reader compatibility without a strong reason

## Guardrails
- preserve current strip geometry first
- preserve low-latency player basics first
- keep Rift Meter optional at the source level
- prefer desktop normalization over addon-side overpacking
- do not broaden scope back into dashboard-first development

## Immediate recommended next work
1. live verification pass of the current Rift Meter flags
2. add DPS/HPS/DTPS to the concrete transport evolution plan
3. update HTTP bridge documentation/examples around normalized combat plus live rates
4. only then consider a richer secondary combat frame
