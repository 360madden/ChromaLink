# ChromaLink.Client

Typed .NET client for the local ChromaLink HTTP bridge.

Use this from external .NET tools, including RiftReader-style consumers, when
you want the reduced world-state contract without hand-parsing the bridge JSON.

## Expected bridge

Default base URL:

```text
http://127.0.0.1:7337/
```

Primary endpoint consumed by this library:

```text
/api/v1/riftreader/world-state
```

Schema endpoint:

```text
/api/v1/riftreader/world-state/schema
```

## Example

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

## Contract boundaries

The current world-state contract is read-only.

It exposes:

- player position
- player vitals/resource basics
- target position/vitals/status
- follow-unit positions/status
- readiness/freshness/source-contract metadata

It does **not** expose:

- heading/facing/yaw
- route planning
- movement control

RiftReader should keep using its own proven facing/control source alongside this
world-state feed.

## Cross-repo ownership

ChromaLink owns this client and the provider contract it wraps. RiftReader-style
consumers should treat it as an optional read-only dependency surface, not as a
reason to edit ChromaLink from a RiftReader-focused task.

If a consumer needs more data, update the ChromaLink endpoint/schema/client in
this repo first, publish a ChromaLink handoff, then integrate the explicit
contract from the consumer repo.
