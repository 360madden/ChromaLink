# Next Product Plan - 2026-04-02 - Remaining Telemetry Phase

This note records the optimal plan before implementing the next remaining telemetry capabilities.

## Goal

Cover the remaining practical telemetry asks without widening the strip, breaking the proven bridge, or pretending unsupported API surfaces exist.

## Directly Feasible in the Audited API

- target cast bar via `Inspect.Unit.Castbar`
- target explicit health/resource/absorb via `Inspect.Unit.Detail`
- buffs/debuffs on player, target, and group units via `Inspect.Buff.List` and `Inspect.Buff.Detail`
- names and zone/shard text via `Inspect.Unit.Detail`, `Inspect.Zone.Detail`, and `Inspect.Shard`
- per-ability cooldown/readiness via `Inspect.Ability.New.Detail`
- multiple group-member status by rotating slot-aware group frames instead of a single watched slot
- group-member cast bars via `Inspect.Unit.Castbar(groupXX)`
- player `pvp`, `mentoring`, `ready`, and `afk` flags

## Not Clearly Exposed

- live movement speed
- heading / facing
- current mount state
- explicit global cooldown
- party leader identity
- exemplar
- soul/build detail

These should not be faked into the telemetry contract.

## Optimal Frame Strategy

Preserve existing live-proven frame types and use the remaining frame-type budget for generic reusable slices:

1. `TargetVitals`
   - health current/max
   - absorb
   - target flags if useful

2. `TargetResources`
   - explicit mana current/max
   - energy current/max
   - power current/max

3. `AuxUnitCast`
   - generic compact cast frame for target or group slots
   - unit selector in payload

4. `AuraPage`
   - generic page for player/target buffs or debuffs
   - two compact aura entries plus total count

5. `TextPage`
   - generic short-text lane for:
     - player name
     - target name
     - zone name
     - shard name

6. `AbilityWatch`
   - configurable tracked-ability cooldown/readiness pages

## Existing Frame Upgrades

- keep `PlayerCast` as-is for the current richer payload
- expand `PlayerCombat` flags to include:
  - `pvp`
  - `mentoring`
  - `ready`
  - `afk`
- evolve `FollowUnitStatus` into a rotating multi-slot group-member channel while keeping the existing slot byte in the payload

## Reader / Bridge Rules

- do not make bridge readiness depend on the new optional slices
- keep the aggregate baseline centered on:
  - `CoreStatus`
  - `PlayerVitals`
  - `PlayerPosition`
- expose new optional slices additively in JSON, CLI, and inspector

## Validation Gates

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- CLI build
- inspector build
- live reload only after code-side verification is green

## Success Criteria

- target cast and target explicit pools are visible through the strip
- player/target aura summaries are visible in compact pages
- names/zone/shard have transport-safe text pages
- configurable ability cooldown/readiness pages exist
- multiple group slots can rotate through the group-status channel
- unsupported items remain explicitly unsupported instead of implied
