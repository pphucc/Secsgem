using Secsgem.Core.Transport;

namespace Secsgem.Samples;

internal static class Util
{
    /// <summary>Polls until <paramref name="connection"/> reaches <paramref name="desired"/> state or timeout.</summary>
    public static async Task WaitForStateAsync(
        IHsmsConnection connection,
        HsmsConnectionState desired,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked     = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        while (connection.State != desired)
        {
            linked.Token.ThrowIfCancellationRequested();
            await Task.Delay(50, linked.Token);
        }
    }
}
