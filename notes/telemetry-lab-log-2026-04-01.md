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

## 2026-04-01 - Session J - clipping-aware observer diagnostics

### Goal

Make observer-lane diagnostics explain boundary problems directly instead of only reporting marker matches.

### Change

- extend observer marker samples with:
  - visible fraction
  - center-in-bounds flag
  - bounds state
- extend observer reports with:
  - visibility hint
  - visible/partial/outside counts
  - per-edge affected counts
- expose the new fields in:
  - capture sidecars
  - annotated BMP overlay summary text
  - inspector sidecar summary
- add a synthetic right-clipping test case

### Why

The old observer report was useful, but it still left too much ambiguity when the lane was shifted or clipped. We need the tooling to tell us whether a failure looks like geometry loss, off-screen placement, or simple color mismatch.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

### Result

- tests passed: `10/10`
- observer diagnostics now distinguish normal visibility from clipping-oriented failures
- saved sidecars and overlays can now explain why the observer lane is unhealthy instead of only showing counts

### Decision

Keep.

### Saved Checkpoint

- pending commit for clipping-aware observer diagnostics

---

## 2026-04-01 - Session K - inspector observer summary without sidecar

### Goal

Make the inspector useful on raw BMPs even when a sidecar JSON file is missing.

### Change

- compute an observer-lane report directly from the loaded frame inside the inspector
- show the current observer health, pattern, and bounds summary in the details pane before sidecar-specific information
- keep sidecar summaries in place as an additional source, not the only source

### Why

The preview overlay was already using live observer analysis, but the text summary still depended too heavily on sidecar data. For debugging ad hoc BMPs, the inspector should explain observer health from the pixels it already has.

### Verification

```powershell
dotnet build .\DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj
```

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

### Result

- inspector build succeeded
- tests passed: `10/10`
- the inspector details pane now reports live observer health even on standalone BMP files

### Decision

Keep.

### Saved Checkpoint

- pending commit for inspector observer summary without sidecar

---

## 2026-04-01 - Session L - machine readability rule

### Goal

Make the strip-design priority explicit so future work does not accidentally optimize for appearance over decode reliability.

### Change

- add an explicit strip-design rule to `PROJECT_PROMPT.md`
- add the same principle to the README introduction

### Why

This project only succeeds if the strip remains reliably machine-readable under live capture. A visible reminder in the core docs helps keep future design decisions aligned with that goal.

### Verification

```powershell
Get-Content -Raw .\PROJECT_PROMPT.md
```

```powershell
Get-Content -TotalCount 12 .\README.md
```

### Result

- the project docs now clearly state that machine readability comes first
- visual neatness is documented as secondary to decoder margin

### Decision

Keep.

### Saved Checkpoint

- pending commit for machine readability rule

---

## 2026-04-01 - Session M - experimental display compensation toggle

### Goal

Create a safe way to test the idea of oversizing the strip internally so the final displayed strip stays closer to the target machine-readable size.

### Change

- add `displayCompensation` config with:
  - `enabled`
  - `mode`
  - `maxScaleX`
  - `maxScaleY`
  - `allowShrink`
- update layout math to derive effective display scale from the current anchor dimensions when compensation is enabled
- add slash commands:
  - `/cl compensate on`
  - `/cl compensate off`
  - `/cl compensate status`
- extend `/cl status` output with compensation summary fields
- document the feature in the README as experimental

### Why

The project has been fighting RIFT UI shrink behavior. A controllable compensation path lets us test the idea that the addon can oversize the strip before RIFT applies its own layout/scaling, so the final on-screen strip may stay closer to the intended readable size.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

### Result

- tests passed: `10/10`
- compensation mode is now available as an explicit experiment instead of an undocumented code branch
- desktop validation stayed green
- live RIFT validation is still pending

### Decision

Keep as experimental.

### Saved Checkpoint

- pending commit for experimental display compensation toggle

---

## 2026-04-01 - Session N - multi-frame throughput expansion

### Goal

Increase strip throughput without making the strip wider or shrinking the segment geometry.

### Change

- add a second transport frame type, `playerVitals`
- keep the transport at the same fixed `24` bytes and `80` segments
- add Lua-side frame rotation so the addon now sends:
  - `coreStatus`
  - `coreStatus`
  - `coreStatus`
  - `playerVitals`
- expand the reader protocol to parse both frame types
- update CLI and inspector summaries to show the decoded frame type and payload
- extend diagnostics artifacts so captures report which frame type was decoded
- add a synthetic round-trip test for `playerVitals`

### Why

Earlier discussion raised the idea of making the strip longer for more throughput. That would either break the `640x360` width budget or force thinner segments. Rotating multiple frame types through the same strip geometry is the safer throughput path because it preserves the current machine-readable baseline.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
dotnet build .\DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- smoke
```

### Result

- tests passed: `11/11`
- inspector build succeeded
- smoke remained accepted
- the transport now supports more than one frame type while keeping the same strip size
- live RIFT validation confirmed after `/reloadui`
- sampled live captures decoded both:
  - `CoreStatus`
  - `PlayerVitals`

### Decision

Keep.

### Saved Checkpoint

- commit `796499c` - `add rotating multi-frame telemetry`

### Live Validation Notes

After `/reloadui`, repeated `capture-dump` runs showed accepted live decodes alternating across frame types. Sample observations included:

- `CoreStatus`, schema `1`, sequence `137`
- `PlayerVitals`, schema `1`, sequence `155`
- `PlayerVitals`, schema `1`, sequence `179`
- `PlayerVitals`, schema `1`, sequence `203`
- `CoreStatus`, schema `1`, sequence `222`
- `PlayerVitals`, schema `1`, sequence `247`

---

## 2026-04-01 - Session O - rotating position telemetry

### Goal

Extend the rotating telemetry model with player position while keeping the strip geometry unchanged.

### Change

- add a third frame type, `playerPosition`
- gather live `coordX`, `coordY`, and `coordZ` from the RIFT player detail
- encode position as signed fixed-point `int32` values at `*100` scale
- add reader support, CLI summary support, and inspector summary support for the new frame
- update the addon rotation to:
  - `coreStatus`
  - `coreStatus`
  - `playerVitals`
  - `coreStatus`
  - `playerPosition`
- add a synthetic round-trip test for `playerPosition`

### Why

Once rotating multi-frame telemetry was proven live, the next highest-value addition was position. It fits naturally into the fixed `24`-byte transport and raises the practical usefulness of the strip without touching the proven physical geometry.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
dotnet build .\DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj
```

### Result

- tests passed: `12/12`
- inspector build succeeded
- local synthetic round-trip for `playerPosition` passed
- live RIFT validation after `/reloadui` did **not** yet show `playerPosition`
- observed live frame counts still matched the older two-frame mix:
  - `CoreStatus`
  - `PlayerVitals`
- this suggests the running client did not pick up the new five-step rotation even after reload, or that another live-side issue is still masking the new frame

### Decision

Keep.

### Saved Checkpoint

- pending commit for rotating position telemetry

### Live Investigation Notes

- added per-frame-type counts to the CLI `live` command to make rotation verification easier
- a `100`-sample live run with `--backend screen` reported:
  - `FrameCount[CoreStatus/schema-1]: 74`
  - `FrameCount[PlayerVitals/schema-1]: 26`
- no `PlayerPosition` frames were observed in that live sample set
- slash-sender scripts were also hardened to abort if RIFT is not actually foreground

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

---

## 2026-04-02 - Session P - header capability proof flags

### Goal

Make a normal live capture prove whether the running addon has loaded the newer multi-frame telemetry build.

### Change

- repurpose the existing reserved header byte as explicit build-capability flags
- set `0x01` for multi-frame rotation support
- set `0x02` for player-position support
- teach the CLI frame summary to print `ReservedFlags` in hex plus readable labels
- add round-trip assertions so every supported frame type preserves the expected capability flags

### Why

The live client was still behaving like the older two-frame rotation after `/reloadui`. A strip-level proof marker is safer and more trustworthy than chat text or focus-sensitive slash-command output because it rides inside the same pixels the reader is already decoding.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- smoke
```

### Result

- tests passed: `12/12`
- `smoke` passed
- CLI frame summaries now print `ReservedFlags: 0x03 (multi-frame, player-position)`
- the local reader path can now prove build capability directly from decoded strip bytes
- live RIFT proof was completed in the next session after a safe `/reloadui`

### Decision

Keep if tests and smoke pass.

### Saved Checkpoint

- pending commit for header capability proof flags

---

## 2026-04-02 - Session Q - live proof of header flags and player position

### Goal

Use one safe reload plus live capture to prove whether the running addon loaded the new build and whether `playerPosition` is actually present on the strip.

### Change

- reload the live addon with `/reloadui`
- capture a live sample set and inspect both frame counts and header capability flags

### Why

The local code path was already proven, but we needed the strip itself to tell us whether the running RIFT client had really loaded the new telemetry build.

### Verification

```powershell
.\scripts\Reload-RiftUi.cmd
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 60 50 --backend screen
```

### Result

- live decode accepted `60/60` samples
- live frame counts were:
  - `CoreStatus`: `35`
  - `PlayerVitals`: `12`
  - `PlayerPosition`: `13`
- the last decoded frame was `PlayerPosition`
- the live frame summary printed `ReservedFlags: 0x03 (multi-frame, player-position)`
- this proves the running addon loaded the newer build and that `playerPosition` is now decoding live

### Decision

Keep.

### Saved Checkpoint

- pending commit for live proof of header flags and player position

---

## 2026-04-02 - Session R - reader-side telemetry aggregate

### Goal

Turn the rotating live frame stream into a coherent reader-side telemetry state that downstream tools can use directly.

### Change

- add a shared `TelemetryAggregate` to the reader layer
- keep the newest accepted `CoreStatus`, `PlayerVitals`, and `PlayerPosition` observations with timestamps
- print an aggregate summary from CLI `live` and `watch`
- write a rolling JSON snapshot for live aggregate state under `%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out`
- add `scripts/Bridge-ChromaLink.cmd` as a simple continuous bridge launcher
- show `ReservedFlags` in inspector header details
- add tests for aggregate readiness and same-type replacement behavior

### Why

Once all three frame types were decoding live, the next practical step was to make the stream useful as a single state object. That reduces the gap between “the strip decodes” and “tools can consume live telemetry.”

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 8 50 --backend screen
```

```powershell
dotnet build .\DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj
```

### Result

- tests passed: `14/14`
- inspector build succeeded
- live sample stayed `Accepted` at `origin 0,0`, `pitch 2.8`, `scale 0.35`
- CLI `live` now prints an aggregate summary with newest `CoreStatus`, `PlayerVitals`, and `PlayerPosition`
- the rolling JSON snapshot was written successfully to:
  - `%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out\chromalink-live-telemetry.json`
- the JSON snapshot included merged aggregate state, frame counts, last detection, last frame metadata, and reserved build flags

### Decision

Keep.

### Saved Checkpoint

- pending commit for reader-side telemetry aggregate

---

## 2026-04-02 - Session S - parallelized bridge assembly

### Goal

Use parallel work lanes to harden the bridge workflow without destabilizing the proven strip baseline.

### Change

- split the work into owned lanes for:
  - CLI bridge contract
  - inspector live snapshot support
  - addon build and rotation status commands
- harden the live telemetry JSON with:
  - `contract.name = chromalink-live-telemetry`
  - `contract.schemaVersion = 1`
  - stable `profile` metadata
  - stable `transport` metadata
- make the inspector watch the rolling live snapshot and show aggregate state alongside capture analysis
- add new read-only addon commands:
  - `/cl build`
  - `/cl version`
  - `/cl caps`
  - `/cl rotation`
  - `/cl rotate`

### Why

At this stage the strip itself was already working. The best next move was to make the bridge easier to consume and easier to debug without changing transport geometry or decode behavior.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
dotnet build .\DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 8 50 --backend screen
```

### Result

- tests passed: `14/14`
- inspector build succeeded
- live sample stayed `Accepted` at `origin 0,0`, `pitch 2.8`, `scale 0.35`
- CLI `live` now prints `TelemetryContract: chromalink-live-telemetry/v1`
- the rolling JSON snapshot includes contract, profile, transport, aggregate, metrics, last detection, and last frame sections
- the inspector now surfaces the live bridge snapshot details alongside artifact analysis
- addon-side read-only commands now expose build flags and rotation info without changing the strip itself

### Decision

Keep.

### Saved Checkpoint

- pending commit for parallelized bridge assembly

---

## 2026-04-02 - Session T - console telemetry consumer

### Goal

Add a minimal consumer on top of the live bridge so the latest merged telemetry state can be viewed without opening the inspector.

### Change

- add `scripts/Show-ChromaLinkTelemetry.ps1`
- add `scripts/Show-ChromaLinkTelemetry.cmd`
- support one-shot display and `-Watch` mode
- read the rolling bridge snapshot instead of touching the decode path directly

### Why

Once the bridge contract was stable, the most practical finishing touch was a lightweight consumer that proves the snapshot is useful to downstream tools.

### Verification

```powershell
.\scripts\Show-ChromaLinkTelemetry.cmd
```

### Result

- the helper script read the rolling live snapshot successfully
- one-shot output showed:
  - contract version
  - bridge readiness
  - aggregate `CoreStatus`
  - aggregate `PlayerVitals`
  - aggregate `PlayerPosition`
  - frame-type counts
- the script needed one resilience fix so `Clear-Host` would not fail in non-interactive shells

### Decision

Keep.

### Saved Checkpoint

- pending commit for console telemetry consumer

---

## 2026-04-02 - Session U - bridge freshness and live-first monitoring

### Goal

Make the live bridge easier to trust and easier to consume without changing the strip itself.

### Change

- add bridge freshness metadata to the rolling JSON snapshot
- add aggregate-level `healthy` and `stale` signals
- add per-frame age/freshness fields for `CoreStatus`, `PlayerVitals`, and `PlayerPosition`
- make the inspector more live-first with a dedicated `Live Bridge` panel and live-only fallback behavior
- add an automation-friendly readiness consumer:
  - `scripts/Test-ChromaLinkTelemetryReady.ps1`
  - `scripts/Test-ChromaLinkTelemetryReady.cmd`

### Why

Once the bridge contract existed, the next product step was making it easy for both people and tools to decide whether the bridge was currently usable.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
dotnet build .\DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 8 50 --backend screen
```

```powershell
.\scripts\Test-ChromaLinkTelemetryReady.cmd
```

### Result

- tests passed: `14/14`
- inspector build succeeded
- live sample stayed `Accepted` at `origin 0,0`, `pitch 2.8`, `scale 0.35`
- rolling snapshot now includes bridge freshness/readiness metadata
- inspector now functions as a clearer live-first bridge monitor
- readiness script reported:
  - `TelemetryReady=true`
  - `TelemetryFresh=true`
  - `TelemetryHasAnyFrame=true`

### Decision

Keep.

### Saved Checkpoint

- pending commit for bridge freshness and live-first monitoring

---

## 2026-04-03 - Session V - monitor launch ergonomics

### Goal

Make the new live monitor as easy to launch from the repo as the other ChromaLink tools.

### Change

- add `scripts/Open-ChromaLink-Monitor.cmd`
- update README workflow text so the monitor has a concrete one-command launch path

### Why

The monitor was already built and documented, but without a simple launcher it still felt less polished than the rest of the toolkit.

### Verification

```powershell
.\scripts\Open-ChromaLink-Monitor.cmd
```

### Result

- the monitor launch wrapper executed without error
- the monitor now has a repo-level entry point consistent with the other ChromaLink tools
- README workflow text now points to a concrete launch command instead of a generic app name

### Decision

Keep.

### Saved Checkpoint

- pending commit for monitor launch ergonomics

---

## 2026-04-04 - Session W - local HTTP bridge

### Goal

Expose the rolling live snapshot through a tiny local HTTP surface so other tools can integrate without parsing files directly.

### Change

- add a new `ChromaLink.HttpBridge` project
- serve the rolling snapshot over localhost
- expose endpoints for:
  - `/latest-snapshot`
  - `/snapshot`
  - `/health`
  - `/ready`
- wire the new project into the solution
- update the probe script to check `/latest-snapshot` as well as the older aliases
- document the local HTTP bridge and its launch/probe helpers in the README

### Why

The bridge snapshot was already the source of truth. An HTTP layer is the cleanest next integration point for other tools that do not want to tail files.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
dotnet build .\DesktopDotNet\ChromaLink.HttpBridge\ChromaLink.HttpBridge.csproj
```

```powershell
dotnet build .\DesktopDotNet\ChromaLink.sln
```

```powershell
$bridge = Start-Process -FilePath dotnet -ArgumentList @('run','--no-build','--project','.\\DesktopDotNet\\ChromaLink.HttpBridge\\ChromaLink.HttpBridge.csproj') -PassThru; try { Start-Sleep -Seconds 4; .\\scripts\\Probe-ChromaLinkHttpBridge.cmd } finally { Stop-Process -Id $bridge.Id -Force -ErrorAction SilentlyContinue }
```

### Result

- solution build succeeded
- HTTP bridge project build succeeded
- the bridge served `/latest-snapshot` and `/snapshot` successfully
- `/health` and `/ready` return structured readiness JSON and may legitimately return `503` when the snapshot is stale or incomplete
- the probe was adjusted so real HTTP error responses are reported accurately instead of being shown as fake misses
- added `Open-ChromaLink-LiveStack.cmd` as a one-command launch path for bridge plus monitor workflow

### Decision

Keep.

### Saved Checkpoint

- pending commit for local HTTP bridge

---

## 2026-04-03 - Session V - live monitor launch and discovery

### Goal

Make the live monitor easy to find and clearly position it relative to the inspector, bridge scripts, and readiness checks.

### Change

- add `ChromaLink.Monitor` to the docs as the primary live viewer for `chromalink-live-telemetry.json`
- document the launch command for the monitor
- clarify the division of labor:
  - monitor for live bridge viewing
  - inspector for BMP/artifact inspection
  - bridge scripts for keeping the snapshot fresh
  - readiness script for automation checks
- add the helper script discovery paths for:
  - `Watch-ChromaLinkTelemetry.cmd`
  - `Open-ChromaLinkTelemetryJson.cmd`
  - `Open-ChromaLinkTelemetryFolder.cmd`
  - `Test-ChromaLinkTelemetryReady.cmd`

### Why

Once the consumer and bridge health work landed, the docs needed to make the monitor the obvious first stop for live telemetry use.

### Verification

Pending local launch/build sanity check.

### Result

Pending.

### Decision

Keep.

### Saved Checkpoint

- pending commit for live monitor launch and discovery

---

## 2026-04-04 - Session W - local HTTP bridge docs

### Goal

Document the upcoming local HTTP bridge so it fits cleanly beside the rolling snapshot, live monitor, inspector, and readiness script.

### Change

- add a local HTTP bridge section to the README
- anchor the bridge to the `DesktopDotNet/ChromaLink.HttpBridge` project name
- reference the helper scripts:
  - `Open-ChromaLinkHttpBridge.cmd`
  - `Launch-ChromaLinkHttpBridge.cmd`
  - `Probe-ChromaLinkHttpBridge.cmd`
- describe the intended division of labor:
  - JSON snapshot as the source of truth
  - live monitor for human viewing
  - inspector for BMP/overlay analysis
  - readiness script for automation checks
  - HTTP bridge for local tool integration
- keep the HTTP wording intentionally flexible so it can absorb the eventual project and endpoint names

### Why

The docs needed to make room for the HTTP bridge layer while keeping the rolling snapshot contract as the source of truth.

### Verification

- confirmed the current docs tree and tool layout
- checked that the README and plan note describe the HTTP bridge as local-only and contract-backed

### Result

- the HTTP bridge now has a clear place in the product story
- the current tools remain the authoritative path for snapshots and readiness

### Decision

Keep.

### Saved Checkpoint

- pending commit for local HTTP bridge docs

---

## 2026-04-05 - Session X - bridge contract hardening

### Goal

Add automated safety rails around the rolling snapshot and local HTTP bridge so the newer consumer layers do not drift accidentally.

### Change

- add snapshot contract tests in `DesktopDotNet/ChromaLink.Tests`
- add testable HTTP bridge structure in `DesktopDotNet/ChromaLink.HttpBridge`
- add endpoint tests for:
  - `/latest-snapshot`
  - `/snapshot`
  - `/health`
  - `/ready`
- cover missing-snapshot and stale/not-ready behavior
- align docs so the HTTP bridge is clearly described as real and local-only

### Why

After adding the monitor, readiness scripts, and HTTP bridge, the highest-value next step was to stabilize the contracts before adding more product layers.

### Verification

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln
```

```powershell
dotnet build .\DesktopDotNet\ChromaLink.HttpBridge\ChromaLink.HttpBridge.csproj
```

```powershell
dotnet build .\DesktopDotNet\ChromaLink.Monitor\ChromaLink.Monitor.csproj
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- live 8 50 --backend screen
```

```powershell
.\scripts\Probe-ChromaLinkHttpBridge.cmd
```

### Result

- full solution tests passed: `20/20`
- HTTP bridge build succeeded after stopping the running bridge process that was holding its own executable open
- monitor build succeeded
- live telemetry capture still accepted and produced a ready aggregate
- bridge probe succeeded with:
  - `200` on `/latest-snapshot`
  - `200` on `/snapshot`
  - readiness endpoints returning structured HTTP status accurately
- the probe script now reports real HTTP `503` readiness responses instead of showing them as fake misses

### Decision

Keep.

### Saved Checkpoint

- pending commit for bridge contract hardening

---

## 2026-04-06 - Session Y - browser dashboard docs

### Goal

Document the browser dashboard as the browser-friendly companion to the local HTTP bridge.

### Change

- add a browser dashboard section to the README
- name the browser dashboard launch helpers:
  - `Open-ChromaLinkDashboard.cmd`
  - `Open-ChromaLink-DashboardStack.cmd`
- position it relative to:
  - HTTP bridge
  - live monitor
  - inspector
  - readiness script
- keep the wording intentionally product-facing and non-committal about exact route names

### Why

The HTTP bridge was already the stable source of truth, so the next docs step was to explain where a browser-based view fits in the toolchain.

### Verification

- checked the updated README wording for consistency with the current HTTP bridge and live monitor sections
- confirmed the docs still describe the inspector as the BMP/artifact tool and the readiness script as the automation gate

### Result

- the README now clearly distinguishes:
  - HTTP bridge
  - browser dashboard
  - live monitor
  - inspector
  - readiness script
- the browser dashboard now has explicit launch helpers documented in the wrapper list

### Decision

Keep.

### Saved Checkpoint

- pending commit for browser dashboard docs

---

## 2026-04-06 - Session Z - browser dashboard launch alignment

### Goal

Align the actual launcher scripts with the new browser dashboard workflow described in the docs.

### Change

- make `Open-ChromaLinkHttpBridge.cmd` open the bridge root by default instead of raw snapshot JSON
- add `Open-ChromaLinkDashboard.cmd`
- add `Open-ChromaLink-DashboardStack.cmd`

### Why

The dashboard pass introduced browser-oriented docs, so the launch scripts needed to match that product story instead of dropping users into raw JSON by default.

### Verification

```powershell
cmd /c .\scripts\Open-ChromaLinkDashboard.cmd
```

```powershell
cmd /c .\scripts\Open-ChromaLink-DashboardStack.cmd
```

```powershell
cmd /c .\scripts\Open-ChromaLinkHttpBridge.cmd
```

### Result

- the browser-oriented launchers all returned cleanly
- the generic HTTP bridge opener now lands on the bridge root instead of raw JSON
- the actual script set now matches the browser dashboard docs

### Decision

Keep.

### Saved Checkpoint

- pending commit for browser dashboard launch alignment

---

## 2026-04-07 - Session AA - lifecycle flow docs

### Goal

Document the current operational flow for ChromaLink: start, inspect, verify, and stop.

### Change

- add a concise lifecycle section to the README
- keep the start/inspect/verify/stop flow aligned with the actual tools that exist now
- preserve the distinction between:
  - rolling snapshot
  - live monitor
  - browser dashboard
  - HTTP bridge
  - readiness script

### Why

The project already had all the pieces; the missing part was a concise operational path that tells a user how the stack fits together end to end.

### Verification

- re-read the updated README section for ordering and tool names
- confirmed the docs still reflect the current launcher set

### Result

- the README now describes a practical lifecycle flow without inventing new behavior
- the browser dashboard, monitor, inspector, and bridge each have a clear role in that flow

### Decision

Keep.

### Saved Checkpoint

- pending commit for lifecycle flow docs

---

## 2026-04-07 - Session AB - lifecycle status and stop docs

### Goal

Keep the lifecycle docs aligned with the actual stack helpers now present in the worktree.

### Change

- update the README lifecycle flow to mention the stack-level status helper alongside the bridge probe and bridge-specific status command
- keep the start / inspect / verify / stop wording concise and consistent with the live monitor, browser dashboard, HTTP bridge, and readiness tooling

### Why

The lifecycle section was already in place, but the newer status and stop wrappers made it worth tightening the docs so the recommended flow names the actual commands a user can run.

### Verification

- reread the lifecycle section for start / inspect / verify / stop ordering
- confirmed the helper names match the scripts already in the worktree

### Result

- the README now points at the practical stack helpers instead of relying on generic stop language
- the lifecycle path stays aligned with the current bridge, dashboard, monitor, and readiness scripts

### Decision

Keep.

### Saved Checkpoint

- pending commit for lifecycle status and stop docs
