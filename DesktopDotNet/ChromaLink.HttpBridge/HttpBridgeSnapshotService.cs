using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record HttpBridgeSnapshotContract(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion);

public sealed record HttpBridgeAggregateSnapshot(
    [property: JsonPropertyName("ready")] bool Ready,
    [property: JsonPropertyName("healthy")] bool Healthy,
    [property: JsonPropertyName("stale")] bool Stale,
    [property: JsonPropertyName("acceptedFrames")] int AcceptedFrames);

public sealed record HttpBridgeHealthSnapshot(
    bool Ok,
    bool Healthy,
    bool Ready,
    bool Fresh,
    bool Stale,
    bool SnapshotExists,
    double? SnapshotAgeSeconds,
    string SnapshotPath,
    HttpBridgeSnapshotContract? Contract,
    HttpBridgeAggregateSnapshot Aggregate)
{ }

public sealed record HttpBridgeHealthPayload(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("healthy")] bool Healthy,
    [property: JsonPropertyName("ready")] bool Ready,
    [property: JsonPropertyName("fresh")] bool Fresh,
    [property: JsonPropertyName("stale")] bool Stale,
    [property: JsonPropertyName("snapshotExists")] bool SnapshotExists,
    [property: JsonPropertyName("snapshotAgeSeconds")] double? SnapshotAgeSeconds,
    [property: JsonPropertyName("snapshotPath")] string SnapshotPath,
    [property: JsonPropertyName("contract")] HttpBridgeSnapshotContract? Contract,
    [property: JsonPropertyName("aggregate")] HttpBridgeAggregateSnapshot Aggregate);

public sealed record HttpBridgeApiManifest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("baseUrl")] string BaseUrl,
    [property: JsonPropertyName("localOnly")] bool LocalOnly,
    [property: JsonPropertyName("snapshotContract")] HttpBridgeSnapshotContract SnapshotContract,
    [property: JsonPropertyName("endpoints")] IReadOnlyList<HttpBridgeApiEndpoint> Endpoints);

public sealed record HttpBridgeApiEndpoint(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("purpose")] string Purpose);

public sealed record HttpBridgeRiftReaderWorldState(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("artifactKind")] string ArtifactKind,
    [property: JsonPropertyName("contract")] HttpBridgeSnapshotContract Contract,
    [property: JsonPropertyName("sourceContract")] HttpBridgeSnapshotContract? SourceContract,
    [property: JsonPropertyName("ready")] bool Ready,
    [property: JsonPropertyName("healthy")] bool Healthy,
    [property: JsonPropertyName("fresh")] bool Fresh,
    [property: JsonPropertyName("stale")] bool Stale,
    [property: JsonPropertyName("snapshotAgeSeconds")] double? SnapshotAgeSeconds,
    [property: JsonPropertyName("snapshotPath")] string SnapshotPath,
    [property: JsonPropertyName("navigation")] HttpBridgeRiftReaderNavigation Navigation,
    [property: JsonPropertyName("player")] HttpBridgeRiftReaderPlayer? Player,
    [property: JsonPropertyName("target")] HttpBridgeRiftReaderTarget? Target,
    [property: JsonPropertyName("followUnits")] IReadOnlyList<HttpBridgeRiftReaderFollowUnit> FollowUnits);

public sealed record HttpBridgeRiftReaderNavigation(
    [property: JsonPropertyName("playerPositionAvailable")] bool PlayerPositionAvailable,
    [property: JsonPropertyName("targetPositionAvailable")] bool TargetPositionAvailable,
    [property: JsonPropertyName("followUnitPositionsAvailable")] bool FollowUnitPositionsAvailable,
    [property: JsonPropertyName("headingAvailable")] bool HeadingAvailable,
    [property: JsonPropertyName("facingAvailable")] bool FacingAvailable,
    [property: JsonPropertyName("routeAvailable")] bool RouteAvailable,
    [property: JsonPropertyName("controlAvailable")] bool ControlAvailable,
    [property: JsonPropertyName("limitations")] IReadOnlyList<string> Limitations);

public sealed record HttpBridgeRiftReaderPlayer(
    [property: JsonPropertyName("position")] HttpBridgeRiftReaderPosition? Position,
    [property: JsonPropertyName("healthCurrent")] int? HealthCurrent,
    [property: JsonPropertyName("healthMax")] int? HealthMax,
    [property: JsonPropertyName("resourceCurrent")] int? ResourceCurrent,
    [property: JsonPropertyName("resourceMax")] int? ResourceMax,
    [property: JsonPropertyName("level")] int? Level,
    [property: JsonPropertyName("calling")] int? Calling,
    [property: JsonPropertyName("role")] int? Role);

public sealed record HttpBridgeRiftReaderTarget(
    [property: JsonPropertyName("position")] HttpBridgeRiftReaderPosition? Position,
    [property: JsonPropertyName("present")] bool? Present,
    [property: JsonPropertyName("alive")] bool? Alive,
    [property: JsonPropertyName("combat")] bool? Combat,
    [property: JsonPropertyName("healthCurrent")] int? HealthCurrent,
    [property: JsonPropertyName("healthMax")] int? HealthMax,
    [property: JsonPropertyName("level")] int? Level);

public sealed record HttpBridgeRiftReaderFollowUnit(
    [property: JsonPropertyName("slot")] int? Slot,
    [property: JsonPropertyName("position")] HttpBridgeRiftReaderPosition? Position,
    [property: JsonPropertyName("present")] bool? Present,
    [property: JsonPropertyName("alive")] bool? Alive,
    [property: JsonPropertyName("combat")] bool? Combat,
    [property: JsonPropertyName("afk")] bool? Afk,
    [property: JsonPropertyName("offline")] bool? Offline,
    [property: JsonPropertyName("blocked")] bool? Blocked,
    [property: JsonPropertyName("ready")] bool? Ready,
    [property: JsonPropertyName("healthPctQ8")] int? HealthPctQ8,
    [property: JsonPropertyName("resourcePctQ8")] int? ResourcePctQ8,
    [property: JsonPropertyName("level")] int? Level,
    [property: JsonPropertyName("calling")] int? Calling,
    [property: JsonPropertyName("role")] int? Role);

public sealed record HttpBridgeRiftReaderPosition(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("z")] double Z,
    [property: JsonPropertyName("observedAtUtc")] DateTimeOffset? ObservedAtUtc,
    [property: JsonPropertyName("ageMs")] double? AgeMs,
    [property: JsonPropertyName("fresh")] bool? Fresh,
    [property: JsonPropertyName("stale")] bool? Stale);

public sealed record HttpBridgeRawSnapshot(
    bool Exists,
    string SnapshotPath,
    int StatusCode,
    string ContentType,
    string? Content,
    string? Error)
{
    public IResult ToResult()
    {
        if (Exists)
        {
            return Results.Text(Content ?? string.Empty, ContentType);
        }

        return Results.Json(new
        {
            ok = false,
            error = Error ?? "Snapshot not found.",
            snapshotPath = SnapshotPath
        }, HttpBridgeSnapshotService.JsonOptions, statusCode: StatusCode);
    }
}

public sealed record HttpBridgeJsonPayload(
    int StatusCode,
    object Payload)
{
    public IResult ToResult()
    {
        return Results.Json(Payload, HttpBridgeSnapshotService.JsonOptions, statusCode: StatusCode);
    }
}

public static class HttpBridgeSnapshotService
{
    public const double FreshnessWindowSeconds = 5.0;
    public const string RiftReaderWorldStateContractName = "chromalink-riftreader-world-state";
    public const int RiftReaderWorldStateContractSchemaVersion = 1;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static HttpBridgeRawSnapshot TryReadRawSnapshot(string snapshotPath)
    {
        if (!File.Exists(snapshotPath))
        {
            return new HttpBridgeRawSnapshot(
                false,
                snapshotPath,
                StatusCodes.Status503ServiceUnavailable,
                "application/json",
                JsonSerializer.Serialize(new
                {
                    ok = false,
                    error = "Snapshot not found.",
                    snapshotPath
                }, JsonOptions),
                "Snapshot not found.");
        }

        var raw = File.ReadAllText(snapshotPath);
        return new HttpBridgeRawSnapshot(true, snapshotPath, StatusCodes.Status200OK, "application/json", raw, null);
    }

    public static HttpBridgeApiManifest BuildApiManifest(string baseUrl)
    {
        return new HttpBridgeApiManifest(
            "ChromaLink HTTP Bridge",
            baseUrl.TrimEnd('/'),
            true,
            new HttpBridgeSnapshotContract("chromalink-live-telemetry", 2),
            new[]
            {
                new HttpBridgeApiEndpoint("/latest-snapshot", "Full rolling telemetry snapshot for diagnostics and advanced consumers."),
                new HttpBridgeApiEndpoint("/snapshot", "Alias for /latest-snapshot."),
                new HttpBridgeApiEndpoint("/api/v1/riftreader/world-state", "Reduced read-only world-state view for RiftReader-style consumers."),
                new HttpBridgeApiEndpoint("/health", "Bridge health, freshness, and source snapshot status."),
                new HttpBridgeApiEndpoint("/ready", "Readiness probe with the same payload shape as /health.")
            });
    }

    public static HttpBridgeJsonPayload TryBuildRiftReaderWorldState(string snapshotPath)
    {
        if (!File.Exists(snapshotPath))
        {
            return new HttpBridgeJsonPayload(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    ok = false,
                    artifactKind = "riftreader-world-state",
                    contract = new HttpBridgeSnapshotContract(
                        RiftReaderWorldStateContractName,
                        RiftReaderWorldStateContractSchemaVersion),
                    error = "Snapshot not found.",
                    snapshotPath
                });
        }

        HttpBridgeHealthSnapshot health;
        JsonDocument document;
        try
        {
            health = BuildHealthDocument(snapshotPath);
            document = JsonDocument.Parse(File.ReadAllText(snapshotPath));
        }
        catch (JsonException ex)
        {
            return new HttpBridgeJsonPayload(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    ok = false,
                    artifactKind = "riftreader-world-state",
                    contract = new HttpBridgeSnapshotContract(
                        RiftReaderWorldStateContractName,
                        RiftReaderWorldStateContractSchemaVersion),
                    error = "Snapshot JSON could not be parsed.",
                    detail = ex.Message,
                    snapshotPath
                });
        }

        using (document)
        {
            var root = document.RootElement;
            if (!TryGetObject(root, "aggregate", out var aggregate))
            {
                return new HttpBridgeJsonPayload(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        ok = false,
                        artifactKind = "riftreader-world-state",
                        contract = new HttpBridgeSnapshotContract(
                            RiftReaderWorldStateContractName,
                            RiftReaderWorldStateContractSchemaVersion),
                        error = "Snapshot aggregate section is missing.",
                        snapshotPath
                    });
            }

            var player = BuildRiftReaderPlayer(aggregate);
            var target = BuildRiftReaderTarget(aggregate);
            var followUnits = BuildRiftReaderFollowUnits(aggregate);
            var navigation = new HttpBridgeRiftReaderNavigation(
                player?.Position?.Fresh == true,
                target?.Position?.Fresh == true,
                followUnits.Any(static unit => unit.Position?.Fresh == true),
                false,
                false,
                false,
                false,
                new[]
                {
                    "Position snapshots are exposed, but heading/facing/yaw are not exposed by ChromaLink.",
                    "This endpoint is read-only and does not provide route planning or movement control.",
                    "Use RiftReader's own facing/control source alongside this world-state feed."
                });

            var payload = new HttpBridgeRiftReaderWorldState(
                health.Ok,
                "riftreader-world-state",
                new HttpBridgeSnapshotContract(
                    RiftReaderWorldStateContractName,
                    RiftReaderWorldStateContractSchemaVersion),
                health.Contract,
                health.Ready,
                health.Healthy,
                health.Fresh,
                health.Stale,
                health.SnapshotAgeSeconds,
                health.SnapshotPath,
                navigation,
                player,
                target,
                followUnits);

            return new HttpBridgeJsonPayload(health.Ok ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable, payload);
        }
    }

    public static HttpBridgeHealthSnapshot BuildHealthDocument(string snapshotPath)
    {
        var snapshotExists = File.Exists(snapshotPath);
        double? snapshotAgeSeconds = snapshotExists
            ? Math.Max(0, (DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(snapshotPath)).TotalSeconds)
            : null;
        var snapshotFresh = snapshotExists && snapshotAgeSeconds <= FreshnessWindowSeconds;
        var snapshotReady = false;
        var snapshotHealthy = false;
        var aggregateReady = false;
        var aggregateHealthy = false;
        var aggregateStale = true;
        var frameCount = 0;
        HttpBridgeSnapshotContract? contract = null;

        if (snapshotExists)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(snapshotPath));
            var root = document.RootElement;

            if (root.TryGetProperty("contract", out var contractElement) && contractElement.ValueKind == JsonValueKind.Object)
            {
                contract = new HttpBridgeSnapshotContract(
                    contractElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "unknown" : "unknown",
                    contractElement.TryGetProperty("schemaVersion", out var versionProp) ? versionProp.GetInt32() : 0);
            }

            if (root.TryGetProperty("aggregate", out var aggregate) && aggregate.ValueKind == JsonValueKind.Object)
            {
                aggregateReady = aggregate.TryGetProperty("ready", out var readyProp) && readyProp.GetBoolean();
                aggregateHealthy = aggregate.TryGetProperty("healthy", out var healthyProp) && healthyProp.GetBoolean();
                aggregateStale = aggregate.TryGetProperty("stale", out var staleProp) && staleProp.GetBoolean();
                frameCount = aggregate.TryGetProperty("acceptedFrames", out var acceptedProp) ? acceptedProp.GetInt32() : 0;
                snapshotReady = aggregateReady;
                snapshotHealthy = aggregateHealthy && snapshotFresh;
            }
        }

        return new HttpBridgeHealthSnapshot(
            snapshotExists && snapshotReady,
            snapshotHealthy,
            snapshotReady,
            snapshotFresh,
            !snapshotFresh,
            snapshotExists,
            snapshotAgeSeconds,
            snapshotPath,
            contract,
            new HttpBridgeAggregateSnapshot(aggregateReady, aggregateHealthy, aggregateStale, frameCount));
    }

    public static HttpBridgeHealthPayload BuildHealthPayload(HttpBridgeHealthSnapshot document)
    {
        return new HttpBridgeHealthPayload(
            document.Ok,
            document.Healthy,
            document.Ready,
            document.Fresh,
            document.Stale,
            document.SnapshotExists,
            document.SnapshotAgeSeconds,
            document.SnapshotPath,
            document.Contract,
            document.Aggregate);
    }

    public static int GetHealthStatusCode(string snapshotPath)
    {
        var document = BuildHealthDocument(snapshotPath);
        return document.Ok ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
    }

    private static HttpBridgeRiftReaderPlayer? BuildRiftReaderPlayer(JsonElement aggregate)
    {
        var hasPlayerPosition = TryGetObject(aggregate, "playerPosition", out var playerPosition);
        var hasPlayerVitals = TryGetObject(aggregate, "playerVitals", out var playerVitals);
        var hasCoreStatus = TryGetObject(aggregate, "coreStatus", out var coreStatus);

        if (!hasPlayerPosition && !hasPlayerVitals && !hasCoreStatus)
        {
            return null;
        }

        return new HttpBridgeRiftReaderPlayer(
            BuildPosition(playerPosition),
            GetInt32(playerVitals, "healthCurrent"),
            GetInt32(playerVitals, "healthMax"),
            GetInt32(playerVitals, "resourceCurrent"),
            GetInt32(playerVitals, "resourceMax"),
            GetInt32(coreStatus, "playerLevel"),
            GetInt32(coreStatus, "playerCalling"),
            GetInt32(coreStatus, "playerRole"));
    }

    private static HttpBridgeRiftReaderTarget? BuildRiftReaderTarget(JsonElement aggregate)
    {
        var hasTargetPosition = TryGetObject(aggregate, "targetPosition", out var targetPosition);
        var hasTargetVitals = TryGetObject(aggregate, "targetVitals", out var targetVitals);
        var hasCoreStatus = TryGetObject(aggregate, "coreStatus", out var coreStatus);

        if (!hasTargetPosition && !hasTargetVitals && !hasCoreStatus)
        {
            return null;
        }

        return new HttpBridgeRiftReaderTarget(
            BuildPosition(targetPosition),
            GetBoolean(targetVitals, "present"),
            GetBoolean(targetVitals, "alive"),
            GetBoolean(targetVitals, "combat"),
            GetInt32(targetVitals, "healthCurrent"),
            GetInt32(targetVitals, "healthMax"),
            GetInt32(targetVitals, "targetLevel") ?? GetInt32(coreStatus, "targetLevel"));
    }

    private static IReadOnlyList<HttpBridgeRiftReaderFollowUnit> BuildRiftReaderFollowUnits(JsonElement aggregate)
    {
        if (!aggregate.TryGetProperty("followUnitStatuses", out var followUnitsElement) ||
            followUnitsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<HttpBridgeRiftReaderFollowUnit>();
        }

        return followUnitsElement
            .EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.Object)
            .Select(static item => new HttpBridgeRiftReaderFollowUnit(
                GetInt32(item, "slot"),
                BuildPosition(item),
                GetBoolean(item, "present"),
                GetBoolean(item, "alive"),
                GetBoolean(item, "combat"),
                GetBoolean(item, "afk"),
                GetBoolean(item, "offline"),
                GetBoolean(item, "blocked"),
                GetBoolean(item, "readyFlag"),
                GetInt32(item, "healthPctQ8"),
                GetInt32(item, "resourcePctQ8"),
                GetInt32(item, "level"),
                GetInt32(item, "calling"),
                GetInt32(item, "role")))
            .ToArray();
    }

    private static HttpBridgeRiftReaderPosition? BuildPosition(JsonElement element)
    {
        var x = GetDouble(element, "x");
        var y = GetDouble(element, "y");
        var z = GetDouble(element, "z");
        if (!x.HasValue || !y.HasValue || !z.HasValue)
        {
            return null;
        }

        return new HttpBridgeRiftReaderPosition(
            x.Value,
            y.Value,
            z.Value,
            GetDateTimeOffset(element, "observedAtUtc"),
            GetDouble(element, "ageMs"),
            GetBoolean(element, "fresh"),
            GetBoolean(element, "stale"));
    }

    private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    private static int? GetInt32(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var result))
        {
            return result;
        }

        return null;
    }

    private static double? GetDouble(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetDouble(out var result))
        {
            return result;
        }

        return null;
    }

    private static bool? GetBoolean(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var value))
        {
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        return null;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            value.TryGetDateTimeOffset(out var result))
        {
            return result;
        }

        return null;
    }
}
