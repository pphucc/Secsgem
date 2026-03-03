namespace Secsgem.Core.Transport;

/// <summary>
/// Contract for an HSMS connection (SEMI E37).
/// Covers TCP lifecycle, state tracking, HSMS control messages, and event notification.
/// </summary>
public interface IHsmsConnection
{
    /// <summary>Connection mode: Active (initiator) or Passive (listener).</summary>
    HsmsConnectionMode Mode { get; }

    /// <summary>Current state in the E37 §7 state machine.</summary>
    HsmsConnectionState State { get; }

    /// <summary>
    /// Opens the connection.
    /// Passive: starts listening for incoming TCP connections.
    /// Active: begins connect attempts to the remote host.
    /// </summary>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes the connection and transitions to <see cref="HsmsConnectionState.NotConnected"/>.</summary>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a fully framed HSMS message (length prefix + header + body).
    /// Intended for use by the SECS session layer, not application code.
    /// </summary>
    Task SendRawAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>Sends SELECT.req (E37 §8.3.1).</summary>
    Task SendSelectRequestAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends SELECT.rsp (E37 §8.3.2). <paramref name="statusCode"/>: 0 = success.</summary>
    Task SendSelectResponseAsync(byte statusCode, CancellationToken cancellationToken = default);

    /// <summary>Sends DESELECT.req (E37 §8.4.1).</summary>
    Task SendDeselectRequestAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends LINKTEST.req (E37 §8.6.1).</summary>
    Task SendLinktestRequestAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends SEPARATE.req (E37 §8.7.1).</summary>
    Task SendSeparateRequestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Raised when a complete HSMS message is received from the TCP stream.
    /// The memory contains the full framed message (header + optional body, without the 4-byte length prefix).
    /// </summary>
    event EventHandler<ReadOnlyMemory<byte>> DataReceived;

    /// <summary>Raised when the HSMS state changes.</summary>
    event EventHandler<HsmsConnectionState> StateChanged;

    /// <summary>Raised when an unrecoverable transport error occurs.</summary>
    event EventHandler<Exception> ErrorOccurred;
}
