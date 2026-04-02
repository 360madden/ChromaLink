# Next Product Plan - 2026-04-02 - Final Product Acceleration

This note records the next planned step before implementation.

## Goal

Accelerate ChromaLink toward a usable final product by focusing on release readiness, handoff quality, and operator safety instead of adding more transport research scope.

## Why This Is The Optimal Next Step

The core technical bridge is already real:

- live `640x360` strip decode works
- rotating multi-frame telemetry works
- rolling JSON snapshot works
- local HTTP bridge works
- monitor and dashboard views exist
- packaged desktop output exists
- packaged lifecycle helpers exist

The biggest remaining gains are now product-facing:

- clearer release packaging
- fewer operator mistakes during live play
- more predictable startup and shutdown
- better handoff quality for another machine or another user

## Product Direction

Focus this pass on four outcomes:

1. Identify the smallest remaining gaps between the current package and a real handoff product.
2. Prefer improvements that reduce friction for first-run use.
3. Keep the proven telemetry baseline unchanged.
4. Parallelize only non-overlapping productization work.

## Constraints

- Keep the proven `640x360` strip baseline unchanged.
- Do not destabilize live decode in pursuit of packaging polish.
- Favor simple release mechanics over installer complexity unless a gap truly requires installation.
- Keep repo-native developer workflows intact.

## Likely High-Leverage Targets

Prioritize from this set after the audit:

1. self-contained package option and docs
2. release-grade package README / quickstart
3. one-command product launcher path with safer defaults
4. package manifest improvements for downstream tooling
5. final surface cleanup of helper naming and discovery

## Validation Gates

Treat this pass as successful only if all of these remain healthy:

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- package script completes successfully
- release/handoff docs match the emitted package
- lifecycle start / status / stop remains correct
- repo ends in a clean, documented checkpoint

## Success Criteria

- the package feels like a real handoff product, not just a developer publish folder
- the first-run path is clearer and safer
- the repo still supports active development without friction

## Result

This pass delivered two concrete handoff improvements:

- package identity in `package-manifest.json`
  - `PackageName`
  - `PackageVersion`
  - `SourceCommit`
- a canonical first-run launcher:
  - `Open-ChromaLink-Product.cmd`

Validation confirmed:

- `dotnet test .\DesktopDotNet\ChromaLink.sln` passed
- `.\scripts\Package-ChromaLinkDesktop.ps1` completed successfully
- the generated package README now acts as a first-run guide
- `Open-ChromaLink-Product.cmd` started the packaged stack, reached `ready`, and opened the packaged monitor
- `Status-ChromaLinkStack.cmd` then reported a healthy packaged stack with package-local process counts
