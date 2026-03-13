using System;
using System.Text;

namespace Secsgem.Core.Secs;

/// <summary>
/// SECS-II ASCII text item.
/// </summary>
public sealed class SecsAscii : SecsItem
{
    private readonly string _value;

    public SecsAscii(string value)
        : base(SecsFormat.Ascii, value is null ? 0 : Encoding.ASCII.GetByteCount(value))
    {
        _value = value ?? string.Empty;
    }

    public string Value => _value;

    public override object? GetValue() => _value;
}

