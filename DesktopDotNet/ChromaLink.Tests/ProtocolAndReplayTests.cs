using ChromaLink.Reader;
using Xunit;

namespace ChromaLink.Tests;

public class ProtocolAndReplayTests
{
    private readonly StripProfile _profile = StripProfiles.Default;

    [Fact]
    public void CoreFrame_RoundTripsThroughRendererAndAnalyzer()
    {
        var bytes = FrameProtocol.BuildCoreFrameBytes(_profile.NumericId, 7, CoreStatusSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        Assert.NotNull(validation.Frame);
        Assert.Equal(FrameType.CoreStatus, validation.Frame!.Header.FrameType);
        Assert.Equal(198, validation.Frame.Payload.PlayerHealthPctQ8);
        Assert.Equal(91, validation.Frame.Payload.TargetHealthPctQ8);
    }

    [Fact]
    public void ReplayRunner_AcceptsConfiguredBenchScenarios()
    {
        var bytes = FrameProtocol.BuildCoreFrameBytes(_profile.NumericId, 9, CoreStatusSnapshot.CreateSynthetic());
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
        Assert.All(results, static result => Assert.True(result.Result.IsAccepted, result.Result.Reason));
    }

    [Fact]
    public void Analyzer_RejectsControlMarkerMismatch()
    {
        var bytes = FrameProtocol.BuildCoreFrameBytes(_profile.NumericId, 3, CoreStatusSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        image.FillRect(0, 0, _profile.SegmentWidth, _profile.SegmentHeight, _profile.GetPaletteColor(7));

        var validation = ColorStripAnalyzer.Analyze(image, _profile);
        Assert.False(validation.IsAccepted);
        Assert.Equal("Control marker mismatch.", validation.Reason);
    }

    [Fact]
    public void Analyzer_CanFallbackWhenOnlyRightControlIsCorrupted()
    {
        var bytes = FrameProtocol.BuildCoreFrameBytes(_profile.NumericId, 12, CoreStatusSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var rightStart = (_profile.SegmentCount - _profile.RightControl.Length) * _profile.SegmentWidth;
        image.FillRect(rightStart, 0, _profile.RightControl.Length * _profile.SegmentWidth, _profile.SegmentHeight, _profile.GetPaletteColor(6));

        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        Assert.NotNull(validation.Detection);
        Assert.Equal("fixed-profile", validation.Detection!.SearchMode);
        Assert.True(validation.Detection.RightControlScore > validation.Detection.LeftControlScore);
    }

    [Fact]
    public void Analyzer_FlatBlackFrame_UsesHelpfulMissingStripReason()
    {
        var image = Bgr24Frame.CreateSolid(_profile.BandWidth, _profile.CaptureHeight, Bgr24Color.Black, "flat");

        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.False(validation.IsAccepted);
        Assert.Contains("blank surface", validation.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyzer_Accepts_RealClientScaledFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "live-client-scale-035.bmp");
        var image = BmpIO.Load(fixturePath);

        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        Assert.NotNull(validation.Detection);
        Assert.Equal("fixed-profile", validation.Detection!.SearchMode);
        Assert.Equal(0.35, validation.Detection.Scale, 2);
        Assert.Equal(2.8, validation.Detection.Pitch, 1);
        Assert.NotNull(validation.ParseResult);
        Assert.True(validation.ParseResult!.HeaderCrcValid);
        Assert.True(validation.ParseResult.PayloadCrcValid);
    }

    [Fact]
    public void Analyzer_Accepts_Synthetic640CanvasAtLiveScale()
    {
        var bytes = FrameProtocol.BuildCoreFrameBytes(_profile.NumericId, 31, CoreStatusSnapshot.CreateSynthetic());
        var strip = ColorStripRenderer.Render(_profile, bytes).ScaleNearest(0.35, "scaled-live-strip");
        var canvas = Bgr24Frame.CreateSolid(_profile.WindowWidth, _profile.CaptureHeight, _profile.GetPaletteColor(0), "scaled-live-canvas");
        canvas.Paste(strip, 0, 0);

        var validation = ColorStripAnalyzer.Analyze(canvas, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        Assert.NotNull(validation.Detection);
        Assert.Equal(0.35, validation.Detection!.Scale, 2);
        Assert.Equal(2.8, validation.Detection.Pitch, 1);
        Assert.NotNull(validation.Frame);
        Assert.Equal(FrameType.CoreStatus, validation.Frame!.Header.FrameType);
    }

    [Fact]
    public void ObserverLaneAnalyzer_Detects_ConfiguredMarkers_OnSyntheticCanvas()
    {
        var canvas = Bgr24Frame.CreateSolid(_profile.WindowWidth, _profile.CaptureHeight, _profile.GetPaletteColor(0), "observer-canvas");
        var observer = _profile.ObserverLane!;

        for (var index = 0; index < observer.MarkerSymbols.Length; index++)
        {
            var fraction = observer.MarkerSymbols.Length > 1
                ? index / (double)(observer.MarkerSymbols.Length - 1)
                : 0.0;
            var left = (int)Math.Round(fraction * Math.Max(0, _profile.BandWidth - observer.MarkerWidth));
            canvas.FillRect(left, observer.OffsetY, observer.MarkerWidth, observer.Height, _profile.GetPaletteColor(observer.MarkerSymbols[index]));
        }

        var report = ObserverLaneAnalyzer.Analyze(canvas, _profile);

        Assert.True(report.IsConfigured);
        Assert.True(report.IsProbablyVisible);
        Assert.Equal(observer.MarkerSymbols.Length, report.MatchedMarkers);
        Assert.Equal("0 1 2 3 4 5 6 7", report.ObservedPattern);
        Assert.Equal("visible", report.VisibilityHint);
        Assert.Equal(observer.MarkerSymbols.Length, report.FullyVisibleMarkers);
        Assert.Equal(0, report.PartiallyVisibleMarkers);
        Assert.Equal(0, report.OutsideMarkers);
    }

    [Fact]
    public void ObserverLaneAnalyzer_Detects_ConfiguredMarkers_OnScaledLiveCanvas()
    {
        var observer = _profile.ObserverLane!;
        var scale = 0.35;
        var canvas = Bgr24Frame.CreateSolid(_profile.WindowWidth, _profile.CaptureHeight, _profile.GetPaletteColor(0), "observer-live-canvas");
        var detection = new DetectionResult(0, 0, 2.8, scale, 0, 0, 0, 229, "fixed-profile", Bgr24Color.Black, Bgr24Color.White);

        for (var index = 0; index < observer.MarkerSymbols.Length; index++)
        {
            var fraction = observer.MarkerSymbols.Length > 1
                ? index / (double)(observer.MarkerSymbols.Length - 1)
                : 0.0;
            var left = fraction * Math.Max(0, _profile.BandWidth - observer.MarkerWidth);
            var scaledLeft = (int)Math.Round(left * scale);
            var scaledTop = (int)Math.Round(observer.OffsetY * scale);
            var scaledWidth = Math.Max(1, (int)Math.Round(observer.MarkerWidth * scale));
            var scaledHeight = Math.Max(1, (int)Math.Round(observer.Height * scale));
            canvas.FillRect(scaledLeft, scaledTop, scaledWidth, scaledHeight, _profile.GetPaletteColor(observer.MarkerSymbols[index]));
        }

        var report = ObserverLaneAnalyzer.Analyze(canvas, _profile, detection);

        Assert.True(report.IsConfigured);
        Assert.True(report.IsProbablyVisible);
        Assert.Equal(observer.MarkerSymbols.Length, report.MatchedMarkers);
        Assert.Equal("0 1 2 3 4 5 6 7", report.ObservedPattern);
        Assert.Equal("visible", report.VisibilityHint);
    }

    [Fact]
    public void ObserverLaneAnalyzer_Flags_RightClipping_WhenMarkersRunPastCanvas()
    {
        var observer = _profile.ObserverLane!;
        var scale = 0.35;
        var canvas = Bgr24Frame.CreateSolid(_profile.WindowWidth, _profile.CaptureHeight, _profile.GetPaletteColor(0), "observer-right-clipped");
        var detection = new DetectionResult(420, 0, 2.8, scale, 0, 0, 0, 229, "fixed-profile", Bgr24Color.Black, Bgr24Color.White);

        for (var index = 0; index < observer.MarkerSymbols.Length; index++)
        {
            var fraction = observer.MarkerSymbols.Length > 1
                ? index / (double)(observer.MarkerSymbols.Length - 1)
                : 0.0;
            var left = fraction * Math.Max(0, _profile.BandWidth - observer.MarkerWidth);
            var scaledLeft = detection.OriginX + (int)Math.Round(left * scale);
            var scaledTop = (int)Math.Round(observer.OffsetY * scale);
            var scaledWidth = Math.Max(1, (int)Math.Round(observer.MarkerWidth * scale));
            var scaledHeight = Math.Max(1, (int)Math.Round(observer.Height * scale));
            canvas.FillRect(scaledLeft, scaledTop, scaledWidth, scaledHeight, _profile.GetPaletteColor(observer.MarkerSymbols[index]));
        }

        var report = ObserverLaneAnalyzer.Analyze(canvas, _profile, detection);

        Assert.True(report.IsConfigured);
        Assert.False(report.IsProbablyVisible);
        Assert.Equal("right-clipped", report.VisibilityHint);
        Assert.True(report.RightEdgeAffectedMarkers > 0);
        Assert.True(report.PartiallyVisibleMarkers > 0 || report.OutsideMarkers > 0);
        Assert.Contains(report.Markers, marker => marker.BoundsState != ObserverMarkerBoundsState.Inside);
    }
}
