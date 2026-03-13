using System;

namespace Secsgem.Core.Secs;

/// <summary>
/// SECS-II boolean item.
/// </summary>
public sealed class SecsBoolean : SecsItem
{
    private readonly bool[] _values;

    public SecsBoolean(params bool[] values)
        : base(SecsFormat.Boolean, values?.Length ?? 0)
    {
        _values = values ?? Array.Empty<bool>();
    }

    public ReadOnlySpan<bool> Values => _values;

    public override object? GetValue() => _values;
}

