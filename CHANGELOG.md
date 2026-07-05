# Changelog

## Unreleased

- Added a package-first launcher flow with package identity in the manifest.
- Added package lifecycle helpers and a self-contained release wrapper.
- Separated background stack startup from explicit UI-opening helpers.
- Polished the release-candidate package guidance toward background-first live play.
- Aligned addon-facing version markers with the canonical `VERSION` file and added repository consistency coverage.
- Tightened replay/bench search bounds so non-live validation stays fast while preserving wide live-capture search.
- Added a read-only `/api/v1/riftreader/world-state` HTTP bridge endpoint and C# consumer example for external tools.
- Added `ChromaLink.Client`, a typed .NET client for consuming the HTTP bridge from RiftReader-style external programs.
- Added `/api/v1/riftreader/world-state/schema` so non-.NET consumers can discover and validate the world-state contract.
- Added `Ensure-ChromaLinkFresh` provider preflight/recovery docs and geometry classifications so `640x360 / P360C` is the fallback minimum while larger 16:9 geometries are accepted by freshness proof.
- Added a facts-only `/api/v1/consumers/combat-assistant/state` HTTP profile and schema for automation-oriented consumers that need combat facts without action/control coupling.

## 0.1.0-dev

- Established the current live `640x360` ChromaLink baseline.
- Proved rotating multi-frame telemetry with `CoreStatus`, `PlayerVitals`, and `PlayerPosition`.
- Added the rolling snapshot, local HTTP bridge, monitor, inspector overlays, and package-based handoff workflow.
