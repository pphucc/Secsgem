using Secsgem.Core.Transport;

namespace Secsgem.Samples;

/// <summary>
/// Runs a single HSMS endpoint (Equipment or Host) for integration testing with external tools.
/// </summary>
internal static class StandaloneDemo
{
    /// <summary>
    /// Runs a Passive (Equipment) endpoint that listens for incoming connections.
    /// Responds to all HSMS control messages automatically.
    /// </summary>
    public static async Task RunEquipmentAsync(int port, bool logRawFrames, CancellationToken ct)
    {
        Console.WriteLine($"[Equipment] Passive — listening on port {port}");
        Console.WriteLine("Press Ctrl+C to stop.\n");

        var options = new HsmsConnectionOptions
        {
            Host = "0.0.0.0",
            Port = port,
            Mode = HsmsConnectionMode.Passive
        };

        await using var connection = new HsmsConnectionLogger("Equipment", new HsmsConnection(options), logRawFrames);
        await connection.OpenAsync(ct);

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException) { }

        await connection.CloseAsync();
    }

    /// <summary>
    /// Runs an Active (Host) endpoint that connects to the remote equipment.
    /// Sends LINKTEST.req periodically once Selected.
    /// </summary>
    public static async Task RunHostAsync(string host, int port, int linktestIntervalSeconds, bool logRawFrames, CancellationToken ct)
    {
        Console.WriteLine($"[Host] Active — connecting to {host}:{port}");
        Console.WriteLine($"[Host] LINKTEST interval: {linktestIntervalSeconds}s");
        Console.WriteLine("Press Ctrl+C to stop.\n");

        var options = new HsmsConnectionOptions
        {
            Host = host,
            Port = port,
            Mode = HsmsConnectionMode.Active,
            T5_ConnectSeparationTimeout = 5
        };

        await using var connection = new HsmsConnectionLogger("Host", new HsmsConnection(options), logRawFrames);
        await connection.OpenAsync(ct);

        try
        {
            // Send periodic LINKTESTs while connected and selected
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(linktestIntervalSeconds), ct);

                if (connection.State == HsmsConnectionState.Selected)
                {
                    Console.WriteLine("[Host] → LINKTEST.req");
                    await connection.SendLinktestRequestAsync(ct);
                }
            }
        }
        catch (OperationCanceledException) { }

        await connection.CloseAsync();
    }
}
