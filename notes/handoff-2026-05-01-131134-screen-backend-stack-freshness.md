# ChromaLink Handoff - screen-backend stack freshness fix

Date: 2026-05-01 14:02:44 -04:00 local / 2026-05-01T18:02:44Z UTC
Repo: C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink
Branch: main
HEAD before commit: ad699cd
Worktree status while writing this handoff:

~~~text
## main...origin/main
 M DesktopDotNet/ChromaLink.Cli/TelemetrySnapshotWriter.cs
 M DesktopDotNet/ChromaLink.Tests/SnapshotContractTests.cs
 M README.md
 M scripts/Bridge-ChromaLink.cmd
 M scripts/Package-ChromaLinkDesktop.ps1
 M scripts/Run-ChromaLink.ps1
?? notes/handoff-2026-05-01-131134-screen-backend-stack-freshness.md
~~~

## TL;DR

The standard ChromaLink provider stack now keeps its rolling telemetry snapshot
fresh for RiftReader by running the watch loop continuously with ScreenBitBlt:

~~~cmd
watch --backend screen
~~~

A second strict-readiness issue was also fixed: aggregate health now uses a
5000ms freshness window instead of 2000ms. The previous 2000ms global aggregate
window was too tight for the current multi-frame rotation cadence and could make
/health report healthy=false even when player.position was fresh.

This was an explicitly authorized ChromaLink provider edit pass. RiftReader files
were not modified.

## Why this change was needed

RiftReader live coordinate validation showed two provider-side failure modes:

| Failure mode | Cause | Fix |
|---|---|---|
| HTTP bridge reachable but snapshot stale | The bridge serves snapshots; it does not refresh them. The standard watch path was not a durable freshness provider. | Make the standard stack start the watch loop continuously with --backend screen. |
| Snapshot fresh but /health healthy=false | Aggregate health required core/vitals/resources/combat all fresher than 2000ms; multi-frame rotation sometimes put lower-priority frames just over 2s. | Align aggregate health window to 5000ms, matching RiftReader live-capture max freshness. |

The previous source launcher called:

~~~cmd
Run-ChromaLink.cmd -Mode watch -Argument2 100
~~~

For the current CLI, a single positional watch argument is interpreted as
durationSeconds, so that could exit after about 100 seconds instead of running as
the standard continuous provider.

## What changed

| File | Change |
|---|---|
| DesktopDotNet/ChromaLink.Cli/TelemetrySnapshotWriter.cs | Changed aggregate freshness window from 2000ms to 5000ms. |
| DesktopDotNet/ChromaLink.Tests/SnapshotContractTests.cs | Updated expected freshness window from 2000ms to 5000ms. |
| scripts/Run-ChromaLink.ps1 | Added optional -Backend forwarding for capture-dump, live, and watch modes. |
| scripts/Bridge-ChromaLink.cmd | Changed source launcher to Run-ChromaLink.cmd -Mode watch -Backend screen, with no duration argument. |
| scripts/Package-ChromaLinkDesktop.ps1 | Changed generated packaged Bridge-ChromaLink.cmd and Start-ChromaLinkStack.cmd to run watch --backend screen. |
| README.md | Documented standard live stack --backend screen and 5000ms aggregate freshness. |
| notes/handoff-2026-05-01-131134-screen-backend-stack-freshness.md | This handoff documenting what changed and why. |

## Boundary note

This is a provider-side ChromaLink runtime/launcher/freshness fix. RiftReader
should keep consuming ChromaLink through published surfaces only:

- GET http://127.0.0.1:7337/api/v1/riftreader/world-state
- GET http://127.0.0.1:7337/api/v1/riftreader/world-state/schema
- a published ChromaLink.Client version, when used

Provider validation and consumer validation must stay separate.

## Validation run

| Validation | Result |
|---|---|
| PowerShell parser check for scripts/Run-ChromaLink.ps1 | Passed |
| PowerShell parser check for scripts/Package-ChromaLinkDesktop.ps1 | Passed |
| git diff --check | Passed; only line-ending warnings were emitted |
| pwsh -File .\scripts\Run-ChromaLink.ps1 -Mode smoke | Passed |
| dotnet test .\DesktopDotNet\ChromaLink.sln -v minimal | Passed, 40/40 |
| dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- validate | Passed: smoke, replay, bench |
| scripts\Start-ChromaLinkStack.cmd passive launch + Status-ChromaLinkStack.ps1 -IncludeProcesses | Passed; endpoints responded and watch/bridge processes were present |
| Test-ChromaLinkTelemetryReady.ps1 -MaxAgeSeconds 5 -RequireAnyFrame | Passed: TelemetryReady=true, TelemetryFresh=true, TelemetryAgeSeconds=0.00, TelemetryHasAnyFrame=true |
| HTTP /health after freshness-window fix | Passed: healthy=true, ready=true, fresh=true, stale=false, aggregate.healthy=true |
| RiftReader capture against ChromaLink provider stack | Passed; status=pass, fresh=true, exported=true |
| Stop-ChromaLinkStack.ps1 and post-stop process check | Passed; no leftover matching ChromaLink helper processes were listed |

## RiftReader consumer proof artifact

RiftReader capture bundle produced after the ChromaLink provider fixes:

C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114

Important files:

| Artifact | Path |
|---|---|
| Summary | C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114\chromalink-live-coords-capture-summary.json |
| Live coords | C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114\live-coords.ndjson |
| Bridge readiness | C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114\chromalink-http-bridge-readiness.json |
| Contract proof | C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114\chromalink-world-state-contract.json |
| Truth surface | C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114\truth-surface.json |
| SavedVariables freshness | C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-provider-stack-riftreader-capture-20260501-140114\savedvariables-freshness.json |

Capture result:

| Field | Value |
|---|---|
| status | pass |
| fresh | true |
| exported | true |
| samplesWritten | 2 |
| freshSamplesWritten | 2 |
| duplicateSamplesSkipped | 9 |
| source | chromalink-riftreader-world-state |
| coords | x=7447.31, y=887.85, z=3027.19 |
| SavedVariables | not-used |

## What was not validated

- No live RIFT input, /reloadui, keypress, click, or movement was sent.
- No packaged desktop artifact was built from scripts/Package-ChromaLinkDesktop.ps1; the package generator was parser-checked and generated launcher content was patched in source.
- The RiftReader capture was stationary coordinate plumbing/freshness proof, not a movement trajectory proof.

## Current operator recipe

For source-tree operation:

~~~powershell
.\scripts\Start-ChromaLinkStack.cmd
.\scripts\Status-ChromaLinkStack.cmd
.\scripts\Test-ChromaLinkTelemetryReady.cmd -MaxAgeSeconds 5 -RequireAnyFrame
~~~

The stack now starts the watch loop through Bridge-ChromaLink.cmd, which routes
to:

~~~powershell
.\scripts\Run-ChromaLink.cmd -Mode watch -Backend screen
~~~

For direct CLI diagnostics:

~~~powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- watch --backend screen
~~~

## Remaining recommended follow-up

1. Commit and push this provider fix and handoff in ChromaLink before more RiftReader consumer changes.
2. If packaging is needed, run scripts\Package-ChromaLinkDesktop.ps1 and smoke the generated package launchers.
3. Run a movement-backed RiftReader capture only with explicit live input approval or manual player movement.
4. Keep /reloadui reserved for addon code/config reloads or a wedged strip; it is not the ongoing freshness mechanism.
5. Preserve separate provider validation and consumer validation records.
