Build ChromaLink as a reliability-first optical telemetry strip for RIFT running on the same Windows machine.

Project identity:
- The addon folder is ChromaLink.
- The addon identifier is ChromaLink.
- Keep the old BarCode tree only as legacy/reference.
- Move forward with ChromaLink as the active project.

Primary goal for this phase:
- Create a fixed 640x360-safe sender/reader path.
- Prioritize deterministic placement, structural lock, integrity validation, replayability, and actionable logging.
- Do not optimize for novelty or bandwidth tricks yet.
- Reliability first, then richer payloads later.

Phase 0 audit:
- Confirm what the existing addon and desktop reader already support.
- Identify which parts are structurally sound and which parts are the bottleneck.
- Treat the old AHK desktop reader as the first major reliability/performance bottleneck.
- Use that audit to justify the cutover path.

Architecture direction:
- Keep the Lua addon sender inside the RIFT addon.
- Replace the old desktop AHK reader path with a .NET 9 desktop reader.
- Make the desktop path replay-first so lock/decode work can be verified offline.
- Separate the reader pipeline into capture, locate, sample, decode, validate, payload parse, replay, and metrics.

Sender profile and geometry:
- Use a fixed P360A profile as the default live profile.
- Target a 640x40 band at the top of the client.
- Use quiet zones and a deterministic grid layout.
- Reserve border space for finder/sync and duplicate metadata edges.
- Keep the payload region monochrome-safe for the baseline.
- Prefer a layout that is easy to reacquire and validate.

Transport design:
- Use a purpose-built monochrome-safe transport.
- Include magic bytes, protocol version, profile id, frame type/lane nibble, sequence, payload length, header CRC16, and payload CRC32C.
- Duplicate the first header bytes on both reserved metadata edges to strengthen acquisition.
- Keep the payload small enough to fit the baseline strip reliably.

Frame types and lanes:
- Implement coreStatus.
- Implement tactical.
- Start with a hot lane only.
- Defer warm/cold scheduling and delta schemes until the baseline is proven.

Payload priorities:
- Support player health/max health/alive/combat/available state.
- Support player primary resource kind/current/max.
- Support player level, calling, and role tokenization.
- Support cast active/channel/uninterruptible/progress.
- Support player offensive stat subset.
- Support target presence, health/resource current/max, level, alive/combat state.
- Support target relation, tier, tagged state, calling tokenization, and radius when exposed.
- Support player/target zone hashes.
- Support player/target X/Z coordinates when exposed.

Explicitly defer for now:
- color payload mode
- hot/warm/cold lane scheduling
- schema-aware deltas and keyframes
- aura transport
- hostile summary lanes
- exact spell-range truth
- facing/rotation
- whole-world replication
- friendly roster tracking
- hidden transport tricks

Desktop tooling:
- Create a .NET 9 solution with reader, CLI, and tests.
- Support commands like smoke, replay, live, watch, bench, and capture-dump.
- Produce replay fixtures and first-reject artifacts for debugging.
- Persist useful lock/geometry state when it helps same-machine reacquisition.

Windowing/resolution workflow:
- Add an external Windows helper to restore and size the RIFT client area to 640x360.
- Do not try to manage actual window geometry from inside the Lua sandbox.
- The helper should report before/after geometry clearly.
- If RIFT is fullscreen or borderless-sized, fail clearly instead of pretending resize succeeded.

Reliability requirements:
- Use a static black/white border for lock.
- Use duplicate header bytes for stronger structure.
- Make offline replay a first-class validation path.
- Cover offset, blur, color-gain drift, and mild scale drift in replay checks.
- Save first-reject BMPs and actionable failure reasons.
- Detect minimized-window cases explicitly.

Definition of "supported now":
- Only claim support for code paths that are actually implemented and proven.
- Keep hostile summaries, aura transport, color symbols, and compressed delta lanes out of current claims until they are live-verified.

Development priorities:
1. Prove the baseline sender/reader path at 640x360.
2. Make replay decoding stable.
3. Make live failures actionable.
4. Add richer transport modes only after live baseline reliability is proven.

Tone of implementation:
- Prefer practical, debuggable, deterministic systems.
- Choose boring reliability over cleverness.
- Make every failure mode observable.
- Keep phase boundaries explicit so future work can layer on safely.
