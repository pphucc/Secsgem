using System;
using System.Text;

namespace Secsgem.Core.Secs;

/// <summary>
/// SECS-II JIS-8 text item.
/// </summary>
public sealed class SecsJis : SecsItem
{
    private readonly string _value;

    // For now we size using ASCII as a placeholder; the codec will apply the correct JIS-8 encoding.
    public SecsJis(string value)
        : base(SecsFormat.Jis, value is null ? 0 : Encoding.ASCII.GetByteCount(value))
    {
        _value = value ?? string.Empty;
    }

    public string Value => _value;

    public override object? GetValue() => _value;
}

