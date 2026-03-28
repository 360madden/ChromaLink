namespace ChromaLink.Reader;

public sealed record GeometryLock(int OriginX, int OriginY, double Pitch, int SourceWidth, int SourceHeight);

public sealed record DetectionResult(
    StripProfile Profile,
    int Threshold,
    double BlackMean,
    double WhiteMean,
    int BorderErrors,
    int OriginX,
    int OriginY,
    double Pitch,
    double BandWidth,
    double BandHeight,
    double Contrast,
    string SearchMode);

public static class StripLocator
{
    public const int MaxBorderErrors = 8;

    public static DetectionResult Locate(Bgr24Frame image, StripProfile profile, GeometryLock? geometryLock = null)
    {
        var pitchCandidates = BuildPitchCandidates(profile, geometryLock);

        if (image.Width == profile.BandWidth && image.Height == profile.BandHeight)
        {
            foreach (var pitch in pitchCandidates)
            {
                var exact = EvaluateCandidate(image, profile, 0, 0, pitch, "exact");
                if (exact is not null && exact.BorderErrors <= MaxBorderErrors)
                {
                    return exact;
                }
            }
        }

        if (geometryLock is not null)
        {
            foreach (var pitch in pitchCandidates)
            {
                var locked = EvaluateCandidate(image, profile, geometryLock.OriginX, geometryLock.OriginY, pitch, "locked");
                if (locked is not null && locked.BorderErrors <= MaxBorderErrors)
                {
                    return locked;
                }
            }
        }

        DetectionResult? best = null;
        var maxOriginX = Math.Max(0, Math.Min(80, image.Width - profile.BandWidth));
        var maxOriginY = Math.Max(0, Math.Min(40, image.Height - profile.BandHeight));
        for (var originY = 0; originY <= maxOriginY; originY++)
        {
            for (var originX = 0; originX <= maxOriginX; originX++)
            {
                foreach (var pitch in pitchCandidates)
                {
                    var candidate = EvaluateCandidate(image, profile, originX, originY, pitch, "search");
                    if (candidate is null)
                    {
                        continue;
                    }

                    if (best is null || candidate.BorderErrors < best.BorderErrors || (candidate.BorderErrors == best.BorderErrors && candidate.Contrast > best.Contrast))
                    {
                        best = candidate;
                    }
                }
            }
        }

        return best ?? throw new InvalidOperationException("Could not locate a ChromaLink strip.");
    }

    public static double SampleCellCenter(Bgr24Frame image, StripProfile profile, int gridColumn, int gridRow, int originX, int originY, double pitch)
    {
        var centerX = originX + profile.QuietLeft + ((gridColumn - 1) * pitch) + (pitch / 2.0);
        var centerY = originY + profile.QuietTop + ((gridRow - 1) * pitch) + (pitch / 2.0);
        return image.GetGrayKernel(centerX, centerY, pitch >= 5.0 ? 1 : 0);
    }

    private static IReadOnlyList<double> BuildPitchCandidates(StripProfile profile, GeometryLock? geometryLock)
    {
        var candidates = new List<double>(8);

        void Add(double pitch)
        {
            if (pitch < profile.Pitch * 0.94 || pitch > profile.Pitch * 1.06)
            {
                return;
            }

            if (candidates.Any(candidate => Math.Abs(candidate - pitch) < 0.001))
            {
                return;
            }

            candidates.Add(pitch);
        }

        if (geometryLock is not null)
        {
            Add(geometryLock.Pitch);
        }

        Add(profile.Pitch);
        Add(profile.Pitch * 0.98);
        Add(profile.Pitch * 1.02);
        Add(profile.Pitch * 0.96);
        Add(profile.Pitch * 1.04);
        Add(profile.Pitch * 0.99);
        Add(profile.Pitch * 1.01);

        return candidates;
    }

    private static DetectionResult? EvaluateCandidate(Bgr24Frame image, StripProfile profile, int originX, int originY, double pitch, string searchMode)
    {
        var bandWidth = profile.BandWidth * (pitch / profile.Pitch);
        var bandHeight = profile.BandHeight * (pitch / profile.Pitch);
        if (originX + bandWidth > image.Width || originY + bandHeight > image.Height)
        {
            return null;
        }

        var blackSamples = new List<double>();
        var whiteSamples = new List<double>();
        for (var column = 1; column <= profile.GridColumns; column++)
        {
            blackSamples.Add(SampleCellCenter(image, profile, column, 1, originX, originY, pitch));
            var bottomSample = SampleCellCenter(image, profile, column, profile.GridRows, originX, originY, pitch);
            if ((column - 1) % 2 == 0)
            {
                blackSamples.Add(bottomSample);
            }
            else
            {
                whiteSamples.Add(bottomSample);
            }
        }

        for (var row = 1; row <= profile.GridRows; row++)
        {
            blackSamples.Add(SampleCellCenter(image, profile, 1, row, originX, originY, pitch));
            var rightSample = SampleCellCenter(image, profile, profile.GridColumns, row, originX, originY, pitch);
            if ((row - 1) % 2 == 0)
            {
                blackSamples.Add(rightSample);
            }
            else
            {
                whiteSamples.Add(rightSample);
            }
        }

        whiteSamples.Add(image.GetGrayKernel(originX + (profile.QuietLeft / 2.0), originY + (profile.QuietTop / 2.0), pitch >= 5.0 ? 1 : 0));
        whiteSamples.Add(image.GetGrayKernel(originX + bandWidth - (profile.QuietRight / 2.0), originY + (profile.QuietTop / 2.0), pitch >= 5.0 ? 1 : 0));
        var blackMean = blackSamples.Average();
        var whiteMean = whiteSamples.Average();
        var contrast = whiteMean - blackMean;
        if (contrast <= 20)
        {
            return null;
        }

        var threshold = (int)Math.Floor((blackMean + whiteMean) / 2.0);
        var borderErrors = CountBorderErrors(image, profile, threshold, originX, originY, pitch);
        return new DetectionResult(profile, threshold, blackMean, whiteMean, borderErrors, originX, originY, pitch, bandWidth, bandHeight, contrast, searchMode);
    }

    private static int CountBorderErrors(Bgr24Frame image, StripProfile profile, int threshold, int originX, int originY, double pitch)
    {
        var errors = 0;
        for (var column = 1; column <= profile.GridColumns; column++)
        {
            if (SampleCellCenter(image, profile, column, 1, originX, originY, pitch) > threshold)
            {
                errors++;
            }

            var bottomDark = SampleCellCenter(image, profile, column, profile.GridRows, originX, originY, pitch) <= threshold;
            if (bottomDark != ((column - 1) % 2 == 0))
            {
                errors++;
            }
        }

        for (var row = 1; row <= profile.GridRows; row++)
        {
            if (SampleCellCenter(image, profile, 1, row, originX, originY, pitch) > threshold)
            {
                errors++;
            }

            var rightDark = SampleCellCenter(image, profile, profile.GridColumns, row, originX, originY, pitch) <= threshold;
            if (rightDark != ((row - 1) % 2 == 0))
            {
                errors++;
            }
        }

        return errors;
    }
}

public sealed record DecodedStrip(
    DetectionResult Detection,
    byte[] LeftDuplicateBytes,
    byte[] RightDuplicateBytes,
    byte[] TransportBytes,
    double MinMargin);

public static class StripDecoder
{
    public static DecodedStrip Decode(Bgr24Frame image, DetectionResult detection)
    {
        var profile = detection.Profile;
        var leftBits = new byte[profile.MetadataCellsPerSide];
        var rightBits = new byte[profile.MetadataCellsPerSide];
        var payloadBits = new byte[profile.PayloadCells];
        var minMargin = double.MaxValue;
        var leftCursor = 0;
        var rightCursor = 0;
        var payloadCursor = 0;

        for (var row = 1; row < profile.GridRows - 1; row++)
        {
            for (var interiorColumn = 1; interiorColumn <= profile.InteriorColumns; interiorColumn++)
            {
                var absoluteColumn = interiorColumn + 1;
                var value = StripLocator.SampleCellCenter(image, profile, absoluteColumn, row + 1, detection.OriginX, detection.OriginY, detection.Pitch);
                minMargin = Math.Min(minMargin, Math.Abs(value - detection.Threshold));
                var bit = (byte)(value <= detection.Threshold ? 1 : 0);
                if (interiorColumn <= profile.MetadataColumnsPerSide)
                {
                    leftBits[leftCursor++] = bit;
                }
                else if (interiorColumn > profile.MetadataColumnsPerSide + profile.PayloadColumns)
                {
                    rightBits[rightCursor++] = bit;
                }
                else
                {
                    payloadBits[payloadCursor++] = bit;
                }
            }
        }

        return new DecodedStrip(detection, BitPacking.BitsToBytes(leftBits), BitPacking.BitsToBytes(rightBits), BitPacking.BitsToBytes(payloadBits), minMargin == double.MaxValue ? 0 : minMargin);
    }
}

public sealed record FrameValidationResult(
    bool IsAccepted,
    string Reason,
    DetectionResult Detection,
    DecodedStrip Decoded,
    TransportFrame? Frame,
    bool DuplicateHeaderMatched);

public static class StripValidator
{
    public static FrameValidationResult Validate(DecodedStrip decoded)
    {
        if (decoded.Detection.BorderErrors > StripLocator.MaxBorderErrors)
        {
            return new FrameValidationResult(false, "Border validation failed", decoded.Detection, decoded, null, false);
        }

        if (!decoded.LeftDuplicateBytes.SequenceEqual(decoded.RightDuplicateBytes))
        {
            return new FrameValidationResult(false, "Header duplicate mismatch", decoded.Detection, decoded, null, false);
        }

        if (!TransportCodec.TryParse(decoded.TransportBytes, out var frame, out var reason))
        {
            return new FrameValidationResult(false, reason, decoded.Detection, decoded, null, false);
        }

        var duplicateMatched = decoded.LeftDuplicateBytes.SequenceEqual(TransportCodec.DuplicateHeaderBytes(frame!.RawBytes));
        return duplicateMatched
            ? new FrameValidationResult(true, "Accepted", decoded.Detection, decoded, frame, true)
            : new FrameValidationResult(false, "Duplicate header bytes disagreed with transport header", decoded.Detection, decoded, null, false);
    }
}
