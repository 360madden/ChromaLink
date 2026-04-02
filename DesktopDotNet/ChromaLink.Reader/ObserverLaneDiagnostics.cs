namespace ChromaLink.Reader;

public sealed record ObserverLaneMarkerSample(
    int MarkerIndex,
    byte ExpectedSymbol,
    byte ObservedSymbol,
    double Confidence,
    double Distance,
    byte SecondChoiceSymbol,
    double SecondChoiceDistance,
    int Left,
    int Top,
    int Width,
    int Height,
    int CenterX,
    int CenterY,
    Bgr24Color SampleColor);

public sealed record ObserverLaneReport(
    bool IsConfigured,
    bool IsProbablyVisible,
    int MatchedMarkers,
    int TotalMarkers,
    double AverageConfidence,
    string ExpectedPattern,
    string ObservedPattern,
    IReadOnlyList<ObserverLaneMarkerSample> Markers);

internal readonly record struct ObserverClassification(
    byte Symbol,
    double Confidence,
    double Distance,
    byte SecondChoiceSymbol,
    double SecondChoiceDistance);

public static class ObserverLaneAnalyzer
{
    public static ObserverLaneReport Analyze(Bgr24Frame image, StripProfile profile, DetectionResult? detection = null)
    {
        if (profile.ObserverLane is null || profile.ObserverLane.MarkerSymbols.Length == 0)
        {
            return new ObserverLaneReport(false, false, 0, 0, 0, "-", "-", Array.Empty<ObserverLaneMarkerSample>());
        }

        var observer = profile.ObserverLane;
        var originX = detection?.OriginX ?? 0;
        var originY = detection?.OriginY ?? 0;
        var scale = detection?.Scale ?? 1.0;
        var samples = new List<ObserverLaneMarkerSample>(observer.MarkerSymbols.Length);
        var matchCount = 0;
        double confidenceTotal = 0;

        for (var index = 0; index < observer.MarkerSymbols.Length; index++)
        {
            var fraction = observer.MarkerSymbols.Length > 1
                ? index / (double)(observer.MarkerSymbols.Length - 1)
                : 0.0;
            var left = fraction * Math.Max(0, profile.BandWidth - observer.MarkerWidth);
            var scaledLeft = (int)Math.Round(originX + (left * scale));
            var scaledTop = (int)Math.Round(originY + (observer.OffsetY * scale));
            var scaledWidth = Math.Max(1, (int)Math.Round(observer.MarkerWidth * scale));
            var scaledHeight = Math.Max(1, (int)Math.Round(observer.Height * scale));
            var centerX = scaledLeft + (scaledWidth / 2);
            var centerY = scaledTop + (scaledHeight / 2);
            var color = image.SampleAverage(centerX, centerY, 1);
            var classification = Classify(color, profile);
            var expected = observer.MarkerSymbols[index];

            if (classification.Symbol == expected)
            {
                matchCount++;
            }

            confidenceTotal += classification.Confidence;
            samples.Add(new ObserverLaneMarkerSample(
                index,
                expected,
                classification.Symbol,
                classification.Confidence,
                classification.Distance,
                classification.SecondChoiceSymbol,
                classification.SecondChoiceDistance,
                scaledLeft,
                scaledTop,
                scaledWidth,
                scaledHeight,
                centerX,
                centerY,
                color));
        }

        var averageConfidence = samples.Count == 0 ? 0 : confidenceTotal / samples.Count;
        var expectedPattern = string.Join(" ", observer.MarkerSymbols.Select(static symbol => symbol.ToString()));
        var observedPattern = string.Join(" ", samples.Select(static sample => sample.ObservedSymbol.ToString()));
        var isProbablyVisible = samples.Count > 0
            && matchCount >= Math.Max(1, (int)Math.Ceiling(samples.Count * 0.75))
            && averageConfidence >= 0.35;

        return new ObserverLaneReport(
            true,
            isProbablyVisible,
            matchCount,
            samples.Count,
            averageConfidence,
            expectedPattern,
            observedPattern,
            samples);
    }

    private static ObserverClassification Classify(Bgr24Color sampleColor, StripProfile profile)
    {
        var bestDistance = double.MaxValue;
        var secondDistance = double.MaxValue;
        byte bestSymbol = 0;
        byte secondSymbol = 0;

        foreach (var entry in profile.Palette)
        {
            var distance = Math.Sqrt(sampleColor.DistanceSquared(entry.Color));
            if (distance < bestDistance)
            {
                secondDistance = bestDistance;
                secondSymbol = bestSymbol;
                bestDistance = distance;
                bestSymbol = entry.Symbol;
            }
            else if (distance < secondDistance)
            {
                secondDistance = distance;
                secondSymbol = entry.Symbol;
            }
        }

        var separation = secondDistance <= 0.0001
            ? 1.0
            : Math.Clamp((secondDistance - bestDistance) / secondDistance, 0.0, 1.0);
        var absolute = Math.Clamp(1.0 - (bestDistance / 180.0), 0.0, 1.0);
        return new ObserverClassification(bestSymbol, separation * absolute, bestDistance, secondSymbol, secondDistance);
    }
}
