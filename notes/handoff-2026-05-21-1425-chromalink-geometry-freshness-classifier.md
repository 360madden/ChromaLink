# ChromaLink geometry freshness classifier handoff

Created: 2026-05-21 14:25 America/New_York
Repo: `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink`
Scope: provider-side geometry/freshness classification and live-safe validation.

## TL;DR

Completed the 1-10 geometry/freshness action list. `640x360 / P360C` is now
treated as the **known-good fallback/minimum**, not the only acceptable geometry.
The new provider helper accepts larger 16:9 geometries only when `/health` and
`/api/v1/riftreader/world-state` prove fresh `player.position`.

## Changed files

| File | Why |
|---|---|
| `scripts/ensure_chromalink_fresh.py` | New Python-first provider status/ensure helper with geometry classifiers, freshness gates, JSON/Markdown artifacts, wait loop, 1280x720 resize proof support, maximize proof support, and self-test. |
| `scripts/Ensure-ChromaLinkFresh.cmd` | Thin launcher for the Python helper. |
| `scripts/Get-RiftInputReadiness.ps1` | Stops treating only exact `640x360` as capture-ready when exact size is not required; larger same-aspect clients now pass readiness if at least `640x360`. |
| `notes/plan-2026-05-21-chromalink-live-telemetry-reliability.md` | Amends the plan: freshness is the decisive gate; `640x360` is fallback/minimum. |

## Live-safe validation

| Test | Result | Artifact |
|---|---|---|
| Helper self-test | Passed | Console self-test: `status=passed`, `cases=8` |
| Baseline status at `640x360` | Passed: `known-good-p360c` | `artifacts\diagnostics\chromalink-ensure-fresh-20260521T182022Z\summary.json` |
| Explicit larger 16:9 `1280x720` | Passed: `larger-16x9-fresh` | `artifacts\diagnostics\chromalink-ensure-fresh-20260521T182351Z\summary.json` |
| Maximized window `1920x1009` | Blocked: `provider-stale`, `player-position-missing`, `unsupported-aspect` | `artifacts\diagnostics\chromalink-ensure-fresh-20260521T182151Z\summary.json` |
| Restore fallback `640x360` | Passed: `known-good-p360c` | `artifacts\diagnostics\chromalink-ensure-fresh-20260521T182426Z\summary.json` |

## Current truth

| Area | Current state |
|---|---|
| Current RIFT client | Restored to `640x360`. |
| Current provider state | Fresh at `640x360`. |
| `1280x720` state | Fresh and valid as `larger-16x9-fresh` during this session. |
| Maximized state | Blocked; observed client `1920x1009` is not 16:9 and ChromaLink did not keep player position fresh. |
| Fullscreen-windowed state | Not toggled; treat as unproven until tested separately. |
| Movement/gameplay input | Not sent. |
| Cheat Engine/debugger | Not used. |
| SavedVariables live truth | Not used. |

## Reclassification rule

Old or future live tests run while RIFT is maximized/fullscreen-windowed should
not be graded as navigation/proof failures unless ChromaLink first proves fresh
provider state. If ChromaLink is stale or `player.position` is stale/missing,
classify the run as:

```text
blocked: provider-stale / unsupported-profile / unsupported-aspect
```

## Resume sequence

```powershell
cd "C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink"
.\scripts\Ensure-ChromaLinkFresh.cmd --status --wait-fresh --json
.\scripts\Ensure-ChromaLinkFresh.cmd --resize-client 1280x720 --wait-fresh --json
.\scripts\Ensure-ChromaLinkFresh.cmd --prepare-window --wait-fresh --json
```

## Top 10 recommended next actions

| # | Action | Why |
|---:|---|---|
| 1 | Keep `Ensure-ChromaLinkFresh.cmd --status --wait-fresh --json` as the first provider check. | One command classifies geometry and freshness. |
| 2 | Use `larger-16x9-fresh` as an accepted provider state. | Prevents false failure at valid larger 16:9 sizes. |
| 3 | Keep `known-good-p360c` as the fallback recovery state. | Fast recovery when larger geometry blocks. |
| 4 | Treat maximized `1920x1009` as blocked until a supported aspect/profile exists. | It was not fresh in the live proof. |
| 5 | Test true fullscreen-windowed separately before reclassifying it. | Windows maximize is not identical to RIFT fullscreen-windowed. |
| 6 | Add a future `P720C`/larger-profile protocol only after more measurements. | Capacity improvements should be deliberate. |
| 7 | Feed the helper's JSON into RiftReader preflight. | Prevents RiftReader from consuming stale ChromaLink. |
| 8 | Keep movement blocked on provider blockers. | ChromaLink provides coords only, not facing/control proof. |
| 9 | Preserve diagnostic artifacts for every geometry proof. | Makes old failures auditable. |
| 10 | Commit provider and consumer slices separately if publishing. | Maintains clean provider/consumer boundaries. |
