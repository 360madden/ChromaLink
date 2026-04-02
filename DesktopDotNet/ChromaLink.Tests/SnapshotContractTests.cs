using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace ChromaLink.Tests;

public class SnapshotContractTests
{
    [Fact]
    public void RollingSnapshot_EmitsStableContractFieldsAndAggregateShape()
    {
        using var scope = new TempSnapshotScope();
        var path = WriteSampleRollingSnapshot(scope);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.Equal("live-telemetry", root.GetProperty("artifactKind").GetString());
        Assert.Equal("chromalink-live-telemetry", root.GetProperty("contract").GetProperty("name").GetString());
        Assert.Equal(1, root.GetProperty("contract").GetProperty("schemaVersion").GetInt32());

        var profile = root.GetProperty("profile");
        Assert.Equal("P360C", profile.GetProperty("id").GetString());
        Assert.Equal(640, profile.GetProperty("windowWidth").GetInt32());
        Assert.Equal(360, profile.GetProperty("windowHeight").GetInt32());
        Assert.Equal(80, profile.GetProperty("segmentCount").GetInt32());
        Assert.Equal(64, profile.GetProperty("payloadSymbolCount").GetInt32());

        var transport = root.GetProperty("transport");
        Assert.Equal(1, transport.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(1, transport.GetProperty("reservedFlags").GetProperty("multiFrameRotation").GetInt32());
        Assert.Equal(2, transport.GetProperty("reservedFlags").GetProperty("playerPosition").GetInt32());
        Assert.Equal(4, transport.GetProperty("reservedFlags").GetProperty("playerCast").GetInt32());

        var aggregateJson = root.GetProperty("aggregate");
        Assert.True(aggregateJson.GetProperty("ready").GetBoolean());
        Assert.True(aggregateJson.GetProperty("healthy").GetBoolean());
        Assert.False(aggregateJson.GetProperty("stale").GetBoolean());
        Assert.Equal(4, aggregateJson.GetProperty("acceptedFrames").GetInt32());

        var freshness = aggregateJson.GetProperty("freshness");
        Assert.Equal(2000.0, freshness.GetProperty("windowMs").GetDouble(), 2);
        Assert.Equal(3, freshness.GetProperty("freshFrameCount").GetInt32());
        Assert.Equal(0, freshness.GetProperty("staleFrameCount").GetInt32());

        Assert.True(aggregateJson.GetProperty("coreStatus").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("playerVitals").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("playerPosition").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("playerCast").GetProperty("fresh").GetBoolean());

        var metrics = root.GetProperty("metrics");
        Assert.Equal(3, metrics.GetProperty("acceptedSamples").GetInt32());
        Assert.Equal(1, metrics.GetProperty("rejectedSamples").GetInt32());
        Assert.Equal(3, metrics.GetProperty("frameTypeCounts").GetProperty("CoreStatus/schema-1").GetInt32());
        Assert.Equal(2, metrics.GetProperty("frameTypeCounts").GetProperty("PlayerVitals/schema-1").GetInt32());
        Assert.Equal(1, metrics.GetProperty("frameTypeCounts").GetProperty("PlayerPosition/schema-1").GetInt32());
        Assert.Equal(1, metrics.GetProperty("frameTypeCounts").GetProperty("PlayerCast/schema-1").GetInt32());

        Assert.Equal("screen", root.GetProperty("lastBackend").GetString());
        Assert.Equal(0, root.GetProperty("lastDetection").GetProperty("originX").GetInt32());
        Assert.Equal("PlayerCast", root.GetProperty("lastFrame").GetProperty("frameType").GetString());
    }

    [Fact]
    public void HttpBridge_LatestSnapshot_ReturnsTheRollingSnapshotJson()
    {
        using var scope = new TempSnapshotScope();
        var path = WriteSampleRollingSnapshot(scope);

        var result = HttpBridgeSnapshotService.TryReadRawSnapshot(path);

        Assert.True(result.Exists);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.StartsWith("application/json", result.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(File.ReadAllText(path), result.Content);
    }

    [Fact]
    public void HttpBridge_LatestSnapshot_ReturnsUnavailablePayload_WhenSnapshotIsMissing()
    {
        using var scope = new TempSnapshotScope();
        var missingPath = scope.GetSnapshotPath("missing.json");

        var result = HttpBridgeSnapshotService.TryReadRawSnapshot(missingPath);

        Assert.False(result.Exists);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.StartsWith("application/json", result.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(result.Content));

        using var document = JsonDocument.Parse(result.Content!);
        Assert.False(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("Snapshot not found.", document.RootElement.GetProperty("error").GetString());
        Assert.Equal(missingPath, document.RootElement.GetProperty("snapshotPath").GetString());
    }

    [Fact]
    public void HttpBridge_Health_IsReadyWhenFresh_AndTurnsStaleWhenTimestampAgesOut()
    {
        using var scope = new TempSnapshotScope();
        var path = WriteSampleRollingSnapshot(scope);

        var freshDocument = HttpBridgeSnapshotService.BuildHealthDocument(path);
        var freshPayload = HttpBridgeSnapshotService.BuildHealthPayload(freshDocument);
        using var freshJson = JsonDocument.Parse(JsonSerializer.Serialize(freshPayload, HttpBridgeSnapshotService.JsonOptions));

        Assert.True(freshPayload.Ok);
        Assert.True(freshPayload.Healthy);
        Assert.True(freshPayload.Ready);
        Assert.False(freshPayload.Stale);
        Assert.True(freshPayload.Fresh);
        Assert.True(freshDocument.Ok);
        Assert.Equal("chromalink-live-telemetry", freshJson.RootElement.GetProperty("contract").GetProperty("name").GetString());
        Assert.Equal(4, freshJson.RootElement.GetProperty("aggregate").GetProperty("acceptedFrames").GetInt32());
        Assert.True(freshJson.RootElement.GetProperty("aggregate").GetProperty("ready").GetBoolean());
        Assert.True(freshJson.RootElement.GetProperty("aggregate").GetProperty("healthy").GetBoolean());
        Assert.False(freshJson.RootElement.GetProperty("aggregate").GetProperty("stale").GetBoolean());

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(-10));

        var staleDocument = HttpBridgeSnapshotService.BuildHealthDocument(path);
        var stalePayload = HttpBridgeSnapshotService.BuildHealthPayload(staleDocument);
        using var staleJson = JsonDocument.Parse(JsonSerializer.Serialize(stalePayload, HttpBridgeSnapshotService.JsonOptions));

        Assert.True(stalePayload.Ok);
        Assert.False(stalePayload.Healthy);
        Assert.True(stalePayload.Ready);
        Assert.True(stalePayload.Stale);
        Assert.False(stalePayload.Fresh);
        Assert.True(staleDocument.Ok);
        Assert.False(staleJson.RootElement.GetProperty("healthy").GetBoolean());
        Assert.True(staleJson.RootElement.GetProperty("ready").GetBoolean());
        Assert.True(staleJson.RootElement.GetProperty("stale").GetBoolean());
        Assert.True(staleJson.RootElement.GetProperty("aggregate").GetProperty("ready").GetBoolean());
        Assert.True(staleJson.RootElement.GetProperty("aggregate").GetProperty("healthy").GetBoolean());
        Assert.False(staleJson.RootElement.GetProperty("aggregate").GetProperty("stale").GetBoolean());
        Assert.Equal("chromalink-live-telemetry", staleJson.RootElement.GetProperty("contract").GetProperty("name").GetString());
    }

    [Fact]
    public async Task HttpBridge_EndPoints_ReturnRawSnapshotAndMissingPayloads()
    {
        using var scope = new TempSnapshotScope();
        var path = WriteBridgeSnapshot(scope, ready: true, healthy: true, stale: false, ageSeconds: 0);
        var expectedBody = File.ReadAllText(path);

        await using var app = CreateBridgeApp(path);
        await app.StartAsync();
        var client = app.GetTestClient();

        foreach (var endpoint in new[] { "/latest-snapshot", "/snapshot" })
        {
            using var response = await client.GetAsync(endpoint);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(expectedBody, body);
        }

        using var missingScope = new TempSnapshotScope();
        var missingPath = missingScope.GetSnapshotPath("missing.json");

        await using var missingApp = CreateBridgeApp(missingPath);
        await missingApp.StartAsync();
        var missingClient = missingApp.GetTestClient();

        foreach (var endpoint in new[] { "/latest-snapshot", "/snapshot" })
        {
            using var response = await missingClient.GetAsync(endpoint);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.False(document.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("Snapshot not found.", document.RootElement.GetProperty("error").GetString());
            Assert.Equal(missingPath, document.RootElement.GetProperty("snapshotPath").GetString());
        }
    }

    [Fact]
    public async Task HttpBridge_Health_AndReady_ExposeFreshReadyAndStaleStates()
    {
        using var scope = new TempSnapshotScope();
        var path = WriteBridgeSnapshot(scope, ready: true, healthy: true, stale: false, ageSeconds: 0);

        await using var app = CreateBridgeApp(path);
        await app.StartAsync();
        var client = app.GetTestClient();

        using (var response = await client.GetAsync("/health"))
        using (var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
            Assert.True(document.RootElement.GetProperty("healthy").GetBoolean());
            Assert.True(document.RootElement.GetProperty("ready").GetBoolean());
            Assert.True(document.RootElement.GetProperty("fresh").GetBoolean());
            Assert.False(document.RootElement.GetProperty("stale").GetBoolean());
            Assert.True(document.RootElement.GetProperty("snapshotExists").GetBoolean());
        }

        using var staleScope = new TempSnapshotScope();
        var stalePath = WriteBridgeSnapshot(staleScope, ready: true, healthy: true, stale: true, ageSeconds: 10);

        await using var staleApp = CreateBridgeApp(stalePath);
        await staleApp.StartAsync();
        var staleClient = staleApp.GetTestClient();

        using (var response = await staleClient.GetAsync("/ready"))
        using (var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
            Assert.False(document.RootElement.GetProperty("healthy").GetBoolean());
            Assert.True(document.RootElement.GetProperty("ready").GetBoolean());
            Assert.False(document.RootElement.GetProperty("fresh").GetBoolean());
            Assert.True(document.RootElement.GetProperty("stale").GetBoolean());
        }

        using var notReadyScope = new TempSnapshotScope();
        var notReadyPath = WriteBridgeSnapshot(notReadyScope, ready: false, healthy: false, stale: false, ageSeconds: 0);

        await using var notReadyApp = CreateBridgeApp(notReadyPath);
        await notReadyApp.StartAsync();
        var notReadyClient = notReadyApp.GetTestClient();

        using (var response = await notReadyClient.GetAsync("/ready"))
        using (var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.False(document.RootElement.GetProperty("ok").GetBoolean());
            Assert.False(document.RootElement.GetProperty("healthy").GetBoolean());
            Assert.False(document.RootElement.GetProperty("ready").GetBoolean());
            Assert.False(document.RootElement.GetProperty("stale").GetBoolean());
        }
    }

    private static WebApplication CreateBridgeApp(string snapshotPath)
    {
        return HttpBridgeApp.CreateApp(Array.Empty<string>(), useTestServer: true, snapshotPathOverride: snapshotPath);
    }

    private static string WriteBridgeSnapshot(TempSnapshotScope scope, bool ready, bool healthy, bool stale, int ageSeconds)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var path = scope.GetSnapshotPath("chromalink-live-telemetry.json");

        var payload = new
        {
            contract = new
            {
                name = "chromalink-live-telemetry",
                schemaVersion = 1
            },
            aggregate = new
            {
                acceptedFrames = 3,
                ready,
                healthy,
                stale
            }
        };

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(payload, HttpBridgeSnapshotService.JsonOptions));
        File.SetLastWriteTimeUtc(path, nowUtc.AddSeconds(-ageSeconds).UtcDateTime);
        return path;
    }

    private static string WriteSampleRollingSnapshot(TempSnapshotScope scope)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var path = scope.GetSnapshotPath("chromalink-live-telemetry.json");

        var payload = new
        {
            artifactKind = "live-telemetry",
            contract = new
            {
                name = "chromalink-live-telemetry",
                schemaVersion = 1
            },
            generatedAtUtc = nowUtc,
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
                    playerCast = 4
                }
            },
            aggregate = new
            {
                acceptedFrames = 4,
                ready = true,
                lastUpdatedUtc = nowUtc.AddMilliseconds(-20),
                healthy = true,
                stale = false,
                freshness = new
                {
                    windowMs = 2000.0,
                    lastUpdatedAgeMs = 20.0,
                    oldestFrameAgeMs = 50.0,
                    newestFrameAgeMs = 10.0,
                    freshFrameCount = 3,
                    staleFrameCount = 0
                },
                coreStatus = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-50),
                    ageMs = 50.0,
                    fresh = true,
                    stale = false,
                    frameType = "CoreStatus",
                    schemaId = 1,
                    sequence = 1,
                    reservedFlags = 0,
                    playerFlags = 1,
                    playerHealthPctQ8 = 255,
                    playerResourceKind = 1,
                    playerResourcePctQ8 = 128,
                    targetFlags = 0,
                    targetHealthPctQ8 = 0,
                    targetResourceKind = 0,
                    targetResourcePctQ8 = 0,
                    playerLevel = 70,
                    targetLevel = 0,
                    playerCalling = 1,
                    playerRole = 1,
                    targetCalling = 0,
                    targetRelation = 0
                },
                playerVitals = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-30),
                    ageMs = 30.0,
                    fresh = true,
                    stale = false,
                    frameType = "PlayerVitals",
                    schemaId = 1,
                    sequence = 2,
                    reservedFlags = 1,
                    healthCurrent = 900,
                    healthMax = 1000,
                    resourceCurrent = 400,
                    resourceMax = 500
                },
                playerPosition = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-10),
                    ageMs = 10.0,
                    fresh = true,
                    stale = false,
                    frameType = "PlayerPosition",
                    schemaId = 1,
                    sequence = 3,
                    reservedFlags = 3,
                    x = 12.5,
                    y = 44.25,
                    z = -8.0
                },
                playerCast = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-15),
                    ageMs = 15.0,
                    fresh = true,
                    stale = false,
                    frameType = "PlayerCast",
                    schemaId = 1,
                    sequence = 4,
                    reservedFlags = 7,
                    castFlags = 9,
                    castActive = true,
                    channeled = false,
                    uninterruptible = false,
                    hasLabel = true,
                    progressPctQ8 = 96,
                    durationQ4 = 10,
                    remainingQ4 = 6,
                    durationSeconds = 2.5,
                    remainingSeconds = 1.5,
                    spellLabel = "HEALING"
                }
            },
            metrics = new
            {
                acceptedSamples = 3,
                rejectedSamples = 1,
                averageCaptureMs = 12.5,
                averageDecodeMs = 4.25,
                medianDecodeMs = 4.0,
                p95DecodeMs = 6.0,
                lastReason = "Accepted",
                frameTypeCounts = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["CoreStatus/schema-1"] = 3,
                    ["PlayerVitals/schema-1"] = 2,
                    ["PlayerPosition/schema-1"] = 1,
                    ["PlayerCast/schema-1"] = 1
                }
            },
            lastBackend = "screen",
            lastDetection = new
            {
                originX = 0,
                originY = 0,
                pitch = 2.8,
                scale = 0.35,
                controlError = 0.0,
                leftControlScore = 1.0,
                rightControlScore = 1.0,
                anchorLumaDelta = 0.0,
                searchMode = "default"
            },
            lastFrame = new
            {
                frameType = "PlayerCast",
                schemaId = 1,
                sequence = 4,
                reservedFlags = 7
            }
        };

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(payload, HttpBridgeSnapshotService.JsonOptions));
        File.SetLastWriteTimeUtc(path, nowUtc.UtcDateTime);
        return path;
    }

    private sealed class TempSnapshotScope : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "chromalink-tests-" + Guid.NewGuid().ToString("N"));

        public TempSnapshotScope()
        {
            Directory.CreateDirectory(_root);
        }

        public string GetSnapshotPath(string fileName)
        {
            return Path.Combine(_root, "ChromaLink", "DesktopDotNet", "out", fileName);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
