using Secsgem.Core.Transport;

namespace Secsgem.Samples;

/// <summary>
/// Runs both Equipment (Passive) and Host (Active) in the same process over loopback.
/// Useful for verifying the HSMS stack without any external tool.
/// </summary>
internal static class LoopbackDemo
{
    public static async Task RunAsync(int port, bool logRawFrames, CancellationToken ct)
    {
        Console.WriteLine($"[Loopback] Equipment (Passive) on port {port}");
        Console.WriteLine($"[Loopback] Host (Active) → 127.0.0.1:{port}");
        Console.WriteLine();

        var equipmentOptions = new HsmsConnectionOptions
        {
            Host = "127.0.0.1",
            Port = port,
            Mode = HsmsConnectionMode.Passive
        };

        var hostOptions = new HsmsConnectionOptions
        {
            Host = "127.0.0.1",
            Port = port,
            Mode = HsmsConnectionMode.Active,
            T5_ConnectSeparationTimeout = 2
        };

        await using var equipment = new HsmsConnectionLogger("Equipment", new HsmsConnection(equipmentOptions), logRawFrames);
        await using var host      = new HsmsConnectionLogger("Host",      new HsmsConnection(hostOptions),      logRawFrames);

        await equipment.OpenAsync(ct);
        await Task.Delay(200, ct);
        await host.OpenAsync(ct);

        Console.WriteLine("Running... Press Ctrl+C to stop, or demo ends in ~15 s.\n");

        try
        {
            await Util.WaitForStateAsync(host, HsmsConnectionState.Selected, TimeSpan.FromSeconds(10), ct);

            for (int i = 0; i < 3; i++)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);

                if (host.State == HsmsConnectionState.Selected)
                {
                    Console.WriteLine("[Host] → LINKTEST.req");
                    await host.SendLinktestRequestAsync(ct);
                }
            }

            if (host.State == HsmsConnectionState.Selected)
            {
                Console.WriteLine("\n[Host] → SEPARATE.req (closing)");
                await host.SendSeparateRequestAsync(ct);
                await Task.Delay(500, ct);
            }
        }
        catch (OperationCanceledException) { }

        await host.CloseAsync();
        await equipment.CloseAsync();
    }
}
