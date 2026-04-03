# Addon harvest plan

## Objective
Use the local reference addons and archives to improve ChromaLink without taking hard dependencies on them.

## What to borrow

### `RIFT\\LLM_RIFT_API_v2_audited.zip`
Borrow:
- event inventory
- safe use of ability / buff / unit events
- live-update API pairings such as `Inspect.*.Detail` + `Event.*`

Deliverable:
- event-driven cache layer in `Core\\StateCache.lua`

### `RIFT\\RiftMeter-v1.2.zip`
Borrow:
- combat signal prioritization
- practical definition of useful combat state

Deliverable:
- `notes\\telemetry-budget.md`
- future telemetry registry priorities

### `RIFT\\nkUI-V1.5.0.zip`
Borrow:
- settings/default merge pattern
- grouped settings UI pattern
- deferred work / watchdog-safe queue idea

Deliverable:
- future `Core\\Settings.lua`
- future `RIFT\\ConfigUI.lua`

### `RIFT\\king-molinator-2.0.0.5.zip`
Borrow:
- trigger registry model
- encounter active / phase / objective model
- cast / tank-swap / buff-priority patterns
- cadence split between fast and slow update tasks

Deliverable:
- future `Core\\TriggerRegistry.lua`
- future `Core\\EncounterState.lua`
- future scheduler burst priorities

## What not to borrow directly
- do not import KBM boss packs
- do not import nkUI wholesale
- do not turn ChromaLink into a damage meter clone
- do not add third-party runtime dependencies just because the code exists locally

## Implementation order
1. design docs
2. event cache foundation
3. telemetry registry
4. dual-strip scheduler split
5. action acknowledgment
6. config UI
7. encounter mode

## Definition of success
- strip 1 stays focused on combat heartbeat
- strip 2 adapts by mode and recent events
- cache reduces repeated polling
- future config UI can toggle individual tracked items across groups
