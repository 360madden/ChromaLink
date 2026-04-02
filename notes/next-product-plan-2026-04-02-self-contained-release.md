# Next Product Plan - 2026-04-02 - Self-Contained Release

This note records the next planned step before implementation.

## Goal

Make the self-contained package path explicit and easy so ChromaLink can be handed to another Windows machine without assuming the matching .NET runtime is already installed.

## Why This Is The Optimal Next Step

The current package is now a much better product:

- package identity exists
- a first-run launcher exists
- packaged lifecycle helpers exist
- the handoff README is usable

The biggest remaining release friction is deployment to a machine that does not already have the right runtime. The package script already supports `-SelfContained`, but that path is not yet a first-class release workflow.

## Product Direction

Focus this pass on three outcomes:

1. Add a simple dedicated self-contained packaging wrapper.
2. Make the self-contained output location obvious and separate from the framework-dependent package.
3. Document when to use each package flavor.

## Constraints

- Keep the current package script as the source of truth.
- Do not change the live telemetry baseline.
- Avoid hidden behavior changes in the default framework-dependent package.

## Target Shape

- keep the existing default package at `artifacts\package`
- add a self-contained package wrapper that writes to:
  - `artifacts\package-selfcontained`
- update docs so the package flavors are easy to distinguish

## Validation Gates

Treat this pass as complete only if all of these remain healthy:

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- default package still builds
- self-contained package wrapper completes successfully
- docs clearly explain the two package flavors

## Success Criteria

- self-contained release path is obvious and repeatable
- default package behavior stays predictable
- handoff to another machine is less dependent on manual runtime setup

## Result

Implemented with:

- `scripts/Package-ChromaLinkDesktop-SelfContained.cmd`
- separate self-contained output root:
  - `artifacts\package-selfcontained`

Validation confirmed:

- `dotnet test .\DesktopDotNet\ChromaLink.sln` passed
- `.\scripts\Package-ChromaLinkDesktop-SelfContained.cmd` completed successfully
- the self-contained output includes the same product launcher and lifecycle surface as the default package
- the README and package-layout note now explain when to use `artifacts\package` versus `artifacts\package-selfcontained`
