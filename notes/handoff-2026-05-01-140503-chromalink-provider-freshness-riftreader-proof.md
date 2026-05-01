# ChromaLink Handoff - provider freshness fix and RiftReader proof

Created: 2026-05-01 14:05:03 -04:00 local / 2026-05-01T18:05:03Z UTC
Repo: `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink`
Branch: `main`
HEAD before commit: `ad699cd Document RiftReader coordination guardrails`
RiftReader consumer proof repo: `C:\RIFT MODDING\RiftReader`
RiftReader status during proof: `main...origin/main`

## TL;DR

The ChromaLink provider stack was fixed so RiftReader can consume fresh live
coordinates through the published world-state API without relying on `/reloadui`
or RIFT SavedVariables.

Two provider-side issues were fixed:

1. Standard stack launchers now run a continuous watch loop with the proven
   `ScreenBitBlt` backend: `watch --backend screen`.
2. Aggregate health now uses a `5000ms` freshness window instead of `2000ms`,
   matching the RiftReader live-capture gate and the current multi-frame rotation
   cadence.

Final consumer proof passed: a RiftReader capture against
`GET http://127.0.0.1:7337/api/v1/riftreader/world-state` returned
`status=pass`, `fresh=true`, and wrote `live-coords.ndjson`.

## Scope boundary

This was an explicitly authorized ChromaLink provider edit pass.

- ChromaLink files were modified.
- RiftReader files were not modified.
- RiftReader was used only as a consumer-validation target against ChromaLink's
  published HTTP world-state contract.
- No live RIFT input, `/reloadui`, click, keypress, focus, or movement was sent.

## Root cause and fix

| Problem | Evidence | Fix |
|---|---|---|
| HTTP bridge could be reachable while serving stale telemetry | RiftReader capture saw old ChromaLink snapshot when no active watch loop refreshed it | Standard stack now runs continuous `watch --backend screen` |
| Source `Bridge-ChromaLink.cmd` used `-Argument2 100` | In current CLI parsing, a single watch positional is `durationSeconds`, so the watch loop could exit after about 100s | Removed timed positional; use `-Backend screen` only |
| `/health` could report `healthy=false` even when `player.position` was fresh | Multi-frame rotation put lower-priority complete-state frames slightly over the old `2000ms` window | Aggregate health freshness window changed to `5000ms` |
| `/reloadui` was tempting as a stale workaround | `/reloadui` is only a reload/reset event; it does not continuously refresh desktop telemetry | Keep watch loop as freshness mechanism; reserve `/reloadui` for addon code/config reloads or wedged strip |

## Files changed

| File | Change |
|---|---|
| `DesktopDotNet/ChromaLink.Cli/TelemetrySnapshotWriter.cs` | Changed aggregate freshness window from `2000.0` to `5000.0` ms. |
| `DesktopDotNet/ChromaLink.Tests/SnapshotContractTests.cs` | Updated test expectations for the `5000.0` ms freshness window. |
| `scripts/Run-ChromaLink.ps1` | Added optional `-Backend` forwarding for `capture-dump`, `live`, and `watch` modes. |
| `scripts/Bridge-ChromaLink.cmd` | Changed standard source launcher to `Run-ChromaLink.cmd -Mode watch -Backend screen`, with no duration argument. |
| `scripts/Package-ChromaLinkDesktop.ps1` | Generated package `Bridge-ChromaLink.cmd` and `Start-ChromaLinkStack.cmd` now run `watch --backend screen`. |
| `README.md` | Documented `--backend screen` standard stack behavior and `5000ms` aggregate freshness. |
| `notes/handoff-2026-05-01-131134-screen-backend-stack-freshness.md` | Earlier in-session handoff with the final proof path. |
| `notes/handoff-2026-05-01-140503-chromalink-provider-freshness-riftreader-proof.md` | This final handoff. |

## Validation run

| Validation | Result |
|---|---|
| PowerShell parser check: `scripts/Run-ChromaLink.ps1` | Passed |
| PowerShell parser check: `scripts/Package-ChromaLinkDesktop.ps1` | Passed |
| `git diff --check` | Passed; line-ending warnings only |
| `pwsh -File .\scripts\Run-ChromaLink.ps1 -Mode smoke` | Passed |
| `dotnet test .\DesktopDotNet\ChromaLink.sln -v minimal` | Passed, `40/40` |
| `dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- validate` | Passed: smoke, replay, bench |
| `scripts\Start-ChromaLinkStack.cmd` passive launch | Passed |
| `scripts\Status-ChromaLinkStack.ps1 -IncludeProcesses` | Passed during proof; endpoints responded and watch/bridge processes were present |
| `scripts\Test-ChromaLinkTelemetryReady.ps1 -MaxAgeSeconds 5 -RequireAnyFrame` | Passed: `TelemetryReady=true`, `TelemetryFresh=true`, `TelemetryAgeSeconds=0.00` |
| HTTP `/health` after final provider fix | Passed: `healthy=true`, `ready=true`, `fresh=true`, `stale=false`, `aggregate.healthy=true` |
| RiftReader capture against ChromaLink provider stack | Passed: `status=pass`, `fresh=true`, `exported=true` |
| `scripts\Stop-ChromaLinkStack.ps1` and post-stop process check | Passed; no leftover matching ChromaLink helper processes were listed |

## RiftReader consumer proof artifact

Final successful RiftReader capture bundle:

`C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114`

Important files:

| Artifact | Path |
|---|---|
| Capture summary | `C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114\chromalink-live-coords-capture-summary.json` |
| Live coords | `C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114\live-coords.ndjson` |
| Bridge readiness | `C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114\chromalink-http-bridge-readiness.json` |
| Contract proof | `C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114\chromalink-world-state-contract.json` |
| Truth surface | `C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114\truth-surface.json` |
| SavedVariables freshness | `C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114\savedvariables-freshness.json` |
| Artifact index | `C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114\artifact-index.json` |

Capture summary highlights:

| Field | Value |
|---|---|
| status | pass |
| fresh | true |
| exported | true |
| samplesWritten | 2 |
| freshSamplesWritten | 2 |
| duplicateSamplesSkipped | 9 |
| Source view | `chromalink-riftreader-world-state` |
| Coordinates | `x=7447.31`, `y=887.85`, `z=3027.19` |
| SavedVariables | `not-used` |

## Current operator recipe

Source-tree standard stack:

```powershell
.\scripts\Start-ChromaLinkStack.cmd
.\scripts\Status-ChromaLinkStack.cmd
.\scripts\Test-ChromaLinkTelemetryReady.cmd -MaxAgeSeconds 5 -RequireAnyFrame
```

The standard stack routes to:

```powershell
.\scripts\Run-ChromaLink.cmd -Mode watch -Backend screen
```

Direct CLI diagnostics:

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- watch --backend screen
```

Consumer endpoint for RiftReader:

```text
GET http://127.0.0.1:7337/api/v1/riftreader/world-state
```

## What was not validated

| Not validated | Reason / note |
|---|---|
| Packaged desktop artifact generation | `scripts\Package-ChromaLinkDesktop.ps1` was parser-checked and generator content patched, but package publish was not run. |
| Movement-backed trajectory proof | Capture was stationary coordinate freshness/plumbing proof only. |
| ReaderBridge-vs-ChromaLink same-moment delta | Not run in this provider-fix pass. |
| Commit/push | Not done yet at handoff creation; verify current git status before resuming. |

## Resume prompt

```text
Resume in C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink on main.
Read newest handoff first:
C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\notes\handoff-2026-05-01-140503-chromalink-provider-freshness-riftreader-proof.md

Current state at handoff creation: provider freshness fix was implemented but uncommitted. Verify current git status before resuming. Standard ChromaLink stack now uses watch --backend screen; aggregate telemetry health uses a 5000ms freshness window. Validation passed, including ChromaLink tests, CLI validate, standard stack freshness, HTTP /health, and a RiftReader capture against /api/v1/riftreader/world-state. Final RiftReader proof bundle: C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114.

Next best action: commit and push the ChromaLink provider fix + handoffs, then optionally run package generation if packaged launchers are needed.
```
