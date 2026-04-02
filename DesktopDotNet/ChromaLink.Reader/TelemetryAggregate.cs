namespace ChromaLink.Reader;

public sealed record FrameObservation<TFrame>(TFrame Frame, DateTimeOffset ObservedAtUtc)
    where TFrame : TelemetryFrame;

public sealed record TelemetryAggregateSnapshot(
    FrameObservation<CoreStatusFrame>? CoreStatus,
    FrameObservation<PlayerVitalsFrame>? PlayerVitals,
    FrameObservation<PlayerPositionFrame>? PlayerPosition,
    DateTimeOffset? LastUpdatedUtc,
    int AcceptedFrames)
{
    public bool HasAny => CoreStatus is not null || PlayerVitals is not null || PlayerPosition is not null;

    public bool HasCompleteState => CoreStatus is not null && PlayerVitals is not null && PlayerPosition is not null;
}

public sealed class TelemetryAggregate
{
    private FrameObservation<CoreStatusFrame>? _coreStatus;
    private FrameObservation<PlayerVitalsFrame>? _playerVitals;
    private FrameObservation<PlayerPositionFrame>? _playerPosition;

    public int AcceptedFrames { get; private set; }

    public DateTimeOffset? LastUpdatedUtc { get; private set; }

    public void Update(TelemetryFrame frame, DateTimeOffset? observedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var observed = observedAtUtc ?? DateTimeOffset.UtcNow;
        switch (frame)
        {
            case CoreStatusFrame coreStatus:
                _coreStatus = new FrameObservation<CoreStatusFrame>(coreStatus, observed);
                break;

            case PlayerVitalsFrame playerVitals:
                _playerVitals = new FrameObservation<PlayerVitalsFrame>(playerVitals, observed);
                break;

            case PlayerPositionFrame playerPosition:
                _playerPosition = new FrameObservation<PlayerPositionFrame>(playerPosition, observed);
                break;

            default:
                throw new InvalidOperationException($"Unsupported telemetry frame type: {frame.GetType().Name}.");
        }

        AcceptedFrames++;
        LastUpdatedUtc = observed;
    }

    public TelemetryAggregateSnapshot Snapshot()
    {
        return new TelemetryAggregateSnapshot(
            _coreStatus,
            _playerVitals,
            _playerPosition,
            LastUpdatedUtc,
            AcceptedFrames);
    }
}
