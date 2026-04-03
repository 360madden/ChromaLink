using System.Diagnostics;
using System.Text;
using ChromaLink.Reader;

return new CliApp().Run(args);

internal sealed class CliApp
{
    private readonly StripProfile _profile = StripProfiles.Default;
    private static readonly CaptureBackend[] DefaultCaptureBackends =
    {
        CaptureBackend.PrintWindow,
        CaptureBackend.DesktopDuplication,
        CaptureBackend.ScreenBitBlt
    };

    public int Run(string[] args)
    {
        var command = args.Length == 0 ? "smoke" : args[0].ToLowerInvariant();
        return command switch
        {
            "smoke" => RunSmoke(),
            "replay" => RunReplay(args),
            "live" => RunLive(args, watchMode: false),
            "watch" => RunLive(args, watchMode: true),
            "bench" => RunBench(),
            "validate" => RunValidate(),
            "capture-dump" => RunCaptureDump(args),
            "prepare-window" => RunPrepareWindow(args),
            "help" or "--help" or "-h" => PrintUsage(),
            _ => Fail($"Unsupported command: {command}")
        };
    }

    private int RunSmoke()
    {
        var snapshot = CoreStatusSnapshot.CreateSynthetic();
        var bytes = FrameProtocol.BuildCoreFrameBytes(_profile.NumericId, 10, snapshot);
        var image = ColorStripRenderer.Render(_profile, bytes);
        var fixtureRoot = PathProvider.EnsureFixtureDirectory();
        var path = Path.Combine(fixtureRoot, "chromalink-color-core.bmp");
        BmpIO.Save(path, image);

        var validation = ColorStripAnalyzer.Analyze(image, _profile);
        Console.WriteLine("ChromaLink");
        Console.WriteLine("Mode: smoke");
        Console.WriteLine($"Success: {validation.IsAccepted.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Fixture: {path}");
        Console.WriteLine($"Reason: {validation.Reason}");
        if (validation.Frame is not null)
        {
            WriteFrameSummary(validation.Frame);
        }

        return validation.IsAccepted ? 0 : 1;
    }

    private int RunReplay(string[] args)
    {
        if (args.Length < 2)
        {
            return Fail("replay requires a BMP path.");
        }

        var image = BmpIO.Load(args[1]);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);
        Console.WriteLine("ChromaLink");
        Console.WriteLine("Mode: replay");
        Console.WriteLine($"Accepted: {validation.IsAccepted.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Reason: {validation.Reason}");
        WriteDetectionSummary(validation.Detection);
        if (validation.Frame is not null)
        {
            WriteFrameSummary(validation.Frame);
        }

        return validation.IsAccepted ? 0 : 1;
    }

    private int RunCaptureDump(string[] args)
    {
        if (!TryParseCaptureInvocation(args, 1, out var invocation, out var error))
        {
            return Fail(error);
        }

        var hwnd = WindowCaptureService.FindRiftWindow();
        if (hwnd == nint.Zero)
        {
            return Fail("No likely RIFT window was found. Expected a rift or rift_x64 game process.");
        }

        var attempts = CaptureAndAnalyze(hwnd, invocation.Backends);
        var selectedAttempt = ChooseBestAttempt(attempts);
        if (selectedAttempt is null)
        {
            return Fail("capture-dump failed: no capture backend produced an image.");
        }

        var capture = selectedAttempt.Capture!;
        var path = Path.Combine(PathProvider.EnsureOutDirectory(), "chromalink-color-capture-dump.bmp");
        BmpIO.Save(path, capture.Image);
        DiagnosticsArtifacts.WriteArtifacts(
            path,
            _profile,
            capture,
            selectedAttempt.Validation!,
            attempts.Select(static attempt =>
                $"{attempt.Backend} | {(attempt.Validation?.IsAccepted == true ? "accepted" : "rejected")} | {attempt.Validation?.Reason ?? attempt.FailureReason ?? "capture failed"} | AvgLuma {(attempt.Signal?.AverageLuma ?? 0):F2} | LumaRange {(attempt.Signal?.LumaRange ?? 0):F2}")
                .ToArray());

        Console.WriteLine("ChromaLink");
        Console.WriteLine("Mode: capture-dump");
        Console.WriteLine($"Path: {path}");
        Console.WriteLine($"Backend: {capture.Backend}");
        Console.WriteLine($"CaptureRect: {capture.CaptureLeft},{capture.CaptureTop} {capture.CaptureWidth}x{capture.CaptureHeight}");
        Console.WriteLine($"Accepted: {selectedAttempt.Validation!.IsAccepted.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Reason: {selectedAttempt.Validation.Reason}");
        Console.WriteLine($"AverageLuma: {(selectedAttempt.Signal?.AverageLuma ?? 0):F2}");
        Console.WriteLine($"LumaRange: {(selectedAttempt.Signal?.LumaRange ?? 0):F2}");
        foreach (var attempt in attempts)
        {
            Console.WriteLine(
                $"Attempt: {attempt.Backend} | {(attempt.Validation?.IsAccepted == true ? "accepted" : "rejected")} | {attempt.Validation?.Reason ?? attempt.FailureReason ?? "capture failed"} | AvgLuma {(attempt.Signal?.AverageLuma ?? 0):F2} | LumaRange {(attempt.Signal?.LumaRange ?? 0):F2}");
        }

        WriteDetectionSummary(selectedAttempt.Validation.Detection);
        if (selectedAttempt.Validation.Frame is not null)
        {
            WriteFrameSummary(selectedAttempt.Validation.Frame);
        }

        return 0;
    }

    private int RunPrepareWindow(string[] args)
    {
        var hwnd = WindowCaptureService.FindRiftWindow();
        if (hwnd == nint.Zero)
        {
            return Fail("No likely RIFT window was found. Expected a rift or rift_x64 game process.");
        }

        var left = args.Length >= 2 ? int.Parse(args[1]) : 32;
        var top = args.Length >= 3 ? int.Parse(args[2]) : 32;
        var result = WindowControlService.EnsureClientSize(hwnd, _profile.WindowWidth, _profile.WindowHeight, left, top);

        Console.WriteLine("ChromaLink");
        Console.WriteLine("Mode: prepare-window");
        Console.WriteLine($"RequestedClient: {_profile.WindowWidth}x{_profile.WindowHeight}");
        Console.WriteLine($"BeforeWindow: {result.Before.WindowLeft},{result.Before.WindowTop} {result.Before.WindowWidth}x{result.Before.WindowHeight}");
        Console.WriteLine($"BeforeClient: {result.Before.ClientLeft},{result.Before.ClientTop} {result.Before.ClientWidth}x{result.Before.ClientHeight}");
        Console.WriteLine($"BeforeMinimized: {result.Before.IsMinimized.ToString().ToLowerInvariant()}");
        Console.WriteLine($"AfterWindow: {result.After.WindowLeft},{result.After.WindowTop} {result.After.WindowWidth}x{result.After.WindowHeight}");
        Console.WriteLine($"AfterClient: {result.After.ClientLeft},{result.After.ClientTop} {result.After.ClientWidth}x{result.After.ClientHeight}");
        Console.WriteLine($"Success: {result.Succeeded.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Reason: {result.Reason}");
        return result.Succeeded ? 0 : 1;
    }

    private int RunBench()
    {
        var bytes = FrameProtocol.BuildCoreFrameBytes(_profile.NumericId, 42, CoreStatusSnapshot.CreateSynthetic());
        var scenarios = new[]
        {
            new PerturbationOptions("baseline"),
            new PerturbationOptions("offset-2px", OffsetX: 2),
            new PerturbationOptions("blur-1", BlurRadius: 1),
            new PerturbationOptions("gain-plus10", RedGain: 1.1, GreenGain: 1.1, BlueGain: 1.1),
            new PerturbationOptions("gain-minus10", RedGain: 0.9, GreenGain: 0.9, BlueGain: 0.9),
            new PerturbationOptions("gamma-0.9", Gamma: 0.9),
            new PerturbationOptions("gamma-1.1", Gamma: 1.1),
            new PerturbationOptions("scale-1.02", Scale: 1.02)
        };

        var results = ReplayRunner.Run(_profile, bytes, scenarios);
        Console.WriteLine("ChromaLink");
        Console.WriteLine("Mode: bench");
        foreach (var result in results)
        {
            Console.WriteLine($"{result.Scenario.Name}: {(result.Result.IsAccepted ? "accepted" : "rejected")} | {result.Result.Reason} | {result.LocateDecodeMs:F2} ms");
        }

        Console.WriteLine($"AverageReplayDecodeMs: {results.Average(static result => result.LocateDecodeMs):F2}");
        Console.WriteLine($"P95ReplayDecodeMs: {Percentile(results.Select(static result => result.LocateDecodeMs).ToArray(), 0.95):F2}");
        return results.All(static result => result.Result.IsAccepted) ? 0 : 1;
    }

    private int RunValidate()
    {
        Console.WriteLine("ChromaLink");
        Console.WriteLine("Mode: validate");

        var smokeExitCode = RunSmoke();

        var fixturePath = Path.Combine(PathProvider.EnsureFixtureDirectory(), "chromalink-color-core.bmp");
        var replayExitCode = File.Exists(fixturePath)
            ? RunReplay(new[] { "replay", fixturePath })
            : Fail($"Replay fixture not found after smoke: {fixturePath}");
        var benchExitCode = RunBench();

        var smokePassed = smokeExitCode == 0;
        var replayPassed = replayExitCode == 0;
        var benchPassed = benchExitCode == 0;

        Console.WriteLine("ValidateSummary:");
        Console.WriteLine($"Smoke: {(smokePassed ? "passed" : "failed")}");
        Console.WriteLine($"Replay: {(replayPassed ? "passed" : "failed")} ({fixturePath})");
        Console.WriteLine($"Bench: {(benchPassed ? "passed" : "failed")}");

        return smokePassed && replayPassed && benchPassed ? 0 : 1;
    }

    private int RunLive(string[] args, bool watchMode)
    {
        if (!TryParseCaptureInvocation(args, 1, out var invocation, out var error))
        {
            return Fail(error);
        }

        var hwnd = WindowCaptureService.FindRiftWindow();
        if (hwnd == nint.Zero)
        {
            return Fail("No likely RIFT window was found. Expected a rift or rift_x64 game process.");
        }

        var iterationLimit = watchMode
            ? int.MaxValue
            : (invocation.Positionals.Count >= 1 ? Math.Max(1, int.Parse(invocation.Positionals[0])) : 5);
        var sleepMs = invocation.Positionals.Count >= 2 ? Math.Max(0, int.Parse(invocation.Positionals[1])) : 100;
        var durationMs = watchMode && invocation.Positionals.Count >= 1
            ? Math.Max(1, int.Parse(invocation.Positionals[0])) * 1000
            : int.MaxValue;
        var metrics = new LiveMetrics();
        var aggregate = new TelemetryAggregate();
        var started = Stopwatch.StartNew();
        FrameValidationResult? lastValidation = null;
        CaptureBackend? lastBackend = null;
        string? firstRejectPath = null;
        string? telemetrySnapshotPath = null;

        for (var iteration = 0; iteration < iterationLimit && started.ElapsedMilliseconds < durationMs; iteration++)
        {
            var attempts = CaptureAndAnalyzePreferred(hwnd, invocation.Backends, lastBackend);
            var bestAttempt = ChooseBestAttempt(attempts);
            if (bestAttempt is not null)
            {
                var primaryValidation = bestAttempt.Validation!;
                foreach (var validation in bestAttempt.Validations)
                {
                    metrics.Add(
                        validation.IsAccepted,
                        bestAttempt.CaptureElapsedMs,
                        bestAttempt.DecodeElapsedMs,
                        validation.Reason,
                        validation.Frame);
                    if (validation.IsAccepted && validation.Frame is not null)
                    {
                        aggregate.Update(validation.Frame);
                    }
                }
                lastValidation = primaryValidation;
                lastBackend = bestAttempt.Backend;

                if (!primaryValidation.IsAccepted && firstRejectPath is null)
                {
                    firstRejectPath = Path.Combine(PathProvider.EnsureOutDirectory(), "chromalink-color-first-reject.bmp");
                    BmpIO.Save(firstRejectPath, bestAttempt.Capture!.Image);
                    DiagnosticsArtifacts.WriteArtifacts(
                        firstRejectPath,
                        _profile,
                        bestAttempt.Capture,
                        primaryValidation,
                        new[]
                        {
                            $"{bestAttempt.Backend} | {(primaryValidation.IsAccepted ? "accepted" : "rejected")} | {primaryValidation.Reason} | AvgLuma {(bestAttempt.Signal?.AverageLuma ?? 0):F2} | LumaRange {(bestAttempt.Signal?.LumaRange ?? 0):F2}"
                        });
                }
            }
            else
            {
                metrics.Add(false, 0, 0, "No capture backend produced an image.");
                lastValidation = new FrameValidationResult(false, "No capture backend produced an image.", null, Array.Empty<SegmentSample>(), null, null);
            }

            telemetrySnapshotPath = TelemetrySnapshotWriter.WriteLatest(aggregate.Snapshot(), metrics, lastBackend, lastValidation);

            if (sleepMs > 0)
            {
                Thread.Sleep(sleepMs);
            }
        }

        Console.WriteLine("ChromaLink");
        Console.WriteLine($"Mode: {(watchMode ? "watch" : "live")}");
        Console.WriteLine($"AcceptedSamples: {metrics.AcceptedCount}");
        Console.WriteLine($"RejectedSamples: {metrics.RejectedCount}");
        Console.WriteLine($"AverageCaptureMs: {metrics.AverageCaptureMs:F2}");
        Console.WriteLine($"AverageDecodeMs: {metrics.AverageDecodeMs:F2}");
        Console.WriteLine($"MedianDecodeMs: {metrics.MedianDecodeMs:F2}");
        Console.WriteLine($"P95DecodeMs: {metrics.P95DecodeMs:F2}");
        Console.WriteLine($"LastReason: {metrics.LastReason}");
        if (metrics.FrameTypeCounts.Count > 0)
        {
            foreach (var entry in metrics.FrameTypeCounts.OrderBy(static entry => entry.Key))
            {
                Console.WriteLine($"FrameCount[{entry.Key}]: {entry.Value}");
            }
        }
        if (lastBackend is not null)
        {
            Console.WriteLine($"LastBackend: {lastBackend}");
        }
        WriteAggregateSummary(aggregate.Snapshot());
        WriteDetectionSummary(lastValidation?.Detection);
        if (lastValidation?.Frame is not null)
        {
            WriteFrameSummary(lastValidation.Frame);
        }

        if (firstRejectPath is not null)
        {
            Console.WriteLine($"FirstRejectBmp: {firstRejectPath}");
        }
        if (telemetrySnapshotPath is not null)
        {
            Console.WriteLine($"TelemetryContract: {TelemetrySnapshotWriter.ContractName}/v{TelemetrySnapshotWriter.ContractSchemaVersion}");
            Console.WriteLine($"TelemetrySnapshot: {telemetrySnapshotPath}");
        }

        return metrics.AcceptedCount > 0 ? 0 : 1;
    }

    private static void WriteDetectionSummary(DetectionResult? detection)
    {
        if (detection is null)
        {
            return;
        }

        Console.WriteLine($"Origin: {detection.OriginX},{detection.OriginY}");
        Console.WriteLine($"Pitch: {detection.Pitch:F3}");
        Console.WriteLine($"Scale: {detection.Scale:F3}");
        Console.WriteLine($"ControlError: {detection.ControlError:F4}");
        Console.WriteLine($"LeftControlScore: {detection.LeftControlScore:F4}");
        Console.WriteLine($"RightControlScore: {detection.RightControlScore:F4}");
        Console.WriteLine($"AnchorLumaDelta: {detection.AnchorLumaDelta:F2}");
        Console.WriteLine($"SearchMode: {detection.SearchMode}");
    }

    private static void WriteFrameSummary(TelemetryFrame frame)
    {
        Console.WriteLine($"FrameType: {frame.Header.FrameType}");
        Console.WriteLine($"Schema: {frame.Header.SchemaId}");
        Console.WriteLine($"Sequence: {frame.Header.Sequence}");
        Console.WriteLine($"ReservedFlags: 0x{frame.Header.ReservedFlags:X2} ({DescribeHeaderFlags((HeaderCapabilityFlags)frame.Header.ReservedFlags)})");

        switch (frame)
        {
            case CoreStatusFrame core:
                Console.WriteLine($"PlayerFlags: {core.Payload.PlayerStateFlags}");
                Console.WriteLine($"PlayerHealthPctQ8: {core.Payload.PlayerHealthPctQ8}");
                Console.WriteLine($"PlayerResourceKind: {core.Payload.PlayerResourceKind}");
                Console.WriteLine($"PlayerResourcePctQ8: {core.Payload.PlayerResourcePctQ8}");
                Console.WriteLine($"TargetFlags: {core.Payload.TargetStateFlags}");
                Console.WriteLine($"TargetHealthPctQ8: {core.Payload.TargetHealthPctQ8}");
                Console.WriteLine($"TargetResourceKind: {core.Payload.TargetResourceKind}");
                Console.WriteLine($"TargetResourcePctQ8: {core.Payload.TargetResourcePctQ8}");
                Console.WriteLine($"PlayerLevel: {core.Payload.PlayerLevel}");
                Console.WriteLine($"TargetLevel: {core.Payload.TargetLevel}");
                Console.WriteLine($"PlayerCalling: {core.Payload.PlayerCallingRolePacked >> 4}");
                Console.WriteLine($"PlayerRole: {core.Payload.PlayerCallingRolePacked & 0x0F}");
                Console.WriteLine($"TargetCalling: {core.Payload.TargetCallingRelationPacked >> 4}");
                Console.WriteLine($"TargetRelation: {core.Payload.TargetCallingRelationPacked & 0x0F}");
                break;

            case PlayerVitalsFrame vitals:
                Console.WriteLine($"HealthCurrent: {vitals.Payload.HealthCurrent}");
                Console.WriteLine($"HealthMax: {vitals.Payload.HealthMax}");
                Console.WriteLine($"ResourceCurrent: {vitals.Payload.ResourceCurrent}");
                Console.WriteLine($"ResourceMax: {vitals.Payload.ResourceMax}");
                break;

            case PlayerPositionFrame position:
                Console.WriteLine($"PositionX: {position.Payload.X:F2}");
                Console.WriteLine($"PositionY: {position.Payload.Y:F2}");
                Console.WriteLine($"PositionZ: {position.Payload.Z:F2}");
                break;

            case PlayerCastFrame cast:
                Console.WriteLine($"CastFlags: {cast.Payload.CastFlags}");
                Console.WriteLine($"CastActive: {((cast.Payload.CastFlags & 0x01) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"CastChanneled: {((cast.Payload.CastFlags & 0x02) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"CastUninterruptible: {((cast.Payload.CastFlags & 0x04) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"CastHasTarget: {((cast.Payload.CastFlags & 0x10) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"SpellLabel: {FormatSpellLabel(cast.Payload.SpellLabel)}");
                Console.WriteLine($"ProgressPctQ8: {cast.Payload.ProgressPctQ8}");
                Console.WriteLine($"DurationSeconds: {cast.Payload.DurationCenti / 100.0:F2}");
                Console.WriteLine($"RemainingSeconds: {cast.Payload.RemainingCenti / 100.0:F2}");
                Console.WriteLine($"CastTargetCode: {cast.Payload.CastTargetCode}");
                break;

            case PlayerResourcesFrame resources:
                Console.WriteLine($"Mana: {resources.Payload.ManaCurrent}/{resources.Payload.ManaMax}");
                Console.WriteLine($"Energy: {resources.Payload.EnergyCurrent}/{resources.Payload.EnergyMax}");
                Console.WriteLine($"Power: {resources.Payload.PowerCurrent}/{resources.Payload.PowerMax}");
                break;

            case PlayerCombatFrame combat:
                Console.WriteLine($"CombatFlags: {combat.Payload.CombatFlags}");
                Console.WriteLine($"HasCombo: {((combat.Payload.CombatFlags & 0x01) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"HasCharge: {((combat.Payload.CombatFlags & 0x02) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"HasPlanar: {((combat.Payload.CombatFlags & 0x04) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"HasAbsorb: {((combat.Payload.CombatFlags & 0x08) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"PvP: {((combat.Payload.CombatFlags & 0x10) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"Mentoring: {((combat.Payload.CombatFlags & 0x20) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"Ready: {((combat.Payload.CombatFlags & 0x40) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"Afk: {((combat.Payload.CombatFlags & 0x80) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"Combo: {combat.Payload.Combo}");
                Console.WriteLine($"Charge: {combat.Payload.ChargeCurrent}/{combat.Payload.ChargeMax}");
                Console.WriteLine($"Planar: {combat.Payload.PlanarCurrent}/{combat.Payload.PlanarMax}");
                Console.WriteLine($"Absorb: {combat.Payload.Absorb}");
                break;

            case TargetPositionFrame targetPosition:
                Console.WriteLine($"TargetPositionX: {targetPosition.Payload.X:F2}");
                Console.WriteLine($"TargetPositionY: {targetPosition.Payload.Y:F2}");
                Console.WriteLine($"TargetPositionZ: {targetPosition.Payload.Z:F2}");
                break;

            case FollowUnitStatusFrame follow:
                Console.WriteLine($"FollowSlot: {follow.Payload.Slot}");
                Console.WriteLine($"FollowFlags: {follow.Payload.FollowFlags}");
                Console.WriteLine($"FollowPresent: {((follow.Payload.FollowFlags & 0x01) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"FollowAlive: {((follow.Payload.FollowFlags & 0x02) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"FollowCombat: {((follow.Payload.FollowFlags & 0x04) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"FollowAfk: {((follow.Payload.FollowFlags & 0x08) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"FollowOffline: {((follow.Payload.FollowFlags & 0x10) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"FollowAggro: {((follow.Payload.FollowFlags & 0x20) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"FollowBlocked: {((follow.Payload.FollowFlags & 0x40) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"FollowReady: {((follow.Payload.FollowFlags & 0x80) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"FollowPosition: {follow.Payload.X:F1},{follow.Payload.Y:F1},{follow.Payload.Z:F1}");
                Console.WriteLine($"FollowHealthPctQ8: {follow.Payload.HealthPctQ8}");
                Console.WriteLine($"FollowResourcePctQ8: {follow.Payload.ResourcePctQ8}");
                Console.WriteLine($"FollowLevel: {follow.Payload.Level}");
                Console.WriteLine($"FollowCalling: {follow.Payload.CallingRolePacked >> 4}");
                Console.WriteLine($"FollowRole: {follow.Payload.CallingRolePacked & 0x0F}");
                break;

            case TargetVitalsFrame targetVitals:
                Console.WriteLine($"TargetHealthCurrent: {targetVitals.Payload.HealthCurrent}");
                Console.WriteLine($"TargetHealthMax: {targetVitals.Payload.HealthMax}");
                Console.WriteLine($"TargetAbsorb: {targetVitals.Payload.Absorb}");
                Console.WriteLine($"TargetPresent: {((targetVitals.Payload.TargetFlags & 0x01) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"TargetAlive: {((targetVitals.Payload.TargetFlags & 0x02) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"TargetCombat: {((targetVitals.Payload.TargetFlags & 0x04) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"TargetTagged: {((targetVitals.Payload.TargetFlags & 0x08) != 0).ToString().ToLowerInvariant()}");
                Console.WriteLine($"TargetLevel: {targetVitals.Payload.TargetLevel}");
                break;

            case TargetResourcesFrame targetResources:
                Console.WriteLine($"TargetMana: {targetResources.Payload.ManaCurrent}/{targetResources.Payload.ManaMax}");
                Console.WriteLine($"TargetEnergy: {targetResources.Payload.EnergyCurrent}/{targetResources.Payload.EnergyMax}");
                Console.WriteLine($"TargetPower: {targetResources.Payload.PowerCurrent}/{targetResources.Payload.PowerMax}");
                break;

            case AuxUnitCastFrame auxUnitCast:
                Console.WriteLine($"UnitSelectorCode: {auxUnitCast.Payload.UnitSelectorCode}");
                Console.WriteLine($"CastFlags: {auxUnitCast.Payload.CastFlags}");
                Console.WriteLine($"ProgressPctQ8: {auxUnitCast.Payload.ProgressPctQ8}");
                Console.WriteLine($"DurationSeconds: {auxUnitCast.Payload.DurationCenti / 100.0:F2}");
                Console.WriteLine($"RemainingSeconds: {auxUnitCast.Payload.RemainingCenti / 100.0:F2}");
                Console.WriteLine($"CastTargetCode: {auxUnitCast.Payload.CastTargetCode}");
                Console.WriteLine($"Label: {FormatSpellLabel(auxUnitCast.Payload.Label)}");
                break;

            case AuraPageFrame auraPage:
                Console.WriteLine($"PageKindCode: {auraPage.Payload.PageKindCode}");
                Console.WriteLine($"TotalAuraCount: {auraPage.Payload.TotalAuraCount}");
                WriteAuraEntrySummary("Entry1", auraPage.Payload.Entry1);
                WriteAuraEntrySummary("Entry2", auraPage.Payload.Entry2);
                break;

            case TextPageFrame textPage:
                Console.WriteLine($"TextKindCode: {textPage.Payload.TextKindCode}");
                Console.WriteLine($"TextHash16: 0x{textPage.Payload.TextHash16:X4}");
                Console.WriteLine($"Label: {FormatSpellLabel(textPage.Payload.Label)}");
                break;

            case AbilityWatchFrame abilityWatch:
                Console.WriteLine($"PageIndex: {abilityWatch.Payload.PageIndex}");
                WriteAbilityEntrySummary("Entry1", abilityWatch.Payload.Entry1);
                WriteAbilityEntrySummary("Entry2", abilityWatch.Payload.Entry2);
                Console.WriteLine($"ShortestCooldownQ4: {abilityWatch.Payload.ShortestCooldownQ4}");
                Console.WriteLine($"ReadyCount: {abilityWatch.Payload.ReadyCount}");
                Console.WriteLine($"CoolingCount: {abilityWatch.Payload.CoolingCount}");
                break;
        }
    }

    private static void WriteAggregateSummary(TelemetryAggregateSnapshot snapshot)
    {
        if (!snapshot.HasAny)
        {
            return;
        }

        Console.WriteLine($"AggregateAcceptedFrames: {snapshot.AcceptedFrames}");
        Console.WriteLine($"AggregateReady: {snapshot.HasCompleteState.ToString().ToLowerInvariant()}");

        var nowUtc = snapshot.LastUpdatedUtc ?? DateTimeOffset.UtcNow;
        WriteAggregateObservation(
            "AggregateCoreStatus",
            snapshot.CoreStatus,
            nowUtc,
            observation =>
                $"seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} hpPctQ8={observation.Frame.Payload.PlayerHealthPctQ8} targetHpPctQ8={observation.Frame.Payload.TargetHealthPctQ8}");
        WriteAggregateObservation(
            "AggregatePlayerVitals",
            snapshot.PlayerVitals,
            nowUtc,
            observation =>
                $"seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} health={observation.Frame.Payload.HealthCurrent}/{observation.Frame.Payload.HealthMax} resource={observation.Frame.Payload.ResourceCurrent}/{observation.Frame.Payload.ResourceMax}");
        WriteAggregateObservation(
            "AggregatePlayerPosition",
            snapshot.PlayerPosition,
            nowUtc,
            observation =>
                $"seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} pos={observation.Frame.Payload.X:F2},{observation.Frame.Payload.Y:F2},{observation.Frame.Payload.Z:F2}");
        WriteAggregateObservation(
            "AggregatePlayerCast",
            snapshot.PlayerCast,
            nowUtc,
            observation =>
                $"seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} active={((observation.Frame.Payload.CastFlags & 0x01) != 0).ToString().ToLowerInvariant()} spell={FormatSpellLabel(observation.Frame.Payload.SpellLabel)} target={observation.Frame.Payload.CastTargetCode} progressQ8={observation.Frame.Payload.ProgressPctQ8} remaining={observation.Frame.Payload.RemainingCenti / 100.0:F2}s");
        WriteAggregateObservation(
            "AggregatePlayerResources",
            snapshot.PlayerResources,
            nowUtc,
            observation =>
                $"seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} mana={observation.Frame.Payload.ManaCurrent}/{observation.Frame.Payload.ManaMax} energy={observation.Frame.Payload.EnergyCurrent}/{observation.Frame.Payload.EnergyMax} power={observation.Frame.Payload.PowerCurrent}/{observation.Frame.Payload.PowerMax}");
        WriteAggregateObservation(
            "AggregatePlayerCombat",
            snapshot.PlayerCombat,
            nowUtc,
            observation =>
                $"seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} combo={observation.Frame.Payload.Combo} charge={observation.Frame.Payload.ChargeCurrent}/{observation.Frame.Payload.ChargeMax} planar={observation.Frame.Payload.PlanarCurrent}/{observation.Frame.Payload.PlanarMax} absorb={observation.Frame.Payload.Absorb}");
        WriteAggregateObservation(
            "AggregateTargetPosition",
            snapshot.TargetPosition,
            nowUtc,
            observation =>
                $"seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} pos={observation.Frame.Payload.X:F2},{observation.Frame.Payload.Y:F2},{observation.Frame.Payload.Z:F2}");
        WriteAggregateObservation(
            "AggregateFollowUnitStatus",
            snapshot.FollowUnitStatus,
            nowUtc,
            observation =>
                $"seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} slot={observation.Frame.Payload.Slot} flags=0x{observation.Frame.Payload.FollowFlags:X2} pos={observation.Frame.Payload.X:F1},{observation.Frame.Payload.Y:F1},{observation.Frame.Payload.Z:F1} hpPctQ8={observation.Frame.Payload.HealthPctQ8}");
        foreach (var entry in snapshot.FollowUnitStatusesBySlot.OrderBy(static entry => entry.Key))
        {
            var observation = entry.Value;
            Console.WriteLine(
                $"AggregateFollowUnitStatus[{entry.Key}]: seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} flags=0x{observation.Frame.Payload.FollowFlags:X2} pos={observation.Frame.Payload.X:F1},{observation.Frame.Payload.Y:F1},{observation.Frame.Payload.Z:F1}");
        }
        WriteAggregateObservation(
            "AggregateTargetVitals",
            snapshot.TargetVitals,
            nowUtc,
            observation =>
                $"seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} hp={observation.Frame.Payload.HealthCurrent}/{observation.Frame.Payload.HealthMax} absorb={observation.Frame.Payload.Absorb} targetFlags=0x{observation.Frame.Payload.TargetFlags:X2} level={observation.Frame.Payload.TargetLevel}");
        WriteAggregateObservation(
            "AggregateTargetResources",
            snapshot.TargetResources,
            nowUtc,
            observation =>
                $"seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} mana={observation.Frame.Payload.ManaCurrent}/{observation.Frame.Payload.ManaMax} energy={observation.Frame.Payload.EnergyCurrent}/{observation.Frame.Payload.EnergyMax} power={observation.Frame.Payload.PowerCurrent}/{observation.Frame.Payload.PowerMax}");
        WriteAggregateObservation(
            "AggregateAuxUnitCast",
            snapshot.AuxUnitCast,
            nowUtc,
            observation =>
                $"seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} unit={observation.Frame.Payload.UnitSelectorCode} flags=0x{observation.Frame.Payload.CastFlags:X2} progressQ8={observation.Frame.Payload.ProgressPctQ8} remaining={observation.Frame.Payload.RemainingCenti / 100.0:F2}s label={FormatSpellLabel(observation.Frame.Payload.Label)}");
        WriteAggregateObservation(
            "AggregateAuraPage",
            snapshot.AuraPage,
            nowUtc,
            observation =>
                $"seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} kind={observation.Frame.Payload.PageKindCode} total={observation.Frame.Payload.TotalAuraCount} entry1={FormatAuraEntry(observation.Frame.Payload.Entry1)} entry2={FormatAuraEntry(observation.Frame.Payload.Entry2)}");
        WriteAggregateObservation(
            "AggregateTextPage",
            snapshot.TextPage,
            nowUtc,
            observation =>
                $"seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} kind={observation.Frame.Payload.TextKindCode} hash=0x{observation.Frame.Payload.TextHash16:X4} label={FormatSpellLabel(observation.Frame.Payload.Label)}");
        WriteAggregateObservation(
            "AggregateAbilityWatch",
            snapshot.AbilityWatch,
            nowUtc,
            observation =>
                $"seq={observation.Frame.Header.Sequence} ageMs={FormatAgeMs(observation.ObservedAtUtc, nowUtc)} page={observation.Frame.Payload.PageIndex} ready={observation.Frame.Payload.ReadyCount} cooling={observation.Frame.Payload.CoolingCount} entry1={FormatAbilityEntry(observation.Frame.Payload.Entry1)} entry2={FormatAbilityEntry(observation.Frame.Payload.Entry2)} shortestQ4={observation.Frame.Payload.ShortestCooldownQ4}");
    }

    private static void WriteAggregateObservation<TFrame>(
        string label,
        FrameObservation<TFrame>? observation,
        DateTimeOffset nowUtc,
        Func<FrameObservation<TFrame>, string> formatter)
        where TFrame : TelemetryFrame
    {
        if (observation is null)
        {
            Console.WriteLine($"{label}: missing");
            return;
        }

        Console.WriteLine($"{label}: {formatter(observation)}");
    }

    private static string FormatAgeMs(DateTimeOffset observedAtUtc, DateTimeOffset nowUtc)
    {
        var age = nowUtc - observedAtUtc;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        return Math.Round(age.TotalMilliseconds, 0).ToString("0");
    }

    private static string FormatSpellLabel(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }

    private static string FormatAuraEntry(AuraPageEntrySnapshot entry)
    {
        return $"id={entry.Id} remQ4={entry.RemainingQ4} stack={entry.Stack} flags=0x{entry.Flags:X2}";
    }

    private static string FormatAbilityEntry(AbilityWatchEntrySnapshot entry)
    {
        return $"id={entry.Id} cdQ4={entry.CooldownQ4} flags=0x{entry.Flags:X2}";
    }

    private static void WriteAuraEntrySummary(string label, AuraPageEntrySnapshot entry)
    {
        Console.WriteLine($"{label}: {FormatAuraEntry(entry)}");
    }

    private static void WriteAbilityEntrySummary(string label, AbilityWatchEntrySnapshot entry)
    {
        Console.WriteLine($"{label}: {FormatAbilityEntry(entry)}");
    }

    private static string DescribeHeaderFlags(HeaderCapabilityFlags flags)
    {
        if (flags == HeaderCapabilityFlags.None)
        {
            return "none";
        }

        var labels = new List<string>();
        if (flags.HasFlag(HeaderCapabilityFlags.MultiFrameRotation))
        {
            labels.Add("multi-frame");
        }

        if (flags.HasFlag(HeaderCapabilityFlags.PlayerPosition))
        {
            labels.Add("player-position");
        }

        if (flags.HasFlag(HeaderCapabilityFlags.PlayerCast))
        {
            labels.Add("player-cast");
        }

        if (flags.HasFlag(HeaderCapabilityFlags.ExpandedStats))
        {
            labels.Add("expanded-stats");
        }

        if (flags.HasFlag(HeaderCapabilityFlags.TargetPosition))
        {
            labels.Add("target-position");
        }

        if (flags.HasFlag(HeaderCapabilityFlags.FollowUnitStatus))
        {
            labels.Add("follow-unit-status");
        }

        if (flags.HasFlag(HeaderCapabilityFlags.AdditionalTelemetry))
        {
            labels.Add("additional-telemetry");
        }

        if (flags.HasFlag(HeaderCapabilityFlags.TextAndAuras))
        {
            labels.Add("text-and-auras");
        }

        var unknown = (byte)(flags & ~(TransportConstants.HeaderCapabilities));
        if (unknown != 0)
        {
            labels.Add($"unknown:0x{unknown:X2}");
        }

        return string.Join(", ", labels);
    }

    private List<CaptureAttempt> CaptureAndAnalyze(nint hwnd, IReadOnlyList<CaptureBackend> backends)
    {
        var attempts = new List<CaptureAttempt>(backends.Count);
        foreach (var backend in backends)
        {
            try
            {
                var captureTimer = Stopwatch.StartNew();
                var capture = WindowCaptureService.CaptureTopSlice(hwnd, _profile, _profile.CaptureHeight - _profile.BandHeight, backend);
                captureTimer.Stop();

                var decodeTimer = Stopwatch.StartNew();
                var validations = ColorStripAnalyzer.AnalyzeStacked(capture.Image, _profile);
                var validation = validations
                    .OrderByDescending(static candidate => candidate.IsAccepted)
                    .ThenBy(static candidate => candidate.Detection is null ? 1 : 0)
                    .ThenBy(static candidate => candidate.Detection?.ControlError ?? double.MaxValue)
                    .First();
                decodeTimer.Stop();

                attempts.Add(new CaptureAttempt(
                    backend,
                    capture,
                    validation,
                    validations,
                    capture.Image.MeasureSignal(),
                    captureTimer.Elapsed.TotalMilliseconds,
                    decodeTimer.Elapsed.TotalMilliseconds,
                    null));
            }
            catch (Exception ex)
            {
                attempts.Add(new CaptureAttempt(backend, null, null, Array.Empty<FrameValidationResult>(), null, 0, 0, ex.Message));
            }
        }

        return attempts;
    }

    private List<CaptureAttempt> CaptureAndAnalyzePreferred(nint hwnd, IReadOnlyList<CaptureBackend> backends, CaptureBackend? preferredBackend)
    {
        if (preferredBackend is null)
        {
            return CaptureAndAnalyze(hwnd, backends);
        }

        var preferredAttempts = CaptureAndAnalyze(hwnd, new[] { preferredBackend.Value });
        var preferredBest = ChooseBestAttempt(preferredAttempts);
        if (preferredBest?.Validation?.IsAccepted == true)
        {
            return preferredAttempts;
        }

        return CaptureAndAnalyze(hwnd, backends);
    }

    private static CaptureAttempt? ChooseBestAttempt(IEnumerable<CaptureAttempt> attempts)
    {
        return attempts
            .Where(static attempt => attempt.Capture is not null && attempt.Validation is not null)
            .OrderByDescending(static attempt => attempt.Validations.Count(static validation => validation.IsAccepted))
            .ThenByDescending(static attempt => attempt.Validation!.IsAccepted)
            .ThenBy(static attempt => attempt.Validation!.Detection is null ? 1 : 0)
            .ThenBy(static attempt => attempt.Validation!.Detection?.ControlError ?? double.MaxValue)
            .ThenByDescending(static attempt => attempt.Signal?.LumaRange ?? 0)
            .ThenByDescending(static attempt => attempt.Signal?.AverageLuma ?? 0)
            .FirstOrDefault();
    }

    private static int PrintUsage()
    {
        Console.WriteLine("ChromaLink CLI");
        Console.WriteLine("  smoke");
        Console.WriteLine("  replay <bmpPath>");
        Console.WriteLine("  live [sampleCount] [sleepMs] [--backend desktopdup|screen|printwindow]");
        Console.WriteLine("  watch [durationSeconds] [sleepMs] [--backend desktopdup|screen|printwindow]");
        Console.WriteLine("  bench");
        Console.WriteLine("  validate");
        Console.WriteLine("  capture-dump [--backend desktopdup|screen|printwindow]");
        Console.WriteLine("  prepare-window [left] [top]");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static double Percentile(double[] samples, double percentile)
    {
        if (samples.Length == 0)
        {
            return 0;
        }

        Array.Sort(samples);
        var index = (int)Math.Ceiling(percentile * samples.Length) - 1;
        return samples[Math.Clamp(index, 0, samples.Length - 1)];
    }

    private static bool TryParseCaptureInvocation(string[] args, int startIndex, out CaptureInvocation invocation, out string error)
    {
        var positionals = new List<string>();
        CaptureBackend? backendOverride = null;

        for (var index = startIndex; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument.Equals("--backend", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    invocation = default;
                    error = "--backend requires a value.";
                    return false;
                }

                if (!TryParseBackendName(args[index + 1], out var parsedBackend))
                {
                    invocation = default;
                    error = $"Unsupported backend: {args[index + 1]}.";
                    return false;
                }

                backendOverride = parsedBackend;
                index++;
                continue;
            }

            if (argument.StartsWith("--backend=", StringComparison.OrdinalIgnoreCase))
            {
                var backendText = argument.Substring("--backend=".Length);
                if (!TryParseBackendName(backendText, out var parsedBackend))
                {
                    invocation = default;
                    error = $"Unsupported backend: {backendText}.";
                    return false;
                }

                backendOverride = parsedBackend;
                continue;
            }

            positionals.Add(argument);
        }

        invocation = new CaptureInvocation(
            positionals,
            backendOverride is null ? DefaultCaptureBackends : new[] { backendOverride.Value });
        error = string.Empty;
        return true;
    }

    private static bool TryParseBackendName(string text, out CaptureBackend backend)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "desktopdup":
            case "desktop-dup":
            case "desktopduplication":
                backend = CaptureBackend.DesktopDuplication;
                return true;
            case "screen":
            case "bitblt":
                backend = CaptureBackend.ScreenBitBlt;
                return true;
            case "printwindow":
            case "print-window":
                backend = CaptureBackend.PrintWindow;
                return true;
            default:
                backend = default;
                return false;
        }
    }

    private sealed record CaptureAttempt(
        CaptureBackend Backend,
        CaptureResult? Capture,
        FrameValidationResult? Validation,
        IReadOnlyList<FrameValidationResult> Validations,
        FrameSignalStats? Signal,
        double CaptureElapsedMs,
        double DecodeElapsedMs,
        string? FailureReason);

    private readonly record struct CaptureInvocation(
        IReadOnlyList<string> Positionals,
        IReadOnlyList<CaptureBackend> Backends);
}
