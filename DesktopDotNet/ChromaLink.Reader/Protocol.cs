namespace ChromaLink.Reader;

public enum FrameType : byte
{
    CoreStatus = 1,
    PlayerVitals = 2,
    PlayerPosition = 3
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
    PlayerPosition = 1 << 1
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
    public static PlayerVitalsSnapshot CreateSynthetic()
    {
        return new PlayerVitalsSnapshot(3260, 3260, 100, 100);
    }
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

public readonly record struct PlayerPositionSnapshot(float X, float Y, float Z)
{
    public static PlayerPositionSnapshot CreateSynthetic()
    {
        return new PlayerPositionSnapshot(123.45f, 200.67f, -50.12f);
    }
}

public sealed record PlayerPositionFrame(
    TelemetryFrameHeader Header,
    PlayerPositionSnapshot Payload,
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
    public const int TransportBytes = 24;
    public const int HeaderBytes = 8;
    public const int PayloadBytes = 12;
    public const int PayloadCrcBytes = 4;
    public const int PayloadSymbols = 64;
    public const HeaderCapabilityFlags HeaderCapabilities =
        HeaderCapabilityFlags.MultiFrameRotation |
        HeaderCapabilityFlags.PlayerPosition;
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
            return new TransportParseResult(
                false,
                $"Expected {TransportConstants.TransportBytes} transport bytes, got {bytes.Length}.",
                false,
                false,
                false,
                false,
                false,
                transportBytes,
                null);
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

        var header = new TelemetryFrameHeader(
            protocolVersion,
            profileId,
            frameType,
            schemaId,
            bytes[4],
            bytes[5],
            actualHeaderCrc);

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
            _ => false
        };
    }

    private static void WriteUInt16BigEndian(Span<byte> payload, int offset, ushort value)
    {
        payload[offset] = (byte)(value >> 8);
        payload[offset + 1] = (byte)(value & 0xFF);
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
}
