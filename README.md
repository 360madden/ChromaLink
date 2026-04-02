# ChromaLink

[![.NET 9](https://img.shields.io/badge/.NET-9-5C2D91?logo=dotnet)](https://dotnet.microsoft.com/)
[![Lua 5.1](https://img.shields.io/badge/Lua-5.1-2C2D72?logo=lua)](https://www.lua.org/)
[![RIFT MMO](https://img.shields.io/badge/RIFT-MMO-FF7A2F)](https://www.riftgame.com/)
[![License MIT](https://img.shields.io/badge/License-MIT-97CA00)](#)

ChromaLink is a reliability-first optical telemetry project for RIFT. A Lua addon renders a structured color strip inside the game client, and a `.NET 9` desktop reader captures that strip from the window, decodes it, and turns it into usable telemetry plus diagnostics.

The important constraint is also the point of the project: the game-to-desktop bridge is the on-screen strip itself. If live capture or replay fails, that failure is visible and debuggable in pixels rather than hidden behind an opaque integration.

Design rule: the strip only needs to be machine-readable. Human readability is optional, and decoder margin matters more than visual elegance.

## Current Scope

ChromaLink is intentionally narrow right now. The active baseline is:

- profile `P360C`
- client `640x360`
- strip `640x24`
- `80` vertical segments at `8x24`
- fixed `8-color` alphabet
- fixed control markers on both edges
- fast heartbeat frame: `coreStatus`
- proven rotating expansions: `playerVitals`, `playerPosition`

Current live proof:

- offline smoke, replay, bench, build, and tests are passing
- default live capture now prefers `PrintWindow` for the most reliable `640x360` path
- `DesktopDuplication` and `ScreenBitBlt` are still available for comparison
- live decode is currently proven at:
  - `origin 0,0`
  - `pitch 2.8`
  - `scale 0.35`
- live captures now decode `CoreStatus`, `PlayerVitals`, and `PlayerPosition` on the running client
- capture sessions emit raw BMP, annotated BMP, and JSON sidecars under `%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out`

## How It Works

```text
[RIFT Client]
   ↓
[Lua Addon]
   ├─ gathers in-game state
   ├─ encodes transport bytes
   └─ renders 640x24 color strip at the top of the client
         ↓ (visible pixels only)
[ChromaLink.Reader]
   ├─ captures the game window
   ├─ finds strip geometry
   ├─ samples symbols
   ├─ validates control markers + CRC
   └─ produces telemetry + diagnostics
         ↓
[CLI / Inspector / Future tools]
```

The older barcode or matrix work is archived for reference only. The active transport is the current reader-first segmented color strip.

## Requirements

- RIFT game client
- Windows 10 or 11
- `.NET 9 SDK`
- a windowed RIFT client that can be prepared to the current baseline

## Quick Start

### Build

```powershell
dotnet build .\DesktopDotNet\ChromaLink.sln
```

### Prepare The RIFT Window

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- prepare-window 32 32
```

### Generate A Synthetic Fixture

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- smoke
```

### Replay A Saved Capture

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- replay "$env:LOCALAPPDATA\ChromaLink\DesktopDotNet\fixtures\chromalink-color-core.bmp"
```

### Capture And Decode Live

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- capture-dump
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 5 100
```

### Open The Inspector

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj
```

### Open The Live Monitor

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Monitor\ChromaLink.Monitor.csproj
```

## CLI Commands

`ChromaLink.Cli` currently supports:

- `smoke`
- `replay <bmpPath>`
- `live [sampleCount] [sleepMs]`
- `watch [durationSeconds] [sleepMs]`
- `bench`
- `capture-dump`
- `prepare-window [left] [top]`

`live` and `watch` now report per-frame-type counts for accepted samples, which makes rotating telemetry easier to verify.

They also emit a compact aggregate summary showing the newest accepted `CoreStatus`, `PlayerVitals`, and `PlayerPosition` observations plus rough age in milliseconds. That makes the rotating strip immediately usable as one reader-side telemetry state instead of three unrelated frame types.

While `live` or `watch` runs, the CLI also writes a rolling machine-readable snapshot to `%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out\chromalink-live-telemetry.json`.

The bridge snapshot now includes a small contract header:

- `contract.name = chromalink-live-telemetry`
- `contract.schemaVersion = 1`
- stable `profile` metadata for the proven `P360C` baseline
- stable `transport` metadata including reserved build-flag meanings
- aggregate freshness fields such as `healthy`, `stale`, and per-frame age/freshness metadata

Capture backend flag:

- `--backend desktopdup|screen|printwindow`
- default backend order for live work:
  - `PrintWindow`
  - `DesktopDuplication`
  - `ScreenBitBlt`

## In-Game Commands

ChromaLink exposes a small slash-command surface inside RIFT:

- `/cl status`
- `/cl build`
- `/cl version`
- `/cl caps`
- `/cl rotation`
- `/cl rotate`
- `/cl diag`
- `/cl refresh`
- `/cl observer on`
- `/cl observer off`
- `/cl observer status`
- `/cl compensate on`
- `/cl compensate off`
- `/cl compensate status`
- `/cl traces on`
- `/cl traces off`

What they are for:

- `status` prints the current strip/layout summary
- `build`, `version`, and `caps` print addon version, protocol/profile, frame types, and header capability flags
- `rotation` and `rotate` print the active frame rotation sequence and heartbeat priority
- `diag` adds nearby native RIFT frame summaries for overlap and layout checks
- `refresh` forces an immediate strip redraw
- `observer` toggles the optional observer lane below the strip
- `compensate` toggles experimental display-compensation sizing that tries to preserve final on-screen strip size when the UI anchor reports shrinkage
- `traces` arms or disables verbose layout tracing for the next `/reloadui`

Display compensation is still experimental. It is meant to test the idea of oversizing the strip internally so the final displayed strip stays closer to the target machine-readable size.

## Observer Lane

ChromaLink now includes an optional observer lane for low-risk capture research. It is off by default and does not change the main strip payload contract.

What it helps with:

- clipping checks
- scale drift checks
- color drift checks
- future capture experiments

Observer diagnostics are now visible in all major tooling layers:

- capture sidecars include an `observerLane` section
- annotated BMP artifacts draw observer marker boxes and sample centers
- the inspector preview draws the same observer geometry directly on the zoomed capture
- observer reports now include visibility hints such as `visible`, `right-clipped`, or `offscreen`
- the inspector details pane can now compute observer health directly from the loaded BMP even when no sidecar exists

## Protocol Snapshot

The current transport is a segmented strip with fixed edge controls:

- `80` total segments
- left edge control markers in segments `1-8`
- payload symbols in segments `9-72`
- right edge control markers in segments `73-80`
- `8-color` alphabet for `3` bits per segment

The current strip carries `24` transport bytes per frame and now supports more than one frame type without changing strip geometry:

- `CoreStatus`
  - player and target summary state
- `PlayerVitals`
  - health current/max
  - resource current/max
- `PlayerPosition`
  - x/y/z world coordinates encoded as fixed-point integers

The current addon rotation keeps `coreStatus` as the dominant heartbeat and periodically inserts `playerVitals` and `playerPosition` to increase throughput over time instead of widening the strip. A recent live sample after `/reloadui` produced:

- `35` accepted `CoreStatus` frames
- `12` accepted `PlayerVitals` frames
- `13` accepted `PlayerPosition` frames
- `ReservedFlags: 0x03`, confirming the live addon loaded the newer multi-frame build

The header `ReservedFlags` byte is now used as a live build-capability marker. Current expected value is `0x03`, which means:

- `0x01` = multi-frame rotation capable
- `0x02` = player-position capable

That gives live captures a direct way to prove whether the running addon actually loaded a newer telemetry build.

## Live Monitor

Use the live monitor when you want a product-style view of the rolling bridge snapshot.

- `ChromaLink.Monitor` is the live-first UI for `chromalink-live-telemetry.json`
- the inspector is still the artifact and BMP analyzer
- `Start-ChromaLinkStack.cmd` starts the local CLI watch loop plus HTTP bridge in the background without opening extra UI
- `Bridge-ChromaLink.cmd` keeps the snapshot fresh in the background
- `Open-ChromaLink-Monitor.cmd` launches the live monitor directly
- `Open-ChromaLink-LiveStack.cmd` reuses `Start-ChromaLinkStack.cmd` and then opens the monitor
- `Open-ChromaLinkHttpBridge.cmd` opens the local HTTP bridge
- `Open-ChromaLinkDashboard.cmd` opens the browser dashboard
- `Open-ChromaLink-DashboardStack.cmd` reuses `Start-ChromaLinkStack.cmd` and then opens the browser dashboard
- `Get-ChromaLinkStackStatus.cmd` checks the stack health endpoints and process counts
- `Status-ChromaLinkStack.cmd` checks the stack bridge and readiness status
- `Status-ChromaLinkHttpBridge.cmd` checks the HTTP bridge endpoints and process counts
- `Stop-ChromaLinkStack.cmd` stops the local ChromaLink stack processes
- `Stop-ChromaLinkHttpBridge.cmd` stops only the HTTP bridge process
- `Probe-ChromaLinkHttpBridge.cmd` checks the local HTTP bridge endpoints
- `Watch-ChromaLinkTelemetry.cmd` opens the console snapshot view if you want something lighter than the GUI
- `Test-ChromaLinkTelemetryReady.cmd` is the automation-friendly gate for readiness and freshness

## Local HTTP Bridge

ChromaLink now includes a tiny local HTTP bridge on top of the same rolling snapshot contract.
The bridge project lives in `DesktopDotNet/ChromaLink.HttpBridge`.

It fits alongside the current tools like this:

- the rolling JSON snapshot remains the source of truth
- the live monitor stays the human-facing live viewer
- the browser dashboard stays the browser-friendly live view
- the inspector stays the BMP and overlay analyzer
- the readiness script stays the automation gate
- the HTTP bridge makes the same live state easier for other local tools to consume
- `Open-ChromaLinkHttpBridge.cmd`, `Launch-ChromaLinkHttpBridge.cmd`, and `Probe-ChromaLinkHttpBridge.cmd` are the bridge helpers
- `Open-ChromaLinkDashboard.cmd` and `Open-ChromaLink-DashboardStack.cmd` are the browser dashboard helpers
- `Get-ChromaLinkStackStatus.cmd`, `Status-ChromaLinkStack.cmd`, `Status-ChromaLinkHttpBridge.cmd`, `Stop-ChromaLinkStack.cmd`, and `Stop-ChromaLinkHttpBridge.cmd` are the lifecycle helpers

Endpoints:

- `/latest-snapshot`
- `/snapshot`
- `/health`
- `/ready`
- `/dashboard`

The bridge is local-only and lightweight, and it reads the snapshot rather than bypassing the existing bridge contract.

Recommended workflow:

1. Start `Start-ChromaLinkStack.cmd` for the safest background capture setup, or `Bridge-ChromaLink.cmd` if you only want the rolling snapshot loop
2. Use `Open-ChromaLink-LiveStack.cmd` when you want the monitor on top of the running stack
3. Use `Open-ChromaLink-DashboardStack.cmd` or `Open-ChromaLinkDashboard.cmd` when you want a browser view
4. Use `Probe-ChromaLinkHttpBridge.cmd`, `Get-ChromaLinkStackStatus.cmd`, or `Status-ChromaLinkHttpBridge.cmd` when you want to verify the local API surface
5. Open the inspector only when you need BMP artifacts or overlay diagnostics
6. Use `Test-ChromaLinkTelemetryReady.cmd` in scripts or checks
7. Stop the stack with `Stop-ChromaLinkStack.cmd` when you are done, or `Stop-ChromaLinkHttpBridge.cmd` if you only want to stop the HTTP bridge

## Wrapper Scripts

Useful helper scripts:

- [scripts/Start-ChromaLinkStack.cmd](scripts/Start-ChromaLinkStack.cmd)
- [scripts/Bridge-ChromaLink.cmd](scripts/Bridge-ChromaLink.cmd)
- [scripts/Launch-ChromaLinkHttpBridge.cmd](scripts/Launch-ChromaLinkHttpBridge.cmd)
- [scripts/Open-ChromaLinkHttpBridge.cmd](scripts/Open-ChromaLinkHttpBridge.cmd)
- [scripts/Open-ChromaLink-LiveStack.cmd](scripts/Open-ChromaLink-LiveStack.cmd)
- [scripts/Open-ChromaLinkDashboard.cmd](scripts/Open-ChromaLinkDashboard.cmd)
- [scripts/Open-ChromaLink-DashboardStack.cmd](scripts/Open-ChromaLink-DashboardStack.cmd)
- [scripts/Get-ChromaLinkStackStatus.cmd](scripts/Get-ChromaLinkStackStatus.cmd)
- [scripts/Status-ChromaLinkStack.cmd](scripts/Status-ChromaLinkStack.cmd)
- [scripts/Status-ChromaLinkHttpBridge.cmd](scripts/Status-ChromaLinkHttpBridge.cmd)
- [scripts/Probe-ChromaLinkHttpBridge.cmd](scripts/Probe-ChromaLinkHttpBridge.cmd)
- [scripts/Stop-ChromaLinkStack.cmd](scripts/Stop-ChromaLinkStack.cmd)
- [scripts/Stop-ChromaLinkHttpBridge.cmd](scripts/Stop-ChromaLinkHttpBridge.cmd)
- [scripts/Prepare-ChromaLink-640x360.cmd](scripts/Prepare-ChromaLink-640x360.cmd)
- [scripts/Smoke-ChromaLink.cmd](scripts/Smoke-ChromaLink.cmd)
- [scripts/Bench-ChromaLink.cmd](scripts/Bench-ChromaLink.cmd)
- [scripts/Live-ChromaLink.cmd](scripts/Live-ChromaLink.cmd)
- [scripts/Show-ChromaLinkTelemetry.cmd](scripts/Show-ChromaLinkTelemetry.cmd)
- [scripts/Watch-ChromaLinkTelemetry.cmd](scripts/Watch-ChromaLinkTelemetry.cmd)
- [scripts/Open-ChromaLink-Monitor.cmd](scripts/Open-ChromaLink-Monitor.cmd)
- [scripts/Open-ChromaLinkTelemetryJson.cmd](scripts/Open-ChromaLinkTelemetryJson.cmd)
- [scripts/Open-ChromaLinkTelemetryFolder.cmd](scripts/Open-ChromaLinkTelemetryFolder.cmd)
- [scripts/Test-ChromaLinkTelemetryReady.cmd](scripts/Test-ChromaLinkTelemetryReady.cmd)
- [scripts/Open-ChromaLink-Inspector.cmd](scripts/Open-ChromaLink-Inspector.cmd)
- [scripts/Reload-RiftUi.cmd](scripts/Reload-RiftUi.cmd)
- [scripts/Send-RiftSlash.cmd](scripts/Send-RiftSlash.cmd)
- [scripts/Resize-RiftClient-640x360.cmd](scripts/Resize-RiftClient-640x360.cmd)
- [scripts/Sweep-RiftResolutions.ps1](scripts/Sweep-RiftResolutions.ps1)

Examples:

```powershell
.\scripts\Sweep-RiftResolutions.ps1 -Resolutions @('640x360') -ReloadUi
```

```powershell
.\scripts\Sweep-RiftResolutions.ps1 -Resolutions @('640x360') -ReloadUi -ObserverLane on
```

## Bridge Output

For a simple always-on desktop bridge, run:

```powershell
.\scripts\Bridge-ChromaLink.cmd
```

That runs the CLI in continuous watch mode and keeps `%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out\chromalink-live-telemetry.json` refreshed with:

- latest merged aggregate state
- aggregate freshness and readiness metadata
- frame counts
- most recent detection geometry
- last decoded frame metadata
- reserved build flags
- bridge contract metadata for downstream consumers
- the `ChromaLink.Monitor` UI is the primary live viewer for that snapshot

## HTTP Bridge

For local API access on top of the rolling snapshot, run:

```powershell
.\scripts\Launch-ChromaLinkHttpBridge.cmd
```

The HTTP bridge is local-only and reads `chromalink-live-telemetry.json` directly. It exposes:

- `/latest-snapshot`
- `/snapshot`
- `/health`
- `/ready`

## Browser Dashboard

The browser dashboard is the browser-friendly companion to the local HTTP bridge.
Use it when you want a zero-install view in a web browser instead of the WinForms monitor.

- lighter than the live monitor
- tied to the HTTP bridge as the source of truth
- separate from the inspector, which remains the BMP and overlay tool
- consistent with the readiness script, which still gates automation

Recommended role split:

- HTTP bridge for local API access
- browser dashboard for quick at-a-glance viewing
- live monitor for the richer desktop view
- inspector for artifact analysis
- readiness script for automation checks

Use `Probe-ChromaLinkHttpBridge.cmd` to sanity-check those endpoints from scripts, or `Open-ChromaLinkHttpBridge.cmd` to jump to the base URL in a browser.

## Lifecycle

The current ChromaLink flow is:

1. Start with `Start-ChromaLinkStack.cmd` for the background CLI watch plus HTTP bridge path, or `Bridge-ChromaLink.cmd` if you only want the rolling snapshot loop.
2. Inspect with `Open-ChromaLink-Monitor.cmd`, `Open-ChromaLinkDashboard.cmd`, or `Open-ChromaLinkHttpBridge.cmd` depending on whether you want the WinForms monitor, browser dashboard, or raw bridge.
3. Verify with `Probe-ChromaLinkHttpBridge.cmd`, `Get-ChromaLinkStackStatus.cmd`, `Status-ChromaLinkStack.cmd`, or `Status-ChromaLinkHttpBridge.cmd`.
4. Stop with `Stop-ChromaLinkStack.cmd`, or `Stop-ChromaLinkHttpBridge.cmd` if you only want to stop the bridge process.

This keeps the rolling JSON snapshot as the source of truth while giving you three views on top of it:

- monitor for artifact-heavy inspection
- browser dashboard for a zero-install browser view
- HTTP bridge and readiness checks for automation

To read the latest merged telemetry snapshot from the console without opening the inspector:

```powershell
.\scripts\Show-ChromaLinkTelemetry.cmd
```

For a simple console watcher:

```powershell
.\scripts\Show-ChromaLinkTelemetry.cmd -Watch
```

If you want a one-click version:

- `Watch-ChromaLinkTelemetry.cmd` keeps the console snapshot view live
- `Open-ChromaLinkTelemetryJson.cmd` opens the raw snapshot file
- `Open-ChromaLinkTelemetryFolder.cmd` opens the telemetry output folder
- `Open-ChromaLink-LiveStack.cmd` starts the background stack and then opens the monitor

For an automation-friendly readiness check that exits nonzero when the bridge is stale or missing:

```powershell
.\scripts\Test-ChromaLinkTelemetryReady.cmd
```

`Reload-RiftUi.cmd` sends the normal RIFT `/reloadui` command to the active game window.

`Send-RiftSlash.cmd` can send other ChromaLink slash commands when we explicitly want scripted in-game control.

The slash-sender helpers now abort if RIFT does not actually become the foreground window, which is safer than typing into another app by mistake.

The inspector also watches the live bridge snapshot now, so it can act as a live-first bridge monitor:

- a dedicated `Live Bridge` panel surfaces readiness and freshness clearly
- the details pane shows aggregate state, frame freshness, metrics, and last backend
- when no BMP is loaded, the inspector can still show useful live bridge state from the rolling snapshot

To reduce the chance of covering the RIFT client during live capture, the helper launchers that open auxiliary windows now default to minimized startup where practical. That includes the repo-native bridge, monitor, stack, and browser-open helpers, plus the packaged stack launchers.

## Packaged Output

ChromaLink is still source-first in this repository. The packaged output is the assembled publish folder we want for handoff or run-from-folder use, not a replacement for the repo.

Create it with:

```powershell
.\scripts\Package-ChromaLinkDesktop.ps1
```

Or build the self-contained release flavor with:

```powershell
.\scripts\Package-ChromaLinkDesktop-SelfContained.cmd
```

The default package layout is:

```text
artifacts\package\
├── Open-ChromaLink-Product.cmd
├── Bridge-ChromaLink.cmd
├── README.md
├── package-manifest.json
├── Open-ChromaLink-Monitor.cmd
├── Status-ChromaLinkStack.cmd
├── Stop-ChromaLinkStack.cmd
├── Start-ChromaLinkStack.cmd
├── Open-ChromaLinkDashboard.cmd
└── desktop\
    ├── ChromaLink.Cli\
    ├── ChromaLink.HttpBridge\
    ├── ChromaLink.Inspector\
    └── ChromaLink.Monitor\
```

Self-contained release output is written separately to:

```text
artifacts\package-selfcontained\
```

Use the packaged output when you want:

- a predictable folder you can hand to another machine or workspace
- the desktop tools assembled together without opening the source tree
- the bridge, monitor, inspector, and dashboard to run from one stable layout

Use the repo-native workflow when you want:

- to edit Lua, reader, CLI, docs, or scripts directly
- to run `dotnet build`, `dotnet test`, `smoke`, `capture-dump`, `live`, or `watch` from source
- to keep the RIFT addon and desktop tools under active development

Practical difference:

- packaged output is the runnable product view
- repo-native is the working view
- both should keep the same bridge contract, live behavior, and validation results

Package-emitted launchers are intentionally narrow:

- `Open-ChromaLink-Product.cmd`
- `Bridge-ChromaLink.cmd`
- `Start-ChromaLinkStack.cmd`
- `Open-ChromaLink-Monitor.cmd`
- `Status-ChromaLinkStack.cmd`
- `Stop-ChromaLinkStack.cmd`
- `Open-ChromaLinkDashboard.cmd`

Packaged launcher roles:

- `Open-ChromaLink-Product.cmd` is the fastest first-run path: start the stack, wait for readiness, then open the monitor
- `Bridge-ChromaLink.cmd` starts the packaged CLI in `watch` mode so the rolling snapshot stays fresh
- `Start-ChromaLinkStack.cmd` starts the packaged CLI watch loop plus HTTP bridge without opening UI
- `Open-ChromaLink-Monitor.cmd` opens the packaged monitor explicitly
- `Status-ChromaLinkStack.cmd` reports local endpoint health, snapshot freshness, and package-local process counts
- `Stop-ChromaLinkStack.cmd` stops only the packaged CLI, HTTP bridge, and monitor processes from that package folder
- `Open-ChromaLinkDashboard.cmd` opens the local dashboard URL

The broader helper surface in `scripts/` stays repo-native. That includes launchers such as `Open-ChromaLink-LiveStack.cmd`, `Open-ChromaLink-Monitor.cmd`, status helpers, stop helpers, and the probe/readiness scripts.

Packaged workflow:

1. Build the package with `.\scripts\Package-ChromaLinkDesktop.ps1`
2. Open `artifacts\package\README.md`
3. Run `Open-ChromaLink-Product.cmd` for the normal first-run path
4. If you want to inspect health directly, run `Status-ChromaLinkStack.cmd`
5. Stop with `Stop-ChromaLinkStack.cmd`

Package flavor guidance:

- use `artifacts\package` when the target machine already has the matching .NET runtime
- use `artifacts\package-selfcontained` when you want the safer handoff option for another Windows machine

## Project Structure

```text
ChromaLink/
├── Core/                         # Lua config and shared addon-side definitions
├── RIFT/                         # Addon bootstrap, rendering, commands, diagnostics
├── DesktopDotNet/                # .NET 9 solution
│   ├── ChromaLink.Reader/        # capture, geometry lock, decode, shared diagnostics
│   ├── ChromaLink.Cli/           # smoke, replay, live, watch, bench, dump, prep
│   ├── ChromaLink.Monitor/       # live bridge UI for the rolling snapshot
│   ├── ChromaLink.Inspector/     # visual frame inspection and overlays
│   ├── ChromaLink.Tests/         # protocol, replay, synthetic, observer tests
│   └── ChromaLink.sln
├── scripts/                      # helper scripts for window prep and capture flows
├── notes/                        # lab log and investigation notes
├── PROJECT_PROMPT.md             # active product and research direction
└── README.md
```

## Outputs

Reader artifacts are written under:

- `%LOCALAPPDATA%\ChromaLink\DesktopDotNet`

Useful locations:

- `fixtures\chromalink-color-core.bmp`
- `out\chromalink-color-capture-dump.bmp`
- `out\chromalink-color-capture-dump-annotated.bmp`
- `out\chromalink-color-capture-dump.json`
- `out\chromalink-color-first-reject.bmp`

## Validation

After addon-side Lua changes:

```text
/reloadui
```

Useful validation commands:

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- smoke
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- capture-dump
```

Live capture is still the gold standard. If `capture-dump`, `live`, or replay rejects a frame with a clear reason, the observability path is doing its job.

## Notes And Source Of Truth

- product direction: [PROJECT_PROMPT.md](PROJECT_PROMPT.md)
- active desktop solution: [DesktopDotNet/ChromaLink.sln](DesktopDotNet/ChromaLink.sln)
- running lab log: [notes/telemetry-lab-log-2026-04-01.md](notes/telemetry-lab-log-2026-04-01.md)
- focused investigation notes: [notes/ui-resolution-investigation-2026-04-01.md](notes/ui-resolution-investigation-2026-04-01.md)

## Next Major Work

The most important remaining work is still practical, not cosmetic:

1. broaden payload coverage beyond `coreStatus`
2. improve live robustness under scaling drift and imperfect capture
3. tighten reject diagnostics by phase and failure type
4. keep the `640x360` baseline solid while wider-resolution support becomes its own mode

---

**ChromaLink** is still early, but the current project already has a real live baseline, reproducible tooling, and a much clearer path for growing telemetry without losing observability.
