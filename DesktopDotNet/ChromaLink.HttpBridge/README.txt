ChromaLink HTTP Bridge

This service exposes the rolling live telemetry snapshot over localhost.

Current direction:
- HTTP is the intended app-facing surface
- raw frame sections remain available for diagnostics
- downstream consumers should prefer normalized sections when available

Endpoints:
- /api/v1
- /api/v1/riftreader/world-state
- /api/v1/riftreader/world-state/schema
- /latest-snapshot
- /snapshot
- /health
- /ready

Default base URL:
- http://127.0.0.1:7337/

Current snapshot contract:
- contract.name = chromalink-live-telemetry
- contract.schemaVersion = 2

Important aggregate sections:
- aggregate.coreStatus
- aggregate.playerVitals
- aggregate.playerResources
- aggregate.playerCombat
- aggregate.riftMeterCombat
- aggregate.combat

Preferred combat section for consumers:
- aggregate.combat

Why prefer aggregate.combat:
- it merges native player combat with Rift Meter combat
- it exposes source-health cues like degraded/stable snapshot
- it includes cross-frame correlation details like sequence delta and observation skew
- it is the right future place to expose first-class live rates like DPS/HPS/DTPS

Use raw `aggregate.riftMeterCombat` when you need transport-level diagnostics.
Use normalized `aggregate.combat` when you need app-facing combat state.

RiftReader-style world-state endpoint:
- GET http://127.0.0.1:7337/api/v1/riftreader/world-state
- JSON schema: http://127.0.0.1:7337/api/v1/riftreader/world-state/schema
- contract.name = chromalink-riftreader-world-state
- contract.schemaVersion = 1
- purpose: small read-only JSON shape for outside programs that need current player/target/follow-unit positions without parsing the full diagnostic snapshot
- includes: player.position, player vitals/level, target.position/vitals, followUnits[] positions/status, readiness/freshness, and source contract metadata
- explicitly does not include: heading/facing/yaw, route planning, or movement control

Typed .NET consumer:
- reference DesktopDotNet/ChromaLink.Client/ChromaLink.Client.csproj
- use ChromaLink.Client.ChromaLinkHttpClient
- call GetRiftReaderWorldStateAsync()
- check response.IsReadyAndFresh before trusting response.PlayerPosition

Typed .NET example:

using ChromaLink.Client;

using var client = new ChromaLinkHttpClient();
var response = await client.GetRiftReaderWorldStateAsync();

if (response.IsReadyAndFresh)
{
    var position = response.PlayerPosition!;
    Console.WriteLine($"Player: {position.X:F2}, {position.Y:F2}, {position.Z:F2}");
}

Minimal C# consumer example:

using System.Text.Json;

using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:7337") };
using var document = JsonDocument.Parse(
    await http.GetStringAsync("/api/v1/riftreader/world-state"));

var root = document.RootElement;
if (root.GetProperty("ok").GetBoolean() &&
    root.GetProperty("navigation").GetProperty("playerPositionAvailable").GetBoolean())
{
    var position = root.GetProperty("player").GetProperty("position");
    var x = position.GetProperty("x").GetDouble();
    var y = position.GetProperty("y").GetDouble();
    var z = position.GetProperty("z").GetDouble();
    Console.WriteLine($"Player: {x:F2}, {y:F2}, {z:F2}");
}
