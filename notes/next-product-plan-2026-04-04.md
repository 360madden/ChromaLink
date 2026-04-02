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

---

## 2026-04-04 - Session W - HTTP bridge helper scripts

### Goal

Add thin scripts around the local HTTP bridge so it is easy to launch, open, and probe from the repo.

### Change

- add `scripts/Launch-ChromaLinkHttpBridge.cmd`
- add `scripts/Open-ChromaLinkHttpBridge.cmd`
- add `scripts/Probe-ChromaLinkHttpBridge.ps1`
- add `scripts/Probe-ChromaLinkHttpBridge.cmd`
- point the open helper at the latest-snapshot endpoint by default
- keep the probe helper contract-driven with `/health`, `/ready`, `/latest-snapshot`, and `/snapshot`

### Why

The bridge already existed, so the right scripts-level improvement was to make it easy to launch and validate without creating another layer of custom plumbing.

### Verification

```powershell
dotnet build .\DesktopDotNet\ChromaLink.HttpBridge\ChromaLink.HttpBridge.csproj
```

```powershell
$bridge = Start-Process -FilePath dotnet -ArgumentList @('run','--project','.\\DesktopDotNet\\ChromaLink.HttpBridge\\ChromaLink.HttpBridge.csproj') -PassThru -WindowStyle Hidden; try { Start-Sleep -Seconds 3; .\\scripts\\Probe-ChromaLinkHttpBridge.cmd -BaseUrl http://127.0.0.1:7337/; } finally { if ($bridge -and -not $bridge.HasExited) { Stop-Process -Id $bridge.Id -Force } }
```

### Result

- the HTTP bridge built successfully
- the probe script reported successful responses from:
  - `/latest-snapshot`
  - `/health`
  - `/ready`
  - `/snapshot`
- the open helper defaults to the latest snapshot view, which is the most useful human-facing entry point

### Decision

Keep.

### Saved Checkpoint

- pending commit for HTTP bridge helper scripts
