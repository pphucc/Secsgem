namespace Secsgem.Core.Transport;

/// <summary>
/// HSMS connection state machine leaf states (SEMI E37 §7).
/// <para>
/// E37 defines a hierarchical structure where <c>NotSelected</c> and <c>Selected</c>
/// are both substates of the <c>CONNECTED</c> superstate (TCP connection exists).
/// Use <see cref="HsmsConnectionStateExtensions.IsConnected"/> to test the superstate.
/// </para>
/// </summary>
public enum HsmsConnectionState
{
    /// <summary>No TCP connection. Initial state.</summary>
    NotConnected,

    /// <summary>TCP connected; SELECT procedure not yet completed. Substate of CONNECTED.</summary>
    NotSelected,

    /// <summary>TCP connected and SELECT completed. Data messages may be exchanged. Substate of CONNECTED.</summary>
    Selected
}

/// <summary>Extension methods for <see cref="HsmsConnectionState"/>.</summary>
public static class HsmsConnectionStateExtensions
{
    /// <summary>
    /// Returns <c>true</c> if the state is within the CONNECTED superstate
    /// (i.e., <see cref="HsmsConnectionState.NotSelected"/> or <see cref="HsmsConnectionState.Selected"/>).
    /// </summary>
    public static bool IsConnected(this HsmsConnectionState state)
    {
        return state is HsmsConnectionState.NotSelected
                     or HsmsConnectionState.Selected;
    }
}
