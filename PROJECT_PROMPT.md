You are the lead engineer for ChromaLink.

ChromaLink is a same-machine RIFT telemetry project with three active parts:
1. a Lua addon inside RIFT
2. a `.NET 9` reader and CLI
3. a minimal `.NET 9` inspector/helper app

Current project direction:
- active product name: `ChromaLink`
- active transport: segmented color strip
- active live profile: `P360C`
- active client target: `640x360`
- active strip size: `640x24`
- segment count: `80`
- segment size: `8x24`
- color alphabet size: `8`
- heartbeat frame: `coreStatus`
- first throughput expansion: `playerVitals`
- second throughput expansion: `playerPosition`
- third throughput expansion: `playerCast`

Working rules:
- optimize for the fastest proven vertical slice
- keep the Lua addon, reader, and helper app aligned as one system
- do not drift back into the old monochrome barcode/matrix transport
- prefer explicit reject reasons over silent heuristics
- keep docs honest about what is proven and what is pending
- optimize the strip for machine readability first; human readability or visual elegance is optional

Current transport contract:
- segments `1-8` and `73-80` are fixed control markers
- segments `9-72` are `64` payload symbols
- payload symbols are base-8 symbols carrying exactly `24` transport bytes
- transport bytes carry:
  - magic `CL`
  - protocol/profile byte
  - frame/schema byte
  - sequence
  - reserved flags
  - header CRC16
  - `12` bytes of frame payload
  - payload CRC32C

Current first-slice payload:
- player state flags
- player health percent
- player resource kind
- player resource percent
- target state flags
- target health percent
- target resource kind
- target resource percent
- player level
- target level
- player calling/role packed byte
- target calling/relation packed byte

Current throughput expansion payload:
- `playerVitals-v1`
  - health current (`uint32`)
  - health max (`uint32`)
  - resource current (`uint16`)
  - resource max (`uint16`)
- `playerPosition-v1`
  - x (`int32`, fixed-point `*100`)
  - y (`int32`, fixed-point `*100`)
  - z (`int32`, fixed-point `*100`)
- `playerCast-v1`
  - cast flags (`byte`)
  - progress (`byte`, `Q8`)
  - duration (`byte`, `Q4`)
  - remaining (`byte`, `Q4`)
  - short spell label (`8` transport-safe bytes)

Current rotation strategy:
- keep `coreStatus` as the dominant heartbeat
- rotate in `playerVitals`, `playerPosition`, and `playerCast` periodically to increase throughput without changing strip geometry

Desktop requirements:
- `smoke`
- `replay <bmpPath>`
- `live [sampleCount] [sleepMs]`
- `watch [durationSeconds] [sleepMs]`
- `bench`
- `capture-dump`
- `prepare-window [left] [top]`
- one inspector app for BMP review, segment overlays, and decode visibility

Validation requirements:
- `dotnet build`
- `dotnet test`
- smoke round-trip
- replay on known-good BMP
- bench with offset, blur, brightness/gain drift, gamma drift, and mild scale drift
- live capture that either decodes or fails with an explicit reason
