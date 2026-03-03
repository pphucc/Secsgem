using Secsgem.Core.Transport;

namespace Secsgem.Samples;

/// <summary>
/// Decorator that logs all <see cref="IHsmsConnection"/> events and raw frames to the console.
/// </summary>
internal sealed class HsmsConnectionLogger : IHsmsConnection, IAsyncDisposable
{
    private readonly string _name;
    private readonly HsmsConnection _inner;
    private readonly bool _logRawFrames;

    public HsmsConnectionLogger(string name, HsmsConnection inner, bool logRawFrames = false)
    {
        _name         = name;
        _inner        = inner;
        _logRawFrames = logRawFrames;

        _inner.StateChanged  += OnStateChanged;
        _inner.DataReceived  += OnDataReceived;
        _inner.ErrorOccurred += OnErrorOccurred;

        if (_logRawFrames)
        {
            _inner.RawMessageSent     += OnRawSent;
            _inner.RawMessageReceived += OnRawReceived;
        }
    }

    public HsmsConnectionMode  Mode  => _inner.Mode;
    public HsmsConnectionState State => _inner.State;

    public Task OpenAsync(CancellationToken ct = default)                               => _inner.OpenAsync(ct);
    public Task CloseAsync(CancellationToken ct = default)                              => _inner.CloseAsync(ct);
    public Task SendRawAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => _inner.SendRawAsync(data, ct);
    public Task SendSelectRequestAsync(CancellationToken ct = default)                  => _inner.SendSelectRequestAsync(ct);
    public Task SendSelectResponseAsync(byte status, CancellationToken ct = default)    => _inner.SendSelectResponseAsync(status, ct);
    public Task SendDeselectRequestAsync(CancellationToken ct = default)                => _inner.SendDeselectRequestAsync(ct);
    public Task SendLinktestRequestAsync(CancellationToken ct = default)                => _inner.SendLinktestRequestAsync(ct);
    public Task SendSeparateRequestAsync(CancellationToken ct = default)                => _inner.SendSeparateRequestAsync(ct);

    public event EventHandler<ReadOnlyMemory<byte>>? DataReceived  { add => _inner.DataReceived  += value; remove => _inner.DataReceived  -= value; }
    public event EventHandler<HsmsConnectionState>?  StateChanged  { add => _inner.StateChanged  += value; remove => _inner.StateChanged  -= value; }
    public event EventHandler<Exception>?            ErrorOccurred { add => _inner.ErrorOccurred += value; remove => _inner.ErrorOccurred -= value; }

    private void OnStateChanged(object? sender, HsmsConnectionState state)
        => Console.WriteLine($"State → {state}");

    private void OnDataReceived(object? sender, ReadOnlyMemory<byte> data)
    {
        // Phase 1: SECS-II codec not yet implemented — log raw size only.
        Console.WriteLine($"Data message received ({data.Length} bytes)");
    }

    private void OnErrorOccurred(object? sender, Exception ex)
        => Console.WriteLine($"ERROR: {ex.Message}");

    private void OnRawSent(object? sender, ReadOnlyMemory<byte> frame)
        => PrintFrame($"TX", frame.Span);

    private void OnRawReceived(object? sender, ReadOnlyMemory<byte> frame)
        => PrintFrame($"RX", frame.Span);

    private static void PrintFrame(string prefix, ReadOnlySpan<byte> frame)
    {
        // Format: [Name] TX|RX  00 00 00 0a  ff ff 00 00  00 05 00 00 00 01
        //                       └─ length ─┘  └─ header bytes ──────────────┘
        var sb = new System.Text.StringBuilder();
        sb.Append(prefix);
        sb.Append("  ");

        for (int i = 0; i < frame.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                sb.Append("  ");
            }

            sb.Append(frame[i].ToString("x2"));
            sb.Append(' ');
        }

        Console.WriteLine(sb.ToString().TrimEnd());
    }

    public async ValueTask DisposeAsync()
    {
        _inner.StateChanged  -= OnStateChanged;
        _inner.DataReceived  -= OnDataReceived;
        _inner.ErrorOccurred -= OnErrorOccurred;

        if (_logRawFrames)
        {
            _inner.RawMessageSent     -= OnRawSent;
            _inner.RawMessageReceived -= OnRawReceived;
        }

        await _inner.CloseAsync();
        _inner.Dispose();
    }
}
