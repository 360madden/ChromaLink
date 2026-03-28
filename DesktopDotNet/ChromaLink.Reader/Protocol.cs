namespace ChromaLink.Reader;

public enum FrameType : byte
{
    CoreStatus = 1
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

public sealed record TelemetryFrameHeader(
    byte ProtocolVersion,
    byte ProfileId,
    FrameType FrameType,
    byte SchemaId,
    byte Sequence,
    byte ReservedFlags,
    ushort HeaderCrc16);

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

public sealed record CoreStatusFrame(
    TelemetryFrameHeader Header,
    CoreStatusSnapshot Payload,
    uint PayloadCrc32C,
    byte[] TransportBytes);

public static class TransportConstants
{
    public const byte MagicC = (byte)'C';
    public const byte MagicL = (byte)'L';
    public const byte ProtocolVersion = 1;
    public const byte CoreFrameType = 1;
    public const byte CoreSchemaId = 1;
    public const int TransportBytes = 24;
    public const int HeaderBytes = 8;
    public const int PayloadBytes = 12;
    public const int PayloadCrcBytes = 4;
    public const int PayloadSymbols = 64;
}

public static class FrameProtocol
{
    public static byte[] BuildCoreFrameBytes(byte profileId, byte sequence, CoreStatusSnapshot snapshot)
    {
        var bytes = new byte[TransportConstants.TransportBytes];
        bytes[0] = TransportConstants.MagicC;
        bytes[1] = TransportConstants.MagicL;
        bytes[2] = (byte)((TransportConstants.ProtocolVersion << 4) | (profileId & 0x0F));
        bytes[3] = (byte)((TransportConstants.CoreFrameType << 4) | TransportConstants.CoreSchemaId);
        bytes[4] = sequence;
        bytes[5] = 0;

        var headerCrc = ComputeCrc16(bytes.AsSpan(0, 6));
        bytes[6] = (byte)(headerCrc >> 8);
        bytes[7] = (byte)(headerCrc & 0xFF);

        bytes[8] = snapshot.PlayerStateFlags;
        bytes[9] = snapshot.PlayerHealthPctQ8;
        bytes[10] = snapshot.PlayerResourceKind;
        bytes[11] = snapshot.PlayerResourcePctQ8;
        bytes[12] = snapshot.TargetStateFlags;
        bytes[13] = snapshot.TargetHealthPctQ8;
        bytes[14] = snapshot.TargetResourceKind;
        bytes[15] = snapshot.TargetResourcePctQ8;
        bytes[16] = snapshot.PlayerLevel;
        bytes[17] = snapshot.TargetLevel;
        bytes[18] = snapshot.PlayerCallingRolePacked;
        bytes[19] = snapshot.TargetCallingRelationPacked;

        var payloadCrc = ComputeCrc32C(bytes.AsSpan(8, TransportConstants.PayloadBytes));
        bytes[20] = (byte)(payloadCrc >> 24);
        bytes[21] = (byte)(payloadCrc >> 16);
        bytes[22] = (byte)(payloadCrc >> 8);
        bytes[23] = (byte)(payloadCrc & 0xFF);
        return bytes;
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
        frame = null;

        if (bytes.Length != TransportConstants.TransportBytes)
        {
            reason = $"Expected {TransportConstants.TransportBytes} transport bytes, got {bytes.Length}.";
            return false;
        }

        if (bytes[0] != TransportConstants.MagicC || bytes[1] != TransportConstants.MagicL)
        {
            reason = "Invalid magic/version.";
            return false;
        }

        var protocolVersion = (byte)(bytes[2] >> 4);
        var profileId = (byte)(bytes[2] & 0x0F);
        var frameType = (byte)(bytes[3] >> 4);
        var schemaId = (byte)(bytes[3] & 0x0F);
        if (protocolVersion != TransportConstants.ProtocolVersion || profileId != StripProfiles.Default.NumericId)
        {
            reason = "Invalid magic/version.";
            return false;
        }

        if (frameType != TransportConstants.CoreFrameType || schemaId != TransportConstants.CoreSchemaId)
        {
            reason = "Invalid magic/version.";
            return false;
        }

        var expectedHeaderCrc = ComputeCrc16(bytes[..6]);
        var actualHeaderCrc = (ushort)((bytes[6] << 8) | bytes[7]);
        if (expectedHeaderCrc != actualHeaderCrc)
        {
            reason = "Header CRC failure.";
            return false;
        }

        var payload = bytes.Slice(8, TransportConstants.PayloadBytes);
        var expectedPayloadCrc = ComputeCrc32C(payload);
        var actualPayloadCrc = (uint)((bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23]);
        if (expectedPayloadCrc != actualPayloadCrc)
        {
            reason = "Payload CRC failure.";
            return false;
        }

        var parsedPayload = new CoreStatusSnapshot(
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
            payload[11]);

        frame = new CoreStatusFrame(
            new TelemetryFrameHeader(
                protocolVersion,
                profileId,
                FrameType.CoreStatus,
                schemaId,
                bytes[4],
                bytes[5],
                actualHeaderCrc),
            parsedPayload,
            actualPayloadCrc,
            bytes.ToArray());
        reason = "Accepted";
        return true;
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
}
