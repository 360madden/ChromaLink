#!/usr/bin/env python3
"""Ensure or diagnose fresh ChromaLink telemetry for RiftReader consumers.

This helper is intentionally provider-owned. It wraps existing ChromaLink
launch/window helpers, probes the published HTTP bridge, classifies geometry
without requiring an exact 640x360 client when telemetry is fresh, and writes
durable JSON/Markdown summaries for handoff/debugging.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import pathlib
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request
from dataclasses import asdict, dataclass, field
from typing import Any


REPO_ROOT = pathlib.Path(__file__).resolve().parents[1]
DEFAULT_BASE_URL = os.environ.get("CHROMALINK_HTTP_BRIDGE_URL", "http://127.0.0.1:7337/")
MIN_CLIENT_WIDTH = 640
MIN_CLIENT_HEIGHT = 360
TARGET_ASPECT_RATIO = 16 / 9
ASPECT_TOLERANCE = 0.025


@dataclass
class CommandEnvelope:
    args: list[str]
    cwd: str
    startedAtUtc: str
    endedAtUtc: str | None = None
    durationSeconds: float | None = None
    exitCode: int | None = None
    stdoutPreview: str = ""
    stderrPreview: str = ""
    timedOut: bool = False


@dataclass
class HttpProbe:
    url: str
    ok: bool
    statusCode: int | None = None
    body: Any = None
    error: str | None = None
    bodyPreview: str = ""


@dataclass
class GeometryStatus:
    processFound: bool = False
    pid: int | None = None
    title: str | None = None
    clientWidth: int | None = None
    clientHeight: int | None = None
    aspectRatio: float | None = None
    isAtLeastMinimum: bool = False
    isSixteenByNine: bool = False
    isExactP360C: bool = False
    classifier: str = "unknown"
    raw: str = ""


@dataclass
class ProviderEvaluation:
    providerFresh: bool = False
    playerPositionFresh: bool = False
    playerPositionAvailable: bool = False
    classifier: str = "provider-unknown"
    blockers: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)


def utc_now_iso() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat()


def trim_text(value: str, limit: int = 4000) -> str:
    value = value or ""
    return value if len(value) <= limit else value[:limit] + "\n...[truncated]"


def normalize_base_url(value: str) -> str:
    if not value:
        return "http://127.0.0.1:7337/"
    return value if value.endswith("/") else value + "/"


def parse_client_size(value: str | None) -> tuple[int | None, int | None]:
    if not value:
        return None, None
    match = re.search(r"(\d+)x(\d+)", value)
    if not match:
        return None, None
    return int(match.group(1)), int(match.group(2))


def classify_geometry(width: int | None, height: int | None, process_found: bool) -> GeometryStatus:
    status = GeometryStatus(processFound=process_found, clientWidth=width, clientHeight=height)
    if not process_found:
        status.classifier = "rift-process-missing"
        return status
    if width is None or height is None or width <= 0 or height <= 0:
        status.classifier = "rift-geometry-unknown"
        return status

    status.aspectRatio = width / height
    status.isAtLeastMinimum = width >= MIN_CLIENT_WIDTH and height >= MIN_CLIENT_HEIGHT
    status.isSixteenByNine = abs(status.aspectRatio - TARGET_ASPECT_RATIO) <= ASPECT_TOLERANCE
    status.isExactP360C = width == MIN_CLIENT_WIDTH and height == MIN_CLIENT_HEIGHT

    if status.isExactP360C:
        status.classifier = "known-good-p360c"
    elif not status.isAtLeastMinimum:
        status.classifier = "below-minimum-profile"
    elif status.isSixteenByNine:
        status.classifier = "larger-16x9-unproven"
    else:
        status.classifier = "larger-non-16x9-unproven"
    return status


def run_command(args: list[str], timeout: int, cwd: pathlib.Path = REPO_ROOT) -> tuple[CommandEnvelope, subprocess.CompletedProcess[str] | None]:
    started = dt.datetime.now(dt.timezone.utc)
    envelope = CommandEnvelope(args=args, cwd=str(cwd), startedAtUtc=started.isoformat())
    try:
        result = subprocess.run(
            args,
            cwd=str(cwd),
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=timeout,
        )
        ended = dt.datetime.now(dt.timezone.utc)
        envelope.endedAtUtc = ended.isoformat()
        envelope.durationSeconds = (ended - started).total_seconds()
        envelope.exitCode = result.returncode
        envelope.stdoutPreview = trim_text(result.stdout)
        envelope.stderrPreview = trim_text(result.stderr)
        return envelope, result
    except subprocess.TimeoutExpired as ex:
        ended = dt.datetime.now(dt.timezone.utc)
        envelope.endedAtUtc = ended.isoformat()
        envelope.durationSeconds = (ended - started).total_seconds()
        envelope.exitCode = None
        envelope.timedOut = True
        envelope.stdoutPreview = trim_text(ex.stdout if isinstance(ex.stdout, str) else "")
        envelope.stderrPreview = trim_text(ex.stderr if isinstance(ex.stderr, str) else "")
        return envelope, None


def probe_json(base_url: str, path: str, timeout: float = 3.0) -> HttpProbe:
    url = normalize_base_url(base_url).rstrip("/") + path
    try:
        with urllib.request.urlopen(url, timeout=timeout) as response:
            raw = response.read().decode("utf-8", errors="replace")
            try:
                body = json.loads(raw)
            except json.JSONDecodeError:
                body = None
            return HttpProbe(url=url, ok=True, statusCode=response.status, body=body, bodyPreview=trim_text(raw, 2000))
    except urllib.error.HTTPError as ex:
        raw = ex.read().decode("utf-8", errors="replace") if ex.fp else ""
        try:
            body = json.loads(raw) if raw else None
        except json.JSONDecodeError:
            body = None
        return HttpProbe(url=url, ok=False, statusCode=ex.code, body=body, error=str(ex), bodyPreview=trim_text(raw, 2000))
    except Exception as ex:  # noqa: BLE001 - report exact local blocker in summary.
        return HttpProbe(url=url, ok=False, error=str(ex))


def run_readiness(commands: list[CommandEnvelope]) -> GeometryStatus:
    script = REPO_ROOT / "scripts" / "Get-RiftInputReadiness.ps1"
    envelope, result = run_command(
        ["pwsh", "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script)],
        timeout=20,
    )
    commands.append(envelope)
    raw = ""
    if result is not None:
        raw = (result.stdout or "") + (("\nSTDERR:\n" + result.stderr) if result.stderr else "")

    process_found = re.search(r"^Name:\s*rift_x64\s*$", raw, re.MULTILINE | re.IGNORECASE) is not None
    client_match = re.search(r"^ClientRect:\s*[-0-9]+,[-0-9]+\s+(\d+x\d+)\s*$", raw, re.MULTILINE)
    width, height = parse_client_size(client_match.group(1) if client_match else None)
    geometry = classify_geometry(width, height, process_found)
    geometry.raw = raw.strip()

    pid_match = re.search(r"^Pid:\s*(\d+)\s*$", raw, re.MULTILINE)
    title_match = re.search(r"^Title:\s*(.+?)\s*$", raw, re.MULTILINE)
    geometry.pid = int(pid_match.group(1)) if pid_match else None
    geometry.title = title_match.group(1) if title_match else None
    return geometry


def start_stack(commands: list[CommandEnvelope]) -> None:
    script = REPO_ROOT / "scripts" / "Start-ChromaLinkStack.cmd"
    envelope, _ = run_command([str(script)], timeout=20)
    commands.append(envelope)


def prepare_window(commands: list[CommandEnvelope]) -> None:
    script = REPO_ROOT / "scripts" / "Run-ChromaLink.ps1"
    envelope, _ = run_command(
        [
            "pwsh",
            "-NoLogo",
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(script),
            "-Mode",
            "prepare-window",
            "-Argument1",
            "32",
            "-Argument2",
            "32",
        ],
        timeout=45,
    )
    commands.append(envelope)


def resize_client(commands: list[CommandEnvelope], size: str) -> None:
    width, height = parse_client_size(size)
    if width is None or height is None:
        raise ValueError(f"Invalid --resize-client value: {size!r}; expected WIDTHxHEIGHT.")
    script = REPO_ROOT / "scripts" / "Resize-RiftClient-640x360.ps1"
    envelope, _ = run_command(
        [
            "pwsh",
            "-NoLogo",
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(script),
            "-ClientWidth",
            str(width),
            "-ClientHeight",
            str(height),
        ],
        timeout=45,
    )
    commands.append(envelope)


def maximize_window(commands: list[CommandEnvelope]) -> None:
    command = r"""
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class ChromaLinkEnsureWindowTools
{
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@
$process = Get-Process -Name rift_x64 -ErrorAction Stop |
  Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } |
  Select-Object -First 1
if ($null -eq $process) {
  throw "RIFT process with a main window was not found."
}
[void][ChromaLinkEnsureWindowTools]::ShowWindow($process.MainWindowHandle, 3)
Start-Sleep -Milliseconds 500
Write-Host ("Maximized RIFT window: pid={0} title={1}" -f $process.Id, $process.MainWindowTitle)
"""
    envelope, _ = run_command(
        ["pwsh", "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", command],
        timeout=20,
    )
    commands.append(envelope)


def evaluate_provider(health: HttpProbe, world: HttpProbe, geometry: GeometryStatus) -> ProviderEvaluation:
    blockers: list[str] = []
    warnings: list[str] = []

    health_body = health.body if isinstance(health.body, dict) else {}
    world_body = world.body if isinstance(world.body, dict) else {}
    navigation = world_body.get("navigation") if isinstance(world_body.get("navigation"), dict) else {}
    player = world_body.get("player") if isinstance(world_body.get("player"), dict) else {}
    position = player.get("position") if isinstance(player.get("position"), dict) else {}

    if not health.ok:
        blockers.append("bridge-down")
    if not world.ok:
        blockers.append("world-state-unavailable")

    root_fresh = (
        health_body.get("healthy") is True
        and health_body.get("ready") is True
        and health_body.get("fresh") is True
        and health_body.get("stale") is False
        and world_body.get("ok") is True
        and world_body.get("ready") is True
        and world_body.get("fresh") is True
        and world_body.get("stale") is False
    )
    player_position_available = navigation.get("playerPositionAvailable") is True
    player_position_fresh = position.get("fresh") is True and position.get("stale") is False

    if health.ok and not root_fresh:
        blockers.append("provider-stale")
    if world.ok and not player_position_available:
        blockers.append("player-position-missing")
    if world.ok and player_position_available and not player_position_fresh:
        blockers.append("player-position-stale")
    if geometry.classifier == "rift-process-missing":
        blockers.append("rift-process-missing")
    elif geometry.classifier == "below-minimum-profile":
        blockers.append("below-minimum-profile")
    elif geometry.classifier == "rift-geometry-unknown":
        blockers.append("rift-geometry-unknown")

    provider_fresh = root_fresh and player_position_available and player_position_fresh and "below-minimum-profile" not in blockers

    geometry_classifier = geometry.classifier
    if provider_fresh:
        if geometry.classifier == "larger-16x9-unproven":
            geometry_classifier = "larger-16x9-fresh"
        elif geometry.classifier == "larger-non-16x9-unproven":
            geometry_classifier = "larger-non-16x9-fresh"
            warnings.append("Telemetry is fresh at a non-16:9 geometry; keep this accepted by freshness but not promoted as preferred.")
    elif geometry.classifier == "larger-16x9-unproven":
        blockers.append("larger-16x9-unproven")
    elif geometry.classifier == "larger-non-16x9-unproven":
        blockers.append("unsupported-aspect")

    # Preserve order while removing duplicates.
    blockers = list(dict.fromkeys(blockers))
    warnings = list(dict.fromkeys(warnings))
    return ProviderEvaluation(
        providerFresh=provider_fresh,
        playerPositionFresh=player_position_fresh,
        playerPositionAvailable=player_position_available,
        classifier=geometry_classifier if provider_fresh else (blockers[0] if blockers else geometry_classifier),
        blockers=blockers,
        warnings=warnings,
    )


def collect_status(base_url: str, commands: list[CommandEnvelope]) -> dict[str, Any]:
    health = probe_json(base_url, "/health")
    ready = probe_json(base_url, "/ready")
    latest = probe_json(base_url, "/latest-snapshot")
    world = probe_json(base_url, "/api/v1/riftreader/world-state")
    schema = probe_json(base_url, "/api/v1/riftreader/world-state/schema")
    geometry = run_readiness(commands)
    evaluation = evaluate_provider(health, world, geometry)
    return {
        "health": asdict(health),
        "ready": asdict(ready),
        "latestSnapshot": asdict(latest),
        "worldState": asdict(world),
        "schema": asdict(schema),
        "riftWindow": asdict(geometry),
        "evaluation": asdict(evaluation),
    }


def write_artifacts(summary: dict[str, Any], artifact_root: pathlib.Path | None, json_stdout: bool) -> tuple[pathlib.Path, pathlib.Path]:
    timestamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    root = artifact_root or (REPO_ROOT / "artifacts" / "diagnostics")
    out_dir = root / f"chromalink-ensure-fresh-{timestamp}"
    out_dir.mkdir(parents=True, exist_ok=True)
    json_path = out_dir / "summary.json"
    md_path = out_dir / "summary.md"
    summary["artifacts"] = {
        "summaryJson": str(json_path),
        "summaryMarkdown": str(md_path),
    }
    json_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")

    result = summary["result"]
    window = summary["checks"]["riftWindow"]
    player = result.get("playerPosition") or {}
    md_path.write_text(
        "\n".join(
            [
                f"# ChromaLink ensure fresh - {timestamp}",
                "",
                f"Status: **{summary['status']}**",
                "",
                "## Verdict",
                "",
                f"- Classifier: `{result['classifier']}`",
                f"- Blockers: {', '.join(summary['blockers']) if summary['blockers'] else 'none'}",
                f"- Warnings: {', '.join(summary['warnings']) if summary['warnings'] else 'none'}",
                "",
                "## Geometry and freshness",
                "",
                "| Gate | Result |",
                "|---|---|",
                f"| RIFT client | {window.get('clientWidth')}x{window.get('clientHeight')} |",
                f"| Aspect ratio | {window.get('aspectRatio')} |",
                f"| Geometry classifier | `{window.get('classifier')}` |",
                f"| Provider fresh | {result.get('providerFresh')} |",
                f"| Player position fresh | {result.get('playerPositionFresh')} |",
                f"| Player position | x={player.get('x')} y={player.get('y')} z={player.get('z')} ageMs={player.get('ageMs')} |",
                "",
                "## Safety",
                "",
                "- Movement sent: false",
                "- Gameplay input sent: false",
                "- Cheat Engine used: false",
                "- Debugger attached: false",
                "- SavedVariables used as live truth: false",
                "",
                "## Next",
                "",
                summary["next"]["recommendedAction"],
                "",
            ]
        ),
        encoding="utf-8",
    )

    if json_stdout:
        print(json.dumps(summary, indent=2))
    else:
        print(json_path)
        print(md_path)
    return json_path, md_path


def build_summary(args: argparse.Namespace, commands: list[CommandEnvelope], checks: dict[str, Any]) -> dict[str, Any]:
    evaluation = checks["evaluation"]
    health = checks["health"].get("body") or {}
    world = checks["worldState"].get("body") or {}
    player = world.get("player") if isinstance(world.get("player"), dict) else {}
    position = player.get("position") if isinstance(player.get("position"), dict) else {}

    status = "passed" if evaluation["providerFresh"] else "blocked"
    recommended = "ChromaLink is fresh; consumers may use world-state player.position as API-now coordinate truth only."
    if not evaluation["providerFresh"]:
        if "bridge-down" in evaluation["blockers"]:
            recommended = "Run scripts/Ensure-ChromaLinkFresh.cmd --ensure-running --wait-fresh --json."
        elif "below-minimum-profile" in evaluation["blockers"] or "rift-geometry-unknown" in evaluation["blockers"]:
            recommended = "Restore the known fallback with scripts/Ensure-ChromaLinkFresh.cmd --prepare-window --wait-fresh --json."
        elif "larger-16x9-unproven" in evaluation["blockers"]:
            recommended = "Keep the larger 16:9 geometry blocked until /health and player.position become fresh, or restore P360C fallback."
        else:
            recommended = "Inspect summary artifacts, keep movement blocked, and restore/restart the provider watch loop before consumer proof."

    return {
        "status": status,
        "createdAtUtc": utc_now_iso(),
        "repo": str(REPO_ROOT),
        "mode": {
            "statusOnly": args.status,
            "ensureRunning": args.ensure_running,
            "prepareWindow": args.prepare_window,
            "resizeClient": args.resize_client,
            "maximizeWindow": args.maximize_window,
            "waitFresh": args.wait_fresh,
        },
        "blockers": evaluation["blockers"],
        "warnings": evaluation["warnings"],
        "checks": checks,
        "result": {
            "providerFresh": evaluation["providerFresh"],
            "playerPositionFresh": evaluation["playerPositionFresh"],
            "playerPositionAvailable": evaluation["playerPositionAvailable"],
            "classifier": evaluation["classifier"],
            "playerPosition": {
                "x": position.get("x"),
                "y": position.get("y"),
                "z": position.get("z"),
                "ageMs": position.get("ageMs"),
                "observedAtUtc": position.get("observedAtUtc"),
            }
            if isinstance(position, dict)
            else None,
            "health": {
                "healthy": health.get("healthy"),
                "ready": health.get("ready"),
                "fresh": health.get("fresh"),
                "stale": health.get("stale"),
                "snapshotAgeSeconds": health.get("snapshotAgeSeconds"),
            },
            "navigation": world.get("navigation"),
        },
        "commands": [asdict(command) for command in commands],
        "actions": {
            "startedBridge": args.ensure_running,
            "startedWatch": args.ensure_running,
            "preparedWindow": args.prepare_window,
            "resizedClient": args.resize_client,
            "maximizedWindow": args.maximize_window,
            "sentGameInput": False,
            "sentMovementInput": False,
        },
        "safety": {
            "movementSent": False,
            "gameplayInputSent": False,
            "cheatEngineUsed": False,
            "debuggerAttached": False,
            "savedVariablesUsedAsLiveTruth": False,
            "gitMutation": False,
        },
        "next": {
            "recommendedAction": recommended,
        },
    }


def run_self_test() -> int:
    cases = [
        (640, 360, True, "known-good-p360c"),
        (1280, 720, True, "larger-16x9-unproven"),
        (1920, 1080, True, "larger-16x9-unproven"),
        (800, 600, True, "larger-non-16x9-unproven"),
        (320, 180, True, "below-minimum-profile"),
        (None, None, False, "rift-process-missing"),
    ]
    failures = []
    for width, height, process_found, expected in cases:
        actual = classify_geometry(width, height, process_found).classifier
        if actual != expected:
            failures.append(f"{width}x{height} process={process_found}: expected {expected}, got {actual}")

    fresh_health = HttpProbe(
        url="health",
        ok=True,
        statusCode=200,
        body={"healthy": True, "ready": True, "fresh": True, "stale": False},
    )
    fresh_world = HttpProbe(
        url="world",
        ok=True,
        statusCode=200,
        body={
            "ok": True,
            "ready": True,
            "fresh": True,
            "stale": False,
            "navigation": {"playerPositionAvailable": True},
            "player": {"position": {"fresh": True, "stale": False, "x": 1, "y": 2, "z": 3}},
        },
    )
    larger = classify_geometry(1280, 720, True)
    evaluated = evaluate_provider(fresh_health, fresh_world, larger)
    if not evaluated.providerFresh or evaluated.classifier != "larger-16x9-fresh":
        failures.append(f"fresh 1280x720 should be larger-16x9-fresh, got {evaluated}")

    stale_world = HttpProbe(
        url="world",
        ok=True,
        statusCode=200,
        body={
            "ok": True,
            "ready": True,
            "fresh": True,
            "stale": False,
            "navigation": {"playerPositionAvailable": True},
            "player": {"position": {"fresh": False, "stale": True, "x": 1, "y": 2, "z": 3}},
        },
    )
    stale_eval = evaluate_provider(fresh_health, stale_world, larger)
    if stale_eval.providerFresh or "player-position-stale" not in stale_eval.blockers or "larger-16x9-unproven" not in stale_eval.blockers:
        failures.append(f"stale 1280x720 should be blocked by stale position and unproven geometry, got {stale_eval}")

    if failures:
        print(json.dumps({"status": "failed", "failures": failures}, indent=2))
        return 1

    print(json.dumps({"status": "passed", "cases": len(cases) + 2}, indent=2))
    return 0


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Diagnose/restore fresh ChromaLink provider telemetry.")
    parser.add_argument("--status", action="store_true", help="Read-only status check. This is the default when no action flags are set.")
    parser.add_argument("--ensure-running", action="store_true", help="Start the existing ChromaLink bridge/watch stack before checking freshness.")
    parser.add_argument("--prepare-window", action="store_true", help="Restore the known 640x360 P360C fallback window before checking freshness.")
    parser.add_argument("--resize-client", metavar="WIDTHxHEIGHT", help="Resize the RIFT client area for an explicit geometry proof, e.g. 1280x720.")
    parser.add_argument("--maximize-window", action="store_true", help="Maximize the RIFT window for a maximized-geometry freshness proof.")
    parser.add_argument("--wait-fresh", action="store_true", help="Poll until provider/player-position freshness passes or times out.")
    parser.add_argument("--timeout-seconds", type=float, default=30.0, help="Maximum wait time for --wait-fresh.")
    parser.add_argument("--poll-seconds", type=float, default=1.0, help="Polling interval for --wait-fresh.")
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL, help="ChromaLink HTTP bridge base URL.")
    parser.add_argument("--artifact-root", type=pathlib.Path, help="Override summary artifact root.")
    parser.add_argument("--json", action="store_true", help="Print full JSON summary to stdout.")
    parser.add_argument("--self-test", action="store_true", help="Run offline classifier self-tests and exit.")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    if args.self_test:
        return run_self_test()

    if not any([args.status, args.ensure_running, args.prepare_window, args.resize_client, args.maximize_window, args.wait_fresh]):
        args.status = True

    commands: list[CommandEnvelope] = []
    if args.ensure_running:
        start_stack(commands)
        time.sleep(2)
    if args.prepare_window:
        prepare_window(commands)
        time.sleep(1)
    if args.resize_client:
        resize_client(commands, args.resize_client)
        time.sleep(1)
    if args.maximize_window:
        maximize_window(commands)
        time.sleep(1)

    checks = collect_status(args.base_url, commands)
    if args.wait_fresh and not checks["evaluation"]["providerFresh"]:
        deadline = time.monotonic() + max(0.0, args.timeout_seconds)
        while time.monotonic() < deadline:
            time.sleep(max(0.1, args.poll_seconds))
            checks = collect_status(args.base_url, commands)
            if checks["evaluation"]["providerFresh"]:
                break

    summary = build_summary(args, commands, checks)
    write_artifacts(summary, args.artifact_root, args.json)
    return 0 if summary["status"] == "passed" else 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
