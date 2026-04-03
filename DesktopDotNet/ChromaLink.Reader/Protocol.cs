namespace ChromaLink.Reader;

public enum FrameType : byte
{
    CoreStatus = 1,
    PlayerVitals = 2,
    PlayerPosition = 3,
    PlayerCast = 4,
    PlayerResources = 5,
    PlayerCombat = 6,
    TargetPosition = 7,
    FollowUnitStatus = 8,
    TargetVitals = 9,
    TargetResources = 10,
    AuxUnitCast = 11,
    AuraPage = 12,
    TextPage = 13,
    AbilityWatch = 14,
    RiftMeterCombat = 15
}

public enum ResourceKind : byte
{
    None = 0,
    Mana = 1,
    Energy = 2,
    Power = 3,
    Charge = 4,
    Planar = 5
}

[Flags]
public enum HeaderCapabilityFlags : byte
{
    None = 0,
    MultiFrameRotation = 1 << 0,
    PlayerPosition = 1 << 1,
    PlayerCast = 1 << 2,
    ExpandedStats = 1 << 3,
    TargetPosition = 1 << 4,
    FollowUnitStatus = 1 << 5,
    AdditionalTelemetry = 1 << 6,
    TextAndAuras = 1 << 7
}

public sealed record TelemetryFrameHeader(
    byte ProtocolVersion,
    byte ProfileId,
    FrameType FrameType,
    byte SchemaId,
    byte Sequence,
    byte ReservedFlags,
    ushort HeaderCrc16);

public abstract record TelemetryFrame(
    TelemetryFrameHeader Header,
    uint PayloadCrc32C,
    byte[] TransportBytes);

public readonly record struct CoreStatusSnapshot(
    byte PlayerStateFlags,
    byte PlayerHealthPctQ8,
    byte PlayerResourceKind,
    byte PlayerResourcePctQ8,
    byte TargetStateFlags,
    byte TargetHealthPctQ8,
    byte TargetResourceKind,
    byte TargetResourcePctQ8,
    byte PlayerLevel,
    byte TargetLevel,
    byte PlayerCallingRolePacked,
    byte TargetCallingRelationPacked)
{
    public static CoreStatusSnapshot CreateSynthetic()
    {
        return new CoreStatusSnapshot(
            0b0000_0111,
            198,
            (byte)ResourceKind.Mana,
            144,
            0b0000_1111,
            91,
            (byte)ResourceKind.None,
            0,
            70,
            72,
            0x31,
            0x42);
    }
}

public readonly record struct PlayerVitalsSnapshot(
    uint HealthCurrent,
    uint HealthMax,
    ushort ResourceCurrent,
    ushort ResourceMax)
{
    public static PlayerVitalsSnapshot CreateSynthetic() => new(3260, 3260, 100, 100);
}

public readonly record struct PlayerPositionSnapshot(float X, float Y, float Z)
{
    public static PlayerPositionSnapshot CreateSynthetic() => new(123.45f, 200.67f, -50.12f);
}

public readonly record struct PlayerCastSnapshot(
    byte CastFlags,
    byte ProgressPctQ8,
    ushort DurationCenti,
    ushort RemainingCenti,
    byte CastTargetCode,
    string SpellLabel)
{
    public static PlayerCastSnapshot CreateSynthetic() => new(0b0001_1001, 96, 250, 150, 2, "HEALI");
}

public readonly record struct PlayerResourcesSnapshot(
    ushort ManaCurrent,
    ushort ManaMax,
    ushort EnergyCurrent,
    ushort EnergyMax,
    ushort PowerCurrent,
    ushort PowerMax)
{
    public static PlayerResourcesSnapshot CreateSynthetic() => new(4200, 5000, 85, 100, 12, 100);
}

public readonly record struct PlayerCombatSnapshot(
    byte CombatFlags,
    byte Combo,
    ushort ChargeCurrent,
    ushort ChargeMax,
    ushort PlanarCurrent,
    ushort PlanarMax,
    ushort Absorb)
{
    public static PlayerCombatSnapshot CreateSynthetic() => new(255, 4, 80, 100, 3, 6, 250);
}

public readonly record struct TargetPositionSnapshot(float X, float Y, float Z)
{
    public static TargetPositionSnapshot CreateSynthetic() => new(128.75f, 201.50f, -48.25f);
}

public readonly record struct FollowUnitStatusSnapshot(
    byte Slot,
    byte FollowFlags,
    float X,
    float Y,
    float Z,
    byte HealthPctQ8,
    byte ResourcePctQ8,
    byte Level,
    byte CallingRolePacked)
{
    public static FollowUnitStatusSnapshot CreateSynthetic()
    {
        return new FollowUnitStatusSnapshot(1, 143, 7123.5f, 865.0f, 3010.5f, 222, 144, 70, 0x31);
    }
}

public readonly record struct TargetVitalsSnapshot(
    uint HealthCurrent,
    uint HealthMax,
    ushort Absorb,
    byte TargetFlags,
    byte TargetLevel)
{
    public static TargetVitalsSnapshot CreateSynthetic() => new(31200, 35000, 120, 0b0000_1111, 72);
}

public readonly record struct TargetResourcesSnapshot(
    ushort ManaCurrent,
    ushort ManaMax,
    ushort EnergyCurrent,
    ushort EnergyMax,
    ushort PowerCurrent,
    ushort PowerMax)
{
    public static TargetResourcesSnapshot CreateSynthetic() => new(2200, 3000, 80, 100, 18, 100);
}

public readonly record struct AuxUnitCastSnapshot(
    byte UnitSelectorCode,
    byte CastFlags,
    byte ProgressPctQ8,
    ushort DurationCenti,
    ushort RemainingCenti,
    byte CastTargetCode,
    string Label)
{
    public static AuxUnitCastSnapshot CreateSynthetic() => new(2, 0b0001_0011, 88, 180, 60, 1, "SHLD");
}

public readonly record struct AuraPageEntrySnapshot(
    ushort Id,
    byte RemainingQ4,
    byte Stack,
    byte Flags)
{
    public static AuraPageEntrySnapshot CreateSynthetic(uint seed)
    {
        return new AuraPageEntrySnapshot((ushort)(1000 + seed), 24, 2, 0b0001_0111);
    }
}

public readonly record struct AuraPageSnapshot(
    byte PageKindCode,
    byte TotalAuraCount,
    AuraPageEntrySnapshot Entry1,
    AuraPageEntrySnapshot Entry2)
{
    public static AuraPageSnapshot CreateSynthetic()
    {
        return new AuraPageSnapshot(1, 8, AuraPageEntrySnapshot.CreateSynthetic(1), AuraPageEntrySnapshot.CreateSynthetic(2));
    }
}

public readonly record struct TextPageSnapshot(
    byte TextKindCode,
    ushort TextHash16,
    string Label)
{
    public static TextPageSnapshot CreateSynthetic() => new(3, 0xBEEF, "AURA TEXT");
}

public readonly record struct AbilityWatchEntrySnapshot(
    ushort Id,
    byte CooldownQ4,
    byte Flags)
{
    public static AbilityWatchEntrySnapshot CreateSynthetic(uint seed)
    {
        return new AbilityWatchEntrySnapshot((ushort)(2000 + seed), 12, 0b0011_0011);
    }
}

public readonly record struct AbilityWatchSnapshot(
    byte PageIndex,
    AbilityWatchEntrySnapshot Entry1,
    AbilityWatchEntrySnapshot Entry2,
    byte ShortestCooldownQ4,
    byte ReadyCount,
    byte CoolingCount)
{
    public static AbilityWatchSnapshot CreateSynthetic()
    {
        return new AbilityWatchSnapshot(4, AbilityWatchEntrySnapshot.CreateSynthetic(1), AbilityWatchEntrySnapshot.CreateSynthetic(2), 8, 3, 2);
    }
}

public readonly record struct RiftMeterCombatSnapshot(
    byte RiftMeterFlags,
    byte CombatCount,
    ushort ActiveCombatDurationDeci,
    byte ActiveCombatPlayerCount,
    byte ActiveCombatHostileCount,
    ushort OverallDurationDeci,
    byte OverallPlayerCount,
    byte OverallHostileCount,
    byte OverallDamageK,
    byte OverallHealingK)
{
    public static RiftMeterCombatSnapshot CreateSynthetic() => new(0x3F, 2, 123, 1, 3, 456, 5, 8, 42, 9);
}

public sealed record CoreStatusFrame(
    TelemetryFrameHeader Header,
    CoreStatusSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record PlayerVitalsFrame(
    TelemetryFrameHeader Header,
    PlayerVitalsSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record PlayerPositionFrame(
    TelemetryFrameHeader Header,
    PlayerPositionSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record PlayerCastFrame(
    TelemetryFrameHeader Header,
    PlayerCastSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record PlayerResourcesFrame(
    TelemetryFrameHeader Header,
    PlayerResourcesSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record PlayerCombatFrame(
    TelemetryFrameHeader Header,
    PlayerCombatSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record TargetPositionFrame(
    TelemetryFrameHeader Header,
    TargetPositionSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record FollowUnitStatusFrame(
    TelemetryFrameHeader Header,
    FollowUnitStatusSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record TargetVitalsFrame(
    TelemetryFrameHeader Header,
    TargetVitalsSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record TargetResourcesFrame(
    TelemetryFrameHeader Header,
    TargetResourcesSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record AuxUnitCastFrame(
    TelemetryFrameHeader Header,
    AuxUnitCastSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record AuraPageFrame(
    TelemetryFrameHeader Header,
    AuraPageSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record TextPageFrame(
    TelemetryFrameHeader Header,
    TextPageSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record AbilityWatchFrame(
    TelemetryFrameHeader Header,
    AbilityWatchSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record RiftMeterCombatFrame(
    TelemetryFrameHeader Header,
    RiftMeterCombatSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes)
    : TelemetryFrame(Header, PayloadCrc32C, TransportBytes);

public sealed record TransportParseResult(
    bool IsAccepted,
    string Reason,
    bool MagicValid,
    bool ProtocolProfileValid,
    bool FrameSchemaValid,
    bool HeaderCrcValid,
    bool PayloadCrcValid,
    byte[] TransportBytes,
    TelemetryFrame? Frame);

public static class TransportConstants
{
    public const byte MagicC = (byte)'C';
    public const byte MagicL = (byte)'L';
    public const byte ProtocolVersion = 1;
    public const byte CoreFrameType = 1;
    public const byte CoreSchemaId = 1;
    public const byte PlayerVitalsFrameType = 2;
    public const byte PlayerVitalsSchemaId = 1;
    public const byte PlayerPositionFrameType = 3;
    public const byte PlayerPositionSchemaId = 1;
    public const byte PlayerCastFrameType = 4;
    public const byte PlayerCastSchemaId = 1;
    public const byte PlayerResourcesFrameType = 5;
    public const byte PlayerResourcesSchemaId = 1;
    public const byte PlayerCombatFrameType = 6;
    public const byte PlayerCombatSchemaId = 1;
    public const byte TargetPositionFrameType = 7;
    public const byte TargetPositionSchemaId = 1;
    public const byte FollowUnitStatusFrameType = 8;
    public const byte FollowUnitStatusSchemaId = 1;
    public const byte TargetVitalsFrameType = 9;
    public const byte TargetVitalsSchemaId = 1;
    public const byte TargetResourcesFrameType = 10;
    public const byte TargetResourcesSchemaId = 1;
    public const byte AuxUnitCastFrameType = 11;
    public const byte AuxUnitCastSchemaId = 1;
    public const byte AuraPageFrameType = 12;
    public const byte AuraPageSchemaId = 1;
    public const byte TextPageFrameType = 13;
    public const byte TextPageSchemaId = 1;
    public const byte AbilityWatchFrameType = 14;
    public const byte AbilityWatchSchemaId = 1;
    public const byte RiftMeterCombatFrameType = 15;
    public const byte RiftMeterCombatSchemaId = 1;
    public const int TransportBytes = 24;
    public const int HeaderBytes = 8;
    public const int PayloadBytes = 12;
    public const int PayloadCrcBytes = 4;
    public const int PayloadSymbols = 64;
    public const HeaderCapabilityFlags HeaderCapabilities =
        HeaderCapabilityFlags.MultiFrameRotation |
        HeaderCapabilityFlags.PlayerPosition |
        HeaderCapabilityFlags.PlayerCast |
        HeaderCapabilityFlags.ExpandedStats |
        HeaderCapabilityFlags.TargetPosition |
        HeaderCapabilityFlags.FollowUnitStatus |
        HeaderCapabilityFlags.AdditionalTelemetry |
        HeaderCapabilityFlags.TextAndAuras;
}

public static class FrameProtocol
{
    public static byte[] BuildCoreFrameBytes(byte profileId, byte sequence, CoreStatusSnapshot snapshot)
    {
        Span<byte> payload = stackalloc byte[TransportConstants.PayloadBytes];
        payload[0] = snapshot.PlayerStateFlags;
        payload[1] = snapshot.PlayerHealthPctQ8;
        payload[2] = snapshot.PlayerResourceKind;
        payload[3] = snapshot.PlayerResourcePctQ8;
        payload[4] = snapshot.TargetStateFlags;
        payload[5] = snapshot.TargetHealthPctQ8;
        payload[6] = snapshot.TargetResourceKind;
        payload[7] = snapshot.TargetResourcePctQ8;
        payload[8] = snapshot.PlayerLevel;
        payload[9] = snapshot.TargetLevel;
        payload[10] = snapshot.PlayerCallingRolePacked;
        payload[11] = snapshot.TargetCallingRelationPacked;
        return BuildFrameBytes(profileId, sequence, FrameType.CoreStatus, TransportConstants.CoreSchemaId, payload);
    }

    public static byte[] BuildPlayerVitalsFrameBytes(byte profileId, byte sequence, PlayerVitalsSnapshot snapshot)
    {
        Span<byte> payload = stackalloc byte[TransportConstants.PayloadBytes];
        WriteUInt32BigEndian(payload, 0, snapshot.HealthCurrent);
        WriteUInt32BigEndian(payload, 4, snapshot.HealthMax);
        WriteUInt16BigEndian(payload, 8, snapshot.ResourceCurrent);
        WriteUInt16BigEndian(payload, 10, snapshot.ResourceMax);
        return BuildFrameBytes(profileId, sequence, FrameType.PlayerVitals, TransportConstants.PlayerVitalsSchemaId, payload);
    }

    public static byte[] BuildPlayerPositionFrameBytes(byte profileId, byte sequence, PlayerPositionSnapshot snapshot)
    {
        Span<byte> payload = stackalloc byte[TransportConstants.PayloadBytes];
        WriteInt32BigEndian(payload, 0, FloatToFixed(snapshot.X));
        WriteInt32BigEndian(payload, 4, FloatToFixed(snapshot.Y));
        WriteInt32BigEndian(payload, 8, FloatToFixed(snapshot.Z));
        return BuildFrameBytes(profileId, sequence, FrameType.PlayerPosition, TransportConstants.PlayerPositionSchemaId, payload);
    }

    public static byte[] BuildPlayerCastFrameBytes(byte profileId, byte sequence, PlayerCastSnapshot snapshot)
    {
        Span<byte> payload = stackalloc byte[TransportConstants.PayloadBytes];
        payload[0] = snapshot.CastFlags;
        payload[1] = snapshot.ProgressPctQ8;
        WriteUInt16BigEndian(payload, 2, snapshot.DurationCenti);
        WriteUInt16BigEndian(payload, 4, snapshot.RemainingCenti);
        payload[6] = snapshot.CastTargetCode;
        WriteAsciiLabel(payload, 7, 5, snapshot.SpellLabel);
        return BuildFrameBytes(profileId, sequence, FrameType.PlayerCast, TransportConstants.PlayerCastSchemaId, payload);
    }

    public static byte[] BuildPlayerResourcesFrameBytes(byte profileId, byte sequence, PlayerResourcesSnapshot snapshot)
    {
        Span<byte> payload = stackalloc byte[TransportConstants.PayloadBytes];
        WriteUInt16BigEndian(payload, 0, snapshot.ManaCurrent);
        WriteUInt16BigEndian(payload, 2, snapshot.ManaMax);
        WriteUInt16BigEndian(payload, 4, snapshot.EnergyCurrent);
        WriteUInt16BigEndian(payload, 6, snapshot.EnergyMax);
        WriteUInt16BigEndian(payload, 8, snapshot.PowerCurrent);
        WriteUInt16BigEndian(payload, 10, snapshot.PowerMax);
        return BuildFrameBytes(profileId, sequence, FrameType.PlayerResources, TransportConstants.PlayerResourcesSchemaId, payload);
    }

    public static byte[] BuildPlayerCombatFrameBytes(byte profileId, byte sequence, PlayerCombatSnapshot snapshot)
    {
        Span<byte> payload = stackalloc byte[TransportConstants.PayloadBytes];
        payload[0] = snapshot.CombatFlags;
        payload[1] = snapshot.Combo;
        WriteUInt16BigEndian(payload, 2, snapshot.ChargeCurrent);
        WriteUInt16BigEndian(payload, 4, snapshot.ChargeMax);
        WriteUInt16BigEndian(payload, 6, snapshot.PlanarCurrent);
        WriteUInt16BigEndian(payload, 8, snapshot.PlanarMax);
        WriteUInt16BigEndian(payload, 10, snapshot.Absorb);
        return BuildFrameBytes(profileId, sequence, FrameType.PlayerCombat, TransportConstants.PlayerCombatSchemaId, payload);
    }

    public static byte[] BuildTargetPositionFrameBytes(byte profileId, byte sequence, TargetPositionSnapshot snapshot)
    {
        Span<byte> payload = stackalloc byte[TransportConstants.PayloadBytes];
        WriteInt32BigEndian(payload, 0, FloatToFixed(snapshot.X));
        WriteInt32BigEndian(payload, 4, FloatToFixed(snapshot.Y));
        WriteInt32BigEndian(payload, 8, FloatToFixed(snapshot.Z));
        return BuildFrameBytes(profileId, sequence, FrameType.TargetPosition, TransportConstants.TargetPositionSchemaId, payload);
    }

    public static byte[] BuildFollowUnitStatusFrameBytes(byte profileId, byte sequence, FollowUnitStatusSnapshot snapshot)
    {
        Span<byte> payload = stackalloc byte[TransportConstants.PayloadBytes];
        payload[0] = snapshot.Slot;
        payload[1] = snapshot.FollowFlags;
        WriteInt16BigEndian(payload, 2, FloatToFixedQ2(snapshot.X));
        WriteInt16BigEndian(payload, 4, FloatToFixedQ2(snapshot.Y));
        WriteInt16BigEndian(payload, 6, FloatToFixedQ2(snapshot.Z));
        payload[8] = snapshot.HealthPctQ8;
        payload[9] = snapshot.ResourcePctQ8;
        payload[10] = snapshot.Level;
        payload[11] = snapshot.CallingRolePacked;
        return BuildFrameBytes(profileId, sequence, FrameType.FollowUnitStatus, TransportConstants.FollowUnitStatusSchemaId, payload);
    }

    public static byte[] BuildTargetVitalsFrameBytes(byte profileId, byte sequence, TargetVitalsSnapshot snapshot)
    {
        Span<byte> payload = stackalloc byte[TransportConstants.PayloadBytes];
        WriteUInt32BigEndian(payload, 0, snapshot.HealthCurrent);
        WriteUInt32BigEndian(payload, 4, snapshot.HealthMax);
        WriteUInt16BigEndian(payload, 8, snapshot.Absorb);
        payload[10] = snapshot.TargetFlags;
        payload[11] = snapshot.TargetLevel;
        return BuildFrameBytes(profileId, sequence, FrameType.TargetVitals, TransportConstants.TargetVitalsSchemaId, payload);
    }

    public static byte[] BuildTargetResourcesFrameBytes(byte profileId, byte sequence, TargetResourcesSnapshot snapshot)
    {
        Span<byte> payload = stackalloc byte[TransportConstants.PayloadBytes];
        WriteUInt16BigEndian(payload, 0, snapshot.ManaCurrent);
        WriteUInt16BigEndian(payload, 2, snapshot.ManaMax);
        WriteUInt16BigEndian(payload, 4, snapshot.EnergyCurrent);
        WriteUInt16BigEndian(payload, 6, snapshot.EnergyMax);
        WriteUInt16BigEndian(payload, 8, snapshot.PowerCurrent);
        WriteUInt16BigEndian(payload, 10, snapshot.PowerMax);
        return BuildFrameBytes(profileId, sequence, FrameType.TargetResources, TransportConstants.TargetResourcesSchemaId, payload);
    }

    public static byte[] BuildAuxUnitCastFrameBytes(byte profileId, byte sequence, AuxUnitCastSnapshot snapshot)
    {
        Span<byte> payload = stackalloc byte[TransportConstants.PayloadBytes];
        payload[0] = snapshot.UnitSelectorCode;
        payload[1] = snapshot.CastFlags;
        payload[2] = snapshot.ProgressPctQ8;
        WriteUInt16BigEndian(payload, 3, snapshot.DurationCenti);
        WriteUInt16BigEndian(payload, 5, snapshot.RemainingCenti);
        payload[7] = snapshot.CastTargetCode;
        WriteAsciiLabel(payload, 8, 4, snapshot.Label);
        return BuildFrameBytes(profileId, sequence, FrameType.AuxUnitCast, TransportConstants.AuxUnitCastSchemaId, payload);
    }

    public static byte[] BuildAuraPageFrameBytes(byte profileId, byte sequence, AuraPageSnapshot snapshot)
    {
        Span<byte> payload = stackalloc byte[TransportConstants.PayloadBytes];
        payload[0] = snapshot.PageKindCode;
        payload[1] = snapshot.TotalAuraCount;
        WriteAuraEntry(payload, 2, snapshot.Entry1);
        WriteAuraEntry(payload, 7, snapshot.Entry2);
        return BuildFrameBytes(profileId, sequence, FrameType.AuraPage, TransportConstants.AuraPageSchemaId, payload);
    }

    public static byte[] BuildTextPageFrameBytes(byte profileId, byte sequence, TextPageSnapshot snapshot)
    {
        Span<byte> payload = stackalloc byte[TransportConstants.PayloadBytes];
        payload[0] = snapshot.TextKindCode;
        WriteUInt16BigEndian(payload, 1, snapshot.TextHash16);
        WriteAsciiLabel(payload, 3, 9, snapshot.Label);
        return BuildFrameBytes(profileId, sequence, FrameType.TextPage, TransportConstants.TextPageSchemaId, payload);
    }

    public static byte[] BuildAbilityWatchFrameBytes(byte profileId, byte sequence, AbilityWatchSnapshot snapshot)
    {
        Span<byte> payload = stackalloc byte[TransportConstants.PayloadBytes];
        payload[0] = snapshot.PageIndex;
        WriteAbilityEntry(payload, 1, snapshot.Entry1);
        WriteAbilityEntry(payload, 5, snapshot.Entry2);
        payload[9] = snapshot.ShortestCooldownQ4;
        payload[10] = snapshot.ReadyCount;
        payload[11] = snapshot.CoolingCount;
        return BuildFrameBytes(profileId, sequence, FrameType.AbilityWatch, TransportConstants.AbilityWatchSchemaId, payload);
    }

    public static byte[] EncodeBytesToPayloadSymbols(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != TransportConstants.TransportBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes));
        }

        var symbols = new byte[TransportConstants.PayloadSymbols];
        for (var symbolIndex = 0; symbolIndex < TransportConstants.PayloadSymbols; symbolIndex++)
        {
            byte symbol = 0;
            for (var bit = 0; bit < 3; bit++)
            {
                var streamBit = (symbolIndex * 3) + bit;
                var byteIndex = streamBit / 8;
                var bitIndex = 7 - (streamBit % 8);
                var bitValue = (bytes[byteIndex] >> bitIndex) & 0x01;
                symbol = (byte)((symbol << 1) | bitValue);
            }

            symbols[symbolIndex] = symbol;
        }

        return symbols;
    }

    public static byte[] DecodePayloadSymbolsToBytes(ReadOnlySpan<byte> symbols)
    {
        if (symbols.Length != TransportConstants.PayloadSymbols)
        {
            throw new ArgumentOutOfRangeException(nameof(symbols));
        }

        var bytes = new byte[TransportConstants.TransportBytes];
        for (var symbolIndex = 0; symbolIndex < symbols.Length; symbolIndex++)
        {
            var symbol = symbols[symbolIndex];
            if (symbol > 7)
            {
                throw new InvalidDataException($"Symbol {symbolIndex} was out of range: {symbol}.");
            }

            for (var bit = 0; bit < 3; bit++)
            {
                var streamBit = (symbolIndex * 3) + bit;
                var byteIndex = streamBit / 8;
                var bitIndex = 7 - (streamBit % 8);
                var bitValue = (symbol >> (2 - bit)) & 0x01;
                bytes[byteIndex] |= (byte)(bitValue << bitIndex);
            }
        }

        return bytes;
    }

    public static bool TryParseCoreFrameBytes(ReadOnlySpan<byte> bytes, out CoreStatusFrame? frame, out string reason)
    {
        var result = AnalyzeFrameBytes(bytes);
        frame = result.Frame as CoreStatusFrame;
        reason = result.Reason;
        return result.IsAccepted && frame is not null;
    }

    public static TransportParseResult AnalyzeCoreFrameBytes(ReadOnlySpan<byte> bytes)
    {
        var result = AnalyzeFrameBytes(bytes);
        if (result.IsAccepted && result.Frame is CoreStatusFrame)
        {
            return result;
        }

        if (!result.IsAccepted)
        {
            return result;
        }

        return result with
        {
            IsAccepted = false,
            Reason = "Decoded frame was not a core-status frame.",
            FrameSchemaValid = false,
            Frame = null
        };
    }

    public static TransportParseResult AnalyzeFrameBytes(ReadOnlySpan<byte> bytes)
    {
        TelemetryFrame? frame = null;
        var transportBytes = bytes.ToArray();

        if (bytes.Length != TransportConstants.TransportBytes)
        {
            return new TransportParseResult(false, $"Expected {TransportConstants.TransportBytes} transport bytes, got {bytes.Length}.", false, false, false, false, false, transportBytes, null);
        }

        var magicValid = bytes[0] == TransportConstants.MagicC && bytes[1] == TransportConstants.MagicL;
        if (!magicValid)
        {
            return new TransportParseResult(false, "Invalid magic/version.", false, false, false, false, false, transportBytes, null);
        }

        var protocolVersion = (byte)(bytes[2] >> 4);
        var profileId = (byte)(bytes[2] & 0x0F);
        var rawFrameType = (byte)(bytes[3] >> 4);
        var schemaId = (byte)(bytes[3] & 0x0F);
        var protocolProfileValid = protocolVersion == TransportConstants.ProtocolVersion && profileId == StripProfiles.Default.NumericId;
        if (!protocolProfileValid)
        {
            return new TransportParseResult(false, "Invalid magic/version.", true, false, false, false, false, transportBytes, null);
        }

        var frameType = Enum.IsDefined(typeof(FrameType), rawFrameType) ? (FrameType)rawFrameType : 0;
        var frameSchemaValid = IsSupportedFrameSchema(frameType, schemaId);
        if (!frameSchemaValid)
        {
            return new TransportParseResult(false, "Invalid frame type/schema.", true, true, false, false, false, transportBytes, null);
        }

        var expectedHeaderCrc = ComputeCrc16(bytes[..6]);
        var actualHeaderCrc = (ushort)((bytes[6] << 8) | bytes[7]);
        var headerCrcValid = expectedHeaderCrc == actualHeaderCrc;
        if (!headerCrcValid)
        {
            return new TransportParseResult(false, "Header CRC failure.", true, true, true, false, false, transportBytes, null);
        }

        var payload = bytes.Slice(8, TransportConstants.PayloadBytes);
        var expectedPayloadCrc = ComputeCrc32C(payload);
        var actualPayloadCrc = (uint)((bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23]);
        var payloadCrcValid = expectedPayloadCrc == actualPayloadCrc;
        if (!payloadCrcValid)
        {
            return new TransportParseResult(false, "Payload CRC failure.", true, true, true, true, false, transportBytes, null);
        }

        var header = new TelemetryFrameHeader(protocolVersion, profileId, frameType, schemaId, bytes[4], bytes[5], actualHeaderCrc);

        frame = frameType switch
        {
            FrameType.CoreStatus => new CoreStatusFrame(
                header,
                new CoreStatusSnapshot(
                    payload[0],
                    payload[1],
                    payload[2],
                    payload[3],
                    payload[4],
                    payload[5],
                    payload[6],
                    payload[7],
                    payload[8],
                    payload[9],
                    payload[10],
                    payload[11]),
                actualPayloadCrc,
                bytes.ToArray()),
            FrameType.PlayerVitals => new PlayerVitalsFrame(
                header,
                new PlayerVitalsSnapshot(
                    ReadUInt32BigEndian(payload, 0),
                    ReadUInt32BigEndian(payload, 4),
                    ReadUInt16BigEndian(payload, 8),
                    ReadUInt16BigEndian(payload, 10)),
                actualPayloadCrc,
                bytes.ToArray()),
            FrameType.PlayerPosition => new PlayerPositionFrame(
                header,
                new PlayerPositionSnapshot(
                    FixedToFloat(ReadInt32BigEndian(payload, 0)),
                    FixedToFloat(ReadInt32BigEndian(payload, 4)),
                    FixedToFloat(ReadInt32BigEndian(payload, 8))),
                actualPayloadCrc,
                bytes.ToArray()),
            FrameType.PlayerCast => new PlayerCastFrame(
                header,
                new PlayerCastSnapshot(
                    payload[0],
                    payload[1],
                    ReadUInt16BigEndian(payload, 2),
                    ReadUInt16BigEndian(payload, 4),
                    payload[6],
                    ReadAsciiLabel(payload, 7, 5)),
                actualPayloadCrc,
                bytes.ToArray()),
            FrameType.PlayerResources => new PlayerResourcesFrame(
                header,
                new PlayerResourcesSnapshot(
                    ReadUInt16BigEndian(payload, 0),
                    ReadUInt16BigEndian(payload, 2),
                    ReadUInt16BigEndian(payload, 4),
                    ReadUInt16BigEndian(payload, 6),
                    ReadUInt16BigEndian(payload, 8),
                    ReadUInt16BigEndian(payload, 10)),
                actualPayloadCrc,
                bytes.ToArray()),
            FrameType.PlayerCombat => new PlayerCombatFrame(
                header,
                new PlayerCombatSnapshot(
                    payload[0],
                    payload[1],
                    ReadUInt16BigEndian(payload, 2),
                    ReadUInt16BigEndian(payload, 4),
                    ReadUInt16BigEndian(payload, 6),
                    ReadUInt16BigEndian(payload, 8),
                    ReadUInt16BigEndian(payload, 10)),
                actualPayloadCrc,
                bytes.ToArray()),
            FrameType.TargetPosition => new TargetPositionFrame(
                header,
                new TargetPositionSnapshot(
                    FixedToFloat(ReadInt32BigEndian(payload, 0)),
                    FixedToFloat(ReadInt32BigEndian(payload, 4)),
                    FixedToFloat(ReadInt32BigEndian(payload, 8))),
                actualPayloadCrc,
                bytes.ToArray()),
            FrameType.FollowUnitStatus => new FollowUnitStatusFrame(
                header,
                new FollowUnitStatusSnapshot(
                    payload[0],
                    payload[1],
                    FixedQ2ToFloat(ReadInt16BigEndian(payload, 2)),
                    FixedQ2ToFloat(ReadInt16BigEndian(payload, 4)),
                    FixedQ2ToFloat(ReadInt16BigEndian(payload, 6)),
                    payload[8],
                    payload[9],
                    payload[10],
                    payload[11]),
                actualPayloadCrc,
                bytes.ToArray()),
            FrameType.TargetVitals => new TargetVitalsFrame(
                header,
                new TargetVitalsSnapshot(
                    ReadUInt32BigEndian(payload, 0),
                    ReadUInt32BigEndian(payload, 4),
                    ReadUInt16BigEndian(payload, 8),
                    payload[10],
                    payload[11]),
                actualPayloadCrc,
                bytes.ToArray()),
            FrameType.TargetResources => new TargetResourcesFrame(
                header,
                new TargetResourcesSnapshot(
                    ReadUInt16BigEndian(payload, 0),
                    ReadUInt16BigEndian(payload, 2),
                    ReadUInt16BigEndian(payload, 4),
                    ReadUInt16BigEndian(payload, 6),
                    ReadUInt16BigEndian(payload, 8),
                    ReadUInt16BigEndian(payload, 10)),
                actualPayloadCrc,
                bytes.ToArray()),
            FrameType.AuxUnitCast => new AuxUnitCastFrame(
                header,
                new AuxUnitCastSnapshot(
                    payload[0],
                    payload[1],
                    payload[2],
                    ReadUInt16BigEndian(payload, 3),
                    ReadUInt16BigEndian(payload, 5),
                    payload[7],
                    ReadAsciiLabel(payload, 8, 4)),
                actualPayloadCrc,
                bytes.ToArray()),
            FrameType.AuraPage => new AuraPageFrame(
                header,
                new AuraPageSnapshot(
                    payload[0],
                    payload[1],
                    ReadAuraEntry(payload, 2),
                    ReadAuraEntry(payload, 7)),
                actualPayloadCrc,
                bytes.ToArray()),
            FrameType.TextPage => new TextPageFrame(
                header,
                new TextPageSnapshot(
                    payload[0],
                    ReadUInt16BigEndian(payload, 1),
                    ReadAsciiLabel(payload, 3, 9)),
                actualPayloadCrc,
                bytes.ToArray()),
            FrameType.AbilityWatch => new AbilityWatchFrame(
                header,
                new AbilityWatchSnapshot(
                    payload[0],
                    ReadAbilityEntry(payload, 1),
                    ReadAbilityEntry(payload, 5),
                    payload[9],
                    payload[10],
                    payload[11]),
                actualPayloadCrc,
                bytes.ToArray()),
            FrameType.RiftMeterCombat => new RiftMeterCombatFrame(
                header,
                new RiftMeterCombatSnapshot(
                    payload[0],
                    payload[1],
                    ReadUInt16BigEndian(payload, 2),
                    payload[4],
                    payload[5],
                    ReadUInt16BigEndian(payload, 6),
                    payload[8],
                    payload[9],
                    payload[10],
                    payload[11]),
                actualPayloadCrc,
                bytes.ToArray()),
            _ => null
        };

        if (frame is null)
        {
            return new TransportParseResult(false, "Invalid frame type/schema.", true, true, false, true, true, transportBytes, null);
        }

        return new TransportParseResult(true, "Accepted", true, true, true, true, true, transportBytes, frame);
    }

    public static ushort ComputeCrc16(ReadOnlySpan<byte> bytes)
    {
        ushort crc = 0xFFFF;
        foreach (var value in bytes)
        {
            crc ^= (ushort)(value << 8);
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
            }
        }

        return crc;
    }

    public static uint ComputeCrc32C(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xFFFF_FFFF;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0x82F63B78u : crc >> 1;
            }
        }

        return ~crc;
    }

    private static byte[] BuildFrameBytes(byte profileId, byte sequence, FrameType frameType, byte schemaId, ReadOnlySpan<byte> payload)
    {
        if (payload.Length != TransportConstants.PayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(payload));
        }

        var bytes = new byte[TransportConstants.TransportBytes];
        bytes[0] = TransportConstants.MagicC;
        bytes[1] = TransportConstants.MagicL;
        bytes[2] = (byte)((TransportConstants.ProtocolVersion << 4) | (profileId & 0x0F));
        bytes[3] = (byte)(((byte)frameType << 4) | (schemaId & 0x0F));
        bytes[4] = sequence;
        bytes[5] = (byte)TransportConstants.HeaderCapabilities;

        var headerCrc = ComputeCrc16(bytes.AsSpan(0, 6));
        bytes[6] = (byte)(headerCrc >> 8);
        bytes[7] = (byte)(headerCrc & 0xFF);

        payload.CopyTo(bytes.AsSpan(8, TransportConstants.PayloadBytes));

        var payloadCrc = ComputeCrc32C(bytes.AsSpan(8, TransportConstants.PayloadBytes));
        bytes[20] = (byte)(payloadCrc >> 24);
        bytes[21] = (byte)(payloadCrc >> 16);
        bytes[22] = (byte)(payloadCrc >> 8);
        bytes[23] = (byte)(payloadCrc & 0xFF);
        return bytes;
    }

    private static bool IsSupportedFrameSchema(FrameType frameType, byte schemaId)
    {
        return frameType switch
        {
            FrameType.CoreStatus => schemaId == TransportConstants.CoreSchemaId,
            FrameType.PlayerVitals => schemaId == TransportConstants.PlayerVitalsSchemaId,
            FrameType.PlayerPosition => schemaId == TransportConstants.PlayerPositionSchemaId,
            FrameType.PlayerCast => schemaId == TransportConstants.PlayerCastSchemaId,
            FrameType.PlayerResources => schemaId == TransportConstants.PlayerResourcesSchemaId,
            FrameType.PlayerCombat => schemaId == TransportConstants.PlayerCombatSchemaId,
            FrameType.TargetPosition => schemaId == TransportConstants.TargetPositionSchemaId,
            FrameType.FollowUnitStatus => schemaId == TransportConstants.FollowUnitStatusSchemaId,
            FrameType.TargetVitals => schemaId == TransportConstants.TargetVitalsSchemaId,
            FrameType.TargetResources => schemaId == TransportConstants.TargetResourcesSchemaId,
            FrameType.AuxUnitCast => schemaId == TransportConstants.AuxUnitCastSchemaId,
            FrameType.AuraPage => schemaId == TransportConstants.AuraPageSchemaId,
            FrameType.TextPage => schemaId == TransportConstants.TextPageSchemaId,
            FrameType.AbilityWatch => schemaId == TransportConstants.AbilityWatchSchemaId,
            FrameType.RiftMeterCombat => schemaId == TransportConstants.RiftMeterCombatSchemaId,
            _ => false
        };
    }

    private static void WriteAsciiLabel(Span<byte> payload, int offset, int length, string? text)
    {
        var source = string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.ToUpperInvariant();

        for (var index = 0; index < length; index++)
        {
            var value = index < source.Length ? source[index] : ' ';
            payload[offset + index] = value is >= ' ' and <= '~'
                ? (byte)value
                : (byte)'?';
        }
    }

    private static string ReadAsciiLabel(ReadOnlySpan<byte> payload, int offset, int length)
    {
        Span<char> chars = stackalloc char[length];
        for (var index = 0; index < length; index++)
        {
            var value = payload[offset + index];
            chars[index] = value is >= 32 and <= 126
                ? (char)value
                : '?';
        }

        return new string(chars).TrimEnd();
    }

    private static void WriteAuraEntry(Span<byte> payload, int offset, AuraPageEntrySnapshot entry)
    {
        WriteUInt16BigEndian(payload, offset, entry.Id);
        payload[offset + 2] = entry.RemainingQ4;
        payload[offset + 3] = entry.Stack;
        payload[offset + 4] = entry.Flags;
    }

    private static AuraPageEntrySnapshot ReadAuraEntry(ReadOnlySpan<byte> payload, int offset)
    {
        return new AuraPageEntrySnapshot(
            ReadUInt16BigEndian(payload, offset),
            payload[offset + 2],
            payload[offset + 3],
            payload[offset + 4]);
    }

    private static void WriteAbilityEntry(Span<byte> payload, int offset, AbilityWatchEntrySnapshot entry)
    {
        WriteUInt16BigEndian(payload, offset, entry.Id);
        payload[offset + 2] = entry.CooldownQ4;
        payload[offset + 3] = entry.Flags;
    }

    private static AbilityWatchEntrySnapshot ReadAbilityEntry(ReadOnlySpan<byte> payload, int offset)
    {
        return new AbilityWatchEntrySnapshot(
            ReadUInt16BigEndian(payload, offset),
            payload[offset + 2],
            payload[offset + 3]);
    }

    private static void WriteUInt16BigEndian(Span<byte> payload, int offset, ushort value)
    {
        payload[offset] = (byte)(value >> 8);
        payload[offset + 1] = (byte)(value & 0xFF);
    }

    private static void WriteInt16BigEndian(Span<byte> payload, int offset, short value)
    {
        var u = unchecked((ushort)value);
        payload[offset] = (byte)(u >> 8);
        payload[offset + 1] = (byte)(u & 0xFF);
    }

    private static void WriteUInt32BigEndian(Span<byte> payload, int offset, uint value)
    {
        payload[offset] = (byte)(value >> 24);
        payload[offset + 1] = (byte)((value >> 16) & 0xFF);
        payload[offset + 2] = (byte)((value >> 8) & 0xFF);
        payload[offset + 3] = (byte)(value & 0xFF);
    }

    private static void WriteInt32BigEndian(Span<byte> payload, int offset, int value)
    {
        var u = unchecked((uint)value);
        payload[offset] = (byte)(u >> 24);
        payload[offset + 1] = (byte)((u >> 16) & 0xFF);
        payload[offset + 2] = (byte)((u >> 8) & 0xFF);
        payload[offset + 3] = (byte)(u & 0xFF);
    }

    private static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> payload, int offset)
    {
        return (ushort)((payload[offset] << 8) | payload[offset + 1]);
    }

    private static short ReadInt16BigEndian(ReadOnlySpan<byte> payload, int offset)
    {
        var u = (ushort)((payload[offset] << 8) | payload[offset + 1]);
        return unchecked((short)u);
    }

    private static uint ReadUInt32BigEndian(ReadOnlySpan<byte> payload, int offset)
    {
        return ((uint)payload[offset] << 24)
             | ((uint)payload[offset + 1] << 16)
             | ((uint)payload[offset + 2] << 8)
             | payload[offset + 3];
    }

    private static int ReadInt32BigEndian(ReadOnlySpan<byte> payload, int offset)
    {
        var u = ((uint)payload[offset] << 24)
              | ((uint)payload[offset + 1] << 16)
              | ((uint)payload[offset + 2] << 8)
              | payload[offset + 3];
        return unchecked((int)u);
    }

    private static int FloatToFixed(float value) =>
        (int)Math.Round(value * 100.0f, MidpointRounding.AwayFromZero);

    private static float FixedToFloat(int value) => value / 100.0f;

    private static short FloatToFixedQ2(float value)
    {
        var scaled = (int)Math.Round(value * 2.0f, MidpointRounding.AwayFromZero);
        scaled = Math.Clamp(scaled, short.MinValue, short.MaxValue);
        return (short)scaled;
    }

    private static float FixedQ2ToFloat(short value) => value / 2.0f;
}
