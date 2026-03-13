using System;

namespace Secsgem.Core.Secs;

/// <summary>
/// Base class for floating-point SECS-II items.
/// </summary>
public abstract class SecsFloatBase<T> : SecsItem where T : unmanaged
{
    private readonly T[] _values;

    protected SecsFloatBase(SecsFormat format, int elementSize, params T[] values)
        : base(format, (values?.Length ?? 0) * elementSize)
    {
        _values = values ?? Array.Empty<T>();
    }

    public ReadOnlySpan<T> Values => _values;

    public override object? GetValue() => _values;
}

public sealed class SecsFloat4 : SecsFloatBase<float>
{
    public SecsFloat4(params float[] values)
        : base(SecsFormat.Float4, sizeof(float), values)
    {
    }
}

public sealed class SecsFloat8 : SecsFloatBase<double>
{
    public SecsFloat8(params double[] values)
        : base(SecsFormat.Float8, sizeof(double), values)
    {
    }
}

