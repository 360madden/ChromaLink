using System.Diagnostics;
using System.Text;
using ChromaLink.Reader;

var app = new CliApp();
return await app.RunAsync(args);

internal sealed class CliApp
{
    private readonly StripProfile _profile = StripProfiles.Default;

    public Task<int> RunAsync(string[] args)
    {
        var command = args.Length == 0 ? "smoke" : args[0].ToLowerInvariant();
        return command switch
        {
            "smoke" => Task.FromResult(RunSmoke()),
            "replay" => Task.FromResult(RunReplay(args)),
            "live" => Task.FromResult(RunLive(args, watchMode: false)),
            "watch" => Task.FromResult(RunLive(args, watchMode: true)),
            "bench" => Task.FromResult(RunBench()),
            "capture-dump" => Task.FromResult(RunCaptureDump()),
            "prepare-window" => Task.FromResult(RunPrepareWindow(args)),
            "help" or "--help" or "-h" => Task.FromResult(PrintUsage()),
            _ => Task.FromResult(Fail($"Unsupported command: {command}"))
        };
    }

    private int RunSmoke()
    {
        var fixtureRoot = PathProvider.EnsureFixtureDirectory();
        var coreBytes = FrameSerializer.BuildCoreFrameBytes(_profile.NumericId, 10, TelemetrySnapshot.CreateSynthetic());
        var tacticalBytes = FrameSerializer.BuildTacticalFrameBytes(_profile.NumericId, 11, TelemetrySnapshot.CreateSynthetic());
        var coreImage = StripRenderer.Render(_profile, coreBytes);
        var tacticalImage = StripRenderer.Render(_profile, tacticalBytes);
        var corePath = Path.Combine(fixtureRoot, "chromalink-core.bmp");
        var tacticalPath = Path.Combine(fixtureRoot, "chromalink-tactical.bmp");
        BmpIO.Save(corePath, coreImage);
        BmpIO.Save(tacticalPath, tacticalImage);

        var coreValidation = DecodeAndValidate(coreImage, null);
        var tacticalValidation = DecodeAndValidate(tacticalImage, null);
        var success = coreValidation.IsAccepted && tacticalValidation.IsAccepted;

        Console.WriteLine("ChromaLink");
        Console.WriteLine("Mode: smoke");
        Console.WriteLine($"Success: {success.ToString().ToLowerInvariant()}");
        Console.WriteLine($"CoreFixture: {corePath}");
        Console.WriteLine($"TacticalFixture: {tacticalPath}");
        Console.WriteLine($"CoreReason: {coreValidation.Reason}");
        Console.WriteLine($"TacticalReason: {tacticalValidation.Reason}");
        return success ? 0 : 1;
    }

    private int RunReplay(string[] args)
    {
        if (args.Length < 2)
        {
            return Fail("replay requires a BMP path.");
        }

        var path = args[1];
        var image = BmpIO.Load(path);
        var validation = DecodeAndValidate(image, null);
        Console.WriteLine("ChromaLink");
        Console.WriteLine("Mode: replay");
        Console.WriteLine($"Accepted: {validation.IsAccepted.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Reason: {validation.Reason}");
        Console.WriteLine($"SearchMode: {validation.Detection.SearchMode}");
        Console.WriteLine($"Origin: {validation.Detection.OriginX},{validation.Detection.OriginY}");
        Console.WriteLine($"Pitch: {validation.Detection.Pitch:F3}");
        if (validation.Frame is not null)
        {
            Console.WriteLine($"FrameType: {validation.Frame.Header.FrameType}");
            Console.WriteLine($"Lane: {validation.Frame.Header.LaneId}");
            Console.WriteLine($"Sequence: {validation.Frame.Header.Sequence}");
            Console.WriteLine($"PayloadLength: {validation.Frame.Header.PayloadLength}");
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

        CaptureResult? capture = null;
        string? failure = null;
        foreach (var backend in new[] { CaptureBackend.PrintWindow, CaptureBackend.ScreenBitBlt })
        {
            try
            {
                capture = WindowCaptureService.CaptureTopSlice(hwnd, _profile, 48, backend);
                break;
            }
            catch (Exception ex)
            {
                failure = ex.Message;
            }
        }

        if (capture is null)
        {
            return Fail($"capture-dump failed: {failure ?? "unknown capture failure"}");
        }

        var path = Path.Combine(PathProvider.EnsureOutDirectory(), "chromalink-capture-dump.bmp");
        BmpIO.Save(path, capture.Image);
        Console.WriteLine("ChromaLink");
        Console.WriteLine("Mode: capture-dump");
        Console.WriteLine($"Path: {path}");
        Console.WriteLine($"Backend: {capture.Backend}");
        Console.WriteLine($"CaptureRect: {capture.CaptureLeft},{capture.CaptureTop} {capture.CaptureWidth}x{capture.CaptureHeight}");
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
        var coreBytes = FrameSerializer.BuildCoreFrameBytes(_profile.NumericId, 42, TelemetrySnapshot.CreateSynthetic());
        var scenarios = new[]
        {
            new PerturbationOptions("baseline"),
            new PerturbationOptions("offset-2x1", 2, 1),
            new PerturbationOptions("blur-1", BlurRadius: 1),
            new PerturbationOptions("warm-shift", RedGain: 1.05, GreenGain: 1.0, BlueGain: 0.95),
            new PerturbationOptions("scale-1.02", Scale: 1.02)
        };

        var results = ReplayRunner.Run(_profile, coreBytes, scenarios);
        Console.WriteLine("ChromaLink");
        Console.WriteLine("Mode: bench");
        foreach (var result in results)
        {
            Console.WriteLine($"{result.Scenario.Name}: {(result.Result.IsAccepted ? "accepted" : "rejected")} | {result.Result.Reason} | {result.LocateDecodeMs:F2} ms");
        }

        Console.WriteLine($"AverageReplayDecodeMs: {results.Average(static result => result.LocateDecodeMs):F2}");
        Console.WriteLine($"P95ReplayDecodeMs: {Percentile(results.Select(static result => result.LocateDecodeMs).ToArray(), 0.95):F2}");
        Console.WriteLine($"UsefulPayloadBytesPerFrame: {TransportConstants.MaxPayloadBytes}");
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
        var lastValidation = default(FrameValidationResult);
        GeometryLock? geometryLock = LoadGeometryLock();
        string? firstRejectPath = null;
        CoreFramePayload? lastCore = null;
        TacticalFramePayload? lastTactical = null;
        var outRoot = PathProvider.EnsureOutDirectory();

        for (var iteration = 0; iteration < iterationLimit && started.ElapsedMilliseconds < durationMs; iteration++)
        {
            var accepted = false;
            foreach (var backend in new[] { CaptureBackend.PrintWindow, CaptureBackend.ScreenBitBlt })
            {
                try
                {
                    var captureTimer = Stopwatch.StartNew();
                    var capture = WindowCaptureService.CaptureTopSlice(hwnd, _profile, 48, backend);
                    captureTimer.Stop();

                    var decodeTimer = Stopwatch.StartNew();
                    var validation = DecodeAndValidate(capture.Image, geometryLock);
                    decodeTimer.Stop();

                    metrics.Add(validation.IsAccepted, captureTimer.Elapsed.TotalMilliseconds, decodeTimer.Elapsed.TotalMilliseconds, validation.Reason);
                    lastValidation = validation;
                    if (validation.IsAccepted)
                    {
                        geometryLock = new GeometryLock(validation.Detection.OriginX, validation.Detection.OriginY, validation.Detection.Pitch, capture.ClientWidth, capture.ClientHeight);
                        SaveGeometryLock(geometryLock);
                        if (validation.Frame!.Header.FrameType == FrameType.CoreStatus)
                        {
                            lastCore = FrameSerializer.ParseCorePayload(validation.Frame.Payload);
                        }
                        else if (validation.Frame!.Header.FrameType == FrameType.Tactical)
                        {
                            lastTactical = FrameSerializer.ParseTacticalPayload(validation.Frame.Payload);
                        }

                        accepted = true;
                        break;
                    }

                    if (firstRejectPath is null)
                    {
                        firstRejectPath = Path.Combine(outRoot, "chromalink-first-reject.bmp");
                        BmpIO.Save(firstRejectPath, capture.Image);
                    }
                }
                catch (Exception ex)
                {
                    metrics.Add(false, 0, 0, $"{backend} capture failed: {ex.Message}");
                    lastValidation = new FrameValidationResult(
                        false,
                        $"{backend} capture failed: {ex.Message}",
                        new DetectionResult(_profile, 0, 0, 0, int.MaxValue, 0, 0, _profile.Pitch, _profile.BandWidth, _profile.BandHeight, 0, "capture-failed"),
                        new DecodedStrip(
                            new DetectionResult(_profile, 0, 0, 0, int.MaxValue, 0, 0, _profile.Pitch, _profile.BandWidth, _profile.BandHeight, 0, "capture-failed"),
                            [],
                            [],
                            [],
                            0),
                        null,
                        false);
                }
            }

            if (sleepMs > 0)
            {
                Thread.Sleep(sleepMs);
            }

            if (!accepted && !watchMode)
            {
                continue;
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
        Console.WriteLine($"LastReason: {lastValidation?.Reason ?? "-"}");
        if (lastValidation?.Frame is not null)
        {
            Console.WriteLine($"LastFrameType: {lastValidation.Frame.Header.FrameType}");
            Console.WriteLine($"LastSequence: {lastValidation.Frame.Header.Sequence}");
        }

        if (lastCore is not null)
        {
            Console.WriteLine($"LastCorePlayer: HP {lastCore.PlayerHealthCurrent}/{lastCore.PlayerHealthMax} | res {lastCore.PlayerResourceCurrent}/{lastCore.PlayerResourceMax}");
        }

        if (lastTactical is not null)
        {
            Console.WriteLine($"LastTacticalPlayer: cast {lastTactical.PlayerCastFlags}/{lastTactical.PlayerCastProgressQ15} | pos {lastTactical.PlayerCoordX10 / 10.0:F1},{lastTactical.PlayerCoordZ10 / 10.0:F1}");
        }

        if (firstRejectPath is not null)
        {
            Console.WriteLine($"FirstRejectBmp: {firstRejectPath}");
        }

        return metrics.AcceptedCount > 0 ? 0 : 1;
    }

    private FrameValidationResult DecodeAndValidate(Bgr24Frame image, GeometryLock? geometryLock)
    {
        var detection = StripLocator.Locate(image, _profile, geometryLock);
        var decoded = StripDecoder.Decode(image, detection);
        return StripValidator.Validate(decoded);
    }

    private static GeometryLock? LoadGeometryLock()
    {
        var path = Path.Combine(PathProvider.EnsureOutDirectory(), "geometry-lock.txt");
        if (!File.Exists(path))
        {
            return null;
        }

        var lines = File.ReadAllLines(path);
        if (lines.Length < 5)
        {
            return null;
        }

        return new GeometryLock(
            int.Parse(lines[0]),
            int.Parse(lines[1]),
            double.Parse(lines[2]),
            int.Parse(lines[3]),
            int.Parse(lines[4]));
    }

    private static void SaveGeometryLock(GeometryLock geometryLock)
    {
        var path = Path.Combine(PathProvider.EnsureOutDirectory(), "geometry-lock.txt");
        File.WriteAllLines(
            path,
            new[]
            {
                geometryLock.OriginX.ToString(),
                geometryLock.OriginY.ToString(),
                geometryLock.Pitch.ToString("F3"),
                geometryLock.SourceWidth.ToString(),
                geometryLock.SourceHeight.ToString()
            });
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
}
