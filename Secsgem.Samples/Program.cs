using Secsgem.Samples;

// ---------------------------------------------------------------------------
// Secsgem.Samples — HSMS demo runner
//
// Usage:
//   dotnet run -- loopback   [--port 5000] [--raw]
//   dotnet run -- equipment  [--port 5000] [--raw]
//   dotnet run -- host       [--host 127.0.0.1] [--port 5000] [--linktest 5] [--raw]
//
// --raw   prints every TX/RX wire frame as hex bytes
// ---------------------------------------------------------------------------

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

string mode   = Arg(args, 0, "loopback");
string remote = ArgNamed(args, "--host",     "127.0.0.1");
bool   raw    = Array.Exists(args, a => a == "--raw");

var address = "127.0.0.1";
var port = 5000;

switch (mode.ToLowerInvariant())
{
    case "loopback":
        await LoopbackDemo.RunAsync(address, port, raw, cts.Token);
        break;

    case "equipment":
        await StandaloneDemo.RunEquipmentAsync(address, port, raw, cts.Token);
        break;

    case "host":
        await StandaloneDemo.RunHostAsync(remote, port, 5, raw, cts.Token);
        break;

    default:
        Console.Error.WriteLine($"Unknown mode: '{mode}'");
        PrintUsage();
        Environment.Exit(1);
        break;
}

Console.WriteLine("\nDone.");

static string Arg(string[] a, int index, string fallback)
    => a.Length > index ? a[index] : fallback;

static string ArgNamed(string[] a, string name, string fallback)
{
    int i = Array.IndexOf(a, name);
    return (i >= 0 && i + 1 < a.Length) ? a[i + 1] : fallback;
}

static void PrintUsage()
{
    Console.WriteLine("""
        Usage:
          dotnet run -- loopback   [--port 5000] [--raw]
          dotnet run -- equipment  [--port 5000] [--raw]
          dotnet run -- host       [--host 127.0.0.1] [--port 5000] [--linktest 5] [--raw]
        """);
}
