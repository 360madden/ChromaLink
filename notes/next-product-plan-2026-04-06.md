# Next Product Plan - 2026-04-06

This note records the next planned step before implementation.

## Goal

Serve a tiny browser dashboard from the local HTTP bridge so ChromaLink telemetry can be viewed from any browser without opening the WinForms monitor.

## Why This Is The Optimal Next Step

The bridge contract and HTTP layer are now stable enough to support another consumer surface.

A browser dashboard is a good next move because:

- it builds directly on the now-tested HTTP bridge
- it gives a zero-install view for the local machine
- it complements the WinForms monitor instead of replacing it

## Product Direction

Focus this pass on three outcomes:

1. Add a tiny served dashboard page on the local HTTP bridge.
2. Keep the page simple, readable, and tied to the existing HTTP endpoints.
3. Make launch/discovery straightforward from the current scripts and README.

## Constraints

- Keep the proven `640x360` strip baseline unchanged.
- Do not alter strip geometry or decode heuristics.
- Use the local HTTP bridge as the source for dashboard data.
- Keep the dashboard lightweight and mostly static.

## Parallel Work Split

### Lane A - Browser Dashboard

Owns:

- `DesktopDotNet/ChromaLink.HttpBridge/*`
- static assets served by the HTTP bridge

Deliver:

- a simple dashboard page served by the bridge
- display of readiness, freshness, and key telemetry values

Avoid:

- addon Lua
- reader detection changes

### Lane B - Launch Helpers

Owns:

- `scripts/*`

Deliver:

- helpers to open the dashboard quickly
- optional combined launch path if it improves usability

Avoid:

- broad docs churn
- functional bridge changes beyond launch ergonomics

### Lane C - Docs Integration

Owns:

- `README.md`
- notes/*

Deliver:

- product-facing docs for where the dashboard fits relative to:
  - HTTP bridge
  - live monitor
  - inspector
  - readiness scripts

Avoid:

- changing telemetry behavior

## Validation Gates

Assemble only if all of these remain healthy:

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- `dotnet build .\DesktopDotNet\ChromaLink.HttpBridge\ChromaLink.HttpBridge.csproj`
- `dotnet build .\DesktopDotNet\ChromaLink.Monitor\ChromaLink.Monitor.csproj`
- one short live sample:
  - `dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 8 50 --backend screen`
- one dashboard-open or dashboard-fetch check through the local HTTP bridge

## Success Criteria

- local users can open a browser and immediately see ChromaLink telemetry
- the dashboard stays thin and contract-driven
- repo ends in a clean, documented checkpoint
