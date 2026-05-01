# ChromaLink External Access Handoff

Date: 2026-05-01
Repo: `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink`
Branch: `main`
Latest commit: `55a915d Add RiftReader schema and typed client support`
Remote state at handoff: `main` aligned with `origin/main`
Worktree state before writing this handoff: clean

## TL;DR

ChromaLink now has a local, read-only, outside-program access lane for RiftReader-style consumers.

Primary external surfaces:

- Full diagnostic snapshot: `GET http://127.0.0.1:7337/latest-snapshot`
- API manifest: `GET http://127.0.0.1:7337/api/v1`
- Reduced RiftReader world state: `GET http://127.0.0.1:7337/api/v1/riftreader/world-state`
- JSON schema: `GET http://127.0.0.1:7337/api/v1/riftreader/world-state/schema`
- Typed .NET client project: `DesktopDotNet/ChromaLink.Client/ChromaLink.Client.csproj`

The new world-state lane is deliberately read-only. It exposes position/status/vitals-style state, not heading/facing/yaw, route planning, or movement control.

## Current truth

### Git / branch truth

- `main` is aligned with `origin/main`.
- Latest commits, newest first:
  - `55a915d Add RiftReader schema and typed client support`
  - `eaed5a1 Add typed HTTP bridge client`
  - `e5eab25 Add RiftReader world-state HTTP endpoint`
  - `dff3138 Harden ChromaLink telemetry contract coverage`

### Implemented external access

#### HTTP bridge

Files:

- `DesktopDotNet/ChromaLink.HttpBridge/HttpBridgeApp.cs`
- `DesktopDotNet/ChromaLink.HttpBridge/HttpBridgeSnapshotService.cs`
- `DesktopDotNet/ChromaLink.HttpBridge/README.txt`

Implemented endpoints:

```text
GET /api/v1
GET /api/v1/riftreader/world-state
GET /api/v1/riftreader/world-state/schema
GET /latest-snapshot
GET /snapshot
GET /health
GET /ready
```

`/api/v1/riftreader/world-state` emits `artifactKind = riftreader-world-state` with contract:

```text
contract.name = chromalink-riftreader-world-state
contract.schemaVersion = 1
```

It includes:

- `ready`, `healthy`, `fresh`, `stale`
- source snapshot metadata
- `navigation` capability flags
- `player.position`, player vitals/resource/level/calling/role
- `target.position`, target vitals/status/level
- `followUnits[]` positions and status fields

It explicitly reports these as unavailable:

- `headingAvailable = false`
- `facingAvailable = false`
- `routeAvailable = false`
- `controlAvailable = false`

This is intentional so RiftReader can consume world state without assuming ChromaLink is a full navigation/control system.

#### Typed .NET client

Files:

- `DesktopDotNet/ChromaLink.Client/ChromaLink.Client.csproj`
- `DesktopDotNet/ChromaLink.Client/ChromaLinkHttpClient.cs`
- `DesktopDotNet/ChromaLink.Client/README.md`

Main client API:

```csharp
using ChromaLink.Client;

using var client = new ChromaLinkHttpClient();
var response = await client.GetRiftReaderWorldStateAsync();

if (response.IsReadyAndFresh)
{
    var position = response.PlayerPosition!;
    Console.WriteLine($"Player: {position.X:F2}, {position.Y:F2}, {position.Z:F2}");
}
```

Additional method:

```csharp
var schemaJson = await client.GetRiftReaderWorldStateSchemaJsonAsync();
```

The client project has basic package metadata and packs successfully with its README.

### Tests added/updated

Files:

- `DesktopDotNet/ChromaLink.Tests/SnapshotContractTests.cs`
- `DesktopDotNet/ChromaLink.Tests/ChromaLinkClientTests.cs`

Coverage includes:

- `/api/v1` manifest advertises world-state and schema endpoints.
- `/api/v1/riftreader/world-state` returns the reduced projection.
- `/api/v1/riftreader/world-state/schema` returns draft 2020-12 schema JSON with the expected contract constants.
- Typed client reads manifest, schema, and world-state.
- Missing snapshot returns an unavailable payload instead of a parsing crash.

## Validation run during handoff

All validation below was run on 2026-05-01 after the latest pushed commit.

```powershell
dotnet test .\DesktopDotNet\ChromaLink.sln -v minimal
```

Result:

```text
Passed: 40/40
```

```powershell
dotnet run --project .\DesktopDotNet\ChromaLink.Cli\ChromaLink.Cli.csproj -- validate
```

Result:

```text
Smoke: passed
Replay: passed
Bench: passed
```

```powershell
git diff --check
```

Result: passed.

Previously validated in the just-finished schema/client slice:

```powershell
dotnet pack .\DesktopDotNet\ChromaLink.Client\ChromaLink.Client.csproj --configuration Debug --output $env:TEMP\chromalink-client-pack-test -v minimal
```

Result: package created successfully at:

```text
C:\Users\mrkoo\AppData\Local\Temp\chromalink-client-pack-test\ChromaLink.Client.1.0.0.nupkg
```

## Not yet validated

- No live RIFT runtime verification was performed in this handoff pass.
- No actual RiftReader repo integration has been made yet.
- The endpoint contract is validated against synthetic/test snapshots, not a fresh live-game snapshot.

## Known boundaries / do not overclaim

- ChromaLink is a useful world-state feed for navigation-adjacent tools.
- It does not currently expose heading/facing/yaw/orientation.
- It does not expose route graph/waypoint planning.
- It does not provide movement control.
- RiftReader should keep using its own proven reader/control/facing sources and treat ChromaLink as an auxiliary local world-state feed.

## Best next work

1. Integrate `ChromaLink.Client` into RiftReader as a read-only optional source.
2. Add a RiftReader-side adapter that gates on `response.IsReadyAndFresh`.
3. Keep ChromaLink world-state use separate from movement/control code.
4. Run the HTTP bridge against a fresh live ChromaLink snapshot and call `/api/v1/riftreader/world-state`.
5. Add a small PowerShell smoke script that starts/queries the bridge endpoints.
6. Tie `ChromaLink.Client` package version to the repo `VERSION` value if packaging becomes real.
7. Add schema validation with a JSON Schema validator if stricter contract enforcement is needed.
8. Consider adding a tiny examples folder for C#, PowerShell, and Python consumers.
9. Do not add CORS unless a browser-based external tool actually needs it.
10. Do not add heading/facing/control fields until a proven source exists.

## Resume prompt

Continue in `C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink` on `main`. Read the newest handoff file in `notes/` first. Current latest commit is `55a915d Add RiftReader schema and typed client support`, aligned with `origin/main`. ChromaLink now exposes `/api/v1/riftreader/world-state`, `/api/v1/riftreader/world-state/schema`, and a typed `DesktopDotNet/ChromaLink.Client` project. Next best step is to consume this from RiftReader as a read-only optional world-state source, gating on `response.IsReadyAndFresh`, while preserving the boundary that ChromaLink does not provide heading/facing/yaw or movement control.
