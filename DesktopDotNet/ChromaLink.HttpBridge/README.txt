ChromaLink HTTP Bridge

This service exposes the rolling live telemetry snapshot over localhost.

Current direction:
- HTTP is the intended app-facing surface
- raw frame sections remain available for diagnostics
- downstream consumers should prefer normalized sections when available

Endpoints:
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

Why:
- it merges native player combat with Rift Meter combat
- it exposes source-health cues like degraded/stable snapshot
- it includes cross-frame correlation details like sequence delta and observation skew
- it is the right future place to expose first-class live rates like DPS/HPS/DTPS

Use raw `aggregate.riftMeterCombat` when you need transport-level diagnostics.
Use normalized `aggregate.combat` when you need app-facing combat state.
