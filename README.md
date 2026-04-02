# ChromaLink

ChromaLink is a reliability-first optical telemetry project for RIFT with three active parts:
- a Lua addon that renders a segmented color strip in game
- a `.NET 9` reader and CLI under `DesktopDotNet/`
- small Windows helpers for window prep, capture inspection, and replay/debugging

The active transport is now a reader-first encoded color strip. The older barcode/matrix baseline is archived for reference only.

## Current Baseline

This repo now targets a single live profile:
- profile `P360C`
- client `640x360`
- top band `640x24`
- `80` vertical segments at `8x24`
- fixed `8-color` alphabet
- fixed control markers on both edges
- one `coreStatus` frame only for the first live slice

Current proof level:
- offline smoke, replay, bench, build, and tests are passing
- live capture works against the running client and currently prefers `PrintWindow` for the most reliable `640x360` baseline, with `DesktopDuplication` and `ScreenBitBlt` still available for comparison
- live decode is proven on the current client and currently locks the real strip at `origin 0,0`, `pitch 2.8`, `scale 0.35`
- capture runs emit raw BMP, annotated BMP, and JSON sidecar diagnostics under `AppData\\Local\\ChromaLink\\DesktopDotNet\\out`

## Quick Start

### 1) Build once

```powershell
dotnet build .\DesktopDotNet\ChromaLink.sln
```

### 2) Prepare the RIFT window

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- prepare-window 32 32
```

### 3) Generate + verify synthetic fixture

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- smoke
```

### 4) Replay a saved capture

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- replay "$env:LOCALAPPDATA\ChromaLink\DesktopDotNet\fixtures\chromalink-color-core.bmp"
```

### 5) Capture + decode live strip

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- capture-dump
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 5 100
```

### 6) Open inspector

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj
```

## CLI Command Surface

`ChromaLink.Cli` currently supports:
- `smoke`
- `replay <bmpPath>`
- `live [sampleCount] [sleepMs]`
- `watch [durationSeconds] [sleepMs]`
- `bench`
- `capture-dump`
- `prepare-window [left] [top]`

Capture command backend flag:
- `--backend desktopdup|screen|printwindow`
- default live backend order: `PrintWindow`, then `DesktopDuplication`, then `ScreenBitBlt`

In-game slash commands:
- `/cl status` prints the current ChromaLink layout summary
- `/cl diag` adds nearby native RIFT frame summaries for overlap/debug checks
- `/cl refresh` forces an immediate strip refresh
- `/cl observer on|off|status` toggles an optional calibration lane below the strip for extra capture diagnostics
- `/cl traces on|off` arms or disables verbose layout tracing for the next `/reloadui`

Observer lane diagnostics:
- capture sidecars now include an `observerLane` section when the profile defines one
- observer sampling follows the live detected strip scale/origin, so the report remains meaningful on the current `scale 0.35` baseline

## Wrapper Scripts

- [Prepare-ChromaLink-640x360.cmd](scripts/Prepare-ChromaLink-640x360.cmd)
- [Smoke-ChromaLink.cmd](scripts/Smoke-ChromaLink.cmd)
- [Bench-ChromaLink.cmd](scripts/Bench-ChromaLink.cmd)
- [Live-ChromaLink.cmd](scripts/Live-ChromaLink.cmd)
- [Open-ChromaLink-Inspector.cmd](scripts/Open-ChromaLink-Inspector.cmd)
- [Reload-RiftUi.cmd](scripts/Reload-RiftUi.cmd)
- [Send-RiftSlash.cmd](scripts/Send-RiftSlash.cmd)
- [Resize-RiftClient-640x360.cmd](scripts/Resize-RiftClient-640x360.cmd)

`Reload-RiftUi.cmd` sends the official RIFT `/reloadui` command to the active game window so addon changes can be refreshed without restarting the client.
`Send-RiftSlash.cmd` can send other slash commands such as `/cl observer on`.

Resolution sweeps can now control the observer lane directly:

```powershell
.\scripts\Sweep-RiftResolutions.ps1 -Resolutions @('640x360') -ReloadUi -ObserverLane on
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

## What Still Needs Development

The project is stable for the first vertical slice, but still intentionally narrow. High-impact next work:

1. **Broader payload coverage**
   - add additional frame schemas beyond `coreStatus`
   - formalize schema versioning and negotiation plan

2. **Live robustness and calibration**
   - reduce sensitivity to UI scaling drift and color/gamma shifts
   - improve auto-lock and recovery after temporary decode loss

3. **Diagnostics quality**
   - richer reject reason reporting grouped by capture, geometry, and symbol decode phases
   - easier side-by-side replay tools for "good vs bad" frame comparisons

4. **Operational ergonomics**
   - single command for end-to-end local validation (`build + test + smoke + replay + bench`)
   - documented tuning profiles for common monitor/resolution setups

## Source Of Truth

- Product direction: [PROJECT_PROMPT.md](PROJECT_PROMPT.md)
- Active desktop solution: [DesktopDotNet/ChromaLink.sln](DesktopDotNet/ChromaLink.sln)
- Barcode-style and archive branches are reference-only
