# Next Product Plan - 2026-04-02 - Release Candidate Polish

This note records the next planned step before implementation.

## Goal

Finish a low-risk release-candidate polish pass, then push the resulting productization chain to GitHub.

## Why This Is The Optimal Next Step

The package flow already works, but the first-run guidance can still be a little too eager to open UI in front of the RIFT client. A short polish pass can make the handoff surface clearer without changing the telemetry baseline or desktop process model.

## Product Direction

Focus this pass on three outcomes:

1. Make the package-facing README nudge players toward background-only startup during live play.
2. Keep the repo README and release checklist aligned with the generated package surface.
3. End at a clean checkpoint that is ready to push as the current release candidate.

## Constraints

- Do not change the strip protocol or live telemetry contract.
- Keep launcher behavior the same unless the change is purely wording or operator guidance.
- Prefer generated-package doc updates over adding new tools or scripts.

## Validation Gates

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- default package build succeeds
- self-contained package build succeeds
- generated package README reflects the new first-run guidance

## Success Criteria

- first-run package docs steer users toward the safest live-play workflow
- repo docs and package docs tell the same story
- the repo ends clean and the release-candidate commits are pushed
