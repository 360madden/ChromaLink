# ChromaLink live telemetry reliability plan

Created: 2026-05-21
Repo: `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink`
Scope: provider-side reliability plan, with separate RiftReader consumer validation.
Status: planning artifact; not a live validation result.

## TL;DR

ChromaLink is the provider-owned live telemetry transport for RiftReader's API-now coordinate truth when the desktop stack is running, the RIFT window matches the proven `P360C` profile, the optical strip decodes continuously, and `/api/v1/riftreader/world-state` reports fresh player position.

The durable fix is **not** merely to restart the bridge. The durable fix is to make ChromaLink operational freshness observable, recoverable, and fail-closed from a single provider-owned workflow, then prove the published HTTP surface from RiftReader without silently crossing repo ownership boundaries.

Final target:

> One operator command can verify or restore ChromaLink fresh world-state, prove the provider state with durable artifacts, and fail closed with exact blocker reasons when it cannot.

## Current known truth and constraints

| Area | Current/known truth |
|---|---|
| Provider owner | ChromaLink owns addon transport, desktop reader, HTTP bridge, schema, typed client, and external contract. |
| Consumer owner | RiftReader consumes only published ChromaLink surfaces unless a ChromaLink edit pass is explicitly authorized. |
| Published RiftReader endpoint | `http://127.0.0.1:7337/api/v1/riftreader/world-state` |
| Schema endpoint | `http://127.0.0.1:7337/api/v1/riftreader/world-state/schema` |
| Health endpoints | `/health`, `/ready`, `/latest-snapshot`, `/snapshot` |
| Proven capture profile | `P360C` |
| Proven RIFT client area | `640x360` |
| Strip facts | `640x24`, `stripCount = 2` |
| Proven live refresh path | `watch --backend screen` plus HTTP bridge |
| Known prior stale root cause | RIFT geometry drifted to a large `1920x1009` client area; restoring `640x360` made provider and RiftReader consumer fresh again. |
| ChromaLink does not provide | Heading, facing, yaw, route planning, or movement control authority. |
| SavedVariables rule | `ReaderBridgeExport.lua` and other SavedVariables are not live truth. They are post-save snapshots only. |

## Non-negotiable success criteria

ChromaLink is provider-fresh only when all required rows pass:

| Gate | Required result |
|---|---|
| HTTP bridge | Listening and responding on configured base URL. |
| `/health` | `healthy=true`, `ready=true`, `fresh=true`, `stale=false`. |
| World-state endpoint | HTTP success with expected contract. |
| Root world-state | `ok=true`, `ready=true`, `fresh=true`, `stale=false`. |
| Navigation availability | `navigation.playerPositionAvailable=true`. |
| Player position | `player.position` exists with numeric `x`, `y`, `z`. |
| Player position freshness | `player.position.fresh=true`, `player.position.stale=false`. |
| Snapshot age | Inside the configured freshness window. |
| Player position age | Inside the configured freshness window. |
| RIFT geometry | Is at least `640x360`; `P360C` is the known-good fallback, while larger 16:9 geometries are accepted only when provider freshness and `player.position.fresh=true` both pass. |
| Watch loop | Active and writing fresh rolling snapshots. |
| Artifacts | Provider status JSON/Markdown written for diagnosis. |

## Safety boundaries

| Boundary | Rule |
|---|---|
| Movement | This plan does not authorize movement, navigation, auto-turn, or gameplay input. |
| Debuggers | No Cheat Engine, x64dbg, breakpoints, watchpoints, or debugger attach are part of this fix. |
| Provider/consumer split | Provider changes happen here in ChromaLink; RiftReader changes happen separately and consume only published surfaces. |
| Git | Stage explicit paths only; do not use `git add .`. |
| Runtime actions | Starting ChromaLink desktop stack, resizing the RIFT window to `640x360`, or resizing to a larger explicit test geometry are operational fixes/proofs, not movement proof. |
| Claims | Endpoint reachability is not enough; player-position freshness is required before RiftReader can use ChromaLink as API-now truth. |

---

# Phase 0 - Scope lock and baseline

## Milestone 0.1 - Declare provider-fix lane

| Step | Action | Exit criteria |
|---:|---|---|
| 0.1.1 | Explicitly switch to ChromaLink provider-fix lane. | No silent RiftReader-provider mutation ambiguity. |
| 0.1.2 | Read `AGENTS.md` and newest `notes/handoff-*chromalink*.md`. | Current repo policy and latest operational truth are active. |
| 0.1.3 | Record current Git state. | Branch and dirty state known before changes. |
| 0.1.4 | Preserve unrelated RiftReader changes. | No cross-repo staging contamination. |

## Milestone 0.2 - Allowed operations matrix

| Operation | Allowed by default? | Notes |
|---|---:|---|
| Read ChromaLink repo | Yes | Required. |
| Read RiftReader consumer scripts/docs | Yes | Required for contract validation. |
| Start ChromaLink bridge/watch | Yes, during provider recovery | Non-destructive local processes. |
| Prepare/resize RIFT window to `640x360` | Yes, when fixing provider freshness | No movement/gameplay input. |
| Send movement or gameplay input | No | Out of scope. |
| Use CE/x64dbg | No | Out of scope. |
| Edit ChromaLink provider helper/docs/tests | Yes | This is the provider repo. |
| Edit RiftReader consumer code | Separate phase only | Keep commits separate. |
| Commit/push | Only after validation and explicit user authorization | Use explicit path staging. |

---

# Phase 1 - Diagnose current provider state

## Milestone 1.1 - Classify the live blocker

The first run should be read-only and should classify the failure before any code changes.

| Step | Check | Blocker classes |
|---:|---|---|
| 1.1.1 | Git status and branch alignment. | `repo-dirty`, `branch-diverged`. |
| 1.1.2 | Port/process owner for `7337`. | `bridge-down`, `bridge-running`. |
| 1.1.3 | Probe `/health`, `/ready`, `/latest-snapshot`, `/api/v1/riftreader/world-state`. | `http-down`, `http-unhealthy`, `world-state-unavailable`. |
| 1.1.4 | Inspect rolling snapshot JSON. | `snapshot-missing`, `snapshot-stale`, `snapshot-malformed`. |
| 1.1.5 | Check watch/CLI process. | `watch-loop-down`. |
| 1.1.6 | Check RIFT process/window/client geometry. | `rift-down`, `wrong-window`, `rift-geometry-drift`. |
| 1.1.7 | Validate player-position frame fields. | `player-position-missing`, `player-position-stale`. |

## Milestone 1.2 - Required diagnostic artifact

Write a durable artifact under:

```text
artifacts/diagnostics/chromalink-provider-status-<timestamp>/summary.json
artifacts/diagnostics/chromalink-provider-status-<timestamp>/summary.md
```

Minimum JSON contract:

```json
{
  "status": "passed | blocked | failed",
  "blockers": [],
  "warnings": [],
  "bridge": {
    "baseUrl": "http://127.0.0.1:7337/",
    "portListening": false,
    "health": null,
    "ready": null,
    "worldState": null
  },
  "snapshot": {
    "path": "",
    "exists": false,
    "lastWriteUtc": null,
    "ageMs": null,
    "fresh": false,
    "stale": true
  },
  "riftWindow": {
    "processFound": false,
    "pid": null,
    "hwnd": null,
    "clientWidth": null,
    "clientHeight": null,
    "matchesP360C": false
  },
  "safety": {
    "movementSent": false,
    "gameplayInputSent": false,
    "cheatEngineUsed": false,
    "debuggerAttached": false
  },
  "recommendation": ""
}
```

---

# Phase 2 - Recover with existing tools first

## Milestone 2.1 - Start or verify provider stack

| Step | Action | Pass criteria |
|---:|---|---|
| 2.1.1 | Run existing stack launcher if bridge/watch is down. | Bridge responds. |
| 2.1.2 | Confirm `watch --backend screen` is active. | Snapshot writer updates. |
| 2.1.3 | Probe `/health`. | Structured health returned. |
| 2.1.4 | Probe `/ready`. | Readiness visible. |

Existing launcher:

```text
scripts/Start-ChromaLinkStack.cmd
```

Known provider watch path:

```text
scripts/Run-ChromaLink.cmd -Mode watch -Backend screen
```

## Milestone 2.2 - Repair geometry when needed

| Step | Action | Pass criteria |
|---:|---|---|
| 2.2.1 | Check RIFT client area. | Exact client size, aspect ratio, and minimum/fallback classification known. |
| 2.2.2 | If below `640x360`, unsupported aspect, or freshness-blocked, run prepare-window. | Client becomes known-good `640x360`. |
| 2.2.3 | Wait for watch loop refresh. | Rolling snapshot updates. |
| 2.2.4 | Reprobe `/health`. | `healthy=true`, `fresh=true`, `stale=false`. |

Known geometry repair command:

```powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass `
  -File "C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\scripts\Run-ChromaLink.ps1" `
  -Mode prepare-window `
  -Argument1 32 `
  -Argument2 32
```

## Milestone 2.3 - Provider freshness proof

| Check | Required result |
|---|---|
| `/health` | `healthy=true`, `ready=true`, `fresh=true`, `stale=false` |
| `/api/v1/riftreader/world-state` | HTTP success |
| Root world-state | `ok=true`, `ready=true`, `fresh=true`, `stale=false` |
| `navigation.playerPositionAvailable` | `true` |
| `player.position.fresh` | `true` |
| `player.position.stale` | `false` |
| `player.position.ageMs` | Within freshness window |
| Snapshot age | Within freshness window |

---

# Phase 3 - Implement durable provider ensure/status workflow

## Milestone 3.1 - Add one provider-owned workflow entrypoint

Preferred durable helper:

```text
scripts/ensure_chromalink_fresh.py
scripts/Ensure-ChromaLinkFresh.cmd
```

Rationale:

| Choice | Decision | Why |
|---|---:|---|
| Python orchestrator | Preferred | Best fit for structured JSON, subprocess timeouts, artifact writing, and fail-closed states. |
| Thin `.cmd` wrapper | Yes | Operator convenience only. |
| Large new PowerShell workflow | Avoid | Existing PowerShell scripts are useful leaves; complex orchestration should be more testable. |
| .NET CLI mode | Acceptable alternative | Use only if integration with existing reader code is clearly better. |

## Milestone 3.2 - Required modes

| Mode | Starts processes? | Resizes window? | Purpose |
|---|---:|---:|---|
| `--status` | No | No | Read-only diagnosis. |
| `--ensure-running` | Yes | No | Start bridge/watch if absent. |
| `--prepare-window` | Optional | Yes | Restore `640x360` geometry. |
| `--wait-fresh` | No | No | Poll until fresh or timeout. |
| `--self-test` | No | No | Validate fixtures/parsing/reporting. |
| `--json` | No | No | Machine-readable stdout. |

## Milestone 3.3 - Required blocker taxonomy

| Blocker | Meaning |
|---|---|
| `bridge-down` | Port/HTTP bridge not available. |
| `watch-loop-down` | Snapshot writer not running/updating. |
| `snapshot-missing` | Rolling JSON does not exist. |
| `snapshot-stale` | Snapshot exists but is older than freshness window. |
| `snapshot-malformed` | Snapshot JSON cannot be parsed or lacks required structure. |
| `world-state-unavailable` | HTTP bridge up but world-state route fails. |
| `world-state-stale` | Root world-state reports stale or not fresh. |
| `player-position-missing` | `player.position` absent or incomplete. |
| `player-position-stale` | Player-position frame is stale or too old. |
| `rift-process-missing` | RIFT process/window is absent. |
| `rift-geometry-drift` | Client area does not match `640x360`. |
| `strip-not-detected` | Decoder cannot locate valid strip. |
| `decode-unhealthy` | Frames decode but aggregate health is bad. |
| `contract-mismatch` | Contract/schema unexpectedly changed. |
| `timeout-waiting-fresh` | Recovery attempted but never reached freshness. |

## Milestone 3.4 - Required run-summary contract

Every non-trivial run writes:

```text
artifacts/diagnostics/chromalink-ensure-fresh-<timestamp>/summary.json
artifacts/diagnostics/chromalink-ensure-fresh-<timestamp>/summary.md
```

Minimum fields:

```yaml
status: "passed | blocked | failed"
blockers: []
warnings: []
bridge:
  baseUrl: ""
  healthStatusCode: null
  readyStatusCode: null
  worldStateStatusCode: null
snapshot:
  path: ""
  ageMs: null
  fresh: false
  stale: true
riftWindow:
  pid: null
  hwnd: null
  clientWidth: null
  clientHeight: null
  matchesP360C: false
actions:
  startedBridge: false
  startedWatch: false
  preparedWindow: false
  sentGameInput: false
  sentMovementInput: false
safety:
  cheatEngineUsed: false
  debuggerAttached: false
result:
  providerFresh: false
  playerPositionFresh: false
  playerPosition: null
next:
  recommendedAction: ""
```

---

# Phase 4 - Provider tests and validation

## Milestone 4.1 - Offline tests

| Step | Validation | Required result |
|---:|---|---|
| 4.1.1 | Helper syntax/compile check. | Pass. |
| 4.1.2 | Fixture: bridge down. | `bridge-down`. |
| 4.1.3 | Fixture: stale snapshot. | `snapshot-stale`. |
| 4.1.4 | Fixture: stale player position. | `player-position-stale`. |
| 4.1.5 | Fixture: fresh world-state. | `status=passed`. |
| 4.1.6 | Fixture: geometry drift. | `rift-geometry-drift`. |
| 4.1.7 | Existing ChromaLink solution tests. | Pass. |
| 4.1.8 | Existing ChromaLink CLI validate. | Pass. |
| 4.1.9 | `git diff --check`. | Pass. |

Known validation commands:

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln -v minimal
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- validate
git --no-pager diff --check
```

## Milestone 4.2 - Live provider validation

| Step | Runtime validation | Required result |
|---:|---|---|
| 4.2.1 | Run `Ensure-ChromaLinkFresh.cmd --status --json`. | Accurate current state. |
| 4.2.2 | Run `Ensure-ChromaLinkFresh.cmd --ensure-running --wait-fresh --json`. | Bridge/watch active or exact blocker. |
| 4.2.3 | If geometry drift, run with `--prepare-window`. | `640x360`. |
| 4.2.4 | Probe `/health`. | Fresh. |
| 4.2.5 | Probe world-state. | Fresh player position. |

---

# Phase 5 - RiftReader consumer proof

## Milestone 5.1 - Keep RiftReader read-only toward provider

RiftReader should report ChromaLink state and consume published surfaces only. It must not mutate ChromaLink provider files or runtime state unless explicitly authorized in a ChromaLink edit pass.

Consumer states should include:

| State | Meaning | Consumer behavior |
|---|---|---|
| `provider-fresh` | ChromaLink can serve as API-now coordinate truth. | Allow API-now comparison. |
| `provider-down` | HTTP refused/unreachable. | Block ChromaLink truth. |
| `provider-stale` | Endpoint/snapshot/player frame stale. | Block ChromaLink truth. |
| `position-missing` | No player position. | Block coordinate proof. |
| `contract-mismatch` | Schema wrong. | Block. |
| `provider-fresh-no-facing` | Position fresh but no heading/facing/yaw. | Use for coords only. |
| `savedvariables-not-used` | Confirms no SavedVariables live truth. | Required marker. |

## Milestone 5.2 - RiftReader proof bundle

A successful consumer proof should write a bundle under RiftReader, such as:

```text
C:\RIFT MODDING\RiftReader\scripts\captures\chromalink-live-coords-<timestamp>\
```

Required files:

| File | Purpose |
|---|---|
| `chromalink-http-bridge-readiness.json` | Endpoint reachability proof. |
| `chromalink-world-state-contract.json` | Schema/contract proof. |
| `chromalink-freshness-preflight.json` | Freshness proof. |
| `truth-surface.json` | Declares ChromaLink live telemetry as API-now. |
| `savedvariables-freshness.json` | Declares SavedVariables not used. |
| `live-coords.ndjson` | Fresh coordinate samples. |
| `chromalink-live-coords-capture-summary.json` | Final consumer result. |

---

# Phase 6 - Cross-repo acceptance gate

## Milestone 6.1 - Provider acceptance

ChromaLink provider slice is accepted only when:

| Gate | Required |
|---|---|
| Provider helper status | `passed`. |
| `/health` | `healthy=true`, `ready=true`, `fresh=true`, `stale=false`. |
| `/api/v1/riftreader/world-state` | HTTP success. |
| Root world-state | `ok=true`, `fresh=true`, `stale=false`. |
| Player position | Present and fresh. |
| Snapshot age | Inside freshness window. |
| Geometry | `640x360` known-good fallback, or larger geometry accepted only by fresh `/health` and fresh `player.position`; otherwise explicit blocker. |
| Tests | ChromaLink tests pass. |
| Handoff | ChromaLink handoff written. |

## Milestone 6.2 - Consumer acceptance

RiftReader consumer slice is accepted only when:

| Gate | Required |
|---|---|
| Consumer preflight | `passed`. |
| Fresh ChromaLink capture bundle | Written. |
| SavedVariables | Explicitly not used as live truth. |
| Movement | Not authorized by ChromaLink alone. |
| RiftReader tests | Relevant tests pass if code changed. |
| Handoff/current-truth | Updated only with validated evidence. |

---

# Phase 7 - Documentation, handoff, and persistence

## Milestone 7.1 - ChromaLink handoff

After implementation or live restoration, write:

```text
notes/handoff-<timestamp>-chromalink-live-stack-reliability.md
```

Required sections:

| Section | Required content |
|---|---|
| TL;DR | Exact result. |
| Root cause | Bridge-down, watch-loop-down, geometry drift, decode stale, or contract issue. |
| Commands | Exact commands used. |
| Artifacts | Provider and RiftReader proof paths. |
| Safety | No movement, no CE, no debugger. |
| Consumer contract | Endpoints and freshness fields. |
| Resume sequence | Step-by-step. |
| Top 10 next actions | Ranked and specific. |

## Milestone 7.2 - Commit strategy

Use separate commits:

| Commit | Repo | Scope |
|---|---|---|
| Provider plan | ChromaLink | This plan document. |
| Provider implementation | ChromaLink | Ensure/status helper, tests, docs. |
| Provider handoff | ChromaLink | Runtime proof handoff after implementation. |
| Consumer integration | RiftReader | Optional read-only preflight/docs. |

Never stage unrelated generated artifacts or consumer changes into provider commits.

---

# Weakness review and corrections

| Weakness in naive plan | Risk | Corrected plan response |
|---|---|---|
| Start coding immediately. | Could solve the wrong failure if the current blocker is simply bridge/watch/geometry down. | Phase 1 diagnosis and Phase 2 existing-tool recovery come before implementation. |
| Treat endpoint reachability as success. | Could serve stale coordinates. | Require root freshness, player-position freshness, age window, and navigation availability. |
| Ignore geometry. | Repeats the known `1920x1009` drift failure. | Geometry is a first-class blocker and repair path. |
| Mix ChromaLink and RiftReader edits. | Cross-repo contamination and unclear ownership. | Provider and consumer phases/commits are separate. |
| Use SavedVariables as fallback. | Stale movement truth. | SavedVariables are explicitly excluded from live truth. |
| Keep orchestration in complex PowerShell. | Brittle workflow, weak testing. | Prefer Python or .NET helper, `.cmd` wrapper only. |
| Claim ChromaLink enables navigation alone. | Overclaims; ChromaLink lacks facing/control. | ChromaLink provides world-state/coords only; RiftReader owns facing/control/proof gates. |
| Skip artifacts. | Future sessions cannot resume safely. | Every run writes JSON/Markdown summaries. |
| Skip tests. | Helper regressions undetected. | Fixtures plus existing solution tests are required. |
| Skip commit/push. | Plan or fix can be lost. | This plan is committed first; implementation later gets explicit commits. |

# 2026-05-21 geometry acceptance amendment

`640x360 / P360C` is the **minimum known-good fallback**, not the only
acceptable runtime geometry. A larger client area should not invalidate a
ChromaLink-dependent test by itself. The decisive gate is provider freshness:

| Observed geometry | Provider freshness | Classification |
|---|---|---|
| `640x360` | Fresh `/health` and fresh `player.position` | `known-good-p360c` |
| Larger 16:9, e.g. `1280x720` | Fresh `/health` and fresh `player.position` | `larger-16x9-fresh` |
| Larger 16:9 | Stale/missing provider or stale/missing player position | `larger-16x9-unproven` / provider blocker |
| Maximized/non-16:9, e.g. `1920x1009` | Stale/missing provider or stale/missing player position | `unsupported-aspect` / provider blocker |
| Any geometry below `640x360` | Any | `below-minimum-profile` |

Historical live-test failures where RIFT was maximized/fullscreen-windowed and
ChromaLink was stale should be reclassified as **setup-blocked / provider
geometry blocked**, not as movement/navigation/proof failures.

# Optimized execution order

| Order | Action | Why |
|---:|---|---|
| 1 | Read newest ChromaLink handoff and repo policy. | Prevents drift. |
| 2 | Run read-only provider status. | Classifies current blocker. |
| 3 | Start existing stack if down. | Fastest restoration. |
| 4 | Check/repair `640x360` geometry. | Known prior root cause. |
| 5 | Verify `/health` and world-state freshness. | Provider proof. |
| 6 | Run RiftReader consumer capture. | Consumer proof. |
| 7 | Implement ensure/status helper only after observing failure class. | Avoids speculative code. |
| 8 | Add fixtures/tests/docs. | Durable quality gate. |
| 9 | Write ChromaLink handoff. | Resume-safe. |
| 10 | Commit/push explicit coherent slices. | Prevents loss and supports rollback. |

# Top 10 recommended next actions

| # | Action | Why |
|---:|---|---|
| 1 | Implement `scripts/ensure_chromalink_fresh.py` with `--status`, `--ensure-running`, `--prepare-window`, and `--wait-fresh`. | Turns manual recovery into a repeatable provider workflow. |
| 2 | Add `scripts/Ensure-ChromaLinkFresh.cmd` as a thin launcher. | Gives the operator one stable entrypoint. |
| 3 | Add fixture tests for bridge-down, stale-snapshot, stale-position, geometry-drift, and fresh-world-state. | Prevents false green states. |
| 4 | Ensure every provider run writes JSON and Markdown summaries. | Makes handoffs and debugging resilient. |
| 5 | Reuse existing `Start-ChromaLinkStack.cmd` and `Run-ChromaLink.ps1` as leaf helpers. | Avoids rewriting proven launch paths. |
| 6 | Keep `watch --backend screen` as the preferred live writer path. | This is the proven freshness route. |
| 7 | Require `player.position.fresh=true` before RiftReader consumes ChromaLink as API-now truth. | Avoids stale coordinate proof. |
| 8 | Keep ChromaLink free of movement/control semantics. | Preserves provider scope. |
| 9 | Add or preserve a ChromaLink runtime handoff after live validation. | Keeps current proof recoverable. |
| 10 | Commit provider and consumer slices separately. | Prevents cross-repo rollback confusion. |
