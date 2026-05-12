# ChromaLink Handoff - P360C geometry freshness fix

Created: 2026-05-12 15:53 EDT / 2026-05-12T19:53Z UTC
Repo: `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink`
Branch: `main`
Scope: provider-side runtime/operations fix; no code changes.

## TL;DR

ChromaLink freshness was restored by correcting the live RIFT window geometry
back to the `P360C` capture profile. The desktop watch loop and HTTP bridge were
already running, but the RIFT client had drifted to a large `1920x1009` client
area on a negative-X monitor. That made the optical capture/decode loop too slow
and left `playerPosition` and other rotated frames stale.

The fix was to resize the live RIFT client to the expected `640x360` client area
with the existing ChromaLink `prepare-window` path. After resize, ChromaLink
health and RiftReader consumer capture both passed.

## Root cause

| Finding | Evidence |
|---|---|
| Watch loop was active | `ChromaLink.Cli.exe watch --backend screen` was running |
| HTTP bridge was reachable | `/api/v1/riftreader/world-state` returned HTTP `200` |
| Provider was stale before fix | `/health` and RiftReader capture reported `healthy=false`, `fresh=false`, `stale=true` |
| RIFT geometry was wrong | Window `1936x1048`; client `1920x1009`; left edge on negative-X monitor |
| ChromaLink expected geometry | `P360C` profile: client `640x360`, strip `640x24`, `80` segments |

The stale state was therefore not a schema bug, code regression, or missing HTTP
bridge. It was an operational geometry drift: ChromaLink was trying to decode the
strip from a window that did not match its proven capture profile.

## What was done

| Step | Result |
|---|---|
| Confirmed repo state | ChromaLink `main...origin/main`, clean |
| Read latest provider handoff | `notes/handoff-2026-05-07-195850-chromalink-push-ready-freshness.md` |
| Checked current telemetry | `aggregate.healthy=false`, `stale=true`; `playerPosition.fresh=false` |
| Checked current RIFT geometry | Window `1936x1048`; client `1920x1009`; location `Left=-1928`, `Top=349` |
| Ran geometry fix | `Run-ChromaLink.ps1 -Mode prepare-window -Argument1 32 -Argument2 32` |
| Verified new geometry | Window `656x399`; client `640x360`; client origin `40,63` |
| Waited for watch loop refresh | Aggregate health turned green across repeated samples |
| Verified HTTP health | `/health`: `healthy=true`, `ready=true`, `fresh=true`, `stale=false` |
| Verified RiftReader consumer path | RiftReader capture against `/api/v1/riftreader/world-state` passed |

## Command used

```powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass `
  -File "C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Run-ChromaLink.ps1" `
  -Mode prepare-window `
  -Argument1 32 `
  -Argument2 32
```

Observed output:

```text
RequestedClient: 640x360
BeforeWindow: -1928,349 1936x1048
BeforeClient: -1920,380 1920x1009
AfterWindow: 32,32 656x399
AfterClient: 40,63 640x360
Success: true
Reason: Window client area matches the requested profile.
```

## Validation

| Validation | Result |
|---|---|
| ChromaLink `/health` | `ok=true`, `healthy=true`, `ready=true`, `fresh=true`, `stale=false` |
| ChromaLink world-state | `playerPositionAvailable=true`; `player.position.fresh=true`; `player.position.stale=false` |
| RiftReader capture | `status=pass`; `fresh=true`; `exported=true`; `freshSamplesWritten=5` |
| SavedVariables live truth | Not used |
| Movement/input | Not sent |
| Cheat Engine / debugger | Not used |

RiftReader proof artifact:

```text
C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-live-coords-20260512-155349\chromalink-live-coords-capture-summary.json
```

Key result:

```text
status=pass
fresh=true
exported=true
samplesWritten=5
freshSamplesWritten=5
```

## Time taken

| Measurement | Duration |
|---|---:|
| From first stale ChromaLink diagnostic sample to final passing RiftReader capture | About 3 minutes 53 seconds |
| From geometry resize action to passing consumer proof | About 1 minute 22 seconds |
| Whole focused ChromaLink-fix pass, including context/guardrail checks | About 7-8 minutes |

## Safety / non-goals

| Item | Status |
|---|---|
| Code changes | None |
| Schema changes | None |
| ChromaLink provider API changes | None |
| RiftReader consumer code changes | None |
| Movement/navigation proof | Not attempted |
| Coordinate memory proof promotion | Not attempted |

This fix restores ChromaLink as a fresh API/live coordinate truth surface for
RiftReader candidate scoring. It does **not** authorize movement, navigation,
actor-facing, auto-turn, or memory-anchor promotion by itself.

## Resume guidance

If ChromaLink world-state goes stale again:

1. first check RIFT client geometry;
2. rerun the `prepare-window` command above if the client is not `640x360`;
3. confirm `watch --backend screen` is running;
4. verify `/health` is `healthy=true`, `fresh=true`, `stale=false`;
5. only then rerun the RiftReader consumer capture.

## Top 10 recommended next actions

| # | Action | Why |
|---:|---|---|
| 1 | Keep RIFT at `640x360` while using ChromaLink | This is the proven `P360C` capture geometry |
| 2 | Treat geometry drift as the first stale-world-state suspect | It was the actual cause in this incident |
| 3 | Keep `watch --backend screen` running | The HTTP bridge serves snapshots; the watch loop refreshes them |
| 4 | Use `/health` as the quick provider check | It exposes `healthy`, `fresh`, and `stale` directly |
| 5 | Use RiftReader capture as the consumer proof | It verifies the actual `/api/v1/riftreader/world-state` path |
| 6 | Do not use `/reloadui` as an ongoing freshness mechanism | Reloads do not maintain desktop telemetry freshness |
| 7 | Add a geometry preflight later | It would catch this failure before stale captures |
| 8 | Consider a one-command prepare/watch/bridge launcher | Reduces operator setup drift |
| 9 | Keep ChromaLink freshness separate from movement proof | Fresh API coords are not movement authorization |
| 10 | Resume coordinate-family work only after freshness remains green | Provider truth is useful only while current and fresh |
