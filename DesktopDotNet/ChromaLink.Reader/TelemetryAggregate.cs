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
    IReadOnlyDictionary<byte, FrameObservation<FollowUnitStatusFrame>> FollowUnitStatusesBySlot,
    FrameObservation<TargetVitalsFrame>? TargetVitals,
    FrameObservation<TargetResourcesFrame>? TargetResources,
    FrameObservation<AuxUnitCastFrame>? AuxUnitCast,
    FrameObservation<AuraPageFrame>? AuraPage,
    FrameObservation<TextPageFrame>? TextPage,
    FrameObservation<AbilityWatchFrame>? AbilityWatch,
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
        FollowUnitStatus is not null ||
        FollowUnitStatusesBySlot.Count > 0 ||
        TargetVitals is not null ||
        TargetResources is not null ||
        AuxUnitCast is not null ||
        AuraPage is not null ||
        TextPage is not null ||
        AbilityWatch is not null;

    public bool HasCompleteState =>
        CoreStatus is not null &&
        PlayerVitals is not null &&
        PlayerResources is not null &&
        PlayerCombat is not null;
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
    private readonly SortedDictionary<byte, FrameObservation<FollowUnitStatusFrame>> _followUnitStatusesBySlot = new();
    private FrameObservation<TargetVitalsFrame>? _targetVitals;
    private FrameObservation<TargetResourcesFrame>? _targetResources;
    private FrameObservation<AuxUnitCastFrame>? _auxUnitCast;
    private FrameObservation<AuraPageFrame>? _auraPage;
    private FrameObservation<TextPageFrame>? _textPage;
    private FrameObservation<AbilityWatchFrame>? _abilityWatch;

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
                _followUnitStatusesBySlot[followUnitStatus.Payload.Slot] = _followUnitStatus;
                break;

            case TargetVitalsFrame targetVitals:
                _targetVitals = new FrameObservation<TargetVitalsFrame>(targetVitals, observed);
                break;

            case TargetResourcesFrame targetResources:
                _targetResources = new FrameObservation<TargetResourcesFrame>(targetResources, observed);
                break;

            case AuxUnitCastFrame auxUnitCast:
                _auxUnitCast = new FrameObservation<AuxUnitCastFrame>(auxUnitCast, observed);
                break;

            case AuraPageFrame auraPage:
                _auraPage = new FrameObservation<AuraPageFrame>(auraPage, observed);
                break;

            case TextPageFrame textPage:
                _textPage = new FrameObservation<TextPageFrame>(textPage, observed);
                break;

            case AbilityWatchFrame abilityWatch:
                _abilityWatch = new FrameObservation<AbilityWatchFrame>(abilityWatch, observed);
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
            _followUnitStatusesBySlot.ToDictionary(static entry => entry.Key, static entry => entry.Value),
            _targetVitals,
            _targetResources,
            _auxUnitCast,
            _auraPage,
            _textPage,
            _abilityWatch,
            LastUpdatedUtc,
            AcceptedFrames);
    }
}
