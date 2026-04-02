# Next Product Plan - 2026-04-02 - Dashboard Upgrade And Topmost Access

This note records the optimal plan before implementing the next dashboard pass.

## Goal

Make the dashboard feel like a real telemetry cockpit instead of a thin bridge status page, while also giving the user an opt-in way to keep a live ChromaLink view above other windows during play.

## Constraints

- the current browser dashboard is served from the local HTTP bridge as a plain static page
- a normal browser page cannot reliably force itself to stay always on top
- the project already has a WinForms monitor that can read the same live snapshot contract
- the dashboard should stay tied to the existing live bridge contract, not invent a second data source

## Optimal Approach

1. Keep the browser dashboard.
   - It is still useful as the zero-install local browser view.
   - Improve it so it exposes the telemetry we already have instead of only the core baseline.

2. Handle always-on-top in the desktop monitor path.
   - Add an opt-in topmost mode to the WinForms monitor.
   - Expose it through launch flags and a visible runtime toggle.
   - Treat that desktop window as the reliable pinned live dashboard during gameplay.

3. Keep the two surfaces aligned around the same contract.
   - browser dashboard for quick local browser access
   - monitor for richer desktop access, including topmost mode

## Browser Dashboard Improvements

- replace the generic three-card layout with telemetry-first sections
- show richer aggregate state:
  - player vitals
  - player cast
  - player resources
  - player combat
  - target vitals
  - target position
  - follow-unit summary
  - generic page summaries for aura, text, and ability watch
- add stronger freshness and stale-state presentation per section
- improve visual hierarchy so the most important state is visible at a glance
- keep the implementation static and lightweight, but split logic cleanly enough to remain maintainable

## Desktop Topmost Improvements

- add `--always-on-top` support to the monitor
- add a visible topmost toggle in the monitor UI
- add a dedicated launcher for the pinned monitor path
- propagate the same launcher into packaged output if the package generator already emits monitor helpers

## Validation Gates

- `dotnet build .\DesktopDotNet\ChromaLink.HttpBridge\ChromaLink.HttpBridge.csproj`
- `dotnet build .\DesktopDotNet\ChromaLink.Monitor\ChromaLink.Monitor.csproj`
- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- launch the upgraded browser dashboard and confirm it renders richer telemetry
- launch the monitor in topmost mode and confirm the opt-in toggle works

## Success Criteria

- the browser dashboard is materially more useful for live telemetry
- the user has a real always-on-top ChromaLink surface for gameplay
- both surfaces read the same snapshot contract
- existing helper and package flows stay aligned
