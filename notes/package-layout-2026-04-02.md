# ChromaLink Desktop Package Layout - 2026-04-02

This note captures the package folder convention used by `scripts/Package-ChromaLinkDesktop.ps1`.

## Output Root

Default output root:

- `artifacts\package`

## Layout

```text
artifacts\package\
├── Bridge-ChromaLink.cmd
├── README.md
├── package-manifest.json
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
- The package emits five top-level launchers:
  - `Bridge-ChromaLink.cmd`
  - `Start-ChromaLinkStack.cmd`
  - `Status-ChromaLinkStack.cmd`
  - `Stop-ChromaLinkStack.cmd`
  - `Open-ChromaLinkDashboard.cmd`
- `Bridge-ChromaLink.cmd` starts the packaged CLI in `watch` mode.
- `Start-ChromaLinkStack.cmd` starts the packaged CLI watch loop, HTTP bridge, and monitor together.
- `Status-ChromaLinkStack.cmd` reports snapshot freshness, endpoint health, and package-local process counts.
- `Stop-ChromaLinkStack.cmd` stops only the package-local CLI, HTTP bridge, and monitor processes.
- Repo-native helper scripts such as status, stop, probe, and live-stack wrappers remain in the source tree and are not copied into the package by default.

## Intent

Keep the package predictable without changing runtime behavior:

- no addon Lua changes
- no telemetry changes
- no installer complexity
- no hidden publish paths
