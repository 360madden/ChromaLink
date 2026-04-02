# ChromaLink Desktop Package Layout - 2026-04-02

This note captures the package folder convention used by `scripts/Package-ChromaLinkDesktop.ps1`.

## Output Root

Default output root:

- `artifacts\package`
- self-contained release wrapper output:
  - `artifacts\package-selfcontained`

## Layout

```text
artifacts\package\
├── Open-ChromaLink-Product.cmd
├── Bridge-ChromaLink.cmd
├── README.md
├── package-manifest.json
├── Open-ChromaLink-Monitor.cmd
├── Open-ChromaLinkDashboardPinned.cmd
├── Status-ChromaLinkStack.cmd
├── Stop-ChromaLinkStack.cmd
├── Start-ChromaLinkStack.cmd
├── Open-ChromaLinkDashboard.cmd
└── desktop\
    ├── ChromaLink.Cli\
    ├── ChromaLink.HttpBridge\
    ├── ChromaLink.Inspector\
    └── ChromaLink.Monitor\
```

## Convention

- Each desktop tool gets its own publish folder.
- The folder name matches the project name.
- `desktop\ChromaLink.HttpBridge` and `desktop\ChromaLink.Monitor` are the quickest path to the running stack.
- `ChromaLink.Cli` and `ChromaLink.Inspector` stay available as standalone tools inside the same package.
- The package emits eight top-level launchers:
  - `Open-ChromaLink-Product.cmd`
  - `Bridge-ChromaLink.cmd`
  - `Start-ChromaLinkStack.cmd`
  - `Open-ChromaLink-Monitor.cmd`
  - `Open-ChromaLinkDashboardPinned.cmd`
  - `Status-ChromaLinkStack.cmd`
  - `Stop-ChromaLinkStack.cmd`
  - `Open-ChromaLinkDashboard.cmd`
- `Open-ChromaLink-Product.cmd` is the package-first launcher:
  - start background stack
  - wait for `/ready`
  - open the monitor
- `Bridge-ChromaLink.cmd` starts the packaged CLI in `watch` mode.
- `Start-ChromaLinkStack.cmd` starts the packaged CLI watch loop plus HTTP bridge without opening UI.
- `Open-ChromaLink-Monitor.cmd` opens the packaged monitor explicitly.
- `Open-ChromaLinkDashboardPinned.cmd` opens the packaged monitor in opt-in always-on-top mode.
- `Status-ChromaLinkStack.cmd` reports snapshot freshness, endpoint health, and package-local process counts.
- `Stop-ChromaLinkStack.cmd` stops only the package-local CLI, HTTP bridge, and monitor processes.
- `package-manifest.json` now includes package identity fields such as `PackageVersion` and `SourceCommit`.
- Repo-native helper scripts such as status, stop, probe, and live-stack wrappers remain in the source tree and are not copied into the package by default.

## Intent

Keep the package predictable without changing runtime behavior:

- no addon Lua changes
- no telemetry changes
- no installer complexity
- no hidden publish paths
