using System;

namespace Secsgem.Core.Secs;

/// <summary>
/// Encodes and decodes SECS-II messages to and from raw byte buffers.
/// </summary>
public interface ISecsCodec
{
    /// <summary>
    /// Encodes a SECS-II item into the provided buffer and returns the number of bytes written.
    /// </summary>
    int EncodeItem(Span<byte> buffer, SecsItem? item);

    /// <summary>
    /// Decodes a SECS-II item from the given buffer.
    /// </summary>
    SecsItem? DecodeItem(ReadOnlySpan<byte> buffer, out int bytesConsumed);
}

