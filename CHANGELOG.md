# Changelog

## Unreleased

- Added a package-first launcher flow with package identity in the manifest.
- Added package lifecycle helpers and a self-contained release wrapper.
- Separated background stack startup from explicit UI-opening helpers.
- Polished the release-candidate package guidance toward background-first live play.
- Aligned addon-facing version markers with the canonical `VERSION` file and added repository consistency coverage.
- Tightened replay/bench search bounds so non-live validation stays fast while preserving wide live-capture search.

## 0.1.0-dev

- Established the current live `640x360` ChromaLink baseline.
- Proved rotating multi-frame telemetry with `CoreStatus`, `PlayerVitals`, and `PlayerPosition`.
- Added the rolling snapshot, local HTTP bridge, monitor, inspector overlays, and package-based handoff workflow.
