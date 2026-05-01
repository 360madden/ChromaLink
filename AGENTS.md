# ChromaLink Agent Policy

This file defines default assistant behavior for work inside
`C:\Users\mrkoo\OneDrive\Documents\RIFT\Interface\AddOns\ChromaLink`.

## Response format

- Lead with the direct answer or result.
- Keep explanations concise, technical, and evidence-backed.
- After finishing a task, include a short optional section with practical next
  steps or improvement ideas when it adds real value.
- When recommendations add value, provide a **Top 10 recommended next actions**
  list. Keep it relevant and non-repetitive.

## ChromaLink ownership boundary

- ChromaLink owns its addon transport, desktop reader, HTTP bridge, schemas,
  typed client, and external-consumer contracts.
- RiftReader may consume ChromaLink through published ChromaLink surfaces such as
  `/api/v1/riftreader/world-state`, the schema endpoint, and
  `ChromaLink.Client`.
- Do not silently let a RiftReader-focused task change ChromaLink behavior,
  schemas, frame rotation, or client APIs. If the user has not explicitly
  switched the task into ChromaLink, write a ChromaLink change request instead.
- Any RiftReader-facing contract change in this repo must update the relevant
  ChromaLink documentation/handoff and clearly state whether RiftReader has only
  requested the change or has actually integrated it.

## Cross-repo coordination rule

- Treat ChromaLink as the **provider** and RiftReader as a **consumer** unless a
  task explicitly says otherwise.
- Provider-side changes belong in this repo and should be committed/pushed from
  a ChromaLink-aware session after reading the latest ChromaLink handoff.
- Consumer-side changes belong in RiftReader and should not edit this repo
  directly unless the user explicitly authorizes a cross-repo edit pass.
- If both repos must change, make the dependency direction explicit:
  1. ChromaLink publishes/updates the contract.
  2. RiftReader consumes the published contract.
  3. Both sides record validation and remaining unknowns.

## Validation

- Prefer the smallest correct patch over broad rewrites.
- For docs-only changes, run at least `git diff --check`.
- For code, schema, client, or endpoint changes, run the most relevant
  ChromaLink tests/build/validation before claiming the change is ready.
