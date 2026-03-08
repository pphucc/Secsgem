namespace Secsgem.Core.Transport;

/// <summary>
/// Configuration for an HSMS connection (SEMI E37).
/// </summary>
public class HsmsConnectionOptions
{
    /// <summary>
    /// Remote address to connect to. Used in <see cref="HsmsConnectionMode.Active"/> mode only.
    /// </summary>
    public string RemoteAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// Local IP address to bind to. Used in <see cref="HsmsConnectionMode.Passive"/> mode only.
    /// Defaults to <c>0.0.0.0</c> (all interfaces). Set to a specific IP to restrict which NIC accepts connections.
    /// </summary>
    public string LocalAddress { get; set; } = "0.0.0.0";

    /// <summary>TCP port number.</summary>
    public int Port { get; set; } = 5000;

    /// <summary>Connection mode: Active (initiator) or Passive (listener).</summary>
    public HsmsConnectionMode Mode { get; set; } = HsmsConnectionMode.Passive;

    /// <summary>T3 – Reply Timeout (seconds). Maximum wait for a reply to a primary data message. E37 §8.3.</summary>
    public int T3_ReplyTimeout { get; set; } = 45;

    /// <summary>T5 – Connect Separation Timeout (seconds). Minimum gap between TCP reconnect attempts. E37 §8.5.</summary>
    public int T5_ConnectSeparationTimeout { get; set; } = 10;

    /// <summary>T6 – Control Timeout (seconds). Maximum wait for a response to an HSMS control message. E37 §8.6.</summary>
    public int T6_ControlTimeout { get; set; } = 5;

    /// <summary>T7 – Not Selected Timeout (seconds). Maximum time a connection may remain NOT SELECTED. E37 §8.7.</summary>
    public int T7_NotSelectedTimeout { get; set; } = 10;

    /// <summary>T8 – Network Intercharacter Timeout (seconds). Maximum time between bytes of a single message. E37 §8.8.</summary>
    public int T8_NetworkIntercharacterTimeout { get; set; } = 5;
}
