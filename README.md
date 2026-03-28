# ChromaLink

ChromaLink has been reset to a clean baseline on 2026-03-28.

The previous experiment was preserved under:
`C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\_archive\chromalink-reset-2026-03-28`

## Fresh Baseline

The addon now does only three things:
- loads reliably in RIFT
- logs startup or runtime errors to chat
- renders a tiny animated proof-of-life strip across the top of the UI

This is intentional. The project is small again so we can decide the next direction from a stable starting point instead of carrying forward half-finished protocol and desktop tooling.

## Current Layout

- `Core/Config.lua`: shared settings for the fresh baseline
- `RIFT/Diagnostics.lua`: lightweight logging
- `RIFT/ErrorTrap.lua`: addon-specific error reporting
- `RIFT/Render.lua`: tiny top-band renderer
- `RIFT/Bootstrap.lua`: load and update loop
- `scripts/Resize-RiftClient-640x360.cmd`: optional Windows helper that restores and sizes the RIFT client area to `640x360`

## Next Build Direction

When we pick the next version, we should add one capability at a time:
1. define the single most important gameplay signal to show
2. render it in the addon with no desktop dependency
3. add transport or decoding only after the on-screen representation feels right
