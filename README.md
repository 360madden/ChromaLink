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
- first throughput expansion in code: `playerVitals`

Current live proof:

- offline smoke, replay, bench, build, and tests are passing
- default live capture now prefers `PrintWindow` for the most reliable `640x360` path
- `DesktopDuplication` and `ScreenBitBlt` are still available for comparison
- live decode is currently proven at:
  - `origin 0,0`
  - `pitch 2.8`
  - `scale 0.35`
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

## CLI Commands

`ChromaLink.Cli` currently supports:

- `smoke`
- `replay <bmpPath>`
- `live [sampleCount] [sleepMs]`
- `watch [durationSeconds] [sleepMs]`
- `bench`
- `capture-dump`
- `prepare-window [left] [top]`

Capture backend flag:

- `--backend desktopdup|screen|printwindow`
- default backend order for live work:
  - `PrintWindow`
  - `DesktopDuplication`
  - `ScreenBitBlt`

## In-Game Commands

ChromaLink exposes a small slash-command surface inside RIFT:

- `/cl status`
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

The current addon rotation keeps `coreStatus` as the dominant heartbeat and periodically inserts `playerVitals` to increase throughput over time instead of widening the strip.

## Wrapper Scripts

Useful helper scripts:

- [scripts/Prepare-ChromaLink-640x360.cmd](scripts/Prepare-ChromaLink-640x360.cmd)
- [scripts/Smoke-ChromaLink.cmd](scripts/Smoke-ChromaLink.cmd)
- [scripts/Bench-ChromaLink.cmd](scripts/Bench-ChromaLink.cmd)
- [scripts/Live-ChromaLink.cmd](scripts/Live-ChromaLink.cmd)
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

`Reload-RiftUi.cmd` sends the normal RIFT `/reloadui` command to the active game window.

`Send-RiftSlash.cmd` can send other ChromaLink slash commands when we explicitly want scripted in-game control.

## Project Structure

```text
ChromaLink/
├── Core/                         # Lua config and shared addon-side definitions
├── RIFT/                         # Addon bootstrap, rendering, commands, diagnostics
├── DesktopDotNet/                # .NET 9 solution
│   ├── ChromaLink.Reader/        # capture, geometry lock, decode, shared diagnostics
│   ├── ChromaLink.Cli/           # smoke, replay, live, watch, bench, dump, prep
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
