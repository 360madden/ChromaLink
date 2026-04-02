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
        var frame = Assert.IsType<CoreStatusFrame>(validation.Frame);
        Assert.Equal(FrameType.CoreStatus, frame.Header.FrameType);
        Assert.Equal(TransportConstants.HeaderCapabilities, (HeaderCapabilityFlags)frame.Header.ReservedFlags);
        Assert.Equal(198, frame.Payload.PlayerHealthPctQ8);
        Assert.Equal(91, frame.Payload.TargetHealthPctQ8);
    }

    [Fact]
    public void PlayerVitalsFrame_RoundTripsThroughRendererAndAnalyzer()
    {
        var bytes = FrameProtocol.BuildPlayerVitalsFrameBytes(_profile.NumericId, 11, PlayerVitalsSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        var frame = Assert.IsType<PlayerVitalsFrame>(validation.Frame);
        Assert.Equal(FrameType.PlayerVitals, frame.Header.FrameType);
        Assert.Equal(TransportConstants.HeaderCapabilities, (HeaderCapabilityFlags)frame.Header.ReservedFlags);
        Assert.Equal((uint)3260, frame.Payload.HealthCurrent);
        Assert.Equal((ushort)100, frame.Payload.ResourceCurrent);
    }

    [Fact]
    public void PlayerPositionFrame_RoundTripsThroughRendererAndAnalyzer()
    {
        var bytes = FrameProtocol.BuildPlayerPositionFrameBytes(_profile.NumericId, 13, PlayerPositionSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        var frame = Assert.IsType<PlayerPositionFrame>(validation.Frame);
        Assert.Equal(FrameType.PlayerPosition, frame.Header.FrameType);
        Assert.Equal(TransportConstants.HeaderCapabilities, (HeaderCapabilityFlags)frame.Header.ReservedFlags);
        Assert.Equal(123.45f, frame.Payload.X, 2);
        Assert.Equal(200.67f, frame.Payload.Y, 2);
        Assert.Equal(-50.12f, frame.Payload.Z, 2);
    }

    [Fact]
    public void PlayerCastFrame_RoundTripsThroughRendererAndAnalyzer()
    {
        var bytes = FrameProtocol.BuildPlayerCastFrameBytes(_profile.NumericId, 15, PlayerCastSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        var frame = Assert.IsType<PlayerCastFrame>(validation.Frame);
        Assert.Equal(FrameType.PlayerCast, frame.Header.FrameType);
        Assert.Equal(TransportConstants.HeaderCapabilities, (HeaderCapabilityFlags)frame.Header.ReservedFlags);
        Assert.Equal((byte)0b0001_1001, frame.Payload.CastFlags);
        Assert.Equal((byte)96, frame.Payload.ProgressPctQ8);
        Assert.Equal((ushort)250, frame.Payload.DurationCenti);
        Assert.Equal((ushort)150, frame.Payload.RemainingCenti);
        Assert.Equal((byte)2, frame.Payload.CastTargetCode);
        Assert.Equal("HEALI", frame.Payload.SpellLabel);
    }

    [Fact]
    public void PlayerResourcesFrame_RoundTripsThroughRendererAndAnalyzer()
    {
        var bytes = FrameProtocol.BuildPlayerResourcesFrameBytes(_profile.NumericId, 17, PlayerResourcesSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        var frame = Assert.IsType<PlayerResourcesFrame>(validation.Frame);
        Assert.Equal(FrameType.PlayerResources, frame.Header.FrameType);
        Assert.Equal(TransportConstants.HeaderCapabilities, (HeaderCapabilityFlags)frame.Header.ReservedFlags);
        Assert.Equal((ushort)4200, frame.Payload.ManaCurrent);
        Assert.Equal((ushort)85, frame.Payload.EnergyCurrent);
        Assert.Equal((ushort)12, frame.Payload.PowerCurrent);
    }

    [Fact]
    public void PlayerCombatFrame_RoundTripsThroughRendererAndAnalyzer()
    {
        var bytes = FrameProtocol.BuildPlayerCombatFrameBytes(_profile.NumericId, 19, PlayerCombatSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        var frame = Assert.IsType<PlayerCombatFrame>(validation.Frame);
        Assert.Equal(FrameType.PlayerCombat, frame.Header.FrameType);
        Assert.Equal(TransportConstants.HeaderCapabilities, (HeaderCapabilityFlags)frame.Header.ReservedFlags);
        Assert.Equal((byte)255, frame.Payload.CombatFlags);
        Assert.Equal((byte)4, frame.Payload.Combo);
        Assert.Equal((ushort)250, frame.Payload.Absorb);
    }

    [Fact]
    public void TargetVitalsFrame_RoundTripsThroughRendererAndAnalyzer()
    {
        var bytes = FrameProtocol.BuildTargetVitalsFrameBytes(_profile.NumericId, 25, TargetVitalsSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        var frame = Assert.IsType<TargetVitalsFrame>(validation.Frame);
        Assert.Equal(FrameType.TargetVitals, frame.Header.FrameType);
        Assert.Equal(TransportConstants.HeaderCapabilities, (HeaderCapabilityFlags)frame.Header.ReservedFlags);
        Assert.Equal((uint)31200, frame.Payload.HealthCurrent);
        Assert.Equal((uint)35000, frame.Payload.HealthMax);
        Assert.Equal((ushort)120, frame.Payload.Absorb);
        Assert.Equal((byte)0b0000_1111, frame.Payload.TargetFlags);
        Assert.Equal((byte)72, frame.Payload.TargetLevel);
    }

    [Fact]
    public void TargetResourcesFrame_RoundTripsThroughRendererAndAnalyzer()
    {
        var bytes = FrameProtocol.BuildTargetResourcesFrameBytes(_profile.NumericId, 27, TargetResourcesSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        var frame = Assert.IsType<TargetResourcesFrame>(validation.Frame);
        Assert.Equal(FrameType.TargetResources, frame.Header.FrameType);
        Assert.Equal(TransportConstants.HeaderCapabilities, (HeaderCapabilityFlags)frame.Header.ReservedFlags);
        Assert.Equal((ushort)2200, frame.Payload.ManaCurrent);
        Assert.Equal((ushort)3000, frame.Payload.ManaMax);
        Assert.Equal((ushort)80, frame.Payload.EnergyCurrent);
        Assert.Equal((ushort)100, frame.Payload.EnergyMax);
        Assert.Equal((ushort)18, frame.Payload.PowerCurrent);
        Assert.Equal((ushort)100, frame.Payload.PowerMax);
    }

    [Fact]
    public void AuxUnitCastFrame_RoundTripsThroughRendererAndAnalyzer()
    {
        var bytes = FrameProtocol.BuildAuxUnitCastFrameBytes(_profile.NumericId, 29, AuxUnitCastSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        var frame = Assert.IsType<AuxUnitCastFrame>(validation.Frame);
        Assert.Equal(FrameType.AuxUnitCast, frame.Header.FrameType);
        Assert.Equal(TransportConstants.HeaderCapabilities, (HeaderCapabilityFlags)frame.Header.ReservedFlags);
        Assert.Equal((byte)2, frame.Payload.UnitSelectorCode);
        Assert.Equal((byte)0b0001_0011, frame.Payload.CastFlags);
        Assert.Equal((byte)88, frame.Payload.ProgressPctQ8);
        Assert.Equal((ushort)180, frame.Payload.DurationCenti);
        Assert.Equal((ushort)60, frame.Payload.RemainingCenti);
        Assert.Equal((byte)1, frame.Payload.CastTargetCode);
        Assert.Equal("SHLD", frame.Payload.Label);
    }

    [Fact]
    public void AuraPageFrame_RoundTripsThroughRendererAndAnalyzer()
    {
        var bytes = FrameProtocol.BuildAuraPageFrameBytes(_profile.NumericId, 31, AuraPageSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        var frame = Assert.IsType<AuraPageFrame>(validation.Frame);
        Assert.Equal(FrameType.AuraPage, frame.Header.FrameType);
        Assert.Equal(TransportConstants.HeaderCapabilities, (HeaderCapabilityFlags)frame.Header.ReservedFlags);
        Assert.Equal((byte)1, frame.Payload.PageKindCode);
        Assert.Equal((byte)8, frame.Payload.TotalAuraCount);
        Assert.Equal((ushort)1001, frame.Payload.Entry1.Id);
        Assert.Equal((ushort)1002, frame.Payload.Entry2.Id);
    }

    [Fact]
    public void TextPageFrame_RoundTripsThroughRendererAndAnalyzer()
    {
        var bytes = FrameProtocol.BuildTextPageFrameBytes(_profile.NumericId, 33, TextPageSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        var frame = Assert.IsType<TextPageFrame>(validation.Frame);
        Assert.Equal(FrameType.TextPage, frame.Header.FrameType);
        Assert.Equal(TransportConstants.HeaderCapabilities, (HeaderCapabilityFlags)frame.Header.ReservedFlags);
        Assert.Equal((byte)3, frame.Payload.TextKindCode);
        Assert.Equal((ushort)0xBEEF, frame.Payload.TextHash16);
        Assert.Equal("AURA TEXT", frame.Payload.Label);
    }

    [Fact]
    public void AbilityWatchFrame_RoundTripsThroughRendererAndAnalyzer()
    {
        var bytes = FrameProtocol.BuildAbilityWatchFrameBytes(_profile.NumericId, 35, AbilityWatchSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        var frame = Assert.IsType<AbilityWatchFrame>(validation.Frame);
        Assert.Equal(FrameType.AbilityWatch, frame.Header.FrameType);
        Assert.Equal(TransportConstants.HeaderCapabilities, (HeaderCapabilityFlags)frame.Header.ReservedFlags);
        Assert.Equal((byte)4, frame.Payload.PageIndex);
        Assert.Equal((ushort)2001, frame.Payload.Entry1.Id);
        Assert.Equal((ushort)2002, frame.Payload.Entry2.Id);
        Assert.Equal((byte)8, frame.Payload.ShortestCooldownQ4);
        Assert.Equal((byte)3, frame.Payload.ReadyCount);
        Assert.Equal((byte)2, frame.Payload.CoolingCount);
    }

    [Fact]
    public void TargetPositionFrame_RoundTripsThroughRendererAndAnalyzer()
    {
        var bytes = FrameProtocol.BuildTargetPositionFrameBytes(_profile.NumericId, 21, TargetPositionSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        var frame = Assert.IsType<TargetPositionFrame>(validation.Frame);
        Assert.Equal(FrameType.TargetPosition, frame.Header.FrameType);
        Assert.Equal(128.75f, frame.Payload.X, 2);
        Assert.Equal(201.50f, frame.Payload.Y, 2);
        Assert.Equal(-48.25f, frame.Payload.Z, 2);
    }

    [Fact]
    public void FollowUnitStatusFrame_RoundTripsThroughRendererAndAnalyzer()
    {
        var bytes = FrameProtocol.BuildFollowUnitStatusFrameBytes(_profile.NumericId, 23, FollowUnitStatusSnapshot.CreateSynthetic());
        var image = ColorStripRenderer.Render(_profile, bytes);
        var validation = ColorStripAnalyzer.Analyze(image, _profile);

        Assert.True(validation.IsAccepted, validation.Reason);
        var frame = Assert.IsType<FollowUnitStatusFrame>(validation.Frame);
        Assert.Equal(FrameType.FollowUnitStatus, frame.Header.FrameType);
        Assert.Equal((byte)1, frame.Payload.Slot);
        Assert.Equal((byte)143, frame.Payload.FollowFlags);
        Assert.Equal(7123.5f, frame.Payload.X, 1);
        Assert.Equal(865.0f, frame.Payload.Y, 1);
        Assert.Equal(3010.5f, frame.Payload.Z, 1);
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

    [Fact]
    public void TelemetryAggregate_BecomesReady_WhenAllFrameTypesArrive()
    {
        var aggregate = new TelemetryAggregate();
        var baseTime = new DateTimeOffset(2026, 4, 2, 12, 0, 0, TimeSpan.Zero);

        var core = Assert.IsType<CoreStatusFrame>(
            FrameProtocol.AnalyzeFrameBytes(
                FrameProtocol.BuildCoreFrameBytes(_profile.NumericId, 7, CoreStatusSnapshot.CreateSynthetic())).Frame);
        var vitals = Assert.IsType<PlayerVitalsFrame>(
            FrameProtocol.AnalyzeFrameBytes(
                FrameProtocol.BuildPlayerVitalsFrameBytes(_profile.NumericId, 11, PlayerVitalsSnapshot.CreateSynthetic())).Frame);
        var position = Assert.IsType<PlayerPositionFrame>(
            FrameProtocol.AnalyzeFrameBytes(
                FrameProtocol.BuildPlayerPositionFrameBytes(_profile.NumericId, 13, PlayerPositionSnapshot.CreateSynthetic())).Frame);
        var cast = Assert.IsType<PlayerCastFrame>(
            FrameProtocol.AnalyzeFrameBytes(
                FrameProtocol.BuildPlayerCastFrameBytes(_profile.NumericId, 15, PlayerCastSnapshot.CreateSynthetic())).Frame);
        var resources = Assert.IsType<PlayerResourcesFrame>(
            FrameProtocol.AnalyzeFrameBytes(
                FrameProtocol.BuildPlayerResourcesFrameBytes(_profile.NumericId, 17, PlayerResourcesSnapshot.CreateSynthetic())).Frame);
        var combat = Assert.IsType<PlayerCombatFrame>(
            FrameProtocol.AnalyzeFrameBytes(
                FrameProtocol.BuildPlayerCombatFrameBytes(_profile.NumericId, 19, PlayerCombatSnapshot.CreateSynthetic())).Frame);
        var targetPosition = Assert.IsType<TargetPositionFrame>(
            FrameProtocol.AnalyzeFrameBytes(
                FrameProtocol.BuildTargetPositionFrameBytes(_profile.NumericId, 21, TargetPositionSnapshot.CreateSynthetic())).Frame);
        var follow = Assert.IsType<FollowUnitStatusFrame>(
            FrameProtocol.AnalyzeFrameBytes(
                FrameProtocol.BuildFollowUnitStatusFrameBytes(_profile.NumericId, 23, FollowUnitStatusSnapshot.CreateSynthetic())).Frame);

        aggregate.Update(core, baseTime);
        aggregate.Update(vitals, baseTime.AddMilliseconds(100));
        aggregate.Update(position, baseTime.AddMilliseconds(200));
        aggregate.Update(cast, baseTime.AddMilliseconds(300));
        aggregate.Update(resources, baseTime.AddMilliseconds(400));
        aggregate.Update(combat, baseTime.AddMilliseconds(500));
        aggregate.Update(targetPosition, baseTime.AddMilliseconds(600));
        aggregate.Update(follow, baseTime.AddMilliseconds(700));

        var snapshot = aggregate.Snapshot();
        Assert.True(snapshot.HasAny);
        Assert.True(snapshot.HasCompleteState);
        Assert.Equal(8, snapshot.AcceptedFrames);
        Assert.NotNull(snapshot.CoreStatus);
        Assert.NotNull(snapshot.PlayerVitals);
        Assert.NotNull(snapshot.PlayerPosition);
        Assert.NotNull(snapshot.PlayerCast);
        Assert.NotNull(snapshot.PlayerResources);
        Assert.NotNull(snapshot.PlayerCombat);
        Assert.NotNull(snapshot.TargetPosition);
        Assert.NotNull(snapshot.FollowUnitStatus);
        Assert.Equal((byte)7, snapshot.CoreStatus!.Frame.Header.Sequence);
        Assert.Equal((byte)11, snapshot.PlayerVitals!.Frame.Header.Sequence);
        Assert.Equal((byte)13, snapshot.PlayerPosition!.Frame.Header.Sequence);
        Assert.Equal((byte)15, snapshot.PlayerCast!.Frame.Header.Sequence);
        Assert.Equal((byte)17, snapshot.PlayerResources!.Frame.Header.Sequence);
        Assert.Equal((byte)19, snapshot.PlayerCombat!.Frame.Header.Sequence);
        Assert.Equal((byte)21, snapshot.TargetPosition!.Frame.Header.Sequence);
        Assert.Equal((byte)23, snapshot.FollowUnitStatus!.Frame.Header.Sequence);
        Assert.Equal(baseTime.AddMilliseconds(700), snapshot.LastUpdatedUtc);
    }

    [Fact]
    public void TelemetryAggregate_ReplacesOlderFrame_WithNewerObservationOfSameType()
    {
        var aggregate = new TelemetryAggregate();
        var baseTime = new DateTimeOffset(2026, 4, 2, 12, 0, 0, TimeSpan.Zero);

        var first = Assert.IsType<PlayerPositionFrame>(
            FrameProtocol.AnalyzeFrameBytes(
                FrameProtocol.BuildPlayerPositionFrameBytes(_profile.NumericId, 13, new PlayerPositionSnapshot(1.23f, 4.56f, 7.89f))).Frame);
        var second = Assert.IsType<PlayerPositionFrame>(
            FrameProtocol.AnalyzeFrameBytes(
                FrameProtocol.BuildPlayerPositionFrameBytes(_profile.NumericId, 14, new PlayerPositionSnapshot(9.87f, 6.54f, 3.21f))).Frame);

        aggregate.Update(first, baseTime);
        aggregate.Update(second, baseTime.AddMilliseconds(150));

        var snapshot = aggregate.Snapshot();
        Assert.Equal(2, snapshot.AcceptedFrames);
        Assert.NotNull(snapshot.PlayerPosition);
        Assert.Equal((byte)14, snapshot.PlayerPosition!.Frame.Header.Sequence);
        Assert.Equal(9.87f, snapshot.PlayerPosition.Frame.Payload.X, 2);
        Assert.Equal(baseTime.AddMilliseconds(150), snapshot.PlayerPosition.ObservedAtUtc);
    }

    [Fact]
    public void TelemetryAggregate_KeepsMultipleFollowSlots()
    {
        var aggregate = new TelemetryAggregate();
        var baseTime = new DateTimeOffset(2026, 4, 2, 12, 0, 0, TimeSpan.Zero);

        var slotOne = Assert.IsType<FollowUnitStatusFrame>(
            FrameProtocol.AnalyzeFrameBytes(
                FrameProtocol.BuildFollowUnitStatusFrameBytes(_profile.NumericId, 41, new FollowUnitStatusSnapshot(1, 0x81, 1.0f, 2.0f, 3.0f, 100, 50, 60, 0x31))).Frame);
        var slotTwo = Assert.IsType<FollowUnitStatusFrame>(
            FrameProtocol.AnalyzeFrameBytes(
                FrameProtocol.BuildFollowUnitStatusFrameBytes(_profile.NumericId, 42, new FollowUnitStatusSnapshot(2, 0x83, 4.0f, 5.0f, 6.0f, 90, 40, 61, 0x32))).Frame);

        aggregate.Update(slotOne, baseTime);
        aggregate.Update(slotTwo, baseTime.AddMilliseconds(50));

        var snapshot = aggregate.Snapshot();
        Assert.NotNull(snapshot.FollowUnitStatus);
        Assert.Equal((byte)2, snapshot.FollowUnitStatus!.Frame.Payload.Slot);
        Assert.Equal(2, snapshot.FollowUnitStatusesBySlot.Count);
        Assert.True(snapshot.FollowUnitStatusesBySlot.ContainsKey(1));
        Assert.True(snapshot.FollowUnitStatusesBySlot.ContainsKey(2));
        Assert.Equal((byte)1, snapshot.FollowUnitStatusesBySlot[1].Frame.Payload.Slot);
        Assert.Equal((byte)2, snapshot.FollowUnitStatusesBySlot[2].Frame.Payload.Slot);
    }
}
