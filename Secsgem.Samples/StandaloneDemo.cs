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
    public static async Task RunEquipmentAsync(string address, int port, bool logRawFrames, CancellationToken ct)
    {
        Console.WriteLine($"[Equipment] Passive — started listening on {address}:{port}");
        Console.WriteLine("Press Ctrl+C to stop.\n");

        var options = new HsmsConnectionOptions
        {
            LocalAddress = address,
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
    public static async Task RunHostAsync(string remoteAddress, int port, bool logRawFrames, CancellationToken ct)
    {
        Console.WriteLine($"[Host] Active — connecting to {remoteAddress}:{port}");
        Console.WriteLine("Press Ctrl+C to stop.\n");

        var options = new HsmsConnectionOptions
        {
            RemoteAddress = remoteAddress,
            Port = port,
            Mode = HsmsConnectionMode.Active,
        };

        await using var connection = new HsmsConnectionLogger("Host", new HsmsConnection(options), logRawFrames);
        await connection.OpenAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);

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
