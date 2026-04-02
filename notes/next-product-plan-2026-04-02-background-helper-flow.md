# Next Product Plan - 2026-04-02 - Background Helper Flow

This note records the next planned step before implementation.

## Goal

Separate background stack startup from explicit UI opening so live-capture workflows can keep the telemetry stack running without raising helper windows over the RIFT client.

## Why This Is The Optimal Next Step

The minimized-window change reduced occlusion risk, but the helper surface still mixes two different intents:

- start background telemetry services
- open auxiliary UI

There is also a functional gap:

- `Open-ChromaLink-DashboardStack.cmd` starts the HTTP bridge and browser, but not the CLI watch loop that keeps the rolling snapshot fresh

The next practical step is to make `Start` mean background stack, and keep `Open` as explicit UI on top of that.

## Product Direction

Focus this pass on four outcomes:

1. Add a repo-native background stack launcher.
2. Make UI-opening helpers reuse the background stack instead of partially reassembling it.
3. Keep browser and monitor launchers explicit.
4. Align the packaged output with the same start-vs-open split where it adds clarity.

## Constraints

- Keep the proven `640x360` strip baseline unchanged.
- Do not change addon Lua or the live decode path.
- Keep the helper naming intuitive.
- Preserve existing lifecycle helpers.

## Implementation Shape

Target behavior:

- `Start-ChromaLinkStack.cmd`
  - starts CLI watch plus HTTP bridge
  - does not open monitor or browser
- `Open-ChromaLink-LiveStack.cmd`
  - reuses `Start-ChromaLinkStack.cmd`
  - then opens the monitor helper
- `Open-ChromaLink-DashboardStack.cmd`
  - reuses `Start-ChromaLinkStack.cmd`
  - then opens the dashboard helper

## Validation Gates

Treat this pass as complete only if all of these remain healthy:

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- `Start-ChromaLinkStack.cmd` starts the expected background processes
- `Status-ChromaLinkStack.cmd` reflects the running stack correctly
- `Stop-ChromaLinkStack.cmd` still stops the stack cleanly
- packaged output still emits a coherent launcher set

## Success Criteria

- safe background start path exists for capture work
- UI-opening helpers no longer own partial stack startup logic
- dashboard stack includes the live producer path

## Result

Implemented by:

- adding `scripts/Start-ChromaLinkStack.cmd` as the repo-native background stack launcher
- updating `Open-ChromaLink-LiveStack.cmd` and `Open-ChromaLink-DashboardStack.cmd` to reuse the background stack
- ensuring the dashboard stack now includes the CLI watch producer path
- aligning the packaged output so:
  - `Start-ChromaLinkStack.cmd` is background-only
  - `Open-ChromaLink-Monitor.cmd` is the explicit packaged monitor opener

Validation confirmed:

- `dotnet test .\DesktopDotNet\ChromaLink.sln` passed
- `Start-ChromaLinkStack.cmd` started the expected background stack without monitor UI
- `Status-ChromaLinkStack.cmd` reported a fresh stack with monitor count at zero
- `Stop-ChromaLinkStack.cmd` stopped the background stack cleanly
- `.\scripts\Package-ChromaLinkDesktop.ps1` emitted the updated package launcher set and docs
