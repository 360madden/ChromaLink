using System.Text.Json;
using ChromaLink.Reader;

internal static class TelemetrySnapshotWriter
{
    public const int ContractSchemaVersion = 1;
    public const string ContractName = "chromalink-live-telemetry";
    private const double FreshnessWindowMilliseconds = 2000.0;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string WriteLatest(
        TelemetryAggregateSnapshot aggregate,
        LiveMetrics metrics,
        CaptureBackend? lastBackend,
        FrameValidationResult? lastValidation)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var outDirectory = PathProvider.EnsureOutDirectory();
        var path = Path.Combine(outDirectory, $"{ContractName}.json");
        var aggregateFreshness = BuildAggregateFreshness(nowUtc, aggregate);
        var payload = new
        {
            artifactKind = "live-telemetry",
            contract = new
            {
                name = ContractName,
                schemaVersion = ContractSchemaVersion
            },
            generatedAtUtc = DateTime.UtcNow,
            profile = new
            {
                id = "P360C",
                numericId = 1,
                windowWidth = 640,
                windowHeight = 360,
                bandWidth = 640,
                bandHeight = 24,
                segmentCount = 80,
                segmentWidth = 8,
                segmentHeight = 24,
                payloadStartIndex = 9,
                payloadSymbolCount = 64
            },
            transport = new
            {
                protocolVersion = 1,
                reservedFlags = new
                {
                    multiFrameRotation = 1,
                    playerPosition = 2,
                    playerCast = 4,
                    expandedStats = 8,
                    targetPosition = 16,
                    followUnitStatus = 32
                }
            },
            aggregate = new
            {
                acceptedFrames = aggregate.AcceptedFrames,
                ready = aggregate.HasCompleteState,
                lastUpdatedUtc = aggregate.LastUpdatedUtc,
                healthy = aggregateFreshness.Healthy,
                stale = aggregateFreshness.Stale,
                freshness = new
                {
                    windowMs = FreshnessWindowMilliseconds,
                    lastUpdatedAgeMs = aggregateFreshness.LastUpdatedAgeMs,
                    oldestFrameAgeMs = aggregateFreshness.OldestFrameAgeMs,
                    newestFrameAgeMs = aggregateFreshness.NewestFrameAgeMs,
                    freshFrameCount = aggregateFreshness.FreshFrameCount,
                    staleFrameCount = aggregateFreshness.StaleFrameCount
                },
                coreStatus = aggregate.CoreStatus is null ? null : new
                {
                    observedAtUtc = aggregate.CoreStatus.ObservedAtUtc,
                    ageMs = AgeMs(nowUtc, aggregate.CoreStatus.ObservedAtUtc),
                    fresh = IsFresh(AgeMs(nowUtc, aggregate.CoreStatus.ObservedAtUtc)),
                    stale = !IsFresh(AgeMs(nowUtc, aggregate.CoreStatus.ObservedAtUtc)),
                    frameType = aggregate.CoreStatus.Frame.Header.FrameType.ToString(),
                    schemaId = aggregate.CoreStatus.Frame.Header.SchemaId,
                    sequence = aggregate.CoreStatus.Frame.Header.Sequence,
                    reservedFlags = aggregate.CoreStatus.Frame.Header.ReservedFlags,
                    playerFlags = aggregate.CoreStatus.Frame.Payload.PlayerStateFlags,
                    playerHealthPctQ8 = aggregate.CoreStatus.Frame.Payload.PlayerHealthPctQ8,
                    playerResourceKind = aggregate.CoreStatus.Frame.Payload.PlayerResourceKind,
                    playerResourcePctQ8 = aggregate.CoreStatus.Frame.Payload.PlayerResourcePctQ8,
                    targetFlags = aggregate.CoreStatus.Frame.Payload.TargetStateFlags,
                    targetHealthPctQ8 = aggregate.CoreStatus.Frame.Payload.TargetHealthPctQ8,
                    targetResourceKind = aggregate.CoreStatus.Frame.Payload.TargetResourceKind,
                    targetResourcePctQ8 = aggregate.CoreStatus.Frame.Payload.TargetResourcePctQ8,
                    playerLevel = aggregate.CoreStatus.Frame.Payload.PlayerLevel,
                    targetLevel = aggregate.CoreStatus.Frame.Payload.TargetLevel,
                    playerCalling = aggregate.CoreStatus.Frame.Payload.PlayerCallingRolePacked >> 4,
                    playerRole = aggregate.CoreStatus.Frame.Payload.PlayerCallingRolePacked & 0x0F,
                    targetCalling = aggregate.CoreStatus.Frame.Payload.TargetCallingRelationPacked >> 4,
                    targetRelation = aggregate.CoreStatus.Frame.Payload.TargetCallingRelationPacked & 0x0F
                },
                playerVitals = aggregate.PlayerVitals is null ? null : new
                {
                    observedAtUtc = aggregate.PlayerVitals.ObservedAtUtc,
                    ageMs = AgeMs(nowUtc, aggregate.PlayerVitals.ObservedAtUtc),
                    fresh = IsFresh(AgeMs(nowUtc, aggregate.PlayerVitals.ObservedAtUtc)),
                    stale = !IsFresh(AgeMs(nowUtc, aggregate.PlayerVitals.ObservedAtUtc)),
                    frameType = aggregate.PlayerVitals.Frame.Header.FrameType.ToString(),
                    schemaId = aggregate.PlayerVitals.Frame.Header.SchemaId,
                    sequence = aggregate.PlayerVitals.Frame.Header.Sequence,
                    reservedFlags = aggregate.PlayerVitals.Frame.Header.ReservedFlags,
                    healthCurrent = aggregate.PlayerVitals.Frame.Payload.HealthCurrent,
                    healthMax = aggregate.PlayerVitals.Frame.Payload.HealthMax,
                    resourceCurrent = aggregate.PlayerVitals.Frame.Payload.ResourceCurrent,
                    resourceMax = aggregate.PlayerVitals.Frame.Payload.ResourceMax
                },
                playerPosition = aggregate.PlayerPosition is null ? null : new
                {
                    observedAtUtc = aggregate.PlayerPosition.ObservedAtUtc,
                    ageMs = AgeMs(nowUtc, aggregate.PlayerPosition.ObservedAtUtc),
                    fresh = IsFresh(AgeMs(nowUtc, aggregate.PlayerPosition.ObservedAtUtc)),
                    stale = !IsFresh(AgeMs(nowUtc, aggregate.PlayerPosition.ObservedAtUtc)),
                    frameType = aggregate.PlayerPosition.Frame.Header.FrameType.ToString(),
                    schemaId = aggregate.PlayerPosition.Frame.Header.SchemaId,
                    sequence = aggregate.PlayerPosition.Frame.Header.Sequence,
                    reservedFlags = aggregate.PlayerPosition.Frame.Header.ReservedFlags,
                    x = aggregate.PlayerPosition.Frame.Payload.X,
                    y = aggregate.PlayerPosition.Frame.Payload.Y,
                    z = aggregate.PlayerPosition.Frame.Payload.Z
                },
                playerCast = aggregate.PlayerCast is null ? null : new
                {
                    observedAtUtc = aggregate.PlayerCast.ObservedAtUtc,
                    ageMs = AgeMs(nowUtc, aggregate.PlayerCast.ObservedAtUtc),
                    fresh = IsFresh(AgeMs(nowUtc, aggregate.PlayerCast.ObservedAtUtc)),
                    stale = !IsFresh(AgeMs(nowUtc, aggregate.PlayerCast.ObservedAtUtc)),
                    frameType = aggregate.PlayerCast.Frame.Header.FrameType.ToString(),
                    schemaId = aggregate.PlayerCast.Frame.Header.SchemaId,
                    sequence = aggregate.PlayerCast.Frame.Header.Sequence,
                    reservedFlags = aggregate.PlayerCast.Frame.Header.ReservedFlags,
                    castFlags = aggregate.PlayerCast.Frame.Payload.CastFlags,
                    castActive = IsFlagSet(aggregate.PlayerCast.Frame.Payload.CastFlags, 0x01),
                    channeled = IsFlagSet(aggregate.PlayerCast.Frame.Payload.CastFlags, 0x02),
                    uninterruptible = IsFlagSet(aggregate.PlayerCast.Frame.Payload.CastFlags, 0x04),
                    hasLabel = IsFlagSet(aggregate.PlayerCast.Frame.Payload.CastFlags, 0x08),
                    hasTarget = IsFlagSet(aggregate.PlayerCast.Frame.Payload.CastFlags, 0x10),
                    progressPctQ8 = aggregate.PlayerCast.Frame.Payload.ProgressPctQ8,
                    durationCenti = aggregate.PlayerCast.Frame.Payload.DurationCenti,
                    remainingCenti = aggregate.PlayerCast.Frame.Payload.RemainingCenti,
                    durationSeconds = CentiToSeconds(aggregate.PlayerCast.Frame.Payload.DurationCenti),
                    remainingSeconds = CentiToSeconds(aggregate.PlayerCast.Frame.Payload.RemainingCenti),
                    castTargetCode = aggregate.PlayerCast.Frame.Payload.CastTargetCode,
                    spellLabel = aggregate.PlayerCast.Frame.Payload.SpellLabel
                },
                playerResources = aggregate.PlayerResources is null ? null : new
                {
                    observedAtUtc = aggregate.PlayerResources.ObservedAtUtc,
                    ageMs = AgeMs(nowUtc, aggregate.PlayerResources.ObservedAtUtc),
                    fresh = IsFresh(AgeMs(nowUtc, aggregate.PlayerResources.ObservedAtUtc)),
                    stale = !IsFresh(AgeMs(nowUtc, aggregate.PlayerResources.ObservedAtUtc)),
                    frameType = aggregate.PlayerResources.Frame.Header.FrameType.ToString(),
                    schemaId = aggregate.PlayerResources.Frame.Header.SchemaId,
                    sequence = aggregate.PlayerResources.Frame.Header.Sequence,
                    reservedFlags = aggregate.PlayerResources.Frame.Header.ReservedFlags,
                    manaCurrent = aggregate.PlayerResources.Frame.Payload.ManaCurrent,
                    manaMax = aggregate.PlayerResources.Frame.Payload.ManaMax,
                    energyCurrent = aggregate.PlayerResources.Frame.Payload.EnergyCurrent,
                    energyMax = aggregate.PlayerResources.Frame.Payload.EnergyMax,
                    powerCurrent = aggregate.PlayerResources.Frame.Payload.PowerCurrent,
                    powerMax = aggregate.PlayerResources.Frame.Payload.PowerMax
                },
                playerCombat = aggregate.PlayerCombat is null ? null : new
                {
                    observedAtUtc = aggregate.PlayerCombat.ObservedAtUtc,
                    ageMs = AgeMs(nowUtc, aggregate.PlayerCombat.ObservedAtUtc),
                    fresh = IsFresh(AgeMs(nowUtc, aggregate.PlayerCombat.ObservedAtUtc)),
                    stale = !IsFresh(AgeMs(nowUtc, aggregate.PlayerCombat.ObservedAtUtc)),
                    frameType = aggregate.PlayerCombat.Frame.Header.FrameType.ToString(),
                    schemaId = aggregate.PlayerCombat.Frame.Header.SchemaId,
                    sequence = aggregate.PlayerCombat.Frame.Header.Sequence,
                    reservedFlags = aggregate.PlayerCombat.Frame.Header.ReservedFlags,
                    combatFlags = aggregate.PlayerCombat.Frame.Payload.CombatFlags,
                    hasCombo = IsFlagSet(aggregate.PlayerCombat.Frame.Payload.CombatFlags, 0x01),
                    hasCharge = IsFlagSet(aggregate.PlayerCombat.Frame.Payload.CombatFlags, 0x02),
                    hasPlanar = IsFlagSet(aggregate.PlayerCombat.Frame.Payload.CombatFlags, 0x04),
                    hasAbsorb = IsFlagSet(aggregate.PlayerCombat.Frame.Payload.CombatFlags, 0x08),
                    combo = aggregate.PlayerCombat.Frame.Payload.Combo,
                    chargeCurrent = aggregate.PlayerCombat.Frame.Payload.ChargeCurrent,
                    chargeMax = aggregate.PlayerCombat.Frame.Payload.ChargeMax,
                    planarCurrent = aggregate.PlayerCombat.Frame.Payload.PlanarCurrent,
                    planarMax = aggregate.PlayerCombat.Frame.Payload.PlanarMax,
                    absorb = aggregate.PlayerCombat.Frame.Payload.Absorb
                },
                targetPosition = aggregate.TargetPosition is null ? null : new
                {
                    observedAtUtc = aggregate.TargetPosition.ObservedAtUtc,
                    ageMs = AgeMs(nowUtc, aggregate.TargetPosition.ObservedAtUtc),
                    fresh = IsFresh(AgeMs(nowUtc, aggregate.TargetPosition.ObservedAtUtc)),
                    stale = !IsFresh(AgeMs(nowUtc, aggregate.TargetPosition.ObservedAtUtc)),
                    frameType = aggregate.TargetPosition.Frame.Header.FrameType.ToString(),
                    schemaId = aggregate.TargetPosition.Frame.Header.SchemaId,
                    sequence = aggregate.TargetPosition.Frame.Header.Sequence,
                    reservedFlags = aggregate.TargetPosition.Frame.Header.ReservedFlags,
                    x = aggregate.TargetPosition.Frame.Payload.X,
                    y = aggregate.TargetPosition.Frame.Payload.Y,
                    z = aggregate.TargetPosition.Frame.Payload.Z
                },
                followUnitStatus = aggregate.FollowUnitStatus is null ? null : new
                {
                    observedAtUtc = aggregate.FollowUnitStatus.ObservedAtUtc,
                    ageMs = AgeMs(nowUtc, aggregate.FollowUnitStatus.ObservedAtUtc),
                    fresh = IsFresh(AgeMs(nowUtc, aggregate.FollowUnitStatus.ObservedAtUtc)),
                    stale = !IsFresh(AgeMs(nowUtc, aggregate.FollowUnitStatus.ObservedAtUtc)),
                    frameType = aggregate.FollowUnitStatus.Frame.Header.FrameType.ToString(),
                    schemaId = aggregate.FollowUnitStatus.Frame.Header.SchemaId,
                    sequence = aggregate.FollowUnitStatus.Frame.Header.Sequence,
                    reservedFlags = aggregate.FollowUnitStatus.Frame.Header.ReservedFlags,
                    slot = aggregate.FollowUnitStatus.Frame.Payload.Slot,
                    followFlags = aggregate.FollowUnitStatus.Frame.Payload.FollowFlags,
                    present = IsFlagSet(aggregate.FollowUnitStatus.Frame.Payload.FollowFlags, 0x01),
                    alive = IsFlagSet(aggregate.FollowUnitStatus.Frame.Payload.FollowFlags, 0x02),
                    combat = IsFlagSet(aggregate.FollowUnitStatus.Frame.Payload.FollowFlags, 0x04),
                    afk = IsFlagSet(aggregate.FollowUnitStatus.Frame.Payload.FollowFlags, 0x08),
                    offline = IsFlagSet(aggregate.FollowUnitStatus.Frame.Payload.FollowFlags, 0x10),
                    aggro = IsFlagSet(aggregate.FollowUnitStatus.Frame.Payload.FollowFlags, 0x20),
                    blocked = IsFlagSet(aggregate.FollowUnitStatus.Frame.Payload.FollowFlags, 0x40),
                    readyFlag = IsFlagSet(aggregate.FollowUnitStatus.Frame.Payload.FollowFlags, 0x80),
                    x = aggregate.FollowUnitStatus.Frame.Payload.X,
                    y = aggregate.FollowUnitStatus.Frame.Payload.Y,
                    z = aggregate.FollowUnitStatus.Frame.Payload.Z,
                    healthPctQ8 = aggregate.FollowUnitStatus.Frame.Payload.HealthPctQ8,
                    resourcePctQ8 = aggregate.FollowUnitStatus.Frame.Payload.ResourcePctQ8,
                    level = aggregate.FollowUnitStatus.Frame.Payload.Level,
                    calling = aggregate.FollowUnitStatus.Frame.Payload.CallingRolePacked >> 4,
                    role = aggregate.FollowUnitStatus.Frame.Payload.CallingRolePacked & 0x0F
                }
            },
            metrics = new
            {
                acceptedSamples = metrics.AcceptedCount,
                rejectedSamples = metrics.RejectedCount,
                averageCaptureMs = metrics.AverageCaptureMs,
                averageDecodeMs = metrics.AverageDecodeMs,
                medianDecodeMs = metrics.MedianDecodeMs,
                p95DecodeMs = metrics.P95DecodeMs,
                lastReason = metrics.LastReason,
                frameTypeCounts = metrics.FrameTypeCounts
                    .OrderBy(static entry => entry.Key)
                    .ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal)
            },
            lastBackend = lastBackend?.ToString(),
            lastDetection = lastValidation?.Detection is null ? null : new
            {
                originX = lastValidation.Detection.OriginX,
                originY = lastValidation.Detection.OriginY,
                pitch = lastValidation.Detection.Pitch,
                scale = lastValidation.Detection.Scale,
                controlError = lastValidation.Detection.ControlError,
                leftControlScore = lastValidation.Detection.LeftControlScore,
                rightControlScore = lastValidation.Detection.RightControlScore,
                anchorLumaDelta = lastValidation.Detection.AnchorLumaDelta,
                searchMode = lastValidation.Detection.SearchMode
            },
            lastFrame = lastValidation?.Frame is null ? null : new
            {
                frameType = lastValidation.Frame.Header.FrameType.ToString(),
                schemaId = lastValidation.Frame.Header.SchemaId,
                sequence = lastValidation.Frame.Header.Sequence,
                reservedFlags = lastValidation.Frame.Header.ReservedFlags
            }
        };

        File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions));
        return path;
    }

    private static AggregateFreshness BuildAggregateFreshness(DateTimeOffset nowUtc, TelemetryAggregateSnapshot aggregate)
    {
        var ages = new List<double?>(3)
        {
            AgeMs(nowUtc, aggregate.CoreStatus?.ObservedAtUtc),
            AgeMs(nowUtc, aggregate.PlayerVitals?.ObservedAtUtc),
            AgeMs(nowUtc, aggregate.PlayerPosition?.ObservedAtUtc)
        };

        var presentAges = ages.Where(static age => age.HasValue).Select(static age => age!.Value).ToArray();
        var freshFrameCount = presentAges.Count(static age => age <= FreshnessWindowMilliseconds);
        var staleFrameCount = presentAges.Length - freshFrameCount;

        return new AggregateFreshness(
            Healthy: aggregate.HasCompleteState && staleFrameCount == 0,
            Stale: !aggregate.HasCompleteState || staleFrameCount > 0,
            LastUpdatedAgeMs: AgeMs(nowUtc, aggregate.LastUpdatedUtc),
            OldestFrameAgeMs: presentAges.Length == 0 ? null : presentAges.Max(),
            NewestFrameAgeMs: presentAges.Length == 0 ? null : presentAges.Min(),
            FreshFrameCount: freshFrameCount,
            StaleFrameCount: staleFrameCount);
    }

    private static double? AgeMs(DateTimeOffset nowUtc, DateTimeOffset? observedAtUtc)
    {
        if (observedAtUtc is null)
        {
            return null;
        }

        var age = nowUtc - observedAtUtc.Value;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        return Math.Round(age.TotalMilliseconds, 2);
    }

    private static bool IsFresh(double? ageMs)
    {
        return ageMs.HasValue && ageMs.Value <= FreshnessWindowMilliseconds;
    }

    private static bool IsFlagSet(byte flags, byte mask)
    {
        return (flags & mask) != 0;
    }

    private static double CentiToSeconds(ushort value)
    {
        return Math.Round(value / 100.0, 2);
    }

    private sealed record AggregateFreshness(
        bool Healthy,
        bool Stale,
        double? LastUpdatedAgeMs,
        double? OldestFrameAgeMs,
        double? NewestFrameAgeMs,
        int FreshFrameCount,
        int StaleFrameCount);
}
