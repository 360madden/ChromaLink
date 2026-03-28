# ChromaLink

ChromaLink was reset to a fresh root commit on March 28, 2026.

The active project stays in this folder:
`C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink`

The GitHub repo stays here:
[360madden/ChromaLink](https://github.com/360madden/ChromaLink)

The old project history was preserved on:
`archive/pre-reset-2026-03-28-134247`

## Current State

This repo is intentionally minimal again.

What exists now:
- a clean RIFT addon scaffold
- the durable project prompt
- a placeholder `.NET 9` workspace root for the future reader/tooling stack

What does not exist yet:
- the restored telemetry strip
- the `.NET 9` reader
- helper apps/tooling
- replay fixtures and tests

## Working Brief

The active project brief lives in `PROJECT_PROMPT.md`.

## Repo Shape

- `Core/`: addon configuration
- `RIFT/`: addon bootstrap and diagnostics
- `DesktopDotNet/`: future `.NET 9` reader/tooling workspace

## Next Milestone

The next real slice should restore the first working vertical path:
1. deterministic addon strip
2. `.NET 9` reader
3. helper tooling that speeds up replay and live validation
