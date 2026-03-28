# ChromaLink

ChromaLink is a reliability-first optical telemetry project for RIFT with three active parts:
- a Lua addon that renders a deterministic top-band strip in game
- a `.NET 9` reader and CLI under `DesktopDotNet/`
- small Windows helper scripts under `scripts/`

This repo now contains the first restored baseline from the legacy BarCode workspace, but the active product is plain `ChromaLink`.

## Current Baseline

What is restored in the live repo:
- the Lua sender stack under `Core/` and `RIFT/`
- the `P360A` sender/reader profile targeting a `640x40` strip on a `640x360` client
- the `.NET 9` reader, CLI, and tests under `DesktopDotNet/`
- the working standalone RIFT window resizer that targets the real game window instead of Minion or Glyph
- click-to-run wrappers for `smoke`, `bench`, `live`, and window preparation

What is still unproven in the fresh repo:
- a live end-to-end decode against the current in-game strip after this reset
- any feature beyond the hot-lane `coreStatus` and `tactical` baseline
- deferred work from the project prompt such as color payloads, delta scheduling, aura transport, and hostile summaries

## Quick Start

CLI examples:

```powershell
dotnet run --project C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- smoke
```

```powershell
dotnet run --project C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- bench
```

```powershell
dotnet run --project C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- prepare-window 32 32
```

```powershell
dotnet run --project C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 20 100
```

Wrapper scripts:
- [Prepare-ChromaLink-640x360.cmd](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Prepare-ChromaLink-640x360.cmd)
- [Smoke-ChromaLink.cmd](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Smoke-ChromaLink.cmd)
- [Bench-ChromaLink.cmd](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Bench-ChromaLink.cmd)
- [Live-ChromaLink.cmd](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Live-ChromaLink.cmd)
- [Resize-RiftClient-640x360.cmd](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Resize-RiftClient-640x360.cmd)
- [Run-ChromaLink.cmd](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Run-ChromaLink.cmd)

If RIFT was already running while Lua files changed, restart the client or reload the addon before expecting `capture-dump` or `live` to see the new strip.

## Outputs

Reader artifacts are written under:
`C:\Users\mrkoo\AppData\Local\ChromaLink\DesktopDotNet`

Useful locations:
- `out\chromalink-capture-dump.bmp`
- `out\chromalink-first-reject.bmp`
- `out\geometry-lock.txt`
- `fixtures\chromalink-core.bmp`
- `fixtures\chromalink-tactical.bmp`

## Source Of Truth

- Project direction lives in [PROJECT_PROMPT.md](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\PROJECT_PROMPT.md)
- Legacy BarCode and archived trees are reference only and are not the active product path
