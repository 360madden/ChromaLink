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

public static class HttpBridgeSnapshotService
{
    public const double FreshnessWindowSeconds = 5.0;

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
}
