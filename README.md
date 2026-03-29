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
- live capture works against the running client with `DesktopDuplication` as the primary backend
- live decode is proven on the current client and currently locks the real strip at `origin 0,0`, `pitch 2.8`, `scale 0.35`
- capture runs now emit raw BMP, annotated BMP, and JSON sidecar diagnostics under `AppData\Local\ChromaLink\DesktopDotNet\out`

## Quick Start

Prepare the RIFT window:

```powershell
dotnet run --project C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- prepare-window 32 32
```

Generate and verify a synthetic color-strip fixture:

```powershell
dotnet run --project C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- smoke
```

Replay a saved capture:

```powershell
dotnet run --project C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- replay C:\Users\mrkoo\AppData\Local\ChromaLink\DesktopDotNet\fixtures\chromalink-color-core.bmp
```

Run the replay bench:

```powershell
dotnet run --project C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- bench
```

Capture the live top band:

```powershell
dotnet run --project C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- capture-dump --backend desktopdup
```

Run a short live decode:

```powershell
dotnet run --project C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 5 100 --backend desktopdup
```

Open the inspector:

```powershell
dotnet run --project C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj
```

Wrapper scripts:
- [Prepare-ChromaLink-640x360.cmd](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Prepare-ChromaLink-640x360.cmd)
- [Smoke-ChromaLink.cmd](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Smoke-ChromaLink.cmd)
- [Bench-ChromaLink.cmd](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Bench-ChromaLink.cmd)
- [Live-ChromaLink.cmd](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Live-ChromaLink.cmd)
- [Open-ChromaLink-Inspector.cmd](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Open-ChromaLink-Inspector.cmd)
- [Reload-RiftUi.cmd](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Reload-RiftUi.cmd)
- [Resize-RiftClient-640x360.cmd](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Resize-RiftClient-640x360.cmd)

If RIFT was already running while Lua files changed, restart the client or reload the addon before expecting `capture-dump` or `live` to see the new strip.
`capture-dump`, `live`, and `watch` accept `--backend desktopdup|screen|printwindow`.
Default live backend order is `DesktopDuplication`, then `ScreenBitBlt`. `PrintWindow` is now debug-only.
`Reload-RiftUi.cmd` sends the official RIFT `/reloadui` command to the active game window so you can refresh addon changes without restarting the client.

## Outputs

Reader artifacts are written under:
`C:\Users\mrkoo\AppData\Local\ChromaLink\DesktopDotNet`

Useful locations:
- `fixtures\chromalink-color-core.bmp`
- `out\chromalink-color-capture-dump.bmp`
- `out\chromalink-color-capture-dump-annotated.bmp`
- `out\chromalink-color-capture-dump.json`
- `out\chromalink-color-first-reject.bmp`

## Source Of Truth

- Product direction lives in [PROJECT_PROMPT.md](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\PROJECT_PROMPT.md)
- Active desktop solution lives at [DesktopDotNet/ChromaLink.sln](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.sln)
- Barcode-style and archive branches are reference only
