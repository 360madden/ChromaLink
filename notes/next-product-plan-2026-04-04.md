# Next Product Plan - 2026-04-04

This note records the next planned step before implementation.

## Goal

Expose the proven bridge snapshot through a tiny local HTTP surface so other tools can integrate with ChromaLink without tailing files directly.

## Why This Is The Optimal Next Step

ChromaLink now has:

- a working strip and reader
- a rolling JSON bridge contract
- readiness and freshness checks
- a live monitor UI

The next clean move is to add a simple local API on top of that contract. This improves interoperability without touching the strip or decoder.

## Product Direction

Focus this pass on three outcomes:

1. Add a tiny local HTTP bridge that serves the latest telemetry snapshot and health.
2. Add simple launch helpers for that bridge.
3. Document how the HTTP layer fits relative to the JSON snapshot, monitor, and readiness script.

## Constraints

- Keep the proven `640x360` strip baseline unchanged.
- Do not alter strip geometry or decode heuristics.
- The rolling snapshot remains the source of truth.
- Keep the HTTP surface local-only and lightweight.

## Parallel Work Split

### Lane A - HTTP Bridge

Owns:

- a new `DesktopDotNet/ChromaLink.HttpBridge/*` project if needed

Deliver:

- a minimal local HTTP listener
- endpoints for latest snapshot and health/readiness
- no dependency on the strip decoder directly

Avoid:

- addon Lua
- reader detection logic
- broad changes outside the new project unless required for solution wiring

### Lane B - Launch Helpers

Owns:

- `scripts/*`

Deliver:

- launch/stop or open helpers for the local HTTP bridge
- small helper scripts only

Avoid:

- functional decode changes
- broad docs changes

### Lane C - Docs Integration

Owns:

- `README.md`
- notes files

Deliver:

- product-facing explanation of the HTTP layer
- clear distinction between:
  - rolling JSON snapshot
  - live monitor UI
  - readiness check
  - local HTTP bridge

Avoid:

- functional telemetry changes

## Validation Gates

Assemble only if all of these remain healthy:

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- build of the new HTTP bridge project
- `dotnet build .\DesktopDotNet\ChromaLink.Monitor\ChromaLink.Monitor.csproj`
- one short live sample:
  - `dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 8 50 --backend screen`
- one local API probe against the bridge

## Success Criteria

- another local tool can read ChromaLink state over HTTP without parsing files directly
- the JSON snapshot remains the backing contract
- the repo ends in a clean, documented checkpoint
