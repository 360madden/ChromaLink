using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChromaLink.Client;

public sealed class ChromaLinkHttpClient : IDisposable
{
    public static readonly Uri DefaultBaseAddress = new("http://127.0.0.1:7337/");

    public const string ApiManifestPath = "api/v1";
    public const string RiftReaderWorldStatePath = "api/v1/riftreader/world-state";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public ChromaLinkHttpClient(HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = DefaultBaseAddress
            };
            _ownsHttpClient = true;
            return;
        }

        _httpClient = httpClient;
        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = DefaultBaseAddress;
        }
    }

    public Task<ChromaLinkApiManifest?> GetApiManifestAsync(CancellationToken cancellationToken = default)
    {
        return _httpClient.GetFromJsonAsync<ChromaLinkApiManifest>(ApiManifestPath, JsonOptions, cancellationToken);
    }

    public async Task<ChromaLinkWorldStateResponse> GetRiftReaderWorldStateAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(RiftReaderWorldStatePath, cancellationToken).ConfigureAwait(false);
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new ChromaLinkWorldStateResponse(response.StatusCode, response.IsSuccessStatusCode, null, rawJson, "Response body was empty.");
        }

        try
        {
            var worldState = JsonSerializer.Deserialize<ChromaLinkRiftReaderWorldState>(rawJson, JsonOptions);
            return new ChromaLinkWorldStateResponse(response.StatusCode, response.IsSuccessStatusCode, worldState, rawJson, null);
        }
        catch (JsonException ex)
        {
            return new ChromaLinkWorldStateResponse(response.StatusCode, response.IsSuccessStatusCode, null, rawJson, ex.Message);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}

public sealed record ChromaLinkWorldStateResponse(
    HttpStatusCode StatusCode,
    bool IsSuccessStatusCode,
    ChromaLinkRiftReaderWorldState? WorldState,
    string RawJson,
    string? ParseError)
{
    public bool HasWorldState => WorldState?.Navigation is not null;

    public bool IsReadyAndFresh =>
        IsSuccessStatusCode &&
        WorldState?.Ok == true &&
        WorldState.Ready &&
        WorldState.Fresh &&
        WorldState.Navigation?.PlayerPositionAvailable == true;

    public ChromaLinkRiftReaderPosition? PlayerPosition => WorldState?.Player?.Position;
}

public sealed record ChromaLinkApiManifest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; init; }

    [JsonPropertyName("localOnly")]
    public bool LocalOnly { get; init; }

    [JsonPropertyName("snapshotContract")]
    public ChromaLinkSnapshotContract? SnapshotContract { get; init; }

    [JsonPropertyName("endpoints")]
    public IReadOnlyList<ChromaLinkApiEndpoint> Endpoints { get; init; } = Array.Empty<ChromaLinkApiEndpoint>();
}

public sealed record ChromaLinkApiEndpoint
{
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("purpose")]
    public string? Purpose { get; init; }
}

public sealed record ChromaLinkSnapshotContract
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }
}

public sealed record ChromaLinkRiftReaderWorldState
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("artifactKind")]
    public string? ArtifactKind { get; init; }

    [JsonPropertyName("contract")]
    public ChromaLinkSnapshotContract? Contract { get; init; }

    [JsonPropertyName("sourceContract")]
    public ChromaLinkSnapshotContract? SourceContract { get; init; }

    [JsonPropertyName("ready")]
    public bool Ready { get; init; }

    [JsonPropertyName("healthy")]
    public bool Healthy { get; init; }

    [JsonPropertyName("fresh")]
    public bool Fresh { get; init; }

    [JsonPropertyName("stale")]
    public bool Stale { get; init; }

    [JsonPropertyName("snapshotAgeSeconds")]
    public double? SnapshotAgeSeconds { get; init; }

    [JsonPropertyName("snapshotPath")]
    public string? SnapshotPath { get; init; }

    [JsonPropertyName("navigation")]
    public ChromaLinkRiftReaderNavigation? Navigation { get; init; }

    [JsonPropertyName("player")]
    public ChromaLinkRiftReaderPlayer? Player { get; init; }

    [JsonPropertyName("target")]
    public ChromaLinkRiftReaderTarget? Target { get; init; }

    [JsonPropertyName("followUnits")]
    public IReadOnlyList<ChromaLinkRiftReaderFollowUnit> FollowUnits { get; init; } = Array.Empty<ChromaLinkRiftReaderFollowUnit>();

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

public sealed record ChromaLinkRiftReaderNavigation
{
    [JsonPropertyName("playerPositionAvailable")]
    public bool PlayerPositionAvailable { get; init; }

    [JsonPropertyName("targetPositionAvailable")]
    public bool TargetPositionAvailable { get; init; }

    [JsonPropertyName("followUnitPositionsAvailable")]
    public bool FollowUnitPositionsAvailable { get; init; }

    [JsonPropertyName("headingAvailable")]
    public bool HeadingAvailable { get; init; }

    [JsonPropertyName("facingAvailable")]
    public bool FacingAvailable { get; init; }

    [JsonPropertyName("routeAvailable")]
    public bool RouteAvailable { get; init; }

    [JsonPropertyName("controlAvailable")]
    public bool ControlAvailable { get; init; }

    [JsonPropertyName("limitations")]
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
}

public sealed record ChromaLinkRiftReaderPlayer
{
    [JsonPropertyName("position")]
    public ChromaLinkRiftReaderPosition? Position { get; init; }

    [JsonPropertyName("healthCurrent")]
    public int? HealthCurrent { get; init; }

    [JsonPropertyName("healthMax")]
    public int? HealthMax { get; init; }

    [JsonPropertyName("resourceCurrent")]
    public int? ResourceCurrent { get; init; }

    [JsonPropertyName("resourceMax")]
    public int? ResourceMax { get; init; }

    [JsonPropertyName("level")]
    public int? Level { get; init; }

    [JsonPropertyName("calling")]
    public int? Calling { get; init; }

    [JsonPropertyName("role")]
    public int? Role { get; init; }
}

public sealed record ChromaLinkRiftReaderTarget
{
    [JsonPropertyName("position")]
    public ChromaLinkRiftReaderPosition? Position { get; init; }

    [JsonPropertyName("present")]
    public bool? Present { get; init; }

    [JsonPropertyName("alive")]
    public bool? Alive { get; init; }

    [JsonPropertyName("combat")]
    public bool? Combat { get; init; }

    [JsonPropertyName("healthCurrent")]
    public int? HealthCurrent { get; init; }

    [JsonPropertyName("healthMax")]
    public int? HealthMax { get; init; }

    [JsonPropertyName("level")]
    public int? Level { get; init; }
}

public sealed record ChromaLinkRiftReaderFollowUnit
{
    [JsonPropertyName("slot")]
    public int? Slot { get; init; }

    [JsonPropertyName("position")]
    public ChromaLinkRiftReaderPosition? Position { get; init; }

    [JsonPropertyName("present")]
    public bool? Present { get; init; }

    [JsonPropertyName("alive")]
    public bool? Alive { get; init; }

    [JsonPropertyName("combat")]
    public bool? Combat { get; init; }

    [JsonPropertyName("afk")]
    public bool? Afk { get; init; }

    [JsonPropertyName("offline")]
    public bool? Offline { get; init; }

    [JsonPropertyName("blocked")]
    public bool? Blocked { get; init; }

    [JsonPropertyName("ready")]
    public bool? Ready { get; init; }

    [JsonPropertyName("healthPctQ8")]
    public int? HealthPctQ8 { get; init; }

    [JsonPropertyName("resourcePctQ8")]
    public int? ResourcePctQ8 { get; init; }

    [JsonPropertyName("level")]
    public int? Level { get; init; }

    [JsonPropertyName("calling")]
    public int? Calling { get; init; }

    [JsonPropertyName("role")]
    public int? Role { get; init; }
}

public sealed record ChromaLinkRiftReaderPosition
{
    [JsonPropertyName("x")]
    public double X { get; init; }

    [JsonPropertyName("y")]
    public double Y { get; init; }

    [JsonPropertyName("z")]
    public double Z { get; init; }

    [JsonPropertyName("observedAtUtc")]
    public DateTimeOffset? ObservedAtUtc { get; init; }

    [JsonPropertyName("ageMs")]
    public double? AgeMs { get; init; }

    [JsonPropertyName("fresh")]
    public bool? Fresh { get; init; }

    [JsonPropertyName("stale")]
    public bool? Stale { get; init; }
}
