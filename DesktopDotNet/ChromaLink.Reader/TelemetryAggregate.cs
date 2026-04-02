namespace ChromaLink.Reader;

public sealed record FrameObservation<TFrame>(TFrame Frame, DateTimeOffset ObservedAtUtc)
    where TFrame : TelemetryFrame;

public sealed record TelemetryAggregateSnapshot(
    FrameObservation<CoreStatusFrame>? CoreStatus,
    FrameObservation<PlayerVitalsFrame>? PlayerVitals,
    FrameObservation<PlayerPositionFrame>? PlayerPosition,
    FrameObservation<PlayerCastFrame>? PlayerCast,
    FrameObservation<PlayerResourcesFrame>? PlayerResources,
    FrameObservation<PlayerCombatFrame>? PlayerCombat,
    FrameObservation<TargetPositionFrame>? TargetPosition,
    FrameObservation<FollowUnitStatusFrame>? FollowUnitStatus,
    DateTimeOffset? LastUpdatedUtc,
    int AcceptedFrames)
{
    public bool HasAny =>
        CoreStatus is not null ||
        PlayerVitals is not null ||
        PlayerPosition is not null ||
        PlayerCast is not null ||
        PlayerResources is not null ||
        PlayerCombat is not null ||
        TargetPosition is not null ||
        FollowUnitStatus is not null;

    public bool HasCompleteState =>
        CoreStatus is not null &&
        PlayerVitals is not null &&
        PlayerPosition is not null;
}

public sealed class TelemetryAggregate
{
    private FrameObservation<CoreStatusFrame>? _coreStatus;
    private FrameObservation<PlayerVitalsFrame>? _playerVitals;
    private FrameObservation<PlayerPositionFrame>? _playerPosition;
    private FrameObservation<PlayerCastFrame>? _playerCast;
    private FrameObservation<PlayerResourcesFrame>? _playerResources;
    private FrameObservation<PlayerCombatFrame>? _playerCombat;
    private FrameObservation<TargetPositionFrame>? _targetPosition;
    private FrameObservation<FollowUnitStatusFrame>? _followUnitStatus;

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

            case PlayerCastFrame playerCast:
                _playerCast = new FrameObservation<PlayerCastFrame>(playerCast, observed);
                break;

            case PlayerResourcesFrame playerResources:
                _playerResources = new FrameObservation<PlayerResourcesFrame>(playerResources, observed);
                break;

            case PlayerCombatFrame playerCombat:
                _playerCombat = new FrameObservation<PlayerCombatFrame>(playerCombat, observed);
                break;

            case TargetPositionFrame targetPosition:
                _targetPosition = new FrameObservation<TargetPositionFrame>(targetPosition, observed);
                break;

            case FollowUnitStatusFrame followUnitStatus:
                _followUnitStatus = new FrameObservation<FollowUnitStatusFrame>(followUnitStatus, observed);
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
            _playerCast,
            _playerResources,
            _playerCombat,
            _targetPosition,
            _followUnitStatus,
            LastUpdatedUtc,
            AcceptedFrames);
    }
}
