# Next Product Plan - 2026-04-02 - Packaged Lifecycle

This note records the next planned step before implementation.

## Goal

Add minimal package-native lifecycle helpers so the packaged ChromaLink stack can be started, inspected, and stopped without depending on the source repo.

## Why This Is The Optimal Next Step

The packaged output can now launch a real live stack:

- packaged `ChromaLink.Cli.exe watch`
- packaged `ChromaLink.HttpBridge.exe`
- packaged `ChromaLink.Monitor.exe`

But once launched, the package still lacks its own:

- stop path
- status path

That leaves an avoidable product gap in the handoff flow. The next useful step is to make the packaged folder operationally self-contained, not just runnable.

## Product Direction

Focus this pass on four outcomes:

1. Reuse the existing repo-native lifecycle behavior as the reference.
2. Emit only a minimal set of package-native helpers.
3. Keep the package layout predictable and easy to inspect.
4. Avoid source-repo assumptions inside the packaged scripts.

## Constraints

- Keep the proven `640x360` strip baseline unchanged.
- Do not change transport, decode, or addon Lua behavior.
- Keep the package helper set small and explicit.
- Prefer package-local process checks over repo-native project launches.
- Preserve the existing repo-native lifecycle scripts.

## Minimal Target

The packaged output should gain:

- a stop helper for the packaged stack
- a status helper for the packaged stack

Those helpers should understand the packaged process set:

- `ChromaLink.Cli`
- `ChromaLink.HttpBridge`
- `ChromaLink.Monitor`

## Validation Gates

Treat this pass as complete only if all of these remain healthy:

- `dotnet test .\DesktopDotNet\ChromaLink.sln`
- `.\scripts\Package-ChromaLinkDesktop.ps1`
- packaged output contains the expected lifecycle helpers
- packaged start, status, and stop flows all work from the package root
- repo ends in a clean, documented checkpoint

## Success Criteria

- the package can start, inspect, and stop itself without repo-native helpers
- package docs explain the lifecycle helpers clearly
- repo-native workflows remain unchanged

## Result

Implemented by extending the package output with:

- `Status-ChromaLinkStack.cmd`
- `Stop-ChromaLinkStack.cmd`
- package-local PowerShell helpers that only target packaged executables under `desktop\`
- manifest launcher metadata for the new lifecycle helpers

Validation confirmed:

- `dotnet test .\DesktopDotNet\ChromaLink.sln` passed
- `.\scripts\Package-ChromaLinkDesktop.ps1` completed successfully after forcing runtime-aware restore evaluation
- the packaged `Status-ChromaLinkStack.cmd` reported endpoint health, snapshot freshness, and package-local process counts
- the packaged `Stop-ChromaLinkStack.cmd` stopped the packaged CLI, HTTP bridge, and monitor processes cleanly
- a follow-up packaged status check showed package-local process counts returned to zero
