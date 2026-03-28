# ChromaLink

ChromaLink is the active RIFT addon project and is now guided by a reliability-first optical telemetry brief.

The durable working brief lives in [PROJECT_PROMPT.md](C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\PROJECT_PROMPT.md).

## Active Direction

The selected direction for ChromaLink is:
- a fixed `640x360`-safe sender/reader path
- deterministic top-band placement and structural lock
- explicit integrity checks and actionable logging
- replay-first verification for the desktop reader
- a Lua addon sender paired with a `.NET 9` desktop reader/tooling path

The repo has not rebuilt all of that yet. The live code is still the fresh baseline we reset to, and that baseline now serves as the starting point for this broader brief.

## Current Live Baseline

What works in the live repo right now:
- the addon loads in RIFT
- startup/runtime errors are surfaced to chat
- a simple proof-of-life strip renders at the top of the UI
- the external `640x360` resize helper works against the real RIFT window

## Current Layout

- `PROJECT_PROMPT.md`: the active long-form project brief
- `Core/Config.lua`: shared settings for the live baseline
- `RIFT/Diagnostics.lua`: lightweight logging
- `RIFT/ErrorTrap.lua`: addon-specific error reporting
- `RIFT/Render.lua`: tiny top-band renderer
- `RIFT/Bootstrap.lua`: load and update loop
- `scripts/Resize-RiftClient-640x360.cmd`: Windows helper that restores and sizes the RIFT client area to `640x360`
- `scripts/Resize-RiftClient-640x360.ps1`: resize logic and before/after reporting

## Immediate Priorities

The next implementation steps should follow the selected brief:
1. prove the fixed sender geometry and `P360A`-style live strip path at `640x360`
2. restore or rebuild the `.NET 9` replay-first reader/tooling path inside this repo
3. keep validation, reject artifacts, and live failure reporting explicit

## Legacy References

Older work remains available for reference:
- archived ChromaLink experiment: `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\_archive\chromalink-reset-2026-03-28`
- legacy `BarCode` tree: `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\BarCode`
