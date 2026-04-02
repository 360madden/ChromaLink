namespace ChromaLink.Reader;

public enum ObserverMarkerBoundsState
{
    Inside,
    Partial,
    Outside
}

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
    double VisibleFraction,
    bool CenterInBounds,
    ObserverMarkerBoundsState BoundsState,
    Bgr24Color SampleColor);

public sealed record ObserverLaneReport(
    bool IsConfigured,
    bool IsProbablyVisible,
    int MatchedMarkers,
    int TotalMarkers,
    double AverageConfidence,
    string ExpectedPattern,
    string ObservedPattern,
    string VisibilityHint,
    int FullyVisibleMarkers,
    int PartiallyVisibleMarkers,
    int OutsideMarkers,
    int LeftEdgeAffectedMarkers,
    int RightEdgeAffectedMarkers,
    int TopEdgeAffectedMarkers,
    int BottomEdgeAffectedMarkers,
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
            return new ObserverLaneReport(false, false, 0, 0, 0, "-", "-", "not-configured", 0, 0, 0, 0, 0, 0, 0, Array.Empty<ObserverLaneMarkerSample>());
        }

        var observer = profile.ObserverLane;
        var originX = detection?.OriginX ?? 0;
        var originY = detection?.OriginY ?? 0;
        var scale = detection?.Scale ?? 1.0;
        var samples = new List<ObserverLaneMarkerSample>(observer.MarkerSymbols.Length);
        var matchCount = 0;
        double confidenceTotal = 0;
        var fullyVisibleMarkers = 0;
        var partiallyVisibleMarkers = 0;
        var outsideMarkers = 0;
        var leftEdgeAffectedMarkers = 0;
        var rightEdgeAffectedMarkers = 0;
        var topEdgeAffectedMarkers = 0;
        var bottomEdgeAffectedMarkers = 0;

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
            var clippedLeft = scaledLeft < 0;
            var clippedTop = scaledTop < 0;
            var clippedRight = scaledLeft + scaledWidth > image.Width;
            var clippedBottom = scaledTop + scaledHeight > image.Height;
            var visibleLeft = Math.Max(0, scaledLeft);
            var visibleTop = Math.Max(0, scaledTop);
            var visibleRight = Math.Min(image.Width, scaledLeft + scaledWidth);
            var visibleBottom = Math.Min(image.Height, scaledTop + scaledHeight);
            var visibleWidth = Math.Max(0, visibleRight - visibleLeft);
            var visibleHeight = Math.Max(0, visibleBottom - visibleTop);
            var visibleArea = visibleWidth * visibleHeight;
            var totalArea = Math.Max(1, scaledWidth * scaledHeight);
            var visibleFraction = visibleArea / (double)totalArea;
            var centerInBounds = centerX >= 0 && centerX < image.Width && centerY >= 0 && centerY < image.Height;
            var boundsState = visibleArea <= 0
                ? ObserverMarkerBoundsState.Outside
                : visibleArea < totalArea
                    ? ObserverMarkerBoundsState.Partial
                    : ObserverMarkerBoundsState.Inside;

            if (classification.Symbol == expected)
            {
                matchCount++;
            }

            confidenceTotal += classification.Confidence;

            switch (boundsState)
            {
                case ObserverMarkerBoundsState.Inside:
                    fullyVisibleMarkers++;
                    break;
                case ObserverMarkerBoundsState.Partial:
                    partiallyVisibleMarkers++;
                    break;
                case ObserverMarkerBoundsState.Outside:
                    outsideMarkers++;
                    break;
            }

            if (clippedLeft)
            {
                leftEdgeAffectedMarkers++;
            }

            if (clippedRight)
            {
                rightEdgeAffectedMarkers++;
            }

            if (clippedTop)
            {
                topEdgeAffectedMarkers++;
            }

            if (clippedBottom)
            {
                bottomEdgeAffectedMarkers++;
            }

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
                visibleFraction,
                centerInBounds,
                boundsState,
                color));
        }

        var averageConfidence = samples.Count == 0 ? 0 : confidenceTotal / samples.Count;
        var expectedPattern = string.Join(" ", observer.MarkerSymbols.Select(static symbol => symbol.ToString()));
        var observedPattern = string.Join(" ", samples.Select(static sample => sample.ObservedSymbol.ToString()));
        var isProbablyVisible = samples.Count > 0
            && matchCount >= Math.Max(1, (int)Math.Ceiling(samples.Count * 0.75))
            && partiallyVisibleMarkers == 0
            && outsideMarkers == 0
            && averageConfidence >= 0.35;
        var visibilityHint = BuildVisibilityHint(
            isProbablyVisible,
            samples.Count,
            averageConfidence,
            matchCount,
            fullyVisibleMarkers,
            partiallyVisibleMarkers,
            outsideMarkers,
            leftEdgeAffectedMarkers,
            rightEdgeAffectedMarkers,
            topEdgeAffectedMarkers,
            bottomEdgeAffectedMarkers);

        return new ObserverLaneReport(
            true,
            isProbablyVisible,
            matchCount,
            samples.Count,
            averageConfidence,
            expectedPattern,
            observedPattern,
            visibilityHint,
            fullyVisibleMarkers,
            partiallyVisibleMarkers,
            outsideMarkers,
            leftEdgeAffectedMarkers,
            rightEdgeAffectedMarkers,
            topEdgeAffectedMarkers,
            bottomEdgeAffectedMarkers,
            samples);
    }

    private static string BuildVisibilityHint(
        bool isProbablyVisible,
        int totalMarkers,
        double averageConfidence,
        int matchCount,
        int fullyVisibleMarkers,
        int partiallyVisibleMarkers,
        int outsideMarkers,
        int leftEdgeAffectedMarkers,
        int rightEdgeAffectedMarkers,
        int topEdgeAffectedMarkers,
        int bottomEdgeAffectedMarkers)
    {
        if (totalMarkers == 0)
        {
            return "no-markers";
        }

        if (outsideMarkers == totalMarkers)
        {
            return "offscreen";
        }

        if (rightEdgeAffectedMarkers > 0 && leftEdgeAffectedMarkers == 0)
        {
            return outsideMarkers > 0 ? "right-offscreen" : "right-clipped";
        }

        if (leftEdgeAffectedMarkers > 0 && rightEdgeAffectedMarkers == 0)
        {
            return outsideMarkers > 0 ? "left-offscreen" : "left-clipped";
        }

        if (bottomEdgeAffectedMarkers > 0 && topEdgeAffectedMarkers == 0)
        {
            return outsideMarkers > 0 ? "bottom-offscreen" : "bottom-clipped";
        }

        if (topEdgeAffectedMarkers > 0 && bottomEdgeAffectedMarkers == 0)
        {
            return outsideMarkers > 0 ? "top-offscreen" : "top-clipped";
        }

        if (partiallyVisibleMarkers > 0 || outsideMarkers > 0)
        {
            return "partially-clipped";
        }

        if (isProbablyVisible)
        {
            return "visible";
        }

        if (fullyVisibleMarkers == totalMarkers && averageConfidence < 0.35)
        {
            return "weak-colors";
        }

        if (matchCount == 0)
        {
            return "pattern-mismatch";
        }

        return "uncertain";
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
