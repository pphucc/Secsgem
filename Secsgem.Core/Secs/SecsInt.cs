using System;

namespace Secsgem.Core.Secs;

/// <summary>
/// Base class for signed integer SECS-II items.
/// </summary>
public abstract class SecsIntBase<T> : SecsItem where T : unmanaged
{
    private readonly T[] _values;

    protected SecsIntBase(SecsFormat format, int elementSize, params T[] values)
        : base(format, (values?.Length ?? 0) * elementSize)
    {
        _values = values ?? Array.Empty<T>();
    }

    public ReadOnlySpan<T> Values => _values;

    public override object? GetValue() => _values;
}

public sealed class SecsInt1 : SecsIntBase<sbyte>
{
    public SecsInt1(params sbyte[] values)
        : base(SecsFormat.Int1, sizeof(sbyte), values)
    {
    }
}

public sealed class SecsInt2 : SecsIntBase<short>
{
    public SecsInt2(params short[] values)
        : base(SecsFormat.Int2, sizeof(short), values)
    {
    }
}

public sealed class SecsInt4 : SecsIntBase<int>
{
    public SecsInt4(params int[] values)
        : base(SecsFormat.Int4, sizeof(int), values)
    {
    }
}

public sealed class SecsInt8 : SecsIntBase<long>
{
    public SecsInt8(params long[] values)
        : base(SecsFormat.Int8, sizeof(long), values)
    {
    }
}

