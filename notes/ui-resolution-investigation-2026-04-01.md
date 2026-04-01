# UI Resolution Investigation (2026-04-01)

## Goal

Understand whether ChromaLink's on-screen strip is only "too small", or whether the RIFT UI system is also moving, rescaling, and truncating the strip as the client resolution changes.

## Method

Live client sweeps were run against the active addon and reader with these client sizes:

- `1600x900`
- `1280x720`
- `960x540`
- `800x450`
- `640x360`

For each size:

1. Resize the running RIFT window to the requested client area.
2. Run `capture-dump --backend desktopdup`.
3. Save the raw BMP, annotated BMP, and JSON sidecar under `%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out`.
4. Repeat the same sweep with `/reloadui` after each resize.

Saved artifact roots from this session:

- `%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out\resolution-sweep`
- `%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out\resolution-sweep-reload`

## Findings

### Sweep without reload

| Resolution | Accepted | Reason | Scale | Pitch | Left observed | Right observed |
| --- | --- | --- | ---: | ---: | --- | --- |
| `1600x900` | no | `Control marker mismatch.` | `0.34` | `2.72` | `5 6 0 3 0 3 0 1` | `0 0 0 0 0 0 0 0` |
| `1280x720` | no | `Control marker mismatch.` | `0.32` | `2.56` | `2 1 0 7 7 0 7 2` | `0 0 0 0 0 0 0 0` |
| `960x540` | no | `Pitch mismatch.` | `-` | `-` | `-` | `-` |
| `800x450` | no | `Pitch mismatch.` | `-` | `-` | `-` | `-` |
| `640x360` | no | `Pitch mismatch.` | `-` | `-` | `-` | `-` |

### Sweep with reload after each resize

| Resolution | Accepted | Reason | Scale | Pitch | Left observed | Right observed |
| --- | --- | --- | ---: | ---: | --- | --- |
| `1600x900` | no | `Control marker mismatch.` | `0.34` | `2.72` | `6 4 0 7 3 0 4 0` | `0 0 0 0 0 0 0 0` |
| `1280x720` | no | `Control marker mismatch.` | `0.32` | `2.56` | `2 6 0 3 7 0 7 2` | `0 0 0 0 0 0 0 0` |
| `960x540` | no | `Pitch mismatch.` | `-` | `-` | `-` | `-` |
| `800x450` | no | `Pitch mismatch.` | `-` | `-` | `-` | `-` |
| `640x360` | no | `Pitch mismatch.` | `-` | `-` | `-` | `-` |

## Interpretation

- The strip is not just "small." Its detected geometry changes with client size.
- At `1600x900` and `1280x720`, the reader still finds a candidate, but the right control markers collapse to all black (`0 0 0 0 0 0 0 0`).
- That strongly suggests truncation or clipping of the strip width, not just uniform scaling.
- At `960x540` and below, the reader cannot even lock a candidate profile and fails with `Pitch mismatch.`
- Running `/reloadui` after each resize does not restore the expected strip geometry.
- The addon therefore appears to be participating in RIFT's normal UI layout/scaling/clipping system rather than behaving like a fixed pixel overlay.

## External Support For The Hypothesis

Gamigo's RIFT support article on UI customization states that **Global UI Scale (Percent)** changes the size of UI elements globally, which is consistent with addon frames being affected by client UI scaling and layout rules:

- [RIFT - Customizing the User Interface](https://support.gamigo.com/en/support/solutions/articles/201000105477-customizing-the-user-interface)

## What This Means For ChromaLink

The current `P360C` assumptions are not resolution-invariant in live play. A real fix likely requires one or more of:

- discovering a frame/context strategy that escapes normal UI scaling or clipping
- adding separate calibrated live profiles for specific client sizes
- redesigning the strip so it survives truncation and reduced pitch
- investigating whether specific native UI anchors or strata are colliding with the top-left overlay space

## Follow-up Experiments After The Initial Sweep

Additional live experiments were run after the first sweep to separate reader problems from render/layout problems.

### 1. Fixed-size root anchored to `UIParent`

The addon was changed from the earlier root-stretching layout to a fixed-size root/frame strategy closer to the older `SignalStrip` approach:

- keep the strip geometry at `640x24`
- anchor the strip/root explicitly instead of stretching it with root scale
- keep the reader search window widened horizontally for live captures larger than the profile width

Result:

- `640x360` recovered to a clean accepted decode again:
  - `scale 0.35`
  - `pitch 2.8`
  - both control markers exact
- `1600x900` stopped collapsing to the tiny `0.32` candidate from the first sweep and began producing near-full-size candidates around:
  - `scale 0.99`
  - `pitch 7.92`

This is meaningful progress: the layout strategy matters, and `640x360` can still be made reliable without changing global UI scale.

### 2. Horizontal offset tuning on the fixed-size layout

The original idea was to move the strip off the crowded top-left HUD zone at wide resolutions.

Tested offsets included:

- `0`
- `16`
- `48`
- `96`
- `220`

Observed behavior:

- `0` is the only tested offset that preserved a clean accepted `640x360` decode.
- any positive horizontal offset produced exact left control markers at `1600x900`, but the right tail collapsed:
  - right control markers became `0 0 0 0 0 0 0 0`
  - parse failed with `Invalid magic/version.`
- large positive offsets therefore help the left edge but appear to push the tail into a bad render/clipping state.

### 3. Native top-level anchor test

The fixed-size strip was also tested against a resolved native top-level anchor instead of `UIParent`, while still avoiding the unsafe `SetParent(...)` path that previously caused runtime errors.

Result:

- it did not fix the missing tail at `1600x900`
- it regressed `640x360`

So this did not outperform the simpler `UIParent` checkpoint.

### 4. Segment parent test

To test whether the `band` frame itself was clipping the tail, segment frames were temporarily parented directly to the root instead of to the `band`.

Result:

- no measurable improvement
- the right-side failure remained

That suggests the remaining high-resolution issue is not just a simple child-of-band clipping artifact.

## Current Best Checkpoint From This Session

The best checkpoint preserved in the working tree at the end of this session is:

- fixed-size `UIParent`-anchored strip layout
- `stripOffsetX = 0`
- widened horizontal reader search window for wide live captures
- layout diagnostics and resolution sweep tooling still present

Current live behavior of that checkpoint:

| Resolution | Accepted | Reason | Scale | Pitch | Left observed | Right observed |
| --- | --- | --- | ---: | ---: | --- | --- |
| `1600x900` | no | `Control marker mismatch.` | `0.99` | `7.92` | `2 1 0 4 2 6 4 0` | `0 0 0 0 0 0 0 0` |
| `640x360` | yes | `Accepted` | `0.35` | `2.80` | `0 1 0 1 2 3 4 5` | `5 4 3 2 1 0 1 0` |

## Updated Interpretation

- The strip problem is not a single issue.
- `640x360` and `1600x900` fail for different reasons.
- At `640x360`, the fixed-size `UIParent` checkpoint can still decode successfully.
- At `1600x900`, the addon can be pushed into near-full-size geometry, but clearing the left side of the HUD without losing the right tail remains unresolved.
- The remaining `1600x900` problem now looks less like "the strip is always tiny" and more like a real render-space/anchor-space boundary problem affecting the tail when the strip is shifted.

## Helper / Quiet-Zone Experiments

After the layout investigation, a second pass explored the "helper addon" idea in a practical form.

### Native UI movement feasibility

The local audited API copy showed:

- `Native` supports inspection methods like `ReadAll`, `ReadPoint`, `GetBounds`
- `Native` supports `SetLayer` and `SetStrata`
- `Native` does **not** expose `SetPoint` / `SetParent` the way addon-created `Frame` objects do

That means a helper addon can likely reorder some native draw priority, but it does **not** appear to have a clean general-purpose way to permanently reposition arbitrary native RIFT UI elements.

So the practical helper direction became:

- create a reserved opaque "quiet zone" behind ChromaLink
- keep the strip above that zone
- use the quiet zone to visually clear the capture band without globally scaling or rewriting the whole RIFT UI

### Quiet-zone result

A full-width opaque black quiet zone across the capture band did help:

- it removed outside UI contamination from the top band
- it preserved the known-good `640x360` decode while active in some configurations
- at `1600x900`, it improved the left-side control quality compared with the same layout without the quiet zone

However it did **not** solve the whole problem:

- the right control tail at `1600x900` still collapsed toward black
- the strip itself still looked partially distorted or truncated before decode

### Slight horizontal compression result

The old `ChromaLink2` code path had `displayScaleX` / `displayScaleY`, so a similar experiment was tested in the current addon.

When the strip was rendered slightly narrower horizontally on wide clients:

- `1600x900` improved noticeably:
  - detection moved to `origin 0,0`
  - scale shifted to `0.90`
  - left control improved to `0 1 0 1 2 4 5 2`
  - right control improved from all-black to `1 0 0 0 0 0 0 0`
- but `640x360` regressed, producing an invalid parse

So slight horizontal compression is a promising **high-resolution-only** lever, but not yet a safe universal default.

## Current Status After Helper Experiments

- The stable baseline remains the previously committed fixed-anchor checkpoint.
- Quiet-zone and wide-client compression work are still useful findings, but they are not yet mature enough to leave enabled as defaults.
- The next serious choice is whether to:
  - keep prioritizing the `640x360` baseline and treat wide-resolution behavior as secondary
  - or build a dedicated helper/high-resolution mode that intentionally trades some geometry assumptions for cleaner wide-client rendering

## Repeat The Sweep

Run either command from the repo root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Sweep-RiftResolutions.ps1
```

```cmd
scripts\Sweep-RiftResolutions.cmd
```

To include `/reloadui` after every resize:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Sweep-RiftResolutions.ps1 -ReloadUi
```
