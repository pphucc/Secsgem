using System.Buffers.Binary;

namespace Secsgem.Core.Transport;

/// <summary>
/// HSMS message header (10 bytes), as defined in SEMI E37 §8.2.
///
/// Wire layout (big-endian):
///   [0–1]  Session ID   — Device ID for data messages; 0xFFFF for control messages
///   [2]    Header Byte 0 — R-bit | W-bit | Stream[6] (data); 0x00 (control)
///   [3]    Header Byte 1 — Function (data); status/reason code (control responses)
///   [4]    PType         — 0x00 = SECS-II encoding
///   [5]    SType         — 0x00 = data message; see E37 Table 1 for control types
///   [6–9]  System Bytes  — unique transaction identifier (big-endian uint32)
///
/// The full HSMS frame is: [4-byte message length] + [10-byte header] + [optional body]
/// </summary>
internal readonly struct HsmsHeader
{
    // SType values — SEMI E37 Table 1
    public const byte SType_DataMessage      = 0x00;
    public const byte SType_SelectRequest    = 0x01;
    public const byte SType_SelectResponse   = 0x02;
    public const byte SType_DeselectRequest  = 0x03;
    public const byte SType_DeselectResponse = 0x04;
    public const byte SType_LinktestRequest  = 0x05;
    public const byte SType_LinktestResponse = 0x06;
    public const byte SType_RejectRequest    = 0x07;
    public const byte SType_SeparateRequest  = 0x09;

    public const ushort ControlSessionId = 0xFFFF;
    public const int    Size             = 10;
    public const int    MessageLengthSize = 4;
    public const int    TotalHeaderSize  = Size + MessageLengthSize; // 14 bytes on wire

    public readonly ushort SessionId;
    public readonly byte   HeaderByte0;
    public readonly byte   HeaderByte1;
    public readonly byte   PType;
    public readonly byte   SType;
    public readonly uint   SystemBytes;

    public bool IsDataMessage   => SType == SType_DataMessage;
    public bool IsReplyExpected => (HeaderByte0 & 0x80) != 0; // W-bit

    // For data messages: byte 2 = [R(1) | W(1) | Stream(6)]
    public byte Stream   => (byte)(HeaderByte0 & 0x7F);
    public byte Function => HeaderByte1;

    public HsmsHeader(
        ushort sessionId,
        byte headerByte0,
        byte headerByte1,
        byte pType,
        byte sType,
        uint systemBytes)
    {
        SessionId   = sessionId;
        HeaderByte0 = headerByte0;
        HeaderByte1 = headerByte1;
        PType       = pType;
        SType       = sType;
        SystemBytes = systemBytes;
    }

    /// <summary>Creates a new outgoing control message header using <see cref="ControlSessionId"/>.</summary>
    public static HsmsHeader CreateControl(byte sType, byte statusCode, uint systemBytes)
    {
        return new HsmsHeader(
            sessionId:   ControlSessionId,
            headerByte0: 0x00,
            headerByte1: statusCode,
            pType:       0x00,
            sType:       sType,
            systemBytes: systemBytes);
    }

    /// <summary>
    /// Creates a response header that echoes the session ID and system bytes from the
    /// incoming <paramref name="request"/>, as required by E37 for SELECT.rsp,
    /// DESELECT.rsp, LINKTEST.rsp, and REJECT.req.
    /// </summary>
    public static HsmsHeader CreateControlResponse(byte sType, byte statusCode, HsmsHeader request)
    {
        return new HsmsHeader(
            sessionId:   request.SessionId,
            headerByte0: 0x00,
            headerByte1: statusCode,
            pType:       0x00,
            sType:       sType,
            systemBytes: request.SystemBytes);
    }

    /// <summary>Creates a data message header.</summary>
    public static HsmsHeader CreateData(
        ushort sessionId,
        byte stream,
        byte function,
        bool replyExpected,
        uint systemBytes)
    {
        byte headerByte0 = (byte)(stream & 0x7F);
        if (replyExpected)
        {
            headerByte0 |= 0x80;
        }

        return new HsmsHeader(
            sessionId:   sessionId,
            headerByte0: headerByte0,
            headerByte1: function,
            pType:       0x00,
            sType:       SType_DataMessage,
            systemBytes: systemBytes);
    }

    /// <summary>Encodes this header into a 10-byte big-endian span.</summary>
    public void EncodeTo(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            throw new ArgumentException($"Destination must be at least {Size} bytes.", nameof(destination));
        }

        BinaryPrimitives.WriteUInt16BigEndian(destination[0..2], SessionId);
        destination[2] = HeaderByte0;
        destination[3] = HeaderByte1;
        destination[4] = PType;
        destination[5] = SType;
        BinaryPrimitives.WriteUInt32BigEndian(destination[6..10], SystemBytes);
    }

    /// <summary>Decodes an <see cref="HsmsHeader"/> from a 10-byte big-endian span.</summary>
    public static HsmsHeader Decode(ReadOnlySpan<byte> source)
    {
        if (source.Length < Size)
        {
            throw new ArgumentException($"Source must be at least {Size} bytes.", nameof(source));
        }

        return new HsmsHeader(
            sessionId:   BinaryPrimitives.ReadUInt16BigEndian(source[0..2]),
            headerByte0: source[2],
            headerByte1: source[3],
            pType:       source[4],
            sType:       source[5],
            systemBytes: BinaryPrimitives.ReadUInt32BigEndian(source[6..10]));
    }

    public override string ToString()
    {
        return IsDataMessage
            ? $"[Data] SID=0x{SessionId:X4} S{Stream}F{Function} W={IsReplyExpected} SysBytes=0x{SystemBytes:X8}"
            : $"[Ctrl] SType=0x{SType:X2} Status=0x{HeaderByte1:X2} SysBytes=0x{SystemBytes:X8}";
    }
}
