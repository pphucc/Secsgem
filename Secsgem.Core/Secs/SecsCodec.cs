using System;
using System.Buffers.Binary;
using System.Text;

namespace Secsgem.Core.Secs;

/// <summary>
/// Default SECS-II codec implementation (E5).
/// </summary>
public sealed class SecsCodec : ISecsCodec
{
    /// <summary>
    /// Encodes a full SECS-II message (header + root item) into a buffer.
    /// </summary>
    public int EncodeMessage(Span<byte> buffer, SecsMessage message)
    {
        // HSMS mapping (E37/E5): 10-byte header carries the SECS-II header fields.
        if (buffer.Length < 10)
        {
            throw new ArgumentException("Buffer too small for SECS message header.", nameof(buffer));
        }

        // Header layout:
        // 0-1: Device ID (big-endian)
        // 2  : Stream (high 7 bits) + W-bit (low 1 bit)
        // 3  : Function
        // 4-7: System bytes (big-endian)
        // 8-9: Reserved (0)
        var h = message.Header;
        BinaryPrimitives.WriteUInt16BigEndian(buffer[..2], h.DeviceId);
        buffer[2] = (byte)((h.Stream << 1) | (h.WBit ? 1 : 0));
        buffer[3] = h.Function;
        BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(4, 4), h.SystemBytes);
        buffer[8] = 0;
        buffer[9] = 0;

        int offset = 10;
        if (message.Item is not null)
        {
            offset += EncodeItem(buffer[offset..], message.Item);
        }

        return offset;
    }

    /// <summary>
    /// Decodes a full SECS-II message (header + root item) from a buffer.
    /// </summary>
    public SecsMessage DecodeMessage(ReadOnlySpan<byte> buffer, out int bytesConsumed)
    {
        if (buffer.Length < 10)
        {
            throw new ArgumentException("Buffer too small for SECS message header.", nameof(buffer));
        }

        ushort deviceId = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(0, 2));
        byte streamAndW = buffer[2];
        byte stream = (byte)(streamAndW >> 1);
        bool wBit = (streamAndW & 0x01) != 0;
        byte function = buffer[3];
        uint systemBytes = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(4, 4));

        var header = new SecsHeader(stream, function, wBit, deviceId, systemBytes);

        int offset = 10;
        SecsItem? item = null;
        if (buffer.Length > offset)
        {
            item = DecodeItem(buffer[offset..], out int itemBytes);
            offset += itemBytes;
        }

        bytesConsumed = offset;
        return new SecsMessage(header, item);
    }

    public int EncodeItem(Span<byte> buffer, SecsItem? item)
    {
        if (item is null)
        {
            return 0;
        }

        // Lists use element count; other items use byte length.
        int dataLength = item.Format == SecsFormat.List ? item.Length : item.Length;

        int lenBytes = dataLength <= byte.MaxValue
            ? 1
            : dataLength <= ushort.MaxValue ? 2 : 3;

        byte formatByte = (byte)(((lenBytes - 1) << 6) | (byte)item.Format);
        int offset = 0;

        buffer[offset++] = formatByte;
        switch (lenBytes)
        {
            case 1:
                buffer[offset++] = (byte)dataLength;
                break;
            case 2:
                BinaryPrimitives.WriteUInt16BigEndian(buffer[offset..], (ushort)dataLength);
                offset += 2;
                break;
            default:
                buffer[offset++] = (byte)((dataLength >> 16) & 0xFF);
                buffer[offset++] = (byte)((dataLength >> 8) & 0xFF);
                buffer[offset++] = (byte)(dataLength & 0xFF);
                break;
        }

        offset += EncodeItemBody(buffer[offset..], item);
        return offset;
    }

    public SecsItem? DecodeItem(ReadOnlySpan<byte> buffer, out int bytesConsumed)
    {
        bytesConsumed = 0;
        if (buffer.IsEmpty)
        {
            return null;
        }

        byte formatByte = buffer[0];
        int lenBytes = ((formatByte >> 6) & 0x03) + 1;
        var format = (SecsFormat)(formatByte & 0x3F);

        if (buffer.Length < 1 + lenBytes)
        {
            throw new ArgumentException("Buffer too small for SECS item length.", nameof(buffer));
        }

        int length = lenBytes switch
        {
            1 => buffer[1],
            2 => BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(1, 2)),
            _ => (buffer[1] << 16) | (buffer[2] << 8) | buffer[3]
        };

        int headerSize = 1 + lenBytes;

        // For lists, the length field is the element count, not the byte length.
        // We therefore pass the remaining span to DecodeList and let it determine
        // how many bytes were actually consumed.
        if (format == SecsFormat.List)
        {
            var listBody = buffer.Slice(headerSize);
            var listItem = DecodeList(listBody, out int listConsumed);
            bytesConsumed = headerSize + listConsumed;
            return listItem;
        }

        if (buffer.Length < headerSize + length)
        {
            throw new ArgumentException("Buffer too small for SECS item content.", nameof(buffer));
        }

        var body = buffer.Slice(headerSize, length);
        SecsItem? item = DecodeItemBody(format, length, body, out int bodyConsumed);

        bytesConsumed = headerSize + bodyConsumed;
        return item;
    }

    private static int EncodeItemBody(Span<byte> buffer, SecsItem item)
    {
        return item switch
        {
            SecsBinary b   => EncodeBinary(buffer, b),
            SecsBoolean b  => EncodeBoolean(buffer, b),
            SecsAscii a    => EncodeAscii(buffer, a),
            SecsJis j      => EncodeJis(buffer, j),
            SecsInt1 i1    => EncodeInt(buffer, i1.Values),
            SecsInt2 i2    => EncodeInt(buffer, i2.Values),
            SecsInt4 i4    => EncodeInt(buffer, i4.Values),
            SecsInt8 i8    => EncodeInt(buffer, i8.Values),
            SecsUint1 u1   => EncodeUint(buffer, u1.Values),
            SecsUint2 u2   => EncodeUint(buffer, u2.Values),
            SecsUint4 u4   => EncodeUint(buffer, u4.Values),
            SecsUint8 u8   => EncodeUint(buffer, u8.Values),
            SecsFloat4 f4  => EncodeFloat(buffer, f4.Values),
            SecsFloat8 f8  => EncodeFloat(buffer, f8.Values),
            SecsList list  => EncodeList(buffer, list),
            _              => throw new NotSupportedException($"Unsupported SECS item type: {item.GetType().Name}")
        };
    }

    private static SecsItem? DecodeItemBody(SecsFormat format, int length, ReadOnlySpan<byte> body, out int consumed)
    {
        consumed = length;
        return format switch
        {
            SecsFormat.Binary  => new SecsBinary(body.ToArray()),
            SecsFormat.Boolean => DecodeBoolean(body),
            SecsFormat.Ascii   => new SecsAscii(Encoding.ASCII.GetString(body)),
            SecsFormat.Jis     => new SecsJis(Encoding.ASCII.GetString(body)), // TODO: real JIS-8
            SecsFormat.Int1    => DecodeSigned<sbyte>(format, body, 1, values => new SecsInt1(values)),
            SecsFormat.Int2    => DecodeSigned<short>(format, body, 2, values => new SecsInt2(values)),
            SecsFormat.Int4    => DecodeSigned<int>(format, body, 4, values => new SecsInt4(values)),
            SecsFormat.Int8    => DecodeSigned<long>(format, body, 8, values => new SecsInt8(values)),
            SecsFormat.Uint1   => new SecsUint1(body.ToArray()),
            SecsFormat.Uint2   => DecodeUnsigned<ushort>(format, body, 2, values => new SecsUint2(values)),
            SecsFormat.Uint4   => DecodeUnsigned<uint>(format, body, 4, values => new SecsUint4(values)),
            SecsFormat.Uint8   => DecodeUnsigned<ulong>(format, body, 8, values => new SecsUint8(values)),
            SecsFormat.Float4  => DecodeFloat<float>(format, body, 4, values => new SecsFloat4(values)),
            SecsFormat.Float8  => DecodeFloat<double>(format, body, 8, values => new SecsFloat8(values)),
            SecsFormat.List    => DecodeList(body, out consumed),
            _                  => throw new NotSupportedException($"Unsupported SECS format: {format}")
        };
    }

    private static int EncodeBinary(Span<byte> buffer, SecsBinary item)
    {
        var span = item.Values;
        span.CopyTo(buffer);
        return span.Length;
    }

    private static int EncodeBoolean(Span<byte> buffer, SecsBoolean item)
    {
        var values = item.Values;
        for (int i = 0; i < values.Length; i++)
        {
            buffer[i] = values[i] ? (byte)1 : (byte)0;
        }
        return values.Length;
    }

    private static SecsBoolean DecodeBoolean(ReadOnlySpan<byte> body)
    {
        var values = new bool[body.Length];
        for (int i = 0; i < body.Length; i++)
        {
            values[i] = body[i] != 0;
        }
        return new SecsBoolean(values);
    }

    private static int EncodeAscii(Span<byte> buffer, SecsAscii item)
    {
        return Encoding.ASCII.GetBytes(item.Value, buffer);
    }

    private static int EncodeJis(Span<byte> buffer, SecsJis item)
    {
        // Placeholder: use ASCII bytes for now; JIS-8 can be added later.
        return Encoding.ASCII.GetBytes(item.Value, buffer);
    }

    private static int EncodeInt<T>(Span<byte> buffer, ReadOnlySpan<T> values) where T : unmanaged
    {
        int size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
        int offset = 0;
        foreach (var v in values)
        {
            switch (size)
            {
                case 1:
                    buffer[offset++] = unchecked((byte)Convert.ToSByte(v));
                    break;
                case 2:
                    BinaryPrimitives.WriteInt16BigEndian(buffer[offset..], Convert.ToInt16(v));
                    offset += 2;
                    break;
                case 4:
                    BinaryPrimitives.WriteInt32BigEndian(buffer[offset..], Convert.ToInt32(v));
                    offset += 4;
                    break;
                case 8:
                    BinaryPrimitives.WriteInt64BigEndian(buffer[offset..], Convert.ToInt64(v));
                    offset += 8;
                    break;
            }
        }
        return offset;
    }

    private static int EncodeUint<T>(Span<byte> buffer, ReadOnlySpan<T> values) where T : unmanaged
    {
        int size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
        int offset = 0;
        foreach (var v in values)
        {
            switch (size)
            {
                case 1:
                    buffer[offset++] = Convert.ToByte(v);
                    break;
                case 2:
                    BinaryPrimitives.WriteUInt16BigEndian(buffer[offset..], Convert.ToUInt16(v));
                    offset += 2;
                    break;
                case 4:
                    BinaryPrimitives.WriteUInt32BigEndian(buffer[offset..], Convert.ToUInt32(v));
                    offset += 4;
                    break;
                case 8:
                    BinaryPrimitives.WriteUInt64BigEndian(buffer[offset..], Convert.ToUInt64(v));
                    offset += 8;
                    break;
            }
        }
        return offset;
    }

    private static int EncodeFloat<T>(Span<byte> buffer, ReadOnlySpan<T> values) where T : unmanaged
    {
        int size = System.Runtime.InteropServices.Marshal.SizeOf<T>();
        int offset = 0;
        foreach (var v in values)
        {
            switch (size)
            {
                case 4:
                    BinaryPrimitives.WriteSingleBigEndian(buffer[offset..], Convert.ToSingle(v));
                    offset += 4;
                    break;
                case 8:
                    BinaryPrimitives.WriteDoubleBigEndian(buffer[offset..], Convert.ToDouble(v));
                    offset += 8;
                    break;
            }
        }
        return offset;
    }

    private static int EncodeList(Span<byte> buffer, SecsList list)
    {
        int offset = 0;
        foreach (var child in list.Items)
        {
            offset += new SecsCodec().EncodeItem(buffer[offset..], child);
        }
        return offset;
    }

    private static SecsItem DecodeList(ReadOnlySpan<byte> body, out int consumed)
    {
        var items = new System.Collections.Generic.List<SecsItem>();
        int offset = 0;
        var codec = new SecsCodec();
        while (offset < body.Length)
        {
            var item = codec.DecodeItem(body[offset..], out int innerConsumed);
            if (item is null || innerConsumed == 0)
            {
                break;
            }

            items.Add(item);
            offset += innerConsumed;
        }

        consumed = offset;
        return new SecsList(items);
    }

    private static SecsItem DecodeSigned<TValue>(SecsFormat format, ReadOnlySpan<byte> body, int elementSize, Func<TValue[], SecsItem> factory)
        where TValue : unmanaged
    {
        if (body.Length % elementSize != 0)
        {
            throw new ArgumentException($"Body length {body.Length} is not a multiple of element size {elementSize} for {format}.");
        }

        int count = body.Length / elementSize;
        var values = new TValue[count];
        for (int i = 0; i < count; i++)
        {
            int offset = i * elementSize;
            object value = elementSize switch
            {
                1 => (sbyte)body[offset],
                2 => BinaryPrimitives.ReadInt16BigEndian(body.Slice(offset, 2)),
                4 => BinaryPrimitives.ReadInt32BigEndian(body.Slice(offset, 4)),
                8 => BinaryPrimitives.ReadInt64BigEndian(body.Slice(offset, 8)),
                _ => throw new NotSupportedException()
            };

            values[i] = (TValue)value;
        }

        return factory(values);
    }

    private static SecsItem DecodeUnsigned<TValue>(SecsFormat format, ReadOnlySpan<byte> body, int elementSize, Func<TValue[], SecsItem> factory)
        where TValue : unmanaged
    {
        if (body.Length % elementSize != 0)
        {
            throw new ArgumentException($"Body length {body.Length} is not a multiple of element size {elementSize} for {format}.");
        }

        int count = body.Length / elementSize;
        var values = new TValue[count];
        for (int i = 0; i < count; i++)
        {
            int offset = i * elementSize;
            object value = elementSize switch
            {
                1 => body[offset],
                2 => BinaryPrimitives.ReadUInt16BigEndian(body.Slice(offset, 2)),
                4 => BinaryPrimitives.ReadUInt32BigEndian(body.Slice(offset, 4)),
                8 => BinaryPrimitives.ReadUInt64BigEndian(body.Slice(offset, 8)),
                _ => throw new NotSupportedException()
            };

            values[i] = (TValue)value;
        }

        return factory(values);
    }

    private static SecsItem DecodeFloat<TValue>(SecsFormat format, ReadOnlySpan<byte> body, int elementSize, Func<TValue[], SecsItem> factory)
        where TValue : unmanaged
    {
        if (body.Length % elementSize != 0)
        {
            throw new ArgumentException($"Body length {body.Length} is not a multiple of element size {elementSize} for {format}.");
        }

        int count = body.Length / elementSize;
        var values = new TValue[count];
        for (int i = 0; i < count; i++)
        {
            int offset = i * elementSize;
            object value = elementSize switch
            {
                4 => BinaryPrimitives.ReadSingleBigEndian(body.Slice(offset, 4)),
                8 => BinaryPrimitives.ReadDoubleBigEndian(body.Slice(offset, 8)),
                _ => throw new NotSupportedException()
            };

            values[i] = (TValue)value;
        }

        return factory(values);
    }
}

