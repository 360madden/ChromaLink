# ChromaLink Desktop Package Layout - 2026-04-02

This note captures the package folder convention used by `scripts/Package-ChromaLinkDesktop.ps1`.

## Output Root

Default output root:

- `artifacts\package`

## Layout

```text
artifacts\package\
├── README.md
├── package-manifest.json
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
- The package only emits two top-level launchers:
  - `Start-ChromaLinkStack.cmd`
  - `Open-ChromaLinkDashboard.cmd`
- Repo-native helper scripts such as status, stop, probe, and live-stack wrappers remain in the source tree and are not copied into the package by default.

## Intent

Keep the package predictable without changing runtime behavior:

- no addon Lua changes
- no telemetry changes
- no installer complexity
- no hidden publish paths
