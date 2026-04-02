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
                    playerPosition = 2
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

    private sealed record AggregateFreshness(
        bool Healthy,
        bool Stale,
        double? LastUpdatedAgeMs,
        double? OldestFrameAgeMs,
        double? NewestFrameAgeMs,
        int FreshFrameCount,
        int StaleFrameCount);
}
