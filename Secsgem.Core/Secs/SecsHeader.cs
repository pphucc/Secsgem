using System;

namespace Secsgem.Core.Secs;

/// <summary>
/// SECS-II message header (mapped onto the HSMS header fields).
/// </summary>
public readonly struct SecsHeader
{
    public SecsHeader(byte stream, byte function, bool wBit, ushort deviceId, uint systemBytes)
    {
        Stream = stream;
        Function = function;
        WBit = wBit;
        DeviceId = deviceId;
        SystemBytes = systemBytes;
    }

    /// <summary>SECS-II stream number (1–127).</summary>
    public byte Stream { get; }

    /// <summary>SECS-II function number (0–255).</summary>
    public byte Function { get; }

    /// <summary>W-bit: true for primary messages expecting a reply; false for secondary messages.</summary>
    public bool WBit { get; }

    /// <summary>Device ID (E5).</summary>
    public ushort DeviceId { get; }

    /// <summary>System bytes, used to correlate request/response.</summary>
    public uint SystemBytes { get; }

    public override string ToString() =>
        $"S{Stream}F{Function} W={(WBit ? 1 : 0)} Dev={DeviceId} Sys={SystemBytes}";
}

