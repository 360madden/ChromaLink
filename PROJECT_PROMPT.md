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
- expanded stats lane: `playerResources`
- combat lane: `playerCombat`
- target coordinate lane: `targetPosition`
- follow-unit lane: `followUnitStatus`
- remaining telemetry lanes: `targetVitals`, `targetResources`, `auxUnitCast`, `auraPage`, `textPage`, `abilityWatch`

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
  - duration (`uint16`, centiseconds)
  - remaining (`uint16`, centiseconds)
  - cast target code (`byte`)
  - short spell label (`5` transport-safe bytes)
- `playerResources-v1`
  - mana current/max (`uint16`, `uint16`)
  - energy current/max (`uint16`, `uint16`)
  - power current/max (`uint16`, `uint16`)
- `playerCombat-v1`
  - combat flags (`byte`)
  - combo (`byte`)
  - charge current/max (`uint16`, `uint16`)
  - planar current/max (`uint16`, `uint16`)
  - absorb (`uint16`)
- `targetPosition-v1`
  - x (`int32`, fixed-point `*100`)
  - y (`int32`, fixed-point `*100`)
  - z (`int32`, fixed-point `*100`)
- `followUnitStatus-v1`
  - slot (`byte`)
  - follow flags (`byte`)
  - x (`int16`, fixed-point `*2`)
  - y (`int16`, fixed-point `*2`)
  - z (`int16`, fixed-point `*2`)
  - health percent (`byte`, `Q8`)
  - resource percent (`byte`, `Q8`)
  - level (`byte`)
  - calling/role packed (`byte`)
- `targetVitals-v1`
  - health current (`uint32`)
  - health max (`uint32`)
  - absorb (`uint16`)
  - target flags (`byte`)
  - target level (`byte`)
- `targetResources-v1`
  - mana current/max (`uint16`, `uint16`)
  - energy current/max (`uint16`, `uint16`)
  - power current/max (`uint16`, `uint16`)
- `auxUnitCast-v1`
  - unit selector (`byte`)
  - cast flags (`byte`)
  - progress (`byte`, `Q8`)
  - duration (`uint16`, centiseconds)
  - remaining (`uint16`, centiseconds)
  - cast target code (`byte`)
  - short spell label (`4` transport-safe bytes)
- `auraPage-v1`
  - page kind (`byte`)
  - total aura count (`byte`)
  - two compact aura entries with:
    - aura id (`uint16`)
    - remaining (`byte`, `Q4`)
    - stack (`byte`)
    - flags (`byte`)
- `textPage-v1`
  - text kind (`byte`)
  - text hash (`uint16`)
  - short transport-safe text label (`9` bytes)
- `abilityWatch-v1`
  - page index (`byte`)
  - two tracked ability entries with:
    - compact id (`uint16`)
    - cooldown (`byte`, `Q4`)
    - flags (`byte`)
  - shortest cooldown (`byte`, `Q4`)
  - ready count (`byte`)
  - cooling count (`byte`)

Current rotation strategy:
- keep `coreStatus` as the dominant heartbeat
- rotate in the secondary slices periodically to increase throughput without changing strip geometry

Current live proof:
- the expanded rotation now decodes live with reserved flags `0x3F`
- live captures have accepted:
  - `CoreStatus`
  - `PlayerVitals`
  - `PlayerPosition`
  - `PlayerCast`
  - `PlayerResources`
  - `PlayerCombat`
- `TargetPosition`
- `FollowUnitStatus`
- `TargetVitals`
- `TargetResources`
- `AuxUnitCast`
- `AuraPage`
- `TextPage`
- `AbilityWatch`

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
