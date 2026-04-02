# Next Product Plan - 2026-04-03

This note records the next planned step before implementation.

## Goal

Add a thin live consumer UI on top of the proven bridge contract so ChromaLink feels like a usable desktop telemetry product, not just a set of scripts and raw JSON files.

## Why This Is The Optimal Next Step

The strip, reader, bridge contract, readiness checks, and inspector monitoring are already in place. The next highest-value move is to make the bridge output immediately legible in a dedicated consumer view.

This should happen before any more strip expansion because:

- the current baseline is already working live
- downstream usability is now the bottleneck
- a thin consumer UI will prove whether the current contract is sufficient

## Product Direction

Focus this pass on three outcomes:

1. Create a small live consumer UI that reads the rolling bridge snapshot.
2. Improve consumer-facing bridge helpers without changing decode behavior.
3. Make the new consumer easy to launch and discover.

## Constraints

- Keep the proven `640x360` strip baseline unchanged.
- Do not alter strip geometry or reader detection heuristics.
- Consume the rolling snapshot contract rather than bypassing it.
- Keep the new consumer lightweight and modular.

## Parallel Work Split

### Lane A - Live Consumer UI

Owns:

- a new `DesktopDotNet/ChromaLink.Monitor/*` project if needed
- or another isolated UI folder if a lighter structure is better

Deliver:

- a simple live monitor UI that reads `chromalink-live-telemetry.json`
- clear display of readiness, freshness, and core telemetry fields

Avoid:

- editing addon Lua
- editing reader detection logic
- broad CLI churn

### Lane B - Consumer Bridge Helpers

Owns:

- `scripts/*`
- narrow consumer-side helper files if needed

Deliver:

- helper scripts or launchers that make the live consumer easier to use
- contract-driven helpers only

Avoid:

- changing strip logic
- changing decode heuristics

### Lane C - Launch And Docs Integration

Owns:

- `README.md`
- notes files
- narrow launcher/discovery polish if needed

Deliver:

- clear way to launch the new live consumer
- concise docs for where it fits relative to the inspector and scripts

Avoid:

- changing functional telemetry behavior

## Validation Gates

Assemble only if all of these remain healthy:

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- build of any new consumer UI project
- `dotnet build .\DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj`
- one short live sample:
  - `dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 8 50 --backend screen`
- consumer launch path opens or reads the live snapshot successfully

## Success Criteria

- the user can open a dedicated live telemetry monitor without digging through raw JSON
- the consumer clearly shows ready/fresh/stale state
- the bridge contract remains the one source of truth
- repo ends in a clean, documented checkpoint
