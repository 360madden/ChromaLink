# Next Product Plan - 2026-04-02 - Packaged Live Stack

This note records the next planned step before implementation.

## Goal

Make the packaged desktop output self-sufficient for live telemetry by including a clear launcher path for the rolling snapshot producer in addition to the existing HTTP bridge and monitor consumers.

## Why This Is The Optimal Next Step

The current package layout is clean and validated, but the emitted `Start-ChromaLinkStack.cmd` only starts:

- `ChromaLink.HttpBridge`
- `ChromaLink.Monitor`

That is not enough to keep `%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out\chromalink-live-telemetry.json` fresh. The live stack still depends on the CLI watch loop that produces the rolling snapshot.

The package should not claim to be a runnable product view unless it can launch the telemetry producer path coherently.

## Product Direction

Focus this pass on four outcomes:

1. Reuse the existing repo-native live bridge behavior as the model.
2. Add a packaged launcher path that starts the rolling snapshot producer.
3. Keep the package layout predictable and easy to inspect.
4. Preserve the existing repo-native workflow and strip baseline.

## Constraints

- Keep the proven `640x360` strip baseline unchanged.
- Do not change addon Lua or strip geometry for this pass.
- Do not bypass the existing rolling snapshot contract.
- Prefer a minimal packaged launcher set over a large script surface.
- Keep package output safe and explicit about what it starts.

## Implementation Shape

The package should emit a live-stack launcher that starts:

1. packaged `ChromaLink.Cli` in watch mode
2. packaged `ChromaLink.HttpBridge`
3. packaged `ChromaLink.Monitor`

The package may also emit narrow companion launchers if they improve clarity, but the main requirement is that the packaged output can refresh live telemetry on its own.

## Validation Gates

Treat this pass as complete only if all of these remain healthy:

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- `.\scripts\Package-ChromaLinkDesktop.ps1`
- packaged output contains the expected producer and consumer launchers
- packaged live launcher starts the producer, bridge, and monitor processes
- repo ends in a clean, documented checkpoint

## Success Criteria

- the package can launch a true live stack, not just viewers
- package docs explain the launcher roles clearly
- repo-native helpers remain intact for development use

## Result

Implemented by extending the package output with:

- `Bridge-ChromaLink.cmd`
- an updated `Start-ChromaLinkStack.cmd` that starts:
  - packaged `ChromaLink.Cli.exe watch`
  - packaged `ChromaLink.HttpBridge.exe`
  - packaged `ChromaLink.Monitor.exe`
- manifest launcher metadata
- updated package docs describing the producer path

Validation confirmed:

- `dotnet test .\DesktopDotNet\ChromaLink.sln` passed
- `.\scripts\Package-ChromaLinkDesktop.ps1` completed successfully
- the packaged launcher started `ChromaLink.Cli`, `ChromaLink.HttpBridge`, and `ChromaLink.Monitor`
- `%LOCALAPPDATA%\ChromaLink\DesktopDotNet\out\chromalink-live-telemetry.json` advanced while the packaged stack was running
