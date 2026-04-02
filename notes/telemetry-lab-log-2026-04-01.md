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
