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
        Assert.Equal("Accepted (left-control fallback)", validation.Reason);
        Assert.NotNull(validation.Detection);
        Assert.Equal("left-control-scan", validation.Detection!.SearchMode);
    }

    [Fact]
    public void Analyzer_FlatBlackFrame_UsesHelpfulMissingStripReason()
    {
        var image = Bgr24Frame.CreateSolid(_profile.BandWidth, _profile.CaptureHeight, Bgr24Color.Black, "flat");

        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.False(validation.IsAccepted);
        Assert.Contains("blank surface", validation.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
