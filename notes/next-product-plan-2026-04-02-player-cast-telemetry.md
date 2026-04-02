# Next Product Plan - 2026-04-02 - Player Cast Telemetry

This note records the next planned step before implementation.

## Goal

Add a compact player-cast telemetry lane so ChromaLink can report the current cast bar and current spell in a form that is useful to leader/follower logic and external HUDs.

## Why This Is The Optimal Next Step

The audited RIFT API exposes a real castbar inspection surface:

- `Inspect.Unit.Castbar("player")`
- `Event.Unit.Castbar`

That gives ChromaLink a clean next telemetry expansion without changing the strip geometry or the proven `640x360` transport baseline.

## Product Direction

Focus this pass on one new rotating frame type:

1. `PlayerCast`

Represent it as a compact 12-byte payload with:

- cast-state flags
- cast progress
- quantized duration / remaining time
- a short transport-safe spell label derived from `abilityName`

## Why This Shape

The strip budget is still 12 payload bytes per frame. A full spell name is too expensive, but a short label plus cast timing is enough to make the current spell understandable on a HUD while keeping the transport stable.

## Constraints

- Keep the proven strip layout and payload size unchanged.
- Do not make bridge readiness depend on an active cast existing.
- Prefer `Inspect.Unit.Castbar` as the source of truth; do not depend on `UI.Native.Castbar` for data.
- Keep the implementation resilient when no cast is active or when `abilityName` is missing.

## Validation Gates

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- player-cast frame round-trips through renderer/analyzer tests
- telemetry aggregate accepts and exposes the new frame
- snapshot writer includes the new cast section without breaking current consumers

## Success Criteria

- the addon can emit player cast telemetry safely every rotation cycle
- the desktop reader decodes a dedicated `PlayerCast` frame
- the CLI and rolling snapshot surface current cast state in a useful compact form
