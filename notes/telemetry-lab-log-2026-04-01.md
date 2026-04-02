# Telemetry Lab Log (2026-04-01)

## Purpose

Keep a lightweight running record of ChromaLink strip work:

- what changed
- why it changed
- how it was tested
- what happened
- whether the change should stay

This file is meant to complement, not replace:

- git commits for stable checkpoints
- focused investigation notes such as `ui-resolution-investigation-2026-04-01.md`
- capture artifacts under `%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out`

## Log Format

For each new experiment or checkpoint, record:

1. timestamp or session marker
2. change summary
3. verification commands
4. result
5. decision: keep, revert, or revisit later

---

## 2026-04-01 - Session A - `640x360` baseline recovery

### Goal

Re-establish a solid `640x360` live baseline after resolution/layout investigation work caused the live path to drift.

### Change

- simplify the Lua strip path back to an explicit `640x360` baseline
- keep the strip render assumptions fixed for the proven profile
- add a synthetic low-res reader test that matches the real live lock:
  - `scale 0.35`
  - `pitch 2.8`
- change the CLI default backend order to prefer `PrintWindow` first, then `DesktopDuplication`, then `ScreenBitBlt`
- harden annotated artifact generation so a bad BMP does not crash `capture-dump`
- change the resolution sweep script default backend to `printwindow`

### Why

`DesktopDuplication` and `ScreenBitBlt` were sometimes capturing occluding desktop content instead of the real RIFT strip at `640x360`. That produced fake decode failures even when the addon strip itself was fine.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- capture-dump
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- capture-dump --backend desktopdup
```

```powershell
.\scripts\Sweep-RiftResolutions.ps1 -Resolutions @('640x360') -ReloadUi
```

### Result

- tests passed: `7/7`
- default `capture-dump` accepted live again
- `640x360` sweep after `/reloadui` accepted
- current proven live lock:
  - `origin 0,0`
  - `pitch 2.8`
  - `scale 0.35`
- direct `DesktopDuplication` still fails in the current desktop environment, but now fails cleanly instead of crashing the workflow

### Decision

Keep.

### Saved Checkpoint

- commit `82a7d43` - `stabilize 640x360 capture baseline`

---

## 2026-04-01 - Session B - quiet default diagnostics

### Goal

Remove the noisy native-layout debug spam from normal addon use without losing the ability to inspect layout when needed.

### Change

- disable layout diagnostics by default
- add safe slash commands:
  - `/cl status`
  - `/cl diag`
  - `/cl refresh`
  - `/cl traces on|off`
- keep layout tracing available, but only as an opt-in path that should be paired with `/reloadui`

### Why

The earlier investigation left native frame tracing enabled by default, which caused chat spam and made normal strip work noisy and confusing.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
.\scripts\Sweep-RiftResolutions.ps1 -Resolutions @('640x360') -ReloadUi -OutputRoot "$env:LOCALAPPDATA\ChromaLink\DesktopDotNet\out\resolution-sweep-640-quiet"
```

### Result

- tests passed: `7/7`
- `640x360` live sweep still accepted after `/reloadui`
- default startup path no longer includes native layout spam
- diagnostic visibility is still available on demand

### Decision

Keep.

### Saved Checkpoint

- commit `7b31609` - `quiet default strip diagnostics`

---

## 2026-04-01 - Session C - optional observer lane

### Goal

Add one low-risk, modular visual aid that can help future capture/debug work without altering the strip payload or decoder contract.

### Change

- add an optional observer lane below the strip
- keep it off by default
- make it toggleable in-game:
  - `/cl observer on`
  - `/cl observer off`
  - `/cl observer status`
- keep the observer lane independent from the old layout-trace path

### Why

Future telemetry work will benefit from a stable, removable visual reference for:

- clipping checks
- scale drift checks
- color drift checks
- future capture experiments

The observer lane is intended to help data collection without disturbing the main strip contract.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
.\scripts\Sweep-RiftResolutions.ps1 -Resolutions @('640x360') -ReloadUi -OutputRoot "$env:LOCALAPPDATA\ChromaLink\DesktopDotNet\out\resolution-sweep-640-observer-off"
```

### Result

- tests passed: `7/7`
- with observer lane disabled by default, `640x360` still accepted after `/reloadui`
- live lock remained:
  - `origin 0,0`
  - `pitch 2.8`
  - `scale 0.35`

### Decision

Keep.

### Saved Checkpoint

- commit `67e1601` - `add optional strip observer lane`

---

## 2026-04-01 - Session D - observer lane automation

### Goal

Make the observer lane operational for scripted capture runs instead of relying on manual in-game toggles.

### Change

- add a generic slash-command helper script:
  - `scripts/Send-RiftSlash.ps1`
  - `scripts/Send-RiftSlash.cmd`
- extend `Sweep-RiftResolutions.ps1` with:
  - `-ObserverLane leave|off|on`
- verify that `/cl observer on` can be sent automatically before a live capture

### Why

The observer lane only becomes truly useful for telemetry investigation if it can be enabled and disabled repeatably during scripted sweeps. That keeps future experiments reproducible and lowers the friction of capture collection.

### Verification

```powershell
.\scripts\Send-RiftSlash.ps1 -CommandText '/cl observer on'
```

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
.\scripts\Sweep-RiftResolutions.ps1 -Resolutions @('640x360') -ReloadUi -ObserverLane on -OutputRoot "$env:LOCALAPPDATA\ChromaLink\DesktopDotNet\out\resolution-sweep-640-observer-on"
```

```powershell
.\scripts\Send-RiftSlash.ps1 -CommandText '/cl observer off'
```

### Result

- tests passed: `7/7`
- the scripted `/cl observer on` command reached the running client
- the `640x360` live sweep still accepted with the observer lane enabled
- live lock remained:
  - `origin 0,0`
  - `pitch 2.8`
  - `scale 0.35`
- the observer lane was then returned to `off` for normal play

### Decision

Keep.

### Saved Checkpoint

- pending commit for observer automation milestone

---

## 2026-04-01 - Session E - scale-aware observer diagnostics

### Goal

Make observer-lane reporting useful on the real live baseline instead of only on full-size synthetic canvases.

### Change

- add a shared reader-layer observer analyzer so multiple tools can reuse the same logic
- extend capture sidecars and annotated BMP overlays with observer-lane summary fields
- extend the inspector sidecar summary with observer-lane visibility details
- make observer-lane sampling scale-aware by following the live detected strip origin and scale
- add tests for:
  - full-size observer lane sampling
  - scaled live `0.35` observer lane sampling

### Why

The first observer report sampled marker positions as if the lane were full-size. That was wrong for the real `640x360` baseline because the live addon still lands at `scale 0.35`, so the analyzer was looking in the wrong places.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- replay "$env:LOCALAPPDATA\ChromaLink\DesktopDotNet\out\resolution-sweep-640-observer-verified\640x360\capture.bmp"
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- capture-dump
```

### Result

- tests passed: `9/9`
- the observer analyzer is now shared instead of CLI-only
- observer diagnostics no longer assume full-size marker positions
- the `640x360` baseline remained accepted after the change
- current sidecars and overlays are now able to report observer-lane diagnostics in a structured way

### Decision

Keep.

### Saved Checkpoint

- pending commit for scale-aware observer diagnostics

---

## 2026-04-01 - Session F - visual observer artifact overlay

### Goal

Make saved capture artifacts visually explain observer-lane results without requiring manual JSON inspection.

### Change

- extend observer marker diagnostics with:
  - sampled rectangle bounds
  - sampled center coordinates
- draw observer marker rectangles and centers into the annotated BMP artifact
- extend the inspector sidecar summary to list observer marker rectangles

### Why

The observer sidecar had become useful, but saved captures still required reading JSON to understand which observer markers matched or where they were sampled. Visual overlays make replay/debugging much faster.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- capture-dump
```

### Result

- tests passed: `9/9`
- capture-dump still succeeded
- current observer sidecar fields now include marker bounds
- the latest capture sidecar reported:
  - `observerLane.probablyVisible = true`
  - `matchedMarkers = 8/8`
- annotated BMP artifacts now have explicit observer marker visuals

### Decision

Keep.

### Saved Checkpoint

- pending commit for visual observer artifact overlay

---

## 2026-04-01 - Session G - inspector observer overlay

### Goal

Make the desktop inspector preview show observer-lane geometry directly instead of only through sidecar text or annotated BMP exports.

### Change

- wire the shared observer analyzer into the inspector preview control
- draw observer marker boxes and sample centers in the zoomed inspector preview

### Why

At this point the CLI artifacts already showed observer geometry, but the live inspector preview still only drew strip ROI lines and payload sample probes. That made observer debugging slower than it needed to be.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
dotnet build .\DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj
```

### Result

- tests passed: `9/9`
- inspector build succeeded
- the inspector now uses the same shared observer report as the CLI artifact path

### Decision

Keep.

### Saved Checkpoint

- pending commit for inspector observer overlay

---

## 2026-04-01 - Session H - README refresh

### Goal

Make the project front page read like a real operational overview instead of a narrow scratchpad.

### Change

- rewrite `README.md` into a clearer project front door
- add:
  - system overview
  - explicit current scope
  - quick start
  - command surface
  - observer lane overview
  - protocol snapshot
  - project structure
  - validation guidance
  - direct pointers to the lab log and investigation notes

### Why

The earlier README had the right facts, but it did not present the project as clearly as it deserved. A stronger README helps future work, onboarding, and comparison against adjacent telemetry projects without changing any runtime behavior.

### Verification

```powershell
Get-Content -Raw .\README.md
```

### Result

- the README now explains ChromaLink as a full telemetry workflow instead of only a list of commands
- the current `640x360` baseline and proven live lock are still stated explicitly
- the observer lane and diagnostics tooling now have a proper documented place in the project front page

### Decision

Keep.

### Saved Checkpoint

- pending commit for README refresh

---

## 2026-04-01 - Session I - README badge polish

### Goal

Make the top badge row look closer to the cleaner project style we want to present publicly.

### Change

- tune the README badge row colors
- add a `License MIT` badge
- keep the existing project links while making the top line feel more intentional

### Why

The structure rewrite made the README stronger, and a cleaner badge row finishes the first visual impression without affecting any runtime behavior.

### Verification

```powershell
Get-Content -TotalCount 8 .\README.md
```

### Result

- the top of the README now better matches the visual style we want
- the project now exposes license info directly in the badge row

### Decision

Keep.

### Saved Checkpoint

- pending commit for README badge polish

---

## Current Stable Baseline At End Of Log

- target client: `640x360`
- live capture default: `PrintWindow`
- live proven lock:
  - `origin 0,0`
  - `pitch 2.8`
  - `scale 0.35`
- normal startup:
  - quiet diagnostics
  - no native trace spam
- optional tooling:
  - status/diag slash commands
  - trace arming
  - observer lane toggle

## Next Entry Template

```md
## YYYY-MM-DD - Session X - short title

### Goal

### Change

### Why

### Verification

### Result

### Decision

### Saved Checkpoint
```
