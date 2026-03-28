using ChromaLink.Reader;

namespace ChromaLink.Tests;

public class ProtocolAndReplayTests
{
    private readonly StripProfile _profile = StripProfiles.Default;

    [Fact]
    public void CoreFrame_RoundTripsThroughSyntheticRendererAndDecoder()
    {
        var bytes = FrameSerializer.BuildCoreFrameBytes(_profile.NumericId, 7, TelemetrySnapshot.CreateSynthetic());
        var image = StripRenderer.Render(_profile, bytes);
        var validation = Validate(image);

        Assert.True(validation.IsAccepted, validation.Reason);
        Assert.NotNull(validation.Frame);
        Assert.Equal(FrameType.CoreStatus, validation.Frame!.Header.FrameType);

        var payload = FrameSerializer.ParseCorePayload(validation.Frame.Payload);
        Assert.Equal(11770, payload.PlayerHealthCurrent);
        Assert.Equal(6320, payload.TargetHealthCurrent);
    }

    [Fact]
    public void TacticalFrame_RoundTripsThroughSyntheticRendererAndDecoder()
    {
        var bytes = FrameSerializer.BuildTacticalFrameBytes(_profile.NumericId, 8, TelemetrySnapshot.CreateSynthetic());
        var image = StripRenderer.Render(_profile, bytes);
        var validation = Validate(image);

        Assert.True(validation.IsAccepted, validation.Reason);
        Assert.NotNull(validation.Frame);
        Assert.Equal(FrameType.Tactical, validation.Frame!.Header.FrameType);

        var payload = FrameSerializer.ParseTacticalPayload(validation.Frame.Payload);
        Assert.Equal(73651, payload.PlayerCoordX10);
        Assert.Equal(18, payload.TargetRadiusQ10);
    }

    [Fact]
    public void ReplayRunner_AcceptsBaselineOffsetAndBlurScenarios()
    {
        var bytes = FrameSerializer.BuildCoreFrameBytes(_profile.NumericId, 9, TelemetrySnapshot.CreateSynthetic());
        var scenarios = new[]
        {
            new PerturbationOptions("baseline"),
            new PerturbationOptions("offset", 2, 1),
            new PerturbationOptions("blur", BlurRadius: 1),
            new PerturbationOptions("scale", Scale: 1.02)
        };

        var results = ReplayRunner.Run(_profile, bytes, scenarios);
        Assert.All(results, static result => Assert.True(result.Result.IsAccepted, result.Result.Reason));
    }

    private FrameValidationResult Validate(Bgr24Frame image)
    {
        var detection = StripLocator.Locate(image, _profile);
        var decoded = StripDecoder.Decode(image, detection);
        return StripValidator.Validate(decoded);
    }
}
