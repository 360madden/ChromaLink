namespace ChromaLink.Reader;

public sealed record DetectionResult(
    int OriginX,
    int OriginY,
    double Pitch,
    double Scale,
    double ControlError,
    string SearchMode,
    Bgr24Color BlackAnchor,
    Bgr24Color WhiteAnchor);

public sealed record SegmentSample(
    int SegmentIndex,
    byte Symbol,
    double Confidence,
    double Distance,
    Bgr24Color SampleColor);

public sealed record FrameValidationResult(
    bool IsAccepted,
    string Reason,
    DetectionResult? Detection,
    IReadOnlyList<SegmentSample> Samples,
    CoreStatusFrame? Frame);

internal readonly record struct NormalizedRgb(double R, double G, double B)
{
    public double DistanceTo(NormalizedRgb other)
    {
        var deltaR = R - other.R;
        var deltaG = G - other.G;
        var deltaB = B - other.B;
        return Math.Sqrt((deltaR * deltaR) + (deltaG * deltaG) + (deltaB * deltaB));
    }
}

internal readonly record struct ColorClassification(byte Symbol, double Confidence, double Distance);

public static class ColorStripAnalyzer
{
    private const double ControlRejectThreshold = 0.30;
    private const double LeftOnlyControlRejectThreshold = 0.38;
    private const double PayloadConfidenceThreshold = 0.10;
    private const double PayloadDistanceThreshold = 0.40;

    public static FrameValidationResult Analyze(Bgr24Frame image, StripProfile? profile = null)
    {
        profile ??= StripProfiles.Default;
        if (!TryLocate(image, profile, out var detection, out var locateReason))
        {
            return new FrameValidationResult(false, locateReason, detection, Array.Empty<SegmentSample>(), null);
        }

        var acceptedDetection = detection!;
        var samples = SampleAllSegments(image, profile, acceptedDetection);
        if (!ControlSegmentsMatch(profile, samples, acceptedDetection.SearchMode == "left-control-scan"))
        {
            return new FrameValidationResult(false, "Control marker mismatch.", detection, samples, null);
        }

        var payloadSymbols = new byte[profile.PayloadSymbolCount];
        for (var index = 0; index < profile.PayloadSymbolCount; index++)
        {
            var sample = samples[profile.PayloadStartIndex + index];
            payloadSymbols[index] = sample.Symbol;
            if (sample.Confidence < PayloadConfidenceThreshold || sample.Distance > PayloadDistanceThreshold)
            {
                return new FrameValidationResult(false, "Low color-classification confidence.", detection, samples, null);
            }
        }

        var bytes = FrameProtocol.DecodePayloadSymbolsToBytes(payloadSymbols);
        if (!FrameProtocol.TryParseCoreFrameBytes(bytes, out var frame, out var reason))
        {
            return new FrameValidationResult(false, reason, detection, samples, null);
        }

        var acceptedReason = acceptedDetection.SearchMode == "left-control-scan"
            ? "Accepted (left-control fallback)"
            : "Accepted";
        return new FrameValidationResult(true, acceptedReason, detection, samples, frame);
    }

    private static bool TryLocate(Bgr24Frame image, StripProfile profile, out DetectionResult? detection, out string reason)
    {
        if (TryLocateWithMode(image, profile, includeRightControl: true, "control-scan", out detection, out reason))
        {
            return true;
        }

        if (TryLocateWithMode(image, profile, includeRightControl: false, "left-control-scan", out detection, out reason))
        {
            return true;
        }

        if (detection is not null)
        {
            reason = "Control marker mismatch.";
            return false;
        }

        reason = DescribeMissingStrip(image);
        return false;
    }

    private static bool TryLocateWithMode(
        Bgr24Frame image,
        StripProfile profile,
        bool includeRightControl,
        string searchMode,
        out DetectionResult? detection,
        out string reason)
    {
        detection = null;
        reason = "Control marker mismatch.";
        Candidate? best = null;

        for (var scaled = 94; scaled <= 106; scaled++)
        {
            var scale = scaled / 100.0;
            var pitch = profile.SegmentWidth * scale;
            var bandWidth = (int)Math.Ceiling(profile.SegmentCount * pitch);
            var bandHeight = (int)Math.Ceiling(profile.SegmentHeight * scale);
            if (bandWidth > image.Width || bandHeight > image.Height)
            {
                continue;
            }

            var maxX = Math.Min(Math.Max(0, image.Width - bandWidth), 12);
            var maxY = Math.Min(Math.Max(0, image.Height - bandHeight), 8);
            for (var originY = 0; originY <= maxY; originY++)
            {
                for (var originX = 0; originX <= maxX; originX++)
                {
                    var candidate = ScoreCandidate(image, profile, originX, originY, scale, includeRightControl);
                    if (candidate is null)
                    {
                        continue;
                    }

                    if (best is null
                        || (candidate.PayloadValidated && !best.PayloadValidated)
                        || (candidate.PayloadValidated == best.PayloadValidated && candidate.ControlError < best.ControlError))
                    {
                        best = candidate;
                    }
                }
            }
        }

        if (best is null)
        {
            return false;
        }

        detection = new DetectionResult(
            best.OriginX,
            best.OriginY,
            best.Pitch,
            best.Scale,
            best.ControlError,
            searchMode,
            best.BlackAnchor,
            best.WhiteAnchor);

        var rejectThreshold = includeRightControl ? ControlRejectThreshold : LeftOnlyControlRejectThreshold;
        if (best.ControlError > rejectThreshold)
        {
            return false;
        }

        if (!includeRightControl && !best.PayloadValidated)
        {
            return false;
        }

        reason = "Accepted";
        return true;
    }

    private static Candidate? ScoreCandidate(Bgr24Frame image, StripProfile profile, int originX, int originY, double scale, bool includeRightControl)
    {
        var pitch = profile.SegmentWidth * scale;
        var segmentHeight = profile.SegmentHeight * scale;
        var radius = Math.Max(0, (int)Math.Round(Math.Min(pitch, segmentHeight) * 0.12));

        var controlSamples = new List<(int SegmentIndex, byte ExpectedSymbol, Bgr24Color Color)>();
        for (var index = 0; index < profile.LeftControl.Length; index++)
        {
            controlSamples.Add((index, profile.LeftControl[index], SampleSegment(image, originX, originY, pitch, segmentHeight, index, radius)));
        }

        for (var index = 0; includeRightControl && index < profile.RightControl.Length; index++)
        {
            var segmentIndex = profile.SegmentCount - profile.RightControl.Length + index;
            controlSamples.Add((segmentIndex, profile.RightControl[index], SampleSegment(image, originX, originY, pitch, segmentHeight, segmentIndex, radius)));
        }

        var blackAnchor = AverageAnchor(controlSamples, 0);
        var whiteAnchor = AverageAnchor(controlSamples, 1);
        if (GetLuma(whiteAnchor) - GetLuma(blackAnchor) < 40.0)
        {
            return null;
        }

        double totalError = 0;
        foreach (var sample in controlSamples)
        {
            var normalizedSample = Normalize(sample.Color, blackAnchor, whiteAnchor);
            var normalizedExpected = NormalizeIdeal(profile.GetPaletteColor(sample.ExpectedSymbol));
            totalError += normalizedSample.DistanceTo(normalizedExpected);
        }

        var controlError = totalError / controlSamples.Count;
        var payloadValidated = includeRightControl || TryValidatePayloadCandidate(image, profile, originX, originY, pitch, scale, blackAnchor, whiteAnchor);
        return new Candidate(originX, originY, pitch, scale, controlError, blackAnchor, whiteAnchor, payloadValidated);
    }

    private static List<SegmentSample> SampleAllSegments(Bgr24Frame image, StripProfile profile, DetectionResult detection)
    {
        var segmentHeight = profile.SegmentHeight * detection.Scale;
        var radius = Math.Max(0, (int)Math.Round(Math.Min(detection.Pitch, segmentHeight) * 0.12));
        var samples = new List<SegmentSample>(profile.SegmentCount);
        for (var segmentIndex = 0; segmentIndex < profile.SegmentCount; segmentIndex++)
        {
            var sampleColor = SampleSegment(image, detection.OriginX, detection.OriginY, detection.Pitch, segmentHeight, segmentIndex, radius);
            var classification = Classify(sampleColor, profile, detection.BlackAnchor, detection.WhiteAnchor);
            samples.Add(new SegmentSample(segmentIndex, classification.Symbol, classification.Confidence, classification.Distance, sampleColor));
        }

        return samples;
    }

    private static bool ControlSegmentsMatch(StripProfile profile, IReadOnlyList<SegmentSample> samples, bool leftOnly)
    {
        for (var index = 0; index < profile.LeftControl.Length; index++)
        {
            if (samples[index].Symbol != profile.LeftControl[index])
            {
                return false;
            }
        }

        if (leftOnly)
        {
            return true;
        }

        for (var index = 0; index < profile.RightControl.Length; index++)
        {
            var segmentIndex = profile.SegmentCount - profile.RightControl.Length + index;
            if (samples[segmentIndex].Symbol != profile.RightControl[index])
            {
                return false;
            }
        }

        return true;
    }

    private static Bgr24Color SampleSegment(Bgr24Frame image, int originX, int originY, double pitch, double segmentHeight, int segmentIndex, int radius)
    {
        var centerX = originX + ((segmentIndex + 0.5) * pitch);
        var centerY = originY + Math.Max(2.0, segmentHeight * 0.25);
        return image.SampleAverage(centerX, centerY, radius);
    }

    private static Bgr24Color AverageAnchor(IEnumerable<(int SegmentIndex, byte ExpectedSymbol, Bgr24Color Color)> samples, byte symbol)
    {
        long sumB = 0;
        long sumG = 0;
        long sumR = 0;
        long count = 0;
        foreach (var sample in samples)
        {
            if (sample.ExpectedSymbol != symbol)
            {
                continue;
            }

            sumB += sample.Color.B;
            sumG += sample.Color.G;
            sumR += sample.Color.R;
            count++;
        }

        if (count == 0)
        {
            return symbol == 0 ? Bgr24Color.Black : Bgr24Color.White;
        }

        return new Bgr24Color((byte)(sumB / count), (byte)(sumG / count), (byte)(sumR / count));
    }

    private static double GetLuma(Bgr24Color color)
    {
        return (color.R * 0.299) + (color.G * 0.587) + (color.B * 0.114);
    }

    private static ColorClassification Classify(Bgr24Color sampleColor, StripProfile profile, Bgr24Color blackAnchor, Bgr24Color whiteAnchor)
    {
        var normalizedSample = Normalize(sampleColor, blackAnchor, whiteAnchor);
        var bestDistance = double.MaxValue;
        var secondDistance = double.MaxValue;
        byte bestSymbol = 0;

        foreach (var entry in profile.Palette)
        {
            var normalizedIdeal = NormalizeIdeal(entry.Color);
            var distance = normalizedSample.DistanceTo(normalizedIdeal);
            if (distance < bestDistance)
            {
                secondDistance = bestDistance;
                bestDistance = distance;
                bestSymbol = entry.Symbol;
            }
            else if (distance < secondDistance)
            {
                secondDistance = distance;
            }
        }

        var separation = secondDistance <= 0.0001 ? 1.0 : Math.Clamp((secondDistance - bestDistance) / secondDistance, 0.0, 1.0);
        var absolute = Math.Clamp(1.0 - (bestDistance / 0.75), 0.0, 1.0);
        return new ColorClassification(bestSymbol, separation * absolute, bestDistance);
    }

    private static string DescribeMissingStrip(Bgr24Frame image)
    {
        var signal = image.MeasureSignal();
        if (signal.LumaRange < 6.0 && signal.AverageLuma < 24.0)
        {
            return "Top band looks flat and near-black. The addon strip may not be loaded, or the capture backend returned a blank surface.";
        }

        if (signal.LumaRange < 12.0)
        {
            return "Top band looks too flat to locate control markers.";
        }

        return "Pitch mismatch.";
    }

    private static bool TryValidatePayloadCandidate(
        Bgr24Frame image,
        StripProfile profile,
        int originX,
        int originY,
        double pitch,
        double scale,
        Bgr24Color blackAnchor,
        Bgr24Color whiteAnchor)
    {
        var detection = new DetectionResult(originX, originY, pitch, scale, 0, "left-control-scan", blackAnchor, whiteAnchor);
        var samples = SampleAllSegments(image, profile, detection);
        if (!ControlSegmentsMatch(profile, samples, leftOnly: true))
        {
            return false;
        }

        var payloadSymbols = new byte[profile.PayloadSymbolCount];
        for (var index = 0; index < profile.PayloadSymbolCount; index++)
        {
            var sample = samples[profile.PayloadStartIndex + index];
            if (sample.Confidence < PayloadConfidenceThreshold || sample.Distance > PayloadDistanceThreshold)
            {
                return false;
            }

            payloadSymbols[index] = sample.Symbol;
        }

        var bytes = FrameProtocol.DecodePayloadSymbolsToBytes(payloadSymbols);
        return FrameProtocol.TryParseCoreFrameBytes(bytes, out _, out _);
    }

    private static NormalizedRgb Normalize(Bgr24Color color, Bgr24Color blackAnchor, Bgr24Color whiteAnchor)
    {
        return new NormalizedRgb(
            NormalizeChannel(color.R, blackAnchor.R, whiteAnchor.R),
            NormalizeChannel(color.G, blackAnchor.G, whiteAnchor.G),
            NormalizeChannel(color.B, blackAnchor.B, whiteAnchor.B));
    }

    private static NormalizedRgb NormalizeIdeal(Bgr24Color color)
    {
        return new NormalizedRgb(
            NormalizeChannel(color.R, Bgr24Color.Black.R, Bgr24Color.White.R),
            NormalizeChannel(color.G, Bgr24Color.Black.G, Bgr24Color.White.G),
            NormalizeChannel(color.B, Bgr24Color.Black.B, Bgr24Color.White.B));
    }

    private static double NormalizeChannel(byte value, byte black, byte white)
    {
        var denominator = Math.Max(8.0, white - black);
        return Math.Clamp((value - black) / denominator, 0.0, 1.0);
    }

    private sealed record Candidate(
        int OriginX,
        int OriginY,
        double Pitch,
        double Scale,
        double ControlError,
        Bgr24Color BlackAnchor,
        Bgr24Color WhiteAnchor,
        bool PayloadValidated);
}
