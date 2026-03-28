using System.Diagnostics;

namespace ChromaLink.Reader;

public sealed class LiveMetrics
{
    private readonly List<double> _captureMs = [];
    private readonly List<double> _decodeMs = [];
    private readonly List<string> _reasons = [];

    public int AcceptedCount { get; private set; }

    public int RejectedCount { get; private set; }

    public void Add(bool accepted, double captureMs, double decodeMs, string reason)
    {
        _captureMs.Add(captureMs);
        _decodeMs.Add(decodeMs);
        _reasons.Add(reason);
        if (accepted)
        {
            AcceptedCount++;
        }
        else
        {
            RejectedCount++;
        }
    }

    public double AverageCaptureMs => _captureMs.Count == 0 ? 0 : _captureMs.Average();

    public double AverageDecodeMs => _decodeMs.Count == 0 ? 0 : _decodeMs.Average();

    public double MedianDecodeMs => Percentile(_decodeMs, 0.50);

    public double P95DecodeMs => Percentile(_decodeMs, 0.95);

    public IReadOnlyDictionary<string, int> ReasonCounts =>
        _reasons.GroupBy(static value => value).ToDictionary(static group => group.Key, static group => group.Count());

    private static double Percentile(List<double> samples, double percentile)
    {
        if (samples.Count == 0)
        {
            return 0;
        }

        var ordered = samples.OrderBy(static value => value).ToArray();
        var index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
    }
}

public sealed record PerturbationOptions(
    string Name,
    int OffsetX = 0,
    int OffsetY = 0,
    int BlurRadius = 0,
    double Scale = 1.0,
    double RedGain = 1.0,
    double GreenGain = 1.0,
    double BlueGain = 1.0);

public static class PerturbationEngine
{
    public static Bgr24Frame Apply(Bgr24Frame source, PerturbationOptions options)
    {
        var translated = ApplyScaleAndOffset(source, options);
        var tinted = ApplyColorGain(translated, options);
        return options.BlurRadius > 0 ? ApplyBlur(tinted, options.BlurRadius) : tinted;
    }

    private static Bgr24Frame ApplyScaleAndOffset(Bgr24Frame source, PerturbationOptions options)
    {
        var scaledWidth = (int)Math.Ceiling(source.Width * Math.Max(1.0, options.Scale));
        var scaledHeight = (int)Math.Ceiling(source.Height * Math.Max(1.0, options.Scale));
        var outputWidth = Math.Max(source.Width, scaledWidth + (Math.Abs(options.OffsetX) * 2));
        var outputHeight = Math.Max(source.Height, scaledHeight + (Math.Abs(options.OffsetY) * 2));
        var output = Bgr24Frame.CreateSolid(outputWidth, outputHeight, Bgr24Color.White, "perturbed");
        var sourceCenterX = (source.Width - 1) / 2.0;
        var sourceCenterY = (source.Height - 1) / 2.0;
        var outputCenterX = (output.Width - 1) / 2.0;
        var outputCenterY = (output.Height - 1) / 2.0;
        for (var y = 0; y < output.Height; y++)
        {
            for (var x = 0; x < output.Width; x++)
            {
                var sourceX = ((x - outputCenterX - options.OffsetX) / options.Scale) + sourceCenterX;
                var sourceY = ((y - outputCenterY - options.OffsetY) / options.Scale) + sourceCenterY;
                if (sourceX < 0 || sourceY < 0 || sourceX >= source.Width || sourceY >= source.Height)
                {
                    continue;
                }

                output.SetColor(x, y, source.GetColor((int)Math.Round(sourceX), (int)Math.Round(sourceY)));
            }
        }

        return output;
    }

    private static Bgr24Frame ApplyColorGain(Bgr24Frame source, PerturbationOptions options)
    {
        var output = source.Copy("perturbed");
        for (var y = 0; y < output.Height; y++)
        {
            for (var x = 0; x < output.Width; x++)
            {
                var color = source.GetColor(x, y);
                output.SetColor(
                    x,
                    y,
                    new Bgr24Color(
                        (byte)Math.Clamp(color.B * options.BlueGain, 0, 255),
                        (byte)Math.Clamp(color.G * options.GreenGain, 0, 255),
                        (byte)Math.Clamp(color.R * options.RedGain, 0, 255)));
            }
        }

        return output;
    }

    private static Bgr24Frame ApplyBlur(Bgr24Frame source, int radius)
    {
        var output = source.Copy("perturbed");
        for (var y = 0; y < output.Height; y++)
        {
            for (var x = 0; x < output.Width; x++)
            {
                double totalB = 0;
                double totalG = 0;
                double totalR = 0;
                var count = 0;
                for (var yOffset = -radius; yOffset <= radius; yOffset++)
                {
                    for (var xOffset = -radius; xOffset <= radius; xOffset++)
                    {
                        var color = source.GetColor(x + xOffset, y + yOffset);
                        totalB += color.B;
                        totalG += color.G;
                        totalR += color.R;
                        count++;
                    }
                }

                output.SetColor(x, y, new Bgr24Color((byte)(totalB / count), (byte)(totalG / count), (byte)(totalR / count)));
            }
        }

        return output;
    }
}

public static class ReplayRunner
{
    public static IReadOnlyList<ReplayScenarioResult> Run(
        StripProfile profile,
        byte[] transportBytes,
        IReadOnlyList<PerturbationOptions> scenarios)
    {
        var baseline = StripRenderer.Render(profile, transportBytes, "synthetic");
        var results = new List<ReplayScenarioResult>(scenarios.Count);
        foreach (var scenario in scenarios)
        {
            var image = PerturbationEngine.Apply(baseline, scenario);
            var stopwatch = Stopwatch.StartNew();
            var detection = StripLocator.Locate(image, profile);
            var decoded = StripDecoder.Decode(image, detection);
            var validation = StripValidator.Validate(decoded);
            stopwatch.Stop();
            results.Add(new ReplayScenarioResult(scenario, validation, stopwatch.Elapsed.TotalMilliseconds));
        }

        return results;
    }
}

public sealed record ReplayScenarioResult(
    PerturbationOptions Scenario,
    FrameValidationResult Result,
    double LocateDecodeMs);

public static class PathProvider
{
    public static string DataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChromaLink", "DesktopDotNet");

    public static string OutRoot => Path.Combine(DataRoot, "out");

    public static string FixtureRoot => Path.Combine(DataRoot, "fixtures");

    public static string EnsureOutDirectory()
    {
        Directory.CreateDirectory(OutRoot);
        return OutRoot;
    }

    public static string EnsureFixtureDirectory()
    {
        Directory.CreateDirectory(FixtureRoot);
        return FixtureRoot;
    }
}
