# ChromaLink — Project Knowledge

ChromaLink is a **reliability-first optical telemetry bridge for RIFT** (the MMO). A Lua addon inside RIFT renders a structured color strip; a .NET 9 desktop stack captures that strip from the game window, decodes it, validates it, and publishes a live telemetry snapshot for local tools.

## Key Directories

| Path | Purpose |
|------|---------|
| `Core/` | Lua addon core (Config, Gather, Protocol, StateCache, RiftMeterAdapter) |
| `RIFT/` | Lua rendering, bootstrap, commands, diagnostics, error trap |
| `DesktopDotNet/` | Full .NET 9 solution |
| `DesktopDotNet/ChromaLink.Reader/` | Capture backends (PrintWindow, DesktopDuplication, ScreenBitBlt), protocol decoding, replay, metrics |
| `DesktopDotNet/ChromaLink.Cli/` | CLI tool (smoke, replay, live, watch, bench, validate, capture-dump, prepare-window) |
| `DesktopDotNet/ChromaLink.Monitor/` | WinForms diagnostic monitor app |
| `DesktopDotNet/ChromaLink.Inspector/` | BMP inspection with segment overlays |
| `DesktopDotNet/ChromaLink.HttpBridge/` | ASP.NET HTTP bridge exposing live telemetry on localhost:7337 |
| `DesktopDotNet/ChromaLink.Client/` | Typed .NET client for consuming the HTTP bridge |
| `DesktopDotNet/ChromaLink.Tests/` | xUnit tests (client, protocol, snapshot contract, repo consistency) |
| `scripts/` | PowerShell/batch helper scripts for running, packaging, testing |
| `notes/` | Planning docs, handoffs, roadmaps |

## Profile & Geometry (P360C)

- RIFT client area: **640x360** (minimum known-good fallback)
- Larger 16:9 geometries (e.g. 1280x720) **accepted** when provider freshness passes
- Maximized/non-16:9 windows (e.g. 1920x1009) **blocked** as `unsupported-aspect`
- Strip size: **640x24**
- Segments: **80** (each 8x24 pixels)
- Payload segments: **9–72** (64 payload symbols)
- Control markers: **1–8** and **73–80**
- Alphabet: **8-color, 3 bits per segment**
- Transport: **24 bytes per frame** (12 payload bytes + headers + CRC)

## Commands

### Build
```powershell
dotnet build .\DesktopDotNet\ChromaLink.sln
```

### Test
```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

### CLI (all modes)
```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- <mode>
```
Modes: `smoke`, `replay <bmpPath>`, `live [count] [sleepMs]`, `watch [duration] [sleepMs]`, `bench`, `validate`, `capture-dump`, `prepare-window [left] [top]`.

Add `--backend screen` for live capture (ScreenBitBlt is the standard live backend).

### Script wrappers
```cmd
.\scripts\Smoke-ChromaLink.cmd
.\scripts\Start-ChromaLinkStack.cmd          # starts HTTP bridge + background stack
.\scripts\Ensure-ChromaLinkFresh.cmd          # provider status/ensure/freshness helper (Python)
.\scripts\Ensure-ChromaLinkFresh.cmd --status --wait-fresh --json   # recommended first check
.\scripts\Open-ChromaLinkDashboard.cmd       # opens live dashboard in browser
.\scripts\Status-ChromaLinkStack.cmd         # checks stack health
.\scripts\Stop-ChromaLinkStack.cmd           # stops stack
.\scripts\Open-ChromaLink-Monitor.cmd        # opens WinForms monitor
.\scripts\Open-ChromaLink-Inspector.cmd      # opens BMP inspector
.\scripts\Run-ChromaLink.ps1 -Mode <mode>    # unified PowerShell runner
.\scripts\Package-ChromaLinkDesktop.ps1      # create framework-dependent package
.\scripts\Package-ChromaLinkDesktop-SelfContained.cmd  # create self-contained package
.\scripts\Test-ChromaLinkTelemetryReady.cmd -MaxAgeSeconds 5 -RequireAnyFrame  # telemetry readiness check
.\scripts\Get-RiftInputReadiness.cmd         # RIFT window readiness check (aspect-aware)
```

### Standard stack startup sequence (source-tree)
```powershell
.\scripts\Start-ChromaLinkStack.cmd
.\scripts\Status-ChromaLinkStack.cmd
.\scripts\Test-ChromaLinkTelemetryReady.cmd -MaxAgeSeconds 5 -RequireAnyFrame
```
The standard stack routes to `watch --backend screen` (continuous, no duration limit).

### Provider freshness repair (geometry drift -> stale provider)
```powershell
.\scripts\Run-ChromaLink.ps1 -Mode prepare-window -Argument1 32 -Argument2 32
```
Then wait for watch loop refresh, then verify:
```powershell
.\scripts\Ensure-ChromaLinkFresh.cmd --status --wait-fresh --json
```

## HTTP Bridge

- URL: `http://127.0.0.1:7337/`
- Endpoints: `/api/v1`, `/api/v1/riftreader/world-state`, `/api/v1/riftreader/world-state/schema`, `/latest-snapshot`, `/snapshot`, `/health`, `/ready`
- Live snapshot JSON: `%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out\chromalink-live-telemetry.json`
- `ChromaLink.Client` provides typed C# client: `ChromaLinkHttpClient`

## Freshness & Health Gates

| Gate | Required state |
|------|---------------|
| `/health` | `healthy=true`, `ready=true`, `fresh=true`, `stale=false` |
| Aggregate freshness window | **5000ms** (widened from 2000ms to match multi-frame rotation) |
| World-state endpoint | `ok=true`, `fresh=true`, `player.position.fresh=true` |
| Watch loop | Active `watch --backend screen` continuously writing rolling snapshots |
| RIFT geometry | At least `640x360`; P360C is fallback, larger 16:9 accepted when fresh |

### Geometry classification

| Observed geometry | Provider freshness | Classification |
|---|---|---|
| `640x360` | Fresh `/health` + fresh `player.position` | `known-good-p360c` |
| Larger 16:9 (e.g. 1280x720) | Fresh /health + fresh player.position | `larger-16x9-fresh` |
| Larger 16:9 | Stale/missing | `larger-16x9-unproven` |
| Maximized/non-16:9 (e.g. 1920x1009) | Stale/missing | `unsupported-aspect` |
| Below `640x360` | Any | `below-minimum-profile` |

### Provider blocker taxonomy
Blocker reasons used by `ensure_chromalink_fresh.py`: `bridge-down`, `watch-loop-down`, `snapshot-missing`, `snapshot-stale`, `snapshot-malformed`, `world-state-unavailable`, `world-state-stale`, `player-position-missing`, `player-position-stale`, `rift-process-missing`, `rift-geometry-drift`, `strip-not-detected`, `decode-unhealthy`, `contract-mismatch`, `timeout-waiting-fresh`.

## Latest Handoff Findings (2026-05-21)

1. **ensure_chromalink_fresh.py** added as a Python-first provider status/ensure helper with `--status`, `--ensure-running`, `--prepare-window`, `--wait-fresh`, `--self-test`, `--json` modes. Writes JSON+Markdown diagnostic artifacts to `artifacts/diagnostics/`.
2. **`640x360` is the minimum known-good fallback**, not the only acceptable geometry. Larger 16:9 geometries pass when provider freshness proves `player.position.fresh=true`.
3. **Maximized windows** (non-16:9, e.g. `1920x1009`) are blocked as `unsupported-aspect` — ChromaLink did not keep player position fresh at that size.
4. **Geometry drift is the #1 suspect** when world-state goes stale. First check RIFT client area before debugging code or schemas.
5. **Standard launchers** (`Bridge-ChromaLink.cmd`, `Start-ChromaLinkStack.cmd`, packaged launchers) now run `watch --backend screen` continuously (no duration argument), so the rolling snapshot stays fresh for external consumers.
6. **Aggregate freshness window = 5000ms** — widened from 2000ms to tolerate multi-frame rotation cadence. Configured in `TelemetrySnapshotWriter.cs`.
7. **Cross-repo coordination**: ChromaLink is the *provider*, RiftReader is the *consumer*. RiftReader gets new data through documented change requests, not silent ChromaLink edits.
8. **ChromaLink does not provide**: heading, facing, yaw, route planning, or movement control authority. These are explicitly `false` in the world-state contract.

## Conventions & Gotchas

- **.NET 9** everywhere, nullable enabled, implicit usings on, Windows-targeted (reader uses WinForms/gdi32/desktop-duplication APIs).
- **Lua 5.1** for the RIFT addon (no LuaJIT, no external modules — plain table-based code).
- **Color strip is the sole transport** — pixels only, no hidden APIs. The game-to-desktop bridge is the on-screen strip itself. This is a design invariant.
- **Cross-repo boundary**: ChromaLink is the *provider*, RiftReader is a *consumer*. Don't let RiftReader tasks silently change ChromaLink contracts. Provider changes happen here; consumer changes happen in RiftReader. Use the change request template in `notes/chromalink-riftreader-coordination.md`.
- **Testing**: xUnit tests exist in `ChromaLink.Tests`. BMP fixtures in `Fixtures/`. Smoke round-trip tests use `--backend printwindow` (no real capture needed).
- **Packaging**: `Package-ChromaLinkDesktop.ps1` produces `artifacts/package/`. The self-contained variant is preferred for cross-machine handoff.
- **Don't use `/reloadui`** as an ongoing freshness mechanism — it only reloads the addon, it doesn't refresh desktop telemetry. Use `watch --backend screen` instead.
- **SavedVariables are not live truth** — they are post-save snapshots. Never use them as a live telemetry fallback.
- **Diagnostic artifacts** are written under `artifacts/diagnostics/` by `Ensure-ChromaLinkFresh.cmd` for every provider status check.
- **Key handoff notes** in chronological order (newest first):
  - `notes/handoff-2026-05-21-1425-chromalink-geometry-freshness-classifier.md` — geometry classification, ensure helper, 1280x720 proof
  - `notes/handoff-2026-05-12-155349-chromalink-p360c-geometry-freshness-fix.md` — geometry drift root cause, prepare-window fix
  - `notes/handoff-2026-05-07-195850-chromalink-push-ready-freshness.md` — provider freshness commit ready to push
  - `notes/handoff-2026-05-01-140503-chromalink-provider-freshness-riftreader-proof.md` — provider fix + RiftReader consumer proof
  - `notes/handoff-2026-05-01-131134-screen-backend-stack-freshness.md` — screen backend, 5000ms window
  - `notes/handoff-2026-05-01-000743-chromalink-external-access.md` — HTTP bridge, world-state endpoint, typed client
  - `notes/chromalink-riftreader-coordination.md` — provider/consumer ownership boundary
