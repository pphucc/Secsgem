using System;

namespace Secsgem.Core.Secs;

/// <summary>
/// SECS-II binary item (format B).
/// </summary>
public sealed class SecsBinary : SecsItem
{
    private readonly byte[] _values;

    public SecsBinary(params byte[] values)
        : base(SecsFormat.Binary, values?.Length ?? 0)
    {
        _values = values ?? Array.Empty<byte>();
    }

    public ReadOnlySpan<byte> Values => _values;

    public override object? GetValue() => _values;
}

