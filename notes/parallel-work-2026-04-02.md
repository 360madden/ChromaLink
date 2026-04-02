# Parallel Work Plan - 2026-04-02

This note defines the current multi-agent split for ChromaLink so work can be assembled cleanly.

## Goal

Advance the project in parallel without overlapping edits or losing track of integration assumptions.

## Shared Rules

- Keep the proven `640x360` baseline intact.
- Do not widen the strip or change segment geometry in this pass.
- Do not revert edits made by other workers.
- Prefer additive changes and clear handoff notes.
- Document any new command, file, or contract field that becomes part of the public workflow.

## Ownership

### Worker A - CLI Bridge Contract

Owns:

- `DesktopDotNet/ChromaLink.Cli/*`

Focus:

- harden the live telemetry JSON snapshot as a stable bridge contract
- add low-risk metadata that helps downstream consumers
- keep output backward-friendly where practical

Must avoid:

- editing inspector files
- editing Lua addon files
- changing strip geometry or reader detection behavior

### Worker B - Inspector Snapshot Support

Owns:

- `DesktopDotNet/ChromaLink.Inspector/*`

Focus:

- make the inspector understand and surface the live telemetry snapshot better
- improve visibility of aggregate state without changing the decode path

Must avoid:

- editing CLI files
- editing Lua addon files
- changing strip detection behavior

### Worker C - Addon Capability Commands

Owns:

- `Core/*.lua`
- `RIFT/*.lua`

Focus:

- add safe addon-side status/capability commands that help confirm live build behavior
- keep commands low risk and debug-oriented

Must avoid:

- editing .NET reader, CLI, or inspector files
- changing the physical strip layout in this pass

## Assembly Notes

- Main thread owns integration, validation, and final docs wording.
- If a worker needs to mention a public command or contract change, it should note that in its final summary so the main thread can document it centrally.
- Integration checkpoint requires:
  - `dotnet test .\DesktopDotNet\ChromaLink.sln`
  - `dotnet build .\DesktopDotNet\ChromaLink.Inspector\ChromaLink.Inspector.csproj`
  - one short live sample if the assembled changes affect runtime output
