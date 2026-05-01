using System.Net;
using System.Text.Json;
using ChromaLink.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace ChromaLink.Tests;

public class ChromaLinkClientTests
{
    [Fact]
    public async Task Client_ReadsRiftReaderWorldState_FromHttpBridge()
    {
        using var scope = new TempSnapshotScope();
        var snapshotPath = WriteClientSnapshot(scope, ready: true, healthy: true, ageSeconds: 0);

        await using var app = CreateBridgeApp(snapshotPath);
        await app.StartAsync();

        using var client = new ChromaLinkHttpClient(app.GetTestClient());

        var manifest = await client.GetApiManifestAsync();
        Assert.NotNull(manifest);
        Assert.True(manifest!.LocalOnly);
        Assert.Contains(manifest.Endpoints, endpoint => endpoint.Path == "/api/v1/riftreader/world-state");

        var response = await client.GetRiftReaderWorldStateAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.IsSuccessStatusCode);
        Assert.Null(response.ParseError);
        Assert.True(response.HasWorldState);
        Assert.True(response.IsReadyAndFresh);
        Assert.NotNull(response.WorldState);
        Assert.True(response.WorldState!.Ok);
        Assert.Equal("chromalink-riftreader-world-state", response.WorldState.Contract?.Name);
        Assert.Equal("chromalink-live-telemetry", response.WorldState.SourceContract?.Name);
        Assert.True(response.WorldState.Navigation?.PlayerPositionAvailable);
        Assert.False(response.WorldState.Navigation?.HeadingAvailable);
        Assert.False(response.WorldState.Navigation?.ControlAvailable);
        Assert.Equal(12.5, response.PlayerPosition?.X);
        Assert.Equal(44.25, response.PlayerPosition?.Y);
        Assert.Equal(-8.0, response.PlayerPosition?.Z);
        Assert.Equal(900, response.WorldState.Player?.HealthCurrent);
        Assert.Equal(128.75, response.WorldState.Target?.Position?.X);
        Assert.Single(response.WorldState.FollowUnits);
        Assert.Equal(1, response.WorldState.FollowUnits[0].Slot);
        Assert.Equal(7123.5, response.WorldState.FollowUnits[0].Position?.X);
    }

    [Fact]
    public async Task Client_ReturnsUnavailableState_WhenSnapshotIsMissing()
    {
        using var scope = new TempSnapshotScope();
        await using var app = CreateBridgeApp(scope.GetSnapshotPath("missing.json"));
        await app.StartAsync();

        using var client = new ChromaLinkHttpClient(app.GetTestClient());

        var response = await client.GetRiftReaderWorldStateAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.False(response.IsSuccessStatusCode);
        Assert.Null(response.ParseError);
        Assert.NotNull(response.WorldState);
        Assert.False(response.WorldState!.Ok);
        Assert.Equal("Snapshot not found.", response.WorldState.Error);
        Assert.False(response.HasWorldState);
        Assert.False(response.IsReadyAndFresh);
    }

    private static WebApplication CreateBridgeApp(string snapshotPath)
    {
        return HttpBridgeApp.CreateApp(Array.Empty<string>(), useTestServer: true, snapshotPathOverride: snapshotPath);
    }

    private static string WriteClientSnapshot(TempSnapshotScope scope, bool ready, bool healthy, int ageSeconds)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var path = scope.GetSnapshotPath("chromalink-live-telemetry.json");

        var payload = new
        {
            artifactKind = "live-telemetry",
            contract = new
            {
                name = "chromalink-live-telemetry",
                schemaVersion = 2
            },
            aggregate = new
            {
                acceptedFrames = 4,
                ready,
                healthy,
                stale = false,
                coreStatus = new
                {
                    playerLevel = 70,
                    playerCalling = 1,
                    playerRole = 1,
                    targetLevel = 72
                },
                playerVitals = new
                {
                    healthCurrent = 900,
                    healthMax = 1000,
                    resourceCurrent = 400,
                    resourceMax = 500
                },
                playerPosition = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-30),
                    ageMs = 30.0,
                    fresh = true,
                    stale = false,
                    x = 12.5,
                    y = 44.25,
                    z = -8.0
                },
                targetVitals = new
                {
                    present = true,
                    alive = true,
                    combat = true,
                    healthCurrent = 31200,
                    healthMax = 35000,
                    targetLevel = 72
                },
                targetPosition = new
                {
                    observedAtUtc = nowUtc.AddMilliseconds(-20),
                    ageMs = 20.0,
                    fresh = true,
                    stale = false,
                    x = 128.75,
                    y = 201.50,
                    z = -48.25
                },
                followUnitStatuses = new[]
                {
                    new
                    {
                        slot = 1,
                        observedAtUtc = nowUtc.AddMilliseconds(-10),
                        ageMs = 10.0,
                        fresh = true,
                        stale = false,
                        present = true,
                        alive = true,
                        combat = false,
                        afk = false,
                        offline = false,
                        blocked = false,
                        readyFlag = true,
                        healthPctQ8 = 222,
                        resourcePctQ8 = 144,
                        level = 70,
                        calling = 3,
                        role = 1,
                        x = 7123.5,
                        y = 865.0,
                        z = 3010.5
                    }
                }
            }
        };

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(payload, HttpBridgeSnapshotService.JsonOptions));
        File.SetLastWriteTimeUtc(path, nowUtc.AddSeconds(-ageSeconds).UtcDateTime);
        return path;
    }

    private sealed class TempSnapshotScope : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "chromalink-client-tests-" + Guid.NewGuid().ToString("N"));

        public TempSnapshotScope()
        {
            Directory.CreateDirectory(_root);
        }

        public string GetSnapshotPath(string fileName)
        {
            return Path.Combine(_root, fileName);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for temp test artifacts.
            }
        }
    }
}
