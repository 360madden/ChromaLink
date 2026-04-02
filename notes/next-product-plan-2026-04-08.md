# Next Product Plan - 2026-04-08

This note records the next planned step before implementation.

## Goal

Add a simple packaging/publish path for the local ChromaLink stack so the current tools can be assembled into a predictable output folder for easier handoff and repeatable local runs.

## Why This Is The Optimal Next Step

ChromaLink now has a meaningful local toolchain:

- CLI capture/bridge
- WinForms monitor
- local HTTP bridge
- browser dashboard
- lifecycle helpers

The next practical step is to make that toolchain easier to bundle and rerun without depending on ad hoc repo navigation.

## Product Direction

Focus this pass on three outcomes:

1. Add a simple publish/package path for the core desktop tools.
2. Keep the output layout predictable and easy to understand.
3. Document how the packaged output relates to the repo-based workflow.

## Constraints

- Keep the proven `640x360` strip baseline unchanged.
- Do not alter strip geometry or decode heuristics.
- Prefer simple publish scripts and folder layout over installer complexity.
- Keep the repo workflow working exactly as it does today.

## Parallel Work Split

### Lane A - Publish Scripts

Owns:

- `scripts/*`
- packaging helpers if needed

Deliver:

- scripts to publish the desktop tools into a predictable output folder

Avoid:

- bridge behavior changes
- addon Lua

### Lane B - Packaging Layout

Owns:

- narrow desktop project changes if needed for packaging
- packaging folder conventions

Deliver:

- a sensible packaged layout for monitor, HTTP bridge, and supporting files

Avoid:

- changing telemetry behavior

### Lane C - Docs Integration

Owns:

- `README.md`
- notes/*

Deliver:

- concise docs for the packaged output and how it differs from repo-native commands

Avoid:

- functional changes

## Validation Gates

Assemble only if all of these remain healthy:

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- publish/package script completes without error
- packaged output folder contains the expected core tools
- repo ends in a clean, documented checkpoint

## Success Criteria

- the current desktop stack can be published into a repeatable output folder
- the package layout is understandable
- repo-based workflows still work unchanged

## Result

Implemented with a lightweight publish helper that writes to:

- `artifacts\desktop-stack\latest`

Package contents:

- `publish\Cli`
- `publish\HttpBridge`
- `publish\Inspector`
- `publish\Monitor`
- `Start-ChromaLinkStack.cmd`
- `Open-ChromaLinkDashboard.cmd`
- `package-manifest.json`

The package script stays framework-dependent by default and only opts into self-contained output when requested.
