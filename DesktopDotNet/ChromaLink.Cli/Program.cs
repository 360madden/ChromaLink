using System.Diagnostics;
using System.Text;
using ChromaLink.Reader;

return new CliApp().Run(args);

internal sealed class CliApp
{
    private readonly StripProfile _profile = StripProfiles.Default;
    private static readonly CaptureBackend[] CaptureBackends = { CaptureBackend.ScreenBitBlt, CaptureBackend.PrintWindow };

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
            "capture-dump" => RunCaptureDump(),
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

    private int RunCaptureDump()
    {
        var hwnd = WindowCaptureService.FindRiftWindow();
        if (hwnd == nint.Zero)
        {
            return Fail("No likely RIFT window was found.");
        }

        var attempts = CaptureAndAnalyze(hwnd);
        var selectedAttempt = ChooseBestAttempt(attempts);
        if (selectedAttempt is null)
        {
            return Fail("capture-dump failed: no capture backend produced an image.");
        }

        var capture = selectedAttempt.Capture!;
        var path = Path.Combine(PathProvider.EnsureOutDirectory(), "chromalink-color-capture-dump.bmp");
        BmpIO.Save(path, capture.Image);

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
            return Fail("No likely RIFT window was found.");
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

    private int RunLive(string[] args, bool watchMode)
    {
        var hwnd = WindowCaptureService.FindRiftWindow();
        if (hwnd == nint.Zero)
        {
            return Fail("No likely RIFT window was found.");
        }

        var iterationLimit = watchMode ? int.MaxValue : (args.Length >= 2 ? Math.Max(1, int.Parse(args[1])) : 5);
        var sleepMs = args.Length >= 3 ? Math.Max(0, int.Parse(args[2])) : 100;
        var durationMs = watchMode && args.Length >= 2 ? Math.Max(1, int.Parse(args[1])) * 1000 : int.MaxValue;
        var metrics = new LiveMetrics();
        var started = Stopwatch.StartNew();
        FrameValidationResult? lastValidation = null;
        CaptureBackend? lastBackend = null;
        string? firstRejectPath = null;

        for (var iteration = 0; iteration < iterationLimit && started.ElapsedMilliseconds < durationMs; iteration++)
        {
            var bestAttempt = ChooseBestAttempt(CaptureAndAnalyze(hwnd));
            if (bestAttempt is not null)
            {
                metrics.Add(
                    bestAttempt.Validation!.IsAccepted,
                    bestAttempt.CaptureElapsedMs,
                    bestAttempt.DecodeElapsedMs,
                    bestAttempt.Validation.Reason);
                lastValidation = bestAttempt.Validation;
                lastBackend = bestAttempt.Backend;

                if (!bestAttempt.Validation.IsAccepted && firstRejectPath is null)
                {
                    firstRejectPath = Path.Combine(PathProvider.EnsureOutDirectory(), "chromalink-color-first-reject.bmp");
                    BmpIO.Save(firstRejectPath, bestAttempt.Capture!.Image);
                }
            }
            else
            {
                metrics.Add(false, 0, 0, "No capture backend produced an image.");
                lastValidation = new FrameValidationResult(false, "No capture backend produced an image.", null, Array.Empty<SegmentSample>(), null);
            }

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
        if (lastBackend is not null)
        {
            Console.WriteLine($"LastBackend: {lastBackend}");
        }
        WriteDetectionSummary(lastValidation?.Detection);
        if (lastValidation?.Frame is not null)
        {
            WriteFrameSummary(lastValidation.Frame);
        }

        if (firstRejectPath is not null)
        {
            Console.WriteLine($"FirstRejectBmp: {firstRejectPath}");
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
        Console.WriteLine($"SearchMode: {detection.SearchMode}");
    }

    private static void WriteFrameSummary(CoreStatusFrame frame)
    {
        Console.WriteLine($"FrameType: {frame.Header.FrameType}");
        Console.WriteLine($"Sequence: {frame.Header.Sequence}");
        Console.WriteLine($"PlayerFlags: {frame.Payload.PlayerStateFlags}");
        Console.WriteLine($"PlayerHealthPctQ8: {frame.Payload.PlayerHealthPctQ8}");
        Console.WriteLine($"PlayerResourceKind: {frame.Payload.PlayerResourceKind}");
        Console.WriteLine($"PlayerResourcePctQ8: {frame.Payload.PlayerResourcePctQ8}");
        Console.WriteLine($"TargetFlags: {frame.Payload.TargetStateFlags}");
        Console.WriteLine($"TargetHealthPctQ8: {frame.Payload.TargetHealthPctQ8}");
        Console.WriteLine($"TargetResourceKind: {frame.Payload.TargetResourceKind}");
        Console.WriteLine($"TargetResourcePctQ8: {frame.Payload.TargetResourcePctQ8}");
        Console.WriteLine($"PlayerLevel: {frame.Payload.PlayerLevel}");
        Console.WriteLine($"TargetLevel: {frame.Payload.TargetLevel}");
        Console.WriteLine($"PlayerCalling: {frame.Payload.PlayerCallingRolePacked >> 4}");
        Console.WriteLine($"PlayerRole: {frame.Payload.PlayerCallingRolePacked & 0x0F}");
        Console.WriteLine($"TargetCalling: {frame.Payload.TargetCallingRelationPacked >> 4}");
        Console.WriteLine($"TargetRelation: {frame.Payload.TargetCallingRelationPacked & 0x0F}");
    }

    private List<CaptureAttempt> CaptureAndAnalyze(nint hwnd)
    {
        var attempts = new List<CaptureAttempt>(CaptureBackends.Length);
        foreach (var backend in CaptureBackends)
        {
            try
            {
                var captureTimer = Stopwatch.StartNew();
                var capture = WindowCaptureService.CaptureTopSlice(hwnd, _profile, _profile.CaptureHeight - _profile.BandHeight, backend);
                captureTimer.Stop();

                var decodeTimer = Stopwatch.StartNew();
                var validation = ColorStripAnalyzer.Analyze(capture.Image, _profile);
                decodeTimer.Stop();

                attempts.Add(new CaptureAttempt(
                    backend,
                    capture,
                    validation,
                    capture.Image.MeasureSignal(),
                    captureTimer.Elapsed.TotalMilliseconds,
                    decodeTimer.Elapsed.TotalMilliseconds,
                    null));
            }
            catch (Exception ex)
            {
                attempts.Add(new CaptureAttempt(backend, null, null, null, 0, 0, ex.Message));
            }
        }

        return attempts;
    }

    private static CaptureAttempt? ChooseBestAttempt(IEnumerable<CaptureAttempt> attempts)
    {
        return attempts
            .Where(static attempt => attempt.Capture is not null && attempt.Validation is not null)
            .OrderByDescending(static attempt => attempt.Validation!.IsAccepted)
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
        Console.WriteLine("  live [sampleCount] [sleepMs]");
        Console.WriteLine("  watch [durationSeconds] [sleepMs]");
        Console.WriteLine("  bench");
        Console.WriteLine("  capture-dump");
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

    private sealed record CaptureAttempt(
        CaptureBackend Backend,
        CaptureResult? Capture,
        FrameValidationResult? Validation,
        FrameSignalStats? Signal,
        double CaptureElapsedMs,
        double DecodeElapsedMs,
        string? FailureReason);
}
