using System.Buffers.Binary;

namespace ChromaLink.Reader;

public enum FrameType : byte
{
    CoreStatus = 1,
    Tactical = 2,
    Calibration = 3,
    Event = 4
}

public enum LaneId : byte
{
    Hot = 1,
    Warm = 2,
    Cold = 3,
    Event = 4
}

public static class TransportConstants
{
    public const byte Magic0 = (byte)'C';

    public const byte Magic1 = (byte)'L';

    public const byte ProtocolVersion = 2;

    public const int HeaderBytes = 8;

    public const int DuplicateHeaderBytes = 3;

    public const int PayloadCrcBytes = 4;

    public const int TransportBytes = 45;

    public const int MaxPayloadBytes = TransportBytes - HeaderBytes - PayloadCrcBytes;
}

public static class Crc16CcittFalse
{
    public static ushort Compute(ReadOnlySpan<byte> bytes)
    {
        ushort crc = 0xFFFF;
        foreach (var value in bytes)
        {
            crc ^= (ushort)(value << 8);
            for (var index = 0; index < 8; index++)
            {
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ 0x1021)
                    : (ushort)(crc << 1);
            }
        }

        return crc;
    }
}

public static class Crc32C
{
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var value in bytes)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        const uint polynomial = 0x82F63B78;
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? polynomial ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}

public static class BitPacking
{
    public static byte[] BytesToBits(ReadOnlySpan<byte> bytes)
    {
        var bits = new byte[bytes.Length * 8];
        var cursor = 0;
        foreach (var value in bytes)
        {
            for (var shift = 7; shift >= 0; shift--)
            {
                bits[cursor++] = (byte)((value >> shift) & 0x1);
            }
        }

        return bits;
    }

    public static byte[] BitsToBytes(ReadOnlySpan<byte> bits)
    {
        var bytes = new byte[(bits.Length + 7) / 8];
        var bitCount = 0;
        var byteIndex = 0;
        byte current = 0;
        foreach (var bit in bits)
        {
            current = (byte)((current << 1) | (bit & 0x1));
            bitCount++;
            if (bitCount == 8)
            {
                bytes[byteIndex++] = current;
                current = 0;
                bitCount = 0;
            }
        }

        if (bitCount > 0)
        {
            bytes[byteIndex] = (byte)(current << (8 - bitCount));
        }

        return bytes;
    }
}

public sealed record TransportHeader(
    byte ProfileId,
    FrameType FrameType,
    LaneId LaneId,
    byte Sequence,
    byte PayloadLength)
{
    public byte VersionProfileByte => (byte)((TransportConstants.ProtocolVersion << 4) | (ProfileId & 0x0F));

    public byte FrameLaneByte => (byte)(((byte)FrameType << 4) | ((byte)LaneId & 0x0F));
}

public sealed record TransportFrame(
    TransportHeader Header,
    byte[] Payload,
    ushort HeaderCrc,
    uint PayloadCrc,
    byte[] RawBytes);

public static class TransportCodec
{
    public static byte[] BuildTransportBytes(TransportHeader header, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > TransportConstants.MaxPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), $"Payload exceeded {TransportConstants.MaxPayloadBytes} bytes.");
        }

        var bytes = new byte[TransportConstants.TransportBytes];
        bytes[0] = TransportConstants.Magic0;
        bytes[1] = TransportConstants.Magic1;
        bytes[2] = header.VersionProfileByte;
        bytes[3] = header.FrameLaneByte;
        bytes[4] = header.Sequence;
        bytes[5] = (byte)payload.Length;
        var headerCrc = Crc16CcittFalse.Compute(bytes.AsSpan(0, 6));
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6, 2), headerCrc);
        payload.CopyTo(bytes.AsSpan(TransportConstants.HeaderBytes, payload.Length));
        var payloadCrc = Crc32C.Compute(payload);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(TransportConstants.HeaderBytes + payload.Length, 4), payloadCrc);
        FillWitness(bytes.AsSpan(TransportConstants.HeaderBytes + payload.Length + 4));
        return bytes;
    }

    public static bool TryParse(ReadOnlySpan<byte> bytes, out TransportFrame? frame, out string reason)
    {
        frame = null;
        reason = string.Empty;
        if (bytes.Length != TransportConstants.TransportBytes)
        {
            reason = $"Unexpected transport byte count: {bytes.Length}.";
            return false;
        }

        if (bytes[0] != TransportConstants.Magic0 || bytes[1] != TransportConstants.Magic1)
        {
            reason = "Transport magic mismatch.";
            return false;
        }

        var version = (byte)(bytes[2] >> 4);
        if (version != TransportConstants.ProtocolVersion)
        {
            reason = $"Unsupported protocol version: {version}.";
            return false;
        }

        var payloadLength = bytes[5];
        if (payloadLength > TransportConstants.MaxPayloadBytes)
        {
            reason = $"Payload length {payloadLength} exceeded {TransportConstants.MaxPayloadBytes}.";
            return false;
        }

        var expectedHeaderCrc = Crc16CcittFalse.Compute(bytes[..6]);
        var actualHeaderCrc = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(6, 2));
        if (expectedHeaderCrc != actualHeaderCrc)
        {
            reason = "Header CRC mismatch.";
            return false;
        }

        var payload = bytes.Slice(TransportConstants.HeaderBytes, payloadLength).ToArray();
        var expectedPayloadCrc = Crc32C.Compute(payload);
        var actualPayloadCrc = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(TransportConstants.HeaderBytes + payloadLength, 4));
        if (expectedPayloadCrc != actualPayloadCrc)
        {
            reason = "Payload CRC32C mismatch.";
            return false;
        }

        frame = new TransportFrame(
            new TransportHeader(
                (byte)(bytes[2] & 0x0F),
                (FrameType)(bytes[3] >> 4),
                (LaneId)(bytes[3] & 0x0F),
                bytes[4],
                payloadLength),
            payload,
            actualHeaderCrc,
            actualPayloadCrc,
            bytes.ToArray());
        return true;
    }

    public static byte[] DuplicateHeaderBytes(ReadOnlySpan<byte> transportBytes)
    {
        return transportBytes[..TransportConstants.DuplicateHeaderBytes].ToArray();
    }

    private static void FillWitness(Span<byte> padding)
    {
        var value = (byte)0xA5;
        for (var index = 0; index < padding.Length; index++)
        {
            padding[index] = value;
            value = value == 0xA5 ? (byte)0x5A : (byte)0xA5;
        }
    }
}

public sealed record TelemetrySnapshot
{
    public ushort StateFlags { get; init; }
    public ushort TacticalMask { get; init; }
    public byte PlayerResourceKindId { get; init; }
    public int PlayerHealthCurrent { get; init; }
    public int PlayerHealthMax { get; init; }
    public int PlayerResourceCurrent { get; init; }
    public int PlayerResourceMax { get; init; }
    public byte PlayerLevel { get; init; }
    public byte PlayerCallingCode { get; init; }
    public byte PlayerRoleCode { get; init; }
    public byte PlayerCastFlags { get; init; }
    public ushort PlayerCastProgressQ15 { get; init; }
    public ushort PlayerPowerAttack { get; init; }
    public ushort PlayerPowerSpell { get; init; }
    public ushort PlayerZoneHash16 { get; init; }
    public ushort TargetZoneHash16 { get; init; }
    public int PlayerCoordX10 { get; init; }
    public int PlayerCoordZ10 { get; init; }
    public int TargetCoordX10 { get; init; }
    public int TargetCoordZ10 { get; init; }
    public byte TargetResourceKindId { get; init; }
    public int TargetHealthCurrent { get; init; }
    public int TargetHealthMax { get; init; }
    public int TargetResourceCurrent { get; init; }
    public int TargetResourceMax { get; init; }
    public byte TargetLevel { get; init; }
    public byte TargetFlags { get; init; }
    public byte TargetRelationCode { get; init; }
    public byte TargetTierCode { get; init; }
    public byte TargetTaggedCode { get; init; }
    public byte TargetCallingCode { get; init; }
    public ushort TargetRadiusQ10 { get; init; }

    public static TelemetrySnapshot CreateSynthetic()
    {
        return new TelemetrySnapshot
        {
            StateFlags = 0x01F7,
            TacticalMask = 0x043F,
            PlayerResourceKindId = 1,
            PlayerHealthCurrent = 11770,
            PlayerHealthMax = 11770,
            PlayerResourceCurrent = 4990,
            PlayerResourceMax = 4990,
            PlayerLevel = 47,
            PlayerCallingCode = 1,
            PlayerRoleCode = 1,
            PlayerCastFlags = 0x03,
            PlayerCastProgressQ15 = 16384,
            PlayerPowerAttack = 1825,
            PlayerPowerSpell = 2630,
            PlayerZoneHash16 = 0x266F,
            TargetZoneHash16 = 0x266F,
            PlayerCoordX10 = 73651,
            PlayerCoordZ10 = 30346,
            TargetCoordX10 = 73808,
            TargetCoordZ10 = 30516,
            TargetResourceKindId = 1,
            TargetHealthCurrent = 6320,
            TargetHealthMax = 9110,
            TargetResourceCurrent = 2120,
            TargetResourceMax = 3500,
            TargetLevel = 46,
            TargetFlags = 0x01,
            TargetRelationCode = 2,
            TargetTierCode = 1,
            TargetTaggedCode = 1,
            TargetCallingCode = 4,
            TargetRadiusQ10 = 18
        };
    }
}

public sealed record CoreFramePayload(
    ushort StateFlags,
    byte PlayerResourceKindId,
    int PlayerHealthCurrent,
    int PlayerHealthMax,
    int PlayerResourceCurrent,
    int PlayerResourceMax,
    byte PlayerLevel,
    byte PlayerCallingCode,
    byte PlayerRoleCode,
    byte TargetResourceKindId,
    int TargetHealthCurrent,
    int TargetHealthMax,
    int TargetResourceCurrent,
    int TargetResourceMax,
    byte TargetLevel,
    byte TargetFlags);

public sealed record TacticalFramePayload(
    ushort TacticalMask,
    ushort StateFlags,
    byte PlayerCastFlags,
    ushort PlayerCastProgressQ15,
    ushort PlayerZoneHash16,
    ushort TargetZoneHash16,
    int PlayerCoordX10,
    int PlayerCoordZ10,
    int TargetCoordX10,
    int TargetCoordZ10,
    byte TargetRelationCode,
    byte TargetTierCode,
    byte TargetTaggedCode,
    byte TargetCallingCode,
    ushort TargetRadiusQ10,
    ushort PlayerPowerAttack,
    ushort PlayerPowerSpell);

public static class FrameSerializer
{
    public static byte[] BuildCoreFrameBytes(byte profileId, byte sequence, TelemetrySnapshot snapshot)
    {
        var payload = new byte[33];
        var index = 0;
        index = WriteUInt16(payload, index, snapshot.StateFlags);
        payload[index++] = snapshot.PlayerResourceKindId;
        index = WriteUInt24(payload, index, snapshot.PlayerHealthCurrent);
        index = WriteUInt24(payload, index, snapshot.PlayerHealthMax);
        index = WriteUInt24(payload, index, snapshot.PlayerResourceCurrent);
        index = WriteUInt24(payload, index, snapshot.PlayerResourceMax);
        payload[index++] = snapshot.PlayerLevel;
        payload[index++] = snapshot.PlayerCallingCode;
        payload[index++] = snapshot.PlayerRoleCode;
        payload[index++] = snapshot.TargetResourceKindId;
        index = WriteUInt24(payload, index, snapshot.TargetHealthCurrent);
        index = WriteUInt24(payload, index, snapshot.TargetHealthMax);
        index = WriteUInt24(payload, index, snapshot.TargetResourceCurrent);
        index = WriteUInt24(payload, index, snapshot.TargetResourceMax);
        payload[index++] = snapshot.TargetLevel;
        payload[index++] = snapshot.TargetFlags;
        return TransportCodec.BuildTransportBytes(new TransportHeader(profileId, FrameType.CoreStatus, LaneId.Hot, sequence, (byte)payload.Length), payload);
    }

    public static byte[] BuildTacticalFrameBytes(byte profileId, byte sequence, TelemetrySnapshot snapshot)
    {
        var payload = new byte[33];
        var index = 0;
        index = WriteUInt16(payload, index, snapshot.TacticalMask);
        index = WriteUInt16(payload, index, snapshot.StateFlags);
        payload[index++] = snapshot.PlayerCastFlags;
        index = WriteUInt16(payload, index, snapshot.PlayerCastProgressQ15);
        index = WriteUInt16(payload, index, snapshot.PlayerZoneHash16);
        index = WriteUInt16(payload, index, snapshot.TargetZoneHash16);
        index = WriteInt24(payload, index, snapshot.PlayerCoordX10);
        index = WriteInt24(payload, index, snapshot.PlayerCoordZ10);
        index = WriteInt24(payload, index, snapshot.TargetCoordX10);
        index = WriteInt24(payload, index, snapshot.TargetCoordZ10);
        payload[index++] = snapshot.TargetRelationCode;
        payload[index++] = snapshot.TargetTierCode;
        payload[index++] = snapshot.TargetTaggedCode;
        payload[index++] = snapshot.TargetCallingCode;
        index = WriteUInt16(payload, index, snapshot.TargetRadiusQ10);
        index = WriteUInt16(payload, index, snapshot.PlayerPowerAttack);
        index = WriteUInt16(payload, index, snapshot.PlayerPowerSpell);
        return TransportCodec.BuildTransportBytes(new TransportHeader(profileId, FrameType.Tactical, LaneId.Hot, sequence, (byte)payload.Length), payload);
    }

    public static CoreFramePayload ParseCorePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 33)
        {
            throw new InvalidDataException($"Expected 33-byte core payload, got {payload.Length}.");
        }

        var index = 0;
        var stateFlags = ReadUInt16(payload, ref index);
        var playerResourceKindId = payload[index++];
        var playerHealthCurrent = ReadUInt24(payload, ref index);
        var playerHealthMax = ReadUInt24(payload, ref index);
        var playerResourceCurrent = ReadUInt24(payload, ref index);
        var playerResourceMax = ReadUInt24(payload, ref index);
        var playerLevel = payload[index++];
        var playerCallingCode = payload[index++];
        var playerRoleCode = payload[index++];
        var targetResourceKindId = payload[index++];
        var targetHealthCurrent = ReadUInt24(payload, ref index);
        var targetHealthMax = ReadUInt24(payload, ref index);
        var targetResourceCurrent = ReadUInt24(payload, ref index);
        var targetResourceMax = ReadUInt24(payload, ref index);
        var targetLevel = payload[index++];
        var targetFlags = payload[index];
        return new CoreFramePayload(stateFlags, playerResourceKindId, playerHealthCurrent, playerHealthMax, playerResourceCurrent, playerResourceMax, playerLevel, playerCallingCode, playerRoleCode, targetResourceKindId, targetHealthCurrent, targetHealthMax, targetResourceCurrent, targetResourceMax, targetLevel, targetFlags);
    }

    public static TacticalFramePayload ParseTacticalPayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 33)
        {
            throw new InvalidDataException($"Expected 33-byte tactical payload, got {payload.Length}.");
        }

        var index = 0;
        var tacticalMask = ReadUInt16(payload, ref index);
        var stateFlags = ReadUInt16(payload, ref index);
        var playerCastFlags = payload[index++];
        var playerCastProgressQ15 = ReadUInt16(payload, ref index);
        var playerZoneHash16 = ReadUInt16(payload, ref index);
        var targetZoneHash16 = ReadUInt16(payload, ref index);
        var playerCoordX10 = ReadInt24(payload, ref index);
        var playerCoordZ10 = ReadInt24(payload, ref index);
        var targetCoordX10 = ReadInt24(payload, ref index);
        var targetCoordZ10 = ReadInt24(payload, ref index);
        var targetRelationCode = payload[index++];
        var targetTierCode = payload[index++];
        var targetTaggedCode = payload[index++];
        var targetCallingCode = payload[index++];
        var targetRadiusQ10 = ReadUInt16(payload, ref index);
        var playerPowerAttack = ReadUInt16(payload, ref index);
        var playerPowerSpell = ReadUInt16(payload, ref index);
        return new TacticalFramePayload(tacticalMask, stateFlags, playerCastFlags, playerCastProgressQ15, playerZoneHash16, targetZoneHash16, playerCoordX10, playerCoordZ10, targetCoordX10, targetCoordZ10, targetRelationCode, targetTierCode, targetTaggedCode, targetCallingCode, targetRadiusQ10, playerPowerAttack, playerPowerSpell);
    }

    private static int WriteUInt16(Span<byte> bytes, int index, int value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(bytes.Slice(index, 2), (ushort)Math.Clamp(value, 0, 0xFFFF));
        return index + 2;
    }

    private static int WriteUInt24(Span<byte> bytes, int index, int value)
    {
        var clamped = Math.Clamp(value, 0, 0xFFFFFF);
        bytes[index] = (byte)((clamped >> 16) & 0xFF);
        bytes[index + 1] = (byte)((clamped >> 8) & 0xFF);
        bytes[index + 2] = (byte)(clamped & 0xFF);
        return index + 3;
    }

    private static int WriteInt24(Span<byte> bytes, int index, int value)
    {
        var clamped = Math.Clamp(value, -0x800000, 0x7FFFFF);
        if (clamped < 0)
        {
            clamped = 0x1000000 + clamped;
        }

        return WriteUInt24(bytes, index, clamped);
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, ref int index)
    {
        var value = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(index, 2));
        index += 2;
        return value;
    }

    private static int ReadUInt24(ReadOnlySpan<byte> bytes, ref int index)
    {
        var value = (bytes[index] << 16) | (bytes[index + 1] << 8) | bytes[index + 2];
        index += 3;
        return value;
    }

    private static int ReadInt24(ReadOnlySpan<byte> bytes, ref int index)
    {
        var value = ReadUInt24(bytes, ref index);
        return (value & 0x800000) != 0 ? value - 0x1000000 : value;
    }
}
