using System;
using System.Collections.Generic;

namespace Secsgem.Core.Secs;

/// <summary>
/// SECS-II list item (can contain zero or more child items).
/// </summary>
public sealed class SecsList : SecsItem
{
    private readonly IReadOnlyList<SecsItem> _items;

    public SecsList(IReadOnlyList<SecsItem> items)
        : base(SecsFormat.List, items?.Count ?? 0)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
    }

    /// <summary>
    /// Child items of this list. <see cref="SecsItem.Length"/> for lists
    /// represents the number of elements, not the number of data bytes
    /// (per SEMI E5 §9.3).
    /// </summary>
    public IReadOnlyList<SecsItem> Items => _items;

    public override object? GetValue() => _items;
}

