# ChromaLink event matrix

## Source references
- `RIFT\\LLM_RIFT_API_v2_audited.zip`
- `RIFT\\king-molinator-2.0.0.5.zip`
- local addon code in `RIFT\\AbilityExport.lua`

## Ability events to consume

| Event | Use in ChromaLink | Initial action |
| --- | --- | --- |
| `Event.Ability.New.Add` | ability list changed | invalidate ability list + refresh tracked ability cache |
| `Event.Ability.New.Remove` | ability list changed | invalidate ability list + remove cached detail |
| `Event.Ability.New.Cooldown.Begin` | ability entered cooldown | invalidate affected ability detail |
| `Event.Ability.New.Cooldown.End` | ability left cooldown | invalidate affected ability detail |
| `Event.Ability.New.Range.True` | ability in range | invalidate affected ability detail |
| `Event.Ability.New.Range.False` | ability out of range | invalidate affected ability detail |
| `Event.Ability.New.Usable.True` | ability usable | invalidate affected ability detail |
| `Event.Ability.New.Usable.False` | ability unusable | invalidate affected ability detail |
| `Event.Ability.New.Target` | target-sensitive ability state changed | invalidate affected ability detail |

## Unit events to consume

| Event | Use in ChromaLink | Initial action |
| --- | --- | --- |
| `Event.Unit.Add` / `Remove` | target / tracked-unit topology changed | invalidate tracked unit records |
| `Event.Unit.Availability.Full` / `Partial` / `None` | tracked unit became available/unavailable | invalidate tracked unit records |
| `Event.Unit.Detail.Health` / `HealthMax` | exact hp changed | invalidate tracked unit records |
| `Event.Unit.Detail.Mana` / `ManaMax` | mana changed | invalidate tracked unit records |
| `Event.Unit.Detail.Energy` / `EnergyMax` | energy changed | invalidate tracked unit records |
| `Event.Unit.Detail.Power` | power changed | invalidate tracked unit records |
| `Event.Unit.Detail.Combo` | combo changed | invalidate tracked unit records |
| `Event.Unit.Detail.Charge` / `ChargeMax` | charge changed | invalidate tracked unit records |
| `Event.Unit.Detail.Planar` / `PlanarMax` | planar changed | invalidate tracked unit records |
| `Event.Unit.Detail.Absorb` | absorb changed | invalidate tracked unit records |
| `Event.Unit.Detail.Combat` | combat entered/left | invalidate tracked unit records and raise combat priority |
| `Event.Unit.Detail.Ready` / `Afk` / `Role` / `Level` | status metadata changed | invalidate tracked unit records |
| `Event.Unit.Detail.Tagged` / `Mark` | target relevance changed | invalidate tracked unit records |
| `Event.Unit.Detail.Coord` | position changed | invalidate tracked unit records |
| `Event.Unit.Castbar` | cast/channel changed | invalidate cached cast state |

## Buff events to consume

| Event | Use in ChromaLink | Initial action |
| --- | --- | --- |
| `Event.Buff.Add` | buff/debuff appeared | invalidate cached buffs for unit |
| `Event.Buff.Change` | stack / remaining changed | invalidate cached buffs for unit |
| `Event.Buff.Description` | richer detail changed | invalidate cached buffs for unit |
| `Event.Buff.Remove` | buff/debuff disappeared | invalidate cached buffs for unit |

## Borrowed architecture patterns

### From King Molinator
- trigger registry by event family
- encounter state start / active / end
- phase and percent objective tracking
- queue flushing on coarser cadence than raw update frequency

### From nkUI
- grouped settings UI
- settings defaults merge
- deferred incremental work if cache expansion grows too heavy

### From RiftMeter
- keep combat priorities tight
- do not mirror every stat continuously

## Initial tracked objects
- player
- player target
- focus
- configured follow slots (`group01` etc.)
- tracked ability list
- player and target auras
