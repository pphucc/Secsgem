using System;

namespace Secsgem.Core.Secs;

/// <summary>
/// Base class for unsigned integer SECS-II items.
/// </summary>
public abstract class SecsUintBase<T> : SecsItem where T : unmanaged
{
    private readonly T[] _values;

    protected SecsUintBase(SecsFormat format, int elementSize, params T[] values)
        : base(format, (values?.Length ?? 0) * elementSize)
    {
        _values = values ?? Array.Empty<T>();
    }

    public ReadOnlySpan<T> Values => _values;

    public override object? GetValue() => _values;
}

public sealed class SecsUint1 : SecsUintBase<byte>
{
    public SecsUint1(params byte[] values)
        : base(SecsFormat.Uint1, sizeof(byte), values)
    {
    }
}

public sealed class SecsUint2 : SecsUintBase<ushort>
{
    public SecsUint2(params ushort[] values)
        : base(SecsFormat.Uint2, sizeof(ushort), values)
    {
    }
}

public sealed class SecsUint4 : SecsUintBase<uint>
{
    public SecsUint4(params uint[] values)
        : base(SecsFormat.Uint4, sizeof(uint), values)
    {
    }
}

public sealed class SecsUint8 : SecsUintBase<ulong>
{
    public SecsUint8(params ulong[] values)
        : base(SecsFormat.Uint8, sizeof(ulong), values)
    {
    }
}

