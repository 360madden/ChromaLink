# ChromaLink Handoff - push-ready freshness fix

Created: 2026-05-07 19:58:50 -04:00 local / 2026-05-07T23:58:50Z UTC
Repo: `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink`
Branch: `main`
HEAD at handoff creation: `c06ea44 Harden ChromaLink live telemetry freshness`
Origin/main at handoff creation: `ad699cd`
Git status before writing this handoff:

```text
## main...origin/main [ahead 1]
```

## TL;DR

ChromaLink `main` has one local commit ready to push:

```text
c06ea44 Harden ChromaLink live telemetry freshness
```

That commit hardens the live telemetry provider path for RiftReader-style
consumers by keeping the standard stack on a continuous `watch --backend screen`
loop and widening aggregate telemetry freshness to `5000ms`.

This handoff was created immediately before pushing that commit to `origin/main`.

## Current truth

| Area | Current state |
|---|---|
| Branch | `main` |
| Local vs remote before handoff | `main` ahead of `origin/main` by 1 commit |
| Push target | `origin/main` |
| Provider commit awaiting push | `c06ea44 Harden ChromaLink live telemetry freshness` |
| Prior pushed commit | `ad699cd Document RiftReader coordination guardrails` |
| Worktree before this handoff | Clean except branch ahead state |

## Provider change in the pending commit

| File / area | Change |
|---|---|
| `DesktopDotNet/ChromaLink.Cli/TelemetrySnapshotWriter.cs` | Aggregate freshness window changed from `2000ms` to `5000ms`. |
| `DesktopDotNet/ChromaLink.Tests/SnapshotContractTests.cs` | Snapshot contract expectations updated for `5000ms` freshness. |
| `scripts/Run-ChromaLink.ps1` | Added `-Backend` forwarding for `capture-dump`, `live`, and `watch` modes. |
| `scripts/Bridge-ChromaLink.cmd` | Standard bridge launcher now runs `Run-ChromaLink.cmd -Mode watch -Backend screen`. |
| `scripts/Package-ChromaLinkDesktop.ps1` | Packaged launchers now start `watch --backend screen`. |
| `README.md` | Documents `ScreenBitBlt` standard stack behavior and `5000ms` aggregate freshness. |
| `notes/handoff-2026-05-01-131134-screen-backend-stack-freshness.md` | Earlier detailed handoff for screen backend/freshness work. |
| `notes/handoff-2026-05-01-140503-chromalink-provider-freshness-riftreader-proof.md` | Detailed provider-fix and RiftReader consumer proof handoff. |

## Validation already recorded in the provider-fix handoff

The detailed prior handoff records these passing validations for the provider fix:

| Validation | Recorded result |
|---|---|
| PowerShell parser checks | Passed for `Run-ChromaLink.ps1` and `Package-ChromaLinkDesktop.ps1` |
| `git diff --check` | Passed; line-ending warnings only |
| `pwsh -File .\scripts\Run-ChromaLink.ps1 -Mode smoke` | Passed |
| `dotnet test .\DesktopDotNet\ChromaLink.sln -v minimal` | Passed, `40/40` |
| `dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- validate` | Passed: smoke, replay, bench |
| Standard stack launch/status/readiness | Passed during proof |
| HTTP `/health` after final provider fix | Passed: healthy/ready/fresh |
| RiftReader capture against `/api/v1/riftreader/world-state` | Passed: `status=pass`, `fresh=true`, `exported=true` |
| Stack stop / leftover process check | Passed |

## What is not being revalidated in this handoff-only commit

| Not revalidated now | Reason |
|---|---|
| Full `dotnet test` | No code changed after the already-validated provider-fix commit; this handoff is docs-only. |
| Live RIFT / RiftReader capture | No new runtime behavior changed after the recorded provider proof. |
| Package generation | Still not rerun; prior handoff already notes generator content was patched but package publish was not run. |

## Push plan

1. Commit this handoff file.
2. Push `main` to `origin/main`.
3. Verify local `main` is aligned with `origin/main` after push.

## Resume prompt

```text
Resume in C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink on main.
Read newest handoff first: C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink\notes\handoff-2026-05-07-195850-chromalink-push-ready-freshness.md
The provider freshness fix is commit c06ea44 (Harden ChromaLink live telemetry freshness). It should be pushed to origin/main together with this handoff. The key operational truth is that the standard stack should run watch --backend screen and aggregate freshness is 5000ms. Prior validation and RiftReader consumer proof are recorded in notes/handoff-2026-05-01-140503-chromalink-provider-freshness-riftreader-proof.md.
```
