# ChromaLink telemetry budget

## Goal
Use the dual-strip transport intentionally instead of rotating every slice with equal weight.

## Strip 1: always-hot combat heartbeat
Keep these fields available as often as possible:

- compact player hp %
- compact player resource %
- compact target hp %
- compact target state flags
- player combat flags
- compact cast summary
- action acknowledgment / action epoch
- encounter summary when encounter mode is active

## Strip 2: adaptive lane
Use strip 2 for richer but less frequent state:

- exact player vitals
- exact player resources
- exact target vitals
- exact target resources
- aura pages
- ability watch pages
- target / follow / auxiliary cast details
- environment and diagnostics pages
- event-burst pages after important changes

## Modes

### Combat
Prioritize:
- core status
- player vitals/resources/combat
- target vitals/resources
- player cast
- target-related auras
- action acknowledgment

### Encounter
Prioritize:
- combat mode baseline
- encounter active
- boss cast summary
- phase index
- selected mechanic summaries

### Environment
Prioritize:
- player position
- target position
- follow unit status
- text pages

### Diagnostics
Prioritize:
- core status
- text pages
- build / profile / readiness information

## Always-hot vs rotated vs on-demand

### Always-hot
- CoreStatus
- PlayerCombat
- compact PlayerCast
- action acknowledgment

### Rotated
- PlayerVitals
- PlayerResources
- TargetVitals
- TargetResources
- AuraPage
- AbilityWatch

### On-demand / mode-only
- TextPage
- PlayerPosition
- TargetPosition
- FollowUnitStatus
- future encounter extras

## Immediate implementation implications
1. Add an event-driven cache layer.
2. Add a telemetry registry with per-item priority and freshness targets.
3. Split strip 1 and strip 2 ownership explicitly.
4. Add action acknowledgment so input tests can be confirmed by telemetry.
