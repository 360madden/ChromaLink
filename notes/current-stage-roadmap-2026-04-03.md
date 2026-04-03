# ChromaLink current stage, design, and roadmap (2026-04-03)

## Current stage
ChromaLink is past the “can we tap into Rift Meter?” stage.

It is now at:
- reliable strip transport baseline established
- compact Rift Meter combat frame implemented
- desktop decode implemented
- normalized merged combat contract implemented
- monitor and CLI proof surfaces updated

The current milestone is best described as:

> **compact end-to-end Rift Meter combat telemetry is live in the transport and available to downstream consumers.**

## Current design summary

### Core principle
Keep ChromaLink centered on the visible strip transport.

### Current source model
- native RIFT gather for always-hot player basics
- Rift Meter as combat enrichment
- desktop reader as the place where multi-frame correlation becomes stable app-facing data

### Current combat model
Combat information now comes from two layers:
- native `playerCombat`
- compact `riftMeterCombat`

Desktop normalization merges them into:
- `aggregate.combat`

That is the preferred design direction because it minimizes addon drift while keeping downstream contracts sane.

## Why this design is working
- minimal disruption to strip geometry and decoder foundations
- native player basics stay fast and authoritative
- Rift Meter adds value without taking ownership of the bridge
- desktop normalization absorbs correlation complexity instead of pushing it into every consumer

## Current strengths
- low-drift evolution from the existing codebase
- compact transport addition rather than broad protocol redesign
- live diagnostic visibility both in-game and on desktop
- proof surfaces aligned with the current mission

## Current weaknesses
- compact totals are lossy
- source-health semantics are still compact and need more live validation
- HTTP bridge docs still lag behind the normalized contract direction
- richer encounter metadata is not transported yet
- live rate stats like DPS/HPS/DTPS are not yet first-class strip telemetry

## Proposed future roadmap

### Breakthrough 1: semantic stabilization
Goal:
- confirm that current compact flags and normalized combat fields behave correctly in live use

Deliverables:
- verified meaning for degraded/stable snapshot behavior
- documented preferred downstream combat fields
- documented send-now live metrics, especially DPS/HPS/DTPS
- no protocol change required unless evidence demands it

### Breakthrough 2: app-facing contract cleanup
Goal:
- make the normalized combat view the clearly documented consumer surface

Deliverables:
- HTTP bridge docs updated around `aggregate.combat`
- downstream examples for local apps
- monitor/CLI/HTTP language aligned
- DPS/HPS/DTPS described as live-decision telemetry rather than post-fight analytics

### Breakthrough 3: live-rate transport evolution
Goal:
- promote DPS/HPS/DTPS into the immediate live telemetry set

Possible additions:
- add DPS/HPS/DTPS directly to the combat transport plan
- keep the evolution minimal and low-risk
- prefer explicit transport or normalized output over downstream guesswork

Constraint:
- treat DPS/HPS/DTPS as send-now metrics, not deferred analytics

### Breakthrough 4: richer secondary combat payload
Goal:
- improve richness without bloating the hot path

Possible additions:
- encounter/segment id or label
- less-lossy totals
- richer state flags
- low-frequency metadata frame

Constraint:
- keep `riftMeterCombat` compact and fast
- do not displace DPS/HPS/DTPS from the live first-class set

### Breakthrough 5: freshness-aware scheduling
Goal:
- use actual freshness/error evidence to drive runtime priority rather than simple combat-state bias

Possible additions:
- temporary burst when combat starts
- increased retry when a frame family goes stale
- adaptive pacing between `playerCombat` and `riftMeterCombat`

## Design guardrails for future work
- do not replace the strip with a hidden integration path
- do not make Rift Meter a hard dependency for basic telemetry
- do not let richer combat analytics harm the hot path
- do not re-expand scope into dashboard-first work unless the bridge is already solid

## Recommended next action
If continuing immediately, the highest-value next step is:
- live-verify the current combat contract and then add DPS/HPS/DTPS to the concrete transport plan as first-class live telemetry
