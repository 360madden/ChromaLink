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
        Assert.Equal(2, profile.GetProperty("stripCount").GetInt32());

        var transport = root.GetProperty("transport");
        Assert.Equal(1, transport.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(1, transport.GetProperty("reservedFlags").GetProperty("multiFrameRotation").GetInt32());
        Assert.Equal(2, transport.GetProperty("reservedFlags").GetProperty("playerPosition").GetInt32());
        Assert.Equal(4, transport.GetProperty("reservedFlags").GetProperty("playerCast").GetInt32());
        Assert.Equal(8, transport.GetProperty("reservedFlags").GetProperty("expandedStats").GetInt32());
        Assert.Equal(16, transport.GetProperty("reservedFlags").GetProperty("targetPosition").GetInt32());
        Assert.Equal(32, transport.GetProperty("reservedFlags").GetProperty("followUnitStatus").GetInt32());
        Assert.Equal(64, transport.GetProperty("reservedFlags").GetProperty("additionalTelemetry").GetInt32());
        Assert.Equal(128, transport.GetProperty("reservedFlags").GetProperty("textAndAuras").GetInt32());

        var aggregateJson = root.GetProperty("aggregate");
        Assert.True(aggregateJson.GetProperty("ready").GetBoolean());
        Assert.True(aggregateJson.GetProperty("healthy").GetBoolean());
        Assert.False(aggregateJson.GetProperty("stale").GetBoolean());
        Assert.Equal(14, aggregateJson.GetProperty("acceptedFrames").GetInt32());

        var freshness = aggregateJson.GetProperty("freshness");
        Assert.Equal(2000.0, freshness.GetProperty("windowMs").GetDouble(), 2);
        Assert.Equal(3, freshness.GetProperty("freshFrameCount").GetInt32());
        Assert.Equal(0, freshness.GetProperty("staleFrameCount").GetInt32());

        Assert.True(aggregateJson.GetProperty("coreStatus").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("playerVitals").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("playerPosition").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("playerCast").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("playerResources").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("playerCombat").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("targetPosition").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("followUnitStatus").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("targetVitals").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("targetResources").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("auxUnitCast").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("auraPage").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("textPage").GetProperty("fresh").GetBoolean());
        Assert.True(aggregateJson.GetProperty("abilityWatch").GetProperty("fresh").GetBoolean());
        var followStatuses = aggregateJson.GetProperty("followUnitStatuses");
        Assert.Equal(2, followStatuses.GetArrayLength());
        var followStatusItems = followStatuses.EnumerateArray().ToArray();
        Assert.Equal(1, followStatusItems[0].GetProperty("slot").GetInt32());
        Assert.Equal(2, followStatusItems[1].GetProperty("slot").GetInt32());

        var metrics = root.GetProperty("metrics");
        Assert.Equal(13, metrics.GetProperty("acceptedSamples").GetInt32());
        Assert.Equal(1, metrics.GetProperty("rejectedSamples").GetInt32());
        Assert.Equal(3, metrics.GetProperty("frameTypeCounts").GetProperty("CoreStatus/schema-1").GetInt32());
        Assert.Equal(2, metrics.GetProperty("frameTypeCounts").GetProperty("PlayerVitals/schema-1").GetInt32());
        Assert.Equal(1, metrics.GetProperty("frameTypeCounts").GetProperty("PlayerPosition/schema-1").GetInt32());
        Assert.Equal(1, metrics.GetProperty("frameTypeCounts").GetProperty("PlayerCast/schema-1").GetInt32());
        Assert.Equal(1, metrics.GetProperty("frameTypeCounts").GetProperty("PlayerResources/schema-1").GetInt32());
        Assert.Equal(1, metrics.GetProperty("frameTypeCounts").GetProperty("PlayerCombat/schema-1").GetInt32());
        Assert.Equal(1, metrics.GetProperty("frameTypeCounts").GetProperty("TargetPosition/schema-1").GetInt32());
        Assert.Equal(1, metrics.GetProperty("frameTypeCounts").GetProperty("FollowUnitStatus/schema-1").GetInt32());
        Assert.Equal(1, metrics.GetProperty("frameTypeCounts").GetProperty("TargetVitals/schema-1").GetInt32());
        Assert.Equal(1, metrics.GetProperty("frameTypeCounts").GetProperty("TargetResources/schema-1").GetInt32());
        Assert.Equal(1, metrics.GetProperty("frameTypeCounts").GetProperty("AuxUnitCast/schema-1").GetInt32());
        Assert.Equal(1, metrics.GetProperty("frameTypeCounts").GetProperty("AuraPage/schema-1").GetInt32());
        Assert.Equal(1, metrics.GetProperty("frameTypeCounts").GetProperty("TextPage/schema-1").GetInt32());
        Assert.Equal(1, metrics.GetProperty("frameTypeCounts").GetProperty("AbilityWatch/schema-1").GetInt32());

        Assert.Equal("screen", root.GetProperty("lastBackend").GetString());
        Assert.Equal(0, root.GetProperty("lastDetection").GetProperty("originX").GetInt32());
        Assert.Equal("FollowUnitStatus", root.GetProperty("lastFrame").GetProperty("frameType").GetString());
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
        Assert.Equal(14, freshJson.RootElement.GetProperty("aggregate").GetProperty("acceptedFrames").GetInt32());
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
                stripCount = 2,
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
                    followUnitStatus = 32,
                    additionalTelemetry = 64,
                    textAndAuras = 128
                }
            },
            aggregate = new
            {
                acceptedFrames = 14,
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
                    reservedFlags = 63,
                    castFlags = 25,
                    castActive = true,
                    channeled = false,
                    uninterruptible = false,
                    hasLabel = true,
                    hasTarget = true,
                    progressPctQ8 = 96,
                    durationCenti = 250,
                    remainingCenti = 150,
                    durationSeconds = 2.5,
                    remainingSeconds = 1.5,
                    castTargetCode = 2,
                    spellLabel = "HEALI"
                },
                playerResources = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-12),
                    ageMs = 12.0,
                    fresh = true,
                    stale = false,
                    frameType = "PlayerResources",
                    schemaId = 1,
                    sequence = 5,
                    reservedFlags = 63,
                    manaCurrent = 4200,
                    manaMax = 5000,
                    energyCurrent = 85,
                    energyMax = 100,
                    powerCurrent = 12,
                    powerMax = 100
                },
                playerCombat = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-11),
                    ageMs = 11.0,
                    fresh = true,
                    stale = false,
                    frameType = "PlayerCombat",
                    schemaId = 1,
                    sequence = 6,
                    reservedFlags = 255,
                    combatFlags = 255,
                    hasCombo = true,
                    hasCharge = true,
                    hasPlanar = true,
                    hasAbsorb = true,
                    pvp = true,
                    mentoring = true,
                    ready = true,
                    afk = true,
                    combo = 4,
                    chargeCurrent = 80,
                    chargeMax = 100,
                    planarCurrent = 3,
                    planarMax = 6,
                    absorb = 250
                },
                targetPosition = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-9),
                    ageMs = 9.0,
                    fresh = true,
                    stale = false,
                    frameType = "TargetPosition",
                    schemaId = 1,
                    sequence = 7,
                    reservedFlags = 63,
                    x = 128.75,
                    y = 201.50,
                    z = -48.25
                },
                targetVitals = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-7),
                    ageMs = 7.0,
                    fresh = true,
                    stale = false,
                    frameType = "TargetVitals",
                    schemaId = 1,
                    sequence = 9,
                    reservedFlags = 255,
                    healthCurrent = 31200,
                    healthMax = 35000,
                    absorb = 120,
                    targetFlags = 15,
                    present = true,
                    alive = true,
                    combat = true,
                    tagged = true,
                    targetLevel = 72
                },
                targetResources = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-6),
                    ageMs = 6.0,
                    fresh = true,
                    stale = false,
                    frameType = "TargetResources",
                    schemaId = 1,
                    sequence = 10,
                    reservedFlags = 255,
                    manaCurrent = 2200,
                    manaMax = 3000,
                    energyCurrent = 80,
                    energyMax = 100,
                    powerCurrent = 18,
                    powerMax = 100
                },
                auxUnitCast = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-5),
                    ageMs = 5.0,
                    fresh = true,
                    stale = false,
                    frameType = "AuxUnitCast",
                    schemaId = 1,
                    sequence = 11,
                    reservedFlags = 255,
                    unitSelectorCode = 2,
                    castFlags = 19,
                    progressPctQ8 = 88,
                    durationCenti = 180,
                    remainingCenti = 60,
                    castTargetCode = 1,
                    label = "SHLD"
                },
                auraPage = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-4),
                    ageMs = 4.0,
                    fresh = true,
                    stale = false,
                    frameType = "AuraPage",
                    schemaId = 1,
                    sequence = 12,
                    reservedFlags = 255,
                    pageKindCode = 1,
                    totalAuraCount = 8,
                    entry1 = new
                    {
                        id = 1001,
                        remainingQ4 = 24,
                        stack = 2,
                        flags = 23
                    },
                    entry2 = new
                    {
                        id = 1002,
                        remainingQ4 = 24,
                        stack = 2,
                        flags = 23
                    }
                },
                textPage = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-3),
                    ageMs = 3.0,
                    fresh = true,
                    stale = false,
                    frameType = "TextPage",
                    schemaId = 1,
                    sequence = 13,
                    reservedFlags = 255,
                    textKindCode = 3,
                    textHash16 = 48879,
                    label = "AURA TEXT"
                },
                abilityWatch = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-2),
                    ageMs = 2.0,
                    fresh = true,
                    stale = false,
                    frameType = "AbilityWatch",
                    schemaId = 1,
                    sequence = 14,
                    reservedFlags = 255,
                    pageIndex = 4,
                    entry1 = new
                    {
                        id = 2001,
                        cooldownQ4 = 12,
                        flags = 51
                    },
                    entry2 = new
                    {
                        id = 2002,
                        cooldownQ4 = 12,
                        flags = 51
                    },
                    shortestCooldownQ4 = 8,
                    readyCount = 3,
                    coolingCount = 2
                },
                followUnitStatus = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-1),
                    ageMs = 1.0,
                    fresh = true,
                    stale = false,
                    frameType = "FollowUnitStatus",
                    schemaId = 1,
                    sequence = 16,
                    reservedFlags = 255,
                    slot = 2,
                    followFlags = 131,
                    present = true,
                    alive = true,
                    combat = false,
                    afk = false,
                    offline = false,
                    aggro = false,
                    blocked = false,
                    readyFlag = true,
                    x = 7124.5,
                    y = 866.0,
                    z = 3011.5,
                    healthPctQ8 = 200,
                    resourcePctQ8 = 120,
                    level = 69,
                    calling = 2,
                    role = 1
                },
                followUnitStatuses = new[]
                {
                    new
                    {
                        slot = 1,
                        observedAtUtc = nowUtc.AddMilliseconds(-1),
                        ageMs = 1.0,
                        fresh = true,
                        stale = false,
                        frameType = "FollowUnitStatus",
                        schemaId = 1,
                        sequence = 15,
                        reservedFlags = 255,
                        followFlags = 143,
                        present = true,
                        alive = true,
                        combat = true,
                        afk = true,
                        offline = false,
                        aggro = false,
                        blocked = false,
                        readyFlag = true,
                        x = 7123.5,
                        y = 865.0,
                        z = 3010.5,
                        healthPctQ8 = 222,
                        resourcePctQ8 = 144,
                        level = 70,
                        calling = 3,
                        role = 1
                    },
                    new
                    {
                        slot = 2,
                        observedAtUtc = nowUtc.AddMilliseconds(-1),
                        ageMs = 1.0,
                        fresh = true,
                        stale = false,
                        frameType = "FollowUnitStatus",
                        schemaId = 1,
                        sequence = 16,
                        reservedFlags = 255,
                        followFlags = 131,
                        present = true,
                        alive = true,
                        combat = false,
                        afk = false,
                        offline = false,
                        aggro = false,
                        blocked = false,
                        readyFlag = true,
                        x = 7124.5,
                        y = 866.0,
                        z = 3011.5,
                        healthPctQ8 = 200,
                        resourcePctQ8 = 120,
                        level = 69,
                        calling = 2,
                        role = 1
                    }
                }
            },
            metrics = new
            {
                acceptedSamples = 13,
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
                    ["PlayerCast/schema-1"] = 1,
                    ["PlayerResources/schema-1"] = 1,
                    ["PlayerCombat/schema-1"] = 1,
                    ["TargetPosition/schema-1"] = 1,
                    ["FollowUnitStatus/schema-1"] = 1,
                    ["TargetVitals/schema-1"] = 1,
                    ["TargetResources/schema-1"] = 1,
                    ["AuxUnitCast/schema-1"] = 1,
                    ["AuraPage/schema-1"] = 1,
                    ["TextPage/schema-1"] = 1,
                    ["AbilityWatch/schema-1"] = 1
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
                frameType = "FollowUnitStatus",
                schemaId = 1,
                sequence = 16,
                reservedFlags = 255
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
