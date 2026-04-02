# Next Product Plan - 2026-04-02 - Expanded Telemetry Capabilities

This note records the planned step before implementation and the shape that was ultimately kept after code verification.

## Goal

Expand ChromaLink to cover the next requested telemetry capabilities without changing the proven strip geometry or regressing the live `640x360` baseline.

## Requested Capability Set

- cast target if exposed with the active ability
- cast duration / remaining with finer precision
- interruptible vs uninterruptible state
- combo points
- charge
- planar charges
- mana / energy / power as separate explicit stats
- absorb
- aggro
- blocked / line-of-sight state
- AFK / offline for party-follow use
- coordinates for self
- coordinates for target

## RIFT API Reality

The audited API supports this split cleanly:

- player and target stats via `Inspect.Unit.Detail`
- castbar state via `Inspect.Unit.Castbar`
- active ability target via `Inspect.Ability.New.Detail(ability).target`
- party-follow unit references via `group01` through `group20`

Important caveats:

- `aggro` and `blocked` are groupmember-only
- `offline` is groupmember-only
- `afk` is player and groupmember scoped
- target coordinates are available only when the target detail is inspectable

## Optimal Product Direction

Preserve the currently proven frames and add new rotating frames instead of widening the strip or overloading the heartbeat.

Add this frame set:

1. `PlayerCast`
   - upgrade the existing slice to carry fine-grained duration / remaining
   - add cast target code
   - keep a compact spell-label field

2. `PlayerResources`
   - explicit mana current/max
   - explicit energy current/max
   - explicit power current/max

3. `PlayerCombat`
   - combo
   - charge current/max
   - planar current/max
   - absorb

4. `TargetPosition`
   - x / y / z for current target

5. `FollowUnitStatus`
   - watched group member slot
   - AFK / offline / aggro / blocked / ready flags
   - compact follow-useful state and coordinates

## Why This Is Optimal

- avoids destabilizing already proven frames
- keeps each payload coherent and easy to reason about
- matches the actual API availability boundaries
- gives leader/follower and external HUD consumers useful slices instead of one overloaded frame

## Constraints

- keep transport payloads at `12` bytes
- keep frame types and docs explicit
- do not make aggregate readiness depend on optional target or groupmember slices
- prefer compact codes and quantized values over transporting long strings

## Validation Gates

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- each new frame round-trips through renderer/analyzer tests
- aggregate and snapshot contract remain stable
- any live validation should be done after code-side verification is green

## Success Criteria

- requested stats are represented by explicit new telemetry slices
- player, target, and watched-group-member data are separated by actual availability rules
- the current live-proven baseline remains intact while payload coverage grows

## Implementation Outcome

Kept in code and tests:

- upgraded `PlayerCast`
- added `PlayerResources`
- added `PlayerCombat`
- added `TargetPosition`
- added `FollowUnitStatus`

Verified so far:

- full solution tests green
- CLI and inspector builds green
- live validation still pending for the newly added post-cast slices
