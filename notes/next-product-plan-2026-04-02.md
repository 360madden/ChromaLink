# Next Product Plan - 2026-04-02

This note records the next planned step before implementation.

## Goal

Move ChromaLink from "working bridge" to "usable product bridge" without destabilizing the proven strip baseline.

The strip and reader are already proving live multi-frame telemetry at `640x360`. The next optimal step is to improve bridge usability and trust, not to change strip geometry again.

## Product Direction

Focus this pass on three outcomes:

1. Make the rolling bridge snapshot easier for other tools to trust.
2. Make the inspector more useful as a live-first bridge monitor.
3. Add one small downstream consumer path that proves the bridge can be consumed without hand-reading JSON.

## Constraints

- Keep the proven `640x360` strip baseline unchanged.
- Do not widen the strip or alter segment geometry.
- Avoid changing detection heuristics unless required by integration.
- Keep new work additive and modular.

## Parallel Work Split

### Lane A - Bridge Health Contract

Owns:

- `DesktopDotNet/ChromaLink.Cli/*`

Deliver:

- additive bridge-health fields in the rolling JSON snapshot
- freshness/staleness signals that downstream tools can trust
- minimal console output changes if useful

Avoid:

- inspector files
- Lua addon files
- reader detection logic

### Lane B - Inspector Live-First Workflow

Owns:

- `DesktopDotNet/ChromaLink.Inspector/*`

Deliver:

- a more obvious live-bridge experience when using the inspector
- low-risk UX that helps monitor the rolling snapshot directly

Avoid:

- CLI files
- Lua addon files
- decode-path changes

### Lane C - Sample Consumer Tooling

Owns:

- `scripts/*` for consumer-side additions
- optionally a new `DesktopDotNet/ChromaLink.Consumer/*` folder if a tiny consumer app is justified

Deliver:

- one practical downstream consumer path beyond raw JSON
- keep it thin and contract-driven

Avoid:

- modifying addon Lua
- changing existing reader detection behavior

## Validation Gates

Assemble only if all of these remain healthy:

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- `dotnet build .\DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj`
- one short live sample:
  - `dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 8 50 --backend screen`

## Success Criteria

- bridge snapshot clearly communicates readiness and freshness
- inspector can serve as a live monitor, not just a BMP analyzer
- at least one downstream consumer path is simple enough to hand to another tool or user
- repo ends in a clean, documented checkpoint
