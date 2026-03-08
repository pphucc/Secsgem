using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Secsgem.Core.Transport;

/// <summary>
/// HSMS connection implementation (SEMI E37).
/// Handles TCP connectivity, state machine, and HSMS control message exchange
/// for both Active (initiator) and Passive (listener) modes.
/// <para>
/// Timers enforced: T5 (connect separation), T6 (control transaction timeout),
/// T7 (not-selected timeout), T8 (intercharacter timeout).
/// </para>
/// </summary>
public class HsmsConnection : IHsmsConnection, IDisposable, IAsyncDisposable
{
    private readonly HsmsConnectionOptions _options;

    private TcpClient? _tcpClient;
    private TcpListener? _tcpListener;
    private NetworkStream? _networkStream;

    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private HsmsConnectionState _state = HsmsConnectionState.NotConnected;

    private CancellationTokenSource? _connectionCts;
    private Task? _backgroundTask;
    private CancellationTokenSource? _t7Cts;
    private CancellationToken _currentConnectionToken;

    // T6 — pending control requests: keyed by SystemBytes, value is a TCS that is
    // completed when the matching response (same SystemBytes) arrives from the remote.
    private readonly Dictionary<uint, TaskCompletionSource<HsmsHeader>> _pendingControlRequests = new();
    private readonly object _pendingControlLock = new();

    private uint _nextSystemBytes;
    private bool _disposed;

    /// <inheritdoc/>
    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived;

    /// <inheritdoc/>
    public event EventHandler<HsmsConnectionState>? StateChanged;

    /// <inheritdoc/>
    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// Raised just before a raw HSMS frame is written to the socket.
    /// The memory contains the full wire frame: [4-byte length] + [header] + [optional body].
    /// </summary>
    public event EventHandler<ReadOnlyMemory<byte>>? RawMessageSent;

    /// <summary>
    /// Raised after a complete raw HSMS frame has been read from the socket.
    /// The memory contains the full wire frame: [4-byte length] + [header] + [optional body].
    /// </summary>
    public event EventHandler<ReadOnlyMemory<byte>>? RawMessageReceived;

    public HsmsConnection(HsmsConnectionOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public HsmsConnectionMode Mode => _options.Mode;

    /// <inheritdoc/>
    public HsmsConnectionState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    /// <inheritdoc/>
    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_backgroundTask is not null && !_backgroundTask.IsCompleted)
        {
            throw new InvalidOperationException("Connection is already open. Call CloseAsync first.");
        }

        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (_options.Mode == HsmsConnectionMode.Passive)
        {
            _backgroundTask = RunPassiveAsync(_connectionCts.Token);
        }
        else
        {
            _backgroundTask = RunActiveAsync(_connectionCts.Token);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_connectionCts is not null)
        {
            await _connectionCts.CancelAsync();
        }

        if (_backgroundTask is not null)
        {
            try
            {
                await _backgroundTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException) { }
            catch (OperationCanceledException) { }
        }

        DisconnectTcp();
        TransitionState(HsmsConnectionState.NotConnected);
    }

    // HSMS-SS: accept one connection at a time (E37 §6.3.3)
    private async Task RunPassiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            _tcpListener = new TcpListener(IPAddress.Parse(_options.LocalAddress), _options.Port);
            _tcpListener.Start();

            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client = await _tcpListener.AcceptTcpClientAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, ex);
                    break;
                }

                await HandleConnectionAsync(client, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
        finally
        {
            _tcpListener?.Stop();
        }
    }

    private async Task RunActiveAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var client = new TcpClient();
            bool connected = false;

            try
            {
                await client.ConnectAsync(_options.RemoteAddress, _options.Port, cancellationToken);
                connected = true;
            }
            catch (OperationCanceledException)
            {
                client.Dispose();
                break;
            }
            catch (Exception ex)
            {
                client.Dispose();
                ErrorOccurred?.Invoke(this, ex);
            }

            if (connected)
            {
                await HandleConnectionAsync(client, cancellationToken);
            }

            // T5: minimum gap between TCP connection attempts (E37 §8.5)
            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_options.T5_ConnectSeparationTimeout),
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        _tcpClient = client;
        _networkStream = client.GetStream();
        _currentConnectionToken = cancellationToken;

        TransitionState(HsmsConnectionState.NotSelected);
        StartT7Timer();

        // The receive loop must be running before we send SELECT.req (Active mode) so that
        // the incoming SELECT.rsp can be dispatched and resolve the T6 pending TCS.
        var receiveTask = ReceiveLoopAsync(_networkStream, cancellationToken);

        // Active entity initiates SELECT after TCP connect (E37 §6.3.4).
        // Now awaits SELECT.rsp with T6 timeout because receive loop is already running.
        if (_options.Mode == HsmsConnectionMode.Active)
        {
            try
            {
                await SendSelectRequestAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                DisconnectTcp();    // closes the stream → terminates receiveTask
            }
        }

        try
        {
            await receiveTask;
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }     // stream closed by DisconnectTcp (e.g. SEPARATE.req or T6 failure)
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
        finally
        {
            StopT7Timer();
            ClearPendingControlRequests();
            DisconnectTcp();
            TransitionState(HsmsConnectionState.NotConnected);
        }
    }

    private async Task ReceiveLoopAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        byte[] lengthBuffer = new byte[HsmsHeader.MessageLengthSize];

        while (!cancellationToken.IsCancellationRequested)
        {
            // Wait for the start of the next message with no timeout — T8 does not apply
            // between messages, only within a single message frame (E37 §8.8).
            bool hasData = await ReadExactAsync(stream, lengthBuffer, lengthBuffer.Length, cancellationToken, applyT8: false);
            if (!hasData)
            {
                break;
            }

            uint messageLength = BinaryPrimitives.ReadUInt32BigEndian(lengthBuffer);

            if (messageLength < HsmsHeader.Size)
            {
                throw new InvalidDataException(
                    $"HSMS message length {messageLength} is below minimum header size {HsmsHeader.Size}.");
            }

            // Maximum body size is bounded by E5's 3-byte length field (~16 MB)
            const uint MaxMessageLength = HsmsHeader.Size + 0x00FFFFFF;
            if (messageLength > MaxMessageLength)
            {
                throw new InvalidDataException(
                    $"HSMS message length {messageLength} exceeds maximum allowed size.");
            }

            // First byte received — T8 now applies for the rest of the message frame.
            byte[] messageBuffer = new byte[messageLength];
            bool hasMessage = await ReadExactAsync(stream, messageBuffer, (int)messageLength, cancellationToken, applyT8: true);
            if (!hasMessage)
            {
                break;
            }

            if (RawMessageReceived is not null)
            {
                // Rebuild the full wire frame (length prefix + message) for logging.
                byte[] fullFrame = new byte[HsmsHeader.MessageLengthSize + messageLength];
                BinaryPrimitives.WriteUInt32BigEndian(fullFrame.AsSpan(0, 4), messageLength);
                messageBuffer.CopyTo(fullFrame, HsmsHeader.MessageLengthSize);
                RawMessageReceived.Invoke(this, fullFrame.AsMemory());
            }

            DispatchMessage(messageBuffer);
        }
    }

    private void DispatchMessage(byte[] messageBuffer)
    {
        HsmsHeader header = HsmsHeader.Decode(messageBuffer.AsSpan(0, HsmsHeader.Size));

        if (header.IsDataMessage)
        {
            DataReceived?.Invoke(this, messageBuffer.AsMemory());
        }
        else
        {
            HandleControlMessage(header);
        }
    }

    private void HandleControlMessage(HsmsHeader header)
    {
        switch (header.SType)
        {
            case HsmsHeader.SType_SelectRequest:
                HandleSelectRequest(header);
                break;

            case HsmsHeader.SType_SelectResponse:
                CompleteControlRequest(header);
                HandleSelectResponse(header);
                break;

            case HsmsHeader.SType_DeselectRequest:
                HandleDeselectRequest(header);
                break;

            case HsmsHeader.SType_DeselectResponse:
                CompleteControlRequest(header);
                break;

            case HsmsHeader.SType_LinktestRequest:
                FireAndForget(SendLinktestResponseAsync(header));
                break;

            case HsmsHeader.SType_LinktestResponse:
                CompleteControlRequest(header);
                break;

            case HsmsHeader.SType_SeparateRequest:
                DisconnectTcp();
                TransitionState(HsmsConnectionState.NotConnected);
                break;

            default:
                FireAndForget(SendRejectAsync(header, reasonCode: 0x01));
                break;
        }
    }

    private void HandleSelectRequest(HsmsHeader request)
    {
        // E37 §8.3: always respond regardless of current state.
        // Status 0x01 (already active) handles the simultaneous SELECT case where
        // both sides sent SELECT.req at the same time and we are already Selected.
        byte statusCode = State == HsmsConnectionState.Selected
            ? (byte)0x01   // already active
            : (byte)0x00;  // communication established

        var response = HsmsHeader.CreateControlResponse(
            HsmsHeader.SType_SelectResponse,
            statusCode,
            request);

        FireAndForget(SendControlFrameAsync(response, _currentConnectionToken));

        if (State == HsmsConnectionState.NotSelected)
        {
            StopT7Timer();
            TransitionState(HsmsConnectionState.Selected);
        }
    }

    private void HandleSelectResponse(HsmsHeader header)
    {
        switch (header.HeaderByte1)
        {
            case 0x00: // Communication Established
            case 0x01: // Communication Already Active — simultaneous SELECT; transition is idempotent
                StopT7Timer();
                TransitionState(HsmsConnectionState.Selected);
                break;

            case 0x02:
                ErrorOccurred?.Invoke(this, new InvalidOperationException(
                    "SELECT.rsp: Connection Not Ready (0x02)."));
                DisconnectTcp();
                break;

            case 0x03:
                ErrorOccurred?.Invoke(this, new InvalidOperationException(
                    "SELECT.rsp: Connect Exhaust (0x03)."));
                DisconnectTcp();
                break;

            default:
                ErrorOccurred?.Invoke(this, new InvalidOperationException(
                    $"SELECT.rsp: Unknown status 0x{header.HeaderByte1:X2}."));
                DisconnectTcp();
                break;
        }
    }

    private void HandleDeselectRequest(HsmsHeader request)
    {
        byte statusCode = State == HsmsConnectionState.Selected
            ? (byte)0x00   // Communication Ended
            : (byte)0x01;  // Communication Not Established

        var response = HsmsHeader.CreateControlResponse(
            HsmsHeader.SType_DeselectResponse,
            statusCode,
            request);

        FireAndForget(SendControlFrameAsync(response, _currentConnectionToken));

        TransitionState(HsmsConnectionState.NotSelected);
        StartT7Timer();
    }

    /// <inheritdoc/>
    public Task SendSelectRequestAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var header = HsmsHeader.CreateControl(
            HsmsHeader.SType_SelectRequest,
            statusCode: 0x00,
            systemBytes: NextSystemBytes());

        return SendControlRequestAsync(header, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SendSelectResponseAsync(byte statusCode, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var header = HsmsHeader.CreateControl(
            HsmsHeader.SType_SelectResponse,
            statusCode: statusCode,
            systemBytes: NextSystemBytes());

        return SendControlFrameAsync(header, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SendDeselectRequestAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var header = HsmsHeader.CreateControl(
            HsmsHeader.SType_DeselectRequest,
            statusCode: 0x00,
            systemBytes: NextSystemBytes());

        return SendControlRequestAsync(header, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SendLinktestRequestAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var header = HsmsHeader.CreateControl(
            HsmsHeader.SType_LinktestRequest,
            statusCode: 0x00,
            systemBytes: NextSystemBytes());

        return SendControlRequestAsync(header, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SendSeparateRequestAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var header = HsmsHeader.CreateControl(
            HsmsHeader.SType_SeparateRequest,
            statusCode: 0x00,
            systemBytes: NextSystemBytes());

        return SendControlFrameAsync(header, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SendRawAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return WriteToStreamAsync(data, cancellationToken);
    }

    private Task SendLinktestResponseAsync(HsmsHeader request)
    {
        var response = HsmsHeader.CreateControlResponse(
            HsmsHeader.SType_LinktestResponse,
            statusCode: 0x00,
            request);

        return SendControlFrameAsync(response, _currentConnectionToken);
    }

    private Task SendRejectAsync(HsmsHeader request, byte reasonCode)
    {
        var response = HsmsHeader.CreateControlResponse(
            HsmsHeader.SType_RejectRequest,
            statusCode: reasonCode,
            request);

        return SendControlFrameAsync(response, _currentConnectionToken);
    }

    private Task SendControlFrameAsync(HsmsHeader header, CancellationToken cancellationToken)
    {
        byte[] frame = new byte[HsmsHeader.TotalHeaderSize];
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(0, 4), HsmsHeader.Size);
        header.EncodeTo(frame.AsSpan(4, HsmsHeader.Size));

        return WriteToStreamAsync(frame.AsMemory(), cancellationToken);
    }

    /// <summary>
    /// Sends a control request and waits for its response, bounded by the T6 Control Timeout.
    /// Registers a pending entry (keyed by SystemBytes) before sending, so the receive loop
    /// can resolve it when the matching response arrives.
    /// Throws <see cref="TimeoutException"/> if no response is received within T6 (E37 §8.6).
    /// </summary>
    private async Task SendControlRequestAsync(HsmsHeader header, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<HsmsHeader>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_pendingControlLock)
        {
            _pendingControlRequests[header.SystemBytes] = tcs;
        }

        try
        {
            await SendControlFrameAsync(header, cancellationToken);

            using var t6Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            t6Cts.CancelAfter(TimeSpan.FromSeconds(_options.T6_ControlTimeout));

            try
            {
                await tcs.Task.WaitAsync(t6Cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"T6: no response to control message (SType=0x{header.SType:X2}) within " +
                    $"{_options.T6_ControlTimeout}s (E37 §8.6).");
            }
        }
        finally
        {
            lock (_pendingControlLock)
            {
                _pendingControlRequests.Remove(header.SystemBytes);
            }
        }
    }

    private async Task WriteToStreamAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (_networkStream is null || _tcpClient is null || !_tcpClient.Connected)
            {
                throw new InvalidOperationException("Cannot send: no active TCP connection.");
            }

            RawMessageSent?.Invoke(this, data);
            await _networkStream.WriteAsync(data, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private void StartT7Timer()
    {
        StopT7Timer();
        _t7Cts = CancellationTokenSource.CreateLinkedTokenSource(_currentConnectionToken);
        FireAndForget(RunT7TimerAsync(_t7Cts.Token));
    }

    private void StopT7Timer()
    {
        if (_t7Cts is not null)
        {
            _t7Cts.Cancel();
            _t7Cts.Dispose();
            _t7Cts = null;
        }
    }

    private async Task RunT7TimerAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                TimeSpan.FromSeconds(_options.T7_NotSelectedTimeout),
                cancellationToken);

            if (State == HsmsConnectionState.NotSelected)
            {
                ErrorOccurred?.Invoke(this, new TimeoutException(
                    $"T7: connection remained NOT SELECTED for {_options.T7_NotSelectedTimeout}s (E37 §8.7)."));

                DisconnectTcp();
                TransitionState(HsmsConnectionState.NotConnected);
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Called when a control response arrives. Looks up the pending TCS by SystemBytes
    /// and completes it so <see cref="SendControlRequestAsync"/> can unblock.
    /// </summary>
    private void CompleteControlRequest(HsmsHeader response)
    {
        TaskCompletionSource<HsmsHeader>? tcs;
        lock (_pendingControlLock)
        {
            _pendingControlRequests.TryGetValue(response.SystemBytes, out tcs);
            if (tcs is not null)
            {
                _pendingControlRequests.Remove(response.SystemBytes);
            }
        }

        tcs?.TrySetResult(response);
    }

    /// <summary>
    /// Cancels all pending control request TCS entries with a connection-closed exception.
    /// Called during connection teardown so T6 waiters don't hang.
    /// </summary>
    private void ClearPendingControlRequests()
    {
        List<TaskCompletionSource<HsmsHeader>> pending;
        lock (_pendingControlLock)
        {
            pending = [.. _pendingControlRequests.Values];
            _pendingControlRequests.Clear();
        }

        var ex = new InvalidOperationException("Connection closed while waiting for control response.");
        foreach (var tcs in pending)
        {
            tcs.TrySetException(ex);
        }
    }

    private void TransitionState(HsmsConnectionState newState)
    {
        bool changed;
        lock (_stateLock)
        {
            changed = _state != newState;
            _state = newState;
        }

        if (changed)
        {
            StateChanged?.Invoke(this, newState);
        }
    }

    private void DisconnectTcp()
    {
        try { _networkStream?.Close(); } catch { }
        try { _tcpClient?.Close(); } catch { }
        _networkStream = null;
        _tcpClient = null;
    }

    private uint NextSystemBytes() => Interlocked.Increment(ref _nextSystemBytes);

    /// <summary>
    /// Reads exactly <paramref name="count"/> bytes. Returns <c>false</c> if the remote
    /// closed the connection cleanly.
    /// When <paramref name="applyT8"/> is <c>true</c>, each individual read is bounded by
    /// the T8 intercharacter timeout (E37 §8.8).
    /// </summary>
    private async Task<bool> ReadExactAsync(
        NetworkStream stream,
        byte[] buffer,
        int count,
        CancellationToken cancellationToken,
        bool applyT8 = true)
    {
        int totalRead = 0;

        while (totalRead < count)
        {
            int bytesRead;

            if (applyT8)
            {
                using var t8Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                t8Cts.CancelAfter(TimeSpan.FromSeconds(_options.T8_NetworkIntercharacterTimeout));

                try
                {
                    bytesRead = await stream.ReadAsync(
                        buffer.AsMemory(totalRead, count - totalRead),
                        t8Cts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"T8: no data within {_options.T8_NetworkIntercharacterTimeout}s (E37 §8.8).");
                }
            }
            else
            {
                bytesRead = await stream.ReadAsync(
                    buffer.AsMemory(totalRead, count - totalRead),
                    cancellationToken);
            }

            if (bytesRead == 0)
            {
                return false;
            }

            totalRead += bytesRead;
        }

        return true;
    }

    /// <summary>
    /// Runs a fire-and-forget task; any unhandled exception is routed to <see cref="ErrorOccurred"/>.
    /// </summary>
    private void FireAndForget(Task task)
    {
        task.ContinueWith(
            t => ErrorOccurred?.Invoke(this, t.Exception!.GetBaseException()),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HsmsConnection));
        }
    }

    /// <summary>
    /// Asynchronously closes the connection and releases all resources.
    /// Prefer this over <see cref="Dispose"/> when awaiting is possible,
    /// so that the background task is properly awaited before cleanup.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await CloseAsync();
            Dispose();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _connectionCts?.Cancel();
            _connectionCts?.Dispose();
            StopT7Timer();
            ClearPendingControlRequests();
            _sendLock.Dispose();
            DisconnectTcp();
            _tcpListener?.Stop();
        }
    }
}
