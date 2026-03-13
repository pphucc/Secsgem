namespace Secsgem.Core.Secs;

/// <summary>
/// SECS-II message: header + root item.
/// </summary>
public sealed class SecsMessage
{
    public SecsMessage(SecsHeader header, SecsItem? root)
    {
        Header = header;
        Item = root;
    }

    /// <summary>Message header (S, F, W, device ID, system bytes).</summary>
    public SecsHeader Header { get; }

    /// <summary>Root SECS-II item. May be null for messages with no data.</summary>
    public SecsItem? Item { get; }
}

