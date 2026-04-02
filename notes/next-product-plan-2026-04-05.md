# Next Product Plan - 2026-04-05

This note records the next planned step before implementation.

## Goal

Stabilize the new bridge layers by adding contract-focused tests around the rolling snapshot and the local HTTP bridge.

## Why This Is The Optimal Next Step

ChromaLink now has several consumer-facing layers on top of the strip:

- rolling JSON snapshot
- readiness/freshness checks
- monitor UI
- local HTTP bridge

Before adding more product features, the best next move is to lock the current contracts down so they do not drift accidentally.

## Product Direction

Focus this pass on three outcomes:

1. Add tests that protect the rolling snapshot contract shape and key fields.
2. Add tests that protect the local HTTP bridge endpoints and response behavior.
3. Tighten docs only where the real contract needs to be stated more explicitly.

## Constraints

- Keep the proven `640x360` strip baseline unchanged.
- Do not alter strip geometry or decode heuristics.
- Prefer tests and contract hardening over new feature expansion in this pass.
- Keep the rolling snapshot as the source of truth for the HTTP layer.

## Parallel Work Split

### Lane A - HTTP Bridge Tests

Owns:

- `DesktopDotNet/ChromaLink.HttpBridge/*`
- test additions that cover the HTTP layer

Deliver:

- automated tests for `/snapshot`, `/latest-snapshot`, `/health`, and `/ready`
- coverage for healthy and stale/not-ready behavior

Avoid:

- addon Lua
- reader detection changes

### Lane B - Snapshot Contract Tests

Owns:

- `DesktopDotNet/ChromaLink.Tests/*`
- narrow shared helpers if needed

Deliver:

- tests that verify important JSON snapshot fields and structure
- coverage for readiness/freshness semantics where practical

Avoid:

- broad product docs churn
- UI changes

### Lane C - Docs Alignment

Owns:

- `README.md`
- notes/*

Deliver:

- concise contract wording where it helps future contributors
- ensure docs match the now-real HTTP bridge and snapshot behavior

Avoid:

- changing functional behavior

## Validation Gates

Assemble only if all of these remain healthy:

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- `dotnet build .\DesktopDotNet\ChromaLink.HttpBridge\ChromaLink.HttpBridge.csproj`
- `dotnet build .\DesktopDotNet\ChromaLink.Monitor\ChromaLink.Monitor.csproj`
- one short live sample:
  - `dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 8 50 --backend screen`
- one bridge probe:
  - `.\scripts\Probe-ChromaLinkHttpBridge.cmd`

## Success Criteria

- consumer-facing bridge layers have automated safety rails
- JSON and HTTP contracts are clearer and less likely to regress
- repo ends in a clean, documented checkpoint
