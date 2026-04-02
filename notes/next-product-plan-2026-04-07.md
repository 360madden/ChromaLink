# Next Product Plan - 2026-04-07

This note records the next planned step before implementation.

## Goal

Add simple lifecycle management for the ChromaLink local stack so starting, stopping, and checking the bridge workflow feels like operating a real local product instead of a loose collection of launchers.

## Why This Is The Optimal Next Step

ChromaLink now has several runnable local surfaces:

- rolling snapshot
- WinForms monitor
- browser dashboard
- local HTTP bridge

The missing piece is operational ergonomics. We can start pieces easily, but lifecycle control is still uneven.

## Product Direction

Focus this pass on three outcomes:

1. Add clean stop/status helpers for the bridge stack.
2. Make the current launchers easier to reason about operationally.
3. Document the lifecycle flow in a way that is easy to follow.

## Constraints

- Keep the proven `640x360` strip baseline unchanged.
- Do not alter strip geometry or decode heuristics.
- Keep the lifecycle helpers local and lightweight.
- Do not over-engineer process orchestration; simple Windows-native control is enough.

## Parallel Work Split

### Lane A - Lifecycle Scripts

Owns:

- `scripts/*`

Deliver:

- simple stop/status helpers for the bridge stack
- low-risk lifecycle scripts for the local tools

Avoid:

- broad bridge behavior changes
- addon Lua

### Lane B - Status Alignment

Owns:

- `scripts/*`
- narrow bridge/status helpers if needed

Deliver:

- ensure lifecycle/status commands line up with readiness and HTTP probe behavior
- keep status output practical

Avoid:

- UI changes
- decode changes

### Lane C - Docs Integration

Owns:

- `README.md`
- notes/*

Deliver:

- concise lifecycle docs:
  - start
  - inspect
  - verify
  - stop

Avoid:

- functional behavior changes

## Validation Gates

Assemble only if all of these remain healthy:

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- `dotnet build .\DesktopDotNet\ChromaLink.HttpBridge\ChromaLink.HttpBridge.csproj`
- one short live sample:
  - `dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 8 50 --backend screen`
- status/stop scripts execute without error

## Success Criteria

- the local ChromaLink stack has a coherent operational story
- users can start, verify, and stop it without guessing
- repo ends in a clean, documented checkpoint
