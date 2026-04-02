# Next Product Plan - 2026-04-02 - Release Versioning

This note records the next planned step before implementation.

## Goal

Add a single source of truth for release versioning and a simple release checklist so package builds are intentional and identifiable.

## Why This Is The Optimal Next Step

The package flow is now much closer to a final product, but two release-readiness basics are still missing:

- a repo-level version source
- a concise repeatable release checklist

Without those, package identity still depends too much on ad hoc local state.

## Product Direction

Focus this pass on three outcomes:

1. Add a repo-level version file.
2. Make the package script read that version instead of hardcoding it.
3. Add a concise release checklist and changelog baseline.

## Constraints

- Keep the package manifest build identity fields.
- Do not change the telemetry baseline.
- Keep the release docs lightweight and actionable.

## Validation Gates

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- default package build succeeds
- self-contained package build succeeds
- `package-manifest.json` reflects the repo version source

## Success Criteria

- version source is explicit
- release docs are lightweight but useful
- package identity is no longer hardcoded inside the packaging script
