# ChromaLink / RiftReader Coordination Guardrail

Date: 2026-05-01

## TL;DR

ChromaLink owns the provider contract. RiftReader consumes that contract. Do not
let a RiftReader-focused session silently modify ChromaLink and then assume a
separate ChromaLink session knows about it.

## Ownership

| Area | Owner | Notes |
|---|---|---|
| ChromaLink color-strip transport | ChromaLink | Frame layout, rotation, payloads, decode contract |
| ChromaLink HTTP bridge | ChromaLink | `/api/v1`, `/latest-snapshot`, health/readiness |
| RiftReader world-state endpoint | ChromaLink | `/api/v1/riftreader/world-state` and schema |
| `ChromaLink.Client` | ChromaLink | Typed .NET client for external consumers |
| RiftReader adapter/use of ChromaLink | RiftReader | Optional read-only consumer integration |
| Movement, facing, control | RiftReader | ChromaLink does not provide control/facing authority |

## Required workflow for RiftReader-driven ChromaLink needs

1. **Request first.** Record the exact missing ChromaLink data or behavior.
2. **Provider change second.** Make ChromaLink changes in this repo, with schema,
   docs, and tests updated together.
3. **Handoff third.** Write a ChromaLink handoff describing the new contract,
   validation, and what remains unproven.
4. **Consumer integration last.** RiftReader consumes the published provider
   contract and records its own validation separately.

## Change request template

```markdown
## ChromaLink change request from RiftReader

- Requested by:
- Date:
- RiftReader use case:
- Needed ChromaLink field/endpoint:
- Existing ChromaLink endpoint checked:
- Why existing data is insufficient:
- Freshness/latency requirement:
- Read-only vs control requirement:
- Proposed ChromaLink contract shape:
- RiftReader integration blocked until:
```

## Hard boundaries

- Do not add heading/facing/yaw/control fields unless ChromaLink has a proven
  source and tests for them.
- Do not treat raw diagnostic snapshot fields as stable consumer contract until
  they are promoted into a documented contract/schema.
- Do not claim RiftReader integration is complete from ChromaLink tests alone.
