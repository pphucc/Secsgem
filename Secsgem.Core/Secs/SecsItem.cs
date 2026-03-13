using System;

namespace Secsgem.Core.Secs;

/// <summary>
/// Base class for all SECS-II data items.
/// </summary>
public abstract class SecsItem
{
    protected SecsItem(SecsFormat format, int length)
    {
        Format = format;
        Length = length;
    }

    /// <summary>
    /// SECS-II item format.
    /// </summary>
    public SecsFormat Format { get; }

    /// <summary>
    /// Total number of data bytes for this item (not including the format/length prefix).
    /// May be zero for empty items or empty lists.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Returns the underlying value in a type-appropriate form (e.g., array for numeric types).
    /// </summary>
    public abstract object? GetValue();
}

