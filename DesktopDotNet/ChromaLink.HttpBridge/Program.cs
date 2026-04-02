using System.Text.Json;

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls(GetDefaultUrl());

var app = builder.Build();
var snapshotPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "ChromaLink",
    "DesktopDotNet",
    "out",
    "chromalink-live-telemetry.json");

app.MapGet("/", () => Results.Text("ChromaLink HTTP Bridge", "text/plain"));
app.MapGet("/latest-snapshot", () => SnapshotResponses.TryReadRawSnapshot(snapshotPath));
app.MapGet("/snapshot", () => SnapshotResponses.TryReadRawSnapshot(snapshotPath));
app.MapGet("/health", () => SnapshotResponses.BuildHealthResponse(snapshotPath));
app.MapGet("/ready", () => SnapshotResponses.BuildHealthResponse(snapshotPath));

app.Run();

static string GetDefaultUrl()
{
    var port = 7337;
    if (int.TryParse(Environment.GetEnvironmentVariable("CHROMALINK_HTTP_BRIDGE_PORT"), out var parsedPort) && parsedPort > 0)
    {
        port = parsedPort;
    }

    return $"http://127.0.0.1:{port}";
}

static class SnapshotResponses
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static IResult TryReadRawSnapshot(string snapshotPath)
    {
        if (!File.Exists(snapshotPath))
        {
            return Results.Json(new
            {
                ok = false,
                error = "Snapshot not found.",
                snapshotPath
            }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var raw = File.ReadAllText(snapshotPath);
        return Results.Text(raw, "application/json");
    }

    public static IResult BuildHealthResponse(string snapshotPath)
    {
        var snapshotExists = File.Exists(snapshotPath);
        double? snapshotAgeSeconds = snapshotExists
            ? Math.Max(0, (DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(snapshotPath)).TotalSeconds)
            : null;
        var snapshotFresh = snapshotExists && snapshotAgeSeconds <= 5.0;
        var snapshotReady = false;
        var snapshotHealthy = false;
        var aggregateReady = false;
        var aggregateHealthy = false;
        var aggregateStale = true;
        var frameCount = 0;
        object? contract = null;

        if (snapshotExists)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(snapshotPath));
            var root = document.RootElement;

            if (root.TryGetProperty("contract", out var contractElement) && contractElement.ValueKind == JsonValueKind.Object)
            {
                contract = new
                {
                    name = contractElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "unknown",
                    schemaVersion = contractElement.TryGetProperty("schemaVersion", out var versionProp) ? versionProp.GetInt32() : 0
                };
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

        var payload = new
        {
            ok = snapshotExists && snapshotReady,
            healthy = snapshotHealthy,
            ready = snapshotReady,
            fresh = snapshotFresh,
            stale = !snapshotFresh,
            snapshotExists,
            snapshotAgeSeconds,
            snapshotPath,
            contract,
            aggregate = new
            {
                ready = aggregateReady,
                healthy = aggregateHealthy,
                stale = aggregateStale,
                acceptedFrames = frameCount
            }
        };

        var statusCode = payload.ok ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
        return Results.Json(payload, JsonOptions, statusCode: statusCode);
    }
}
