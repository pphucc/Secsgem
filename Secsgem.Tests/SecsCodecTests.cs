using System;
using System.Linq;
using Secsgem.Core.Secs;
using Xunit;

namespace Secsgem.Tests;

public class SecsCodecTests
{
    private readonly SecsCodec _codec = new();

    [Fact]
    public void Boolean_SingleValue_RoundTrips()
    {
        var item = new SecsBoolean(true);

        Span<byte> buffer = stackalloc byte[16];
        var written = _codec.EncodeItem(buffer, item);

        var decoded = _codec.DecodeItem(buffer[..written], out var consumed);

        Assert.Equal(written, consumed);
        var decodedBool = Assert.IsType<SecsBoolean>(decoded);
        Assert.Equal(new[] { true }, decodedBool.Values.ToArray());
    }

    [Fact]
    public void Uint1_MultipleValues_RoundTrips()
    {
        var item = new SecsUint1(1, 2, 3);

        Span<byte> buffer = stackalloc byte[32];
        var written = _codec.EncodeItem(buffer, item);

        var decoded = _codec.DecodeItem(buffer[..written], out var consumed);

        Assert.Equal(written, consumed);
        var decodedU1 = Assert.IsType<SecsUint1>(decoded);
        Assert.Equal(new byte[] { 1, 2, 3 }, decodedU1.Values.ToArray());
    }

    [Fact]
    public void List_WithNestedItems_RoundTrips()
    {
        var list = new SecsList(new SecsItem[]
        {
            new SecsAscii("A"),
            new SecsUint1(1, 2),
            new SecsBoolean(true, false)
        });

        Span<byte> buffer = stackalloc byte[128];
        var written = _codec.EncodeItem(buffer, list);

        var decoded = _codec.DecodeItem(buffer[..written], out var consumed);

        Assert.Equal(written, consumed);
        var decodedList = Assert.IsType<SecsList>(decoded);
        Assert.Equal(3, decodedList.Items.Count);

        Assert.IsType<SecsAscii>(decodedList.Items[0]);
        Assert.IsType<SecsUint1>(decodedList.Items[1]);
        Assert.IsType<SecsBoolean>(decodedList.Items[2]);
    }

    [Fact]
    public void Message_WithHeader_AndItem_RoundTrips()
    {
        var header = new SecsHeader(stream: 1, function: 2, wBit: true, deviceId: 0x1234, systemBytes: 0x01020304);
        var item = new SecsList(
        [
            new SecsAscii("HELLO"),
            new SecsUint1(1, 2),
            new SecsBoolean(true, false)
        ]);
        var message = new SecsMessage(header, item);

        Span<byte> buffer = stackalloc byte[128];
        var written = _codec.EncodeMessage(buffer, message);

        var decoded = _codec.DecodeMessage(buffer[..written], out var consumed);

        Assert.Equal(written, consumed);
        Assert.Equal(message.Header.Stream, decoded.Header.Stream);
        Assert.Equal(message.Header.Function, decoded.Header.Function);
        Assert.Equal(message.Header.WBit, decoded.Header.WBit);
        Assert.Equal(message.Header.DeviceId, decoded.Header.DeviceId);
        Assert.Equal(message.Header.SystemBytes, decoded.Header.SystemBytes);

        var decodedList = Assert.IsType<SecsList>(decoded.Item);
        Assert.Equal(3, decodedList.Items.Count);
        var decodedAscii = Assert.IsType<SecsAscii>(decodedList.Items[0]);
        Assert.Equal("HELLO", decodedAscii.Value);
        var decodedU1 = Assert.IsType<SecsUint1>(decodedList.Items[1]);
        Assert.Equal([1, 2], decodedU1.Values.ToArray());
        var decodedBoolean = Assert.IsType<SecsBoolean>(decodedList.Items[2]);
        Assert.Equal([true, false], decodedBoolean.Values.ToArray());
    }
}