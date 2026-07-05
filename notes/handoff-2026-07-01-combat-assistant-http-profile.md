# ChromaLink combat-assistant HTTP profile handoff

Created: 2026-07-01 America/New_York
Repo: `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink`
Branch: `codex/combat-assistant-http-profile`
Scope: provider-side HTTP contract expansion for automation-oriented combat-assistant consumers.

## TL;DR

Added a separate facts-only combat-assistant consumer profile on the local HTTP bridge. This does not change the color-strip transport, frame rotation, RiftReader world-state contract, movement/control behavior, or action recommendation policy.

## New public endpoints

```text
GET /api/v1/consumers/combat-assistant/state
GET /api/v1/consumers/combat-assistant/state/schema
```

Contract:

```text
artifactKind = combat-assistant-state
contract.name = chromalink-combat-assistant-state
contract.schemaVersion = 1
sourceContract = chromalink-live-telemetry/v2
```

## Behavior

The endpoint groups already-decoded rolling aggregate facts for combat consumers:

- player: `coreStatus`, `vitals`, `resources`, `combat`, `cast`, `position`
- target: `coreStatus`, `vitals`, `resources`, `position`
- top-level optional sections: `abilityWatch`, `auraPage`, `combat`
- capabilities explicitly state `factsOnly=true`, no action suggestions, no control, and no movement

Readiness is intentionally profile-specific: the endpoint is ready when player combat-assistant facts are present. Optional target, ability-watch, aura, and merged-combat sections can be null or stale without breaking the contract.

## Changed areas

- HTTP bridge manifest/schema/state projection
- `ChromaLink.Client` typed fetch/schema helpers and response records
- Snapshot/client contract tests
- README, changelog, and project prompt documentation

## Validation

Passed:

```powershell
git diff --check
dotnet test DesktopDotNet\ChromaLink.Tests\ChromaLink.Tests.csproj --no-restore
```

Test result: 43 passed, 0 failed.

## Remaining notes

- Local git status still includes pre-existing geometry/freshness doc updates that were preserved during repo hygiene.
- Line-ending-only Lua/toc noise was restored; duplicate untracked addon-root Lua copies were hidden via local `.git/info/exclude` instead of deleted.
- Live provider freshness proof was not run in this slice; the implementation was contract/test validated against synthetic snapshots.

## Top 10 recommended next actions

| # | Action | Why |
|---:|---|---|
| 1 | Review the new combat-assistant payload shape against an actual combat-assistant consumer. | Confirms the facts grouping is useful before transport changes. |
| 2 | Run live provider preflight with `scripts\Ensure-ChromaLinkFresh.cmd --status --wait-fresh --json`. | Confirms the HTTP bridge can serve fresh state today. |
| 3 | Capture one live response from `/api/v1/consumers/combat-assistant/state`. | Creates provider-side evidence for the new contract. |
| 4 | Keep RiftReader world-state consumers on `/api/v1/riftreader/world-state`. | Avoids coupling combat-assistant needs into the navigation-adjacent contract. |
| 5 | Add consumer-side freshness gates before any bot decisions. | Prevents stale combat facts from driving automation. |
| 6 | Configure `abilityWatch.trackedAbilities` for the intended combat assistant. | Makes ability readiness facts actionable without adding rotation logic. |
| 7 | Decide whether aura pages need consumer-specific filtering. | Prevents overloading consumers with generic buff/debuff pages. |
| 8 | Measure live freshness of player/target/ability/aura sections during combat. | Determines whether transport rotation tuning is actually needed. |
| 9 | Defer color-strip frame changes until freshness evidence requires them. | Keeps v1 minimal and low risk. |
| 10 | Commit provider and consumer changes separately if RiftReader later consumes this profile. | Preserves provider/consumer boundaries. |
