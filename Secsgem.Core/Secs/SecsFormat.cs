namespace Secsgem.Core.Secs;

/// <summary>
/// SECS-II item format codes (SEMI E5 §9).
///
/// These values correspond to the lower bits of the SECS-II format byte
/// (as shown in the SEMI E5 table: Binary/Octal columns). The codec will
/// combine these with the length-of-length bits when emitting bytes.
/// </summary>
public enum SecsFormat : byte
{
    // From SEMI E5 "Table 1 Item Format Codes" (Binary/Octal).
    List       = 0x00, // 000000 (octal 00)
    Binary     = 0x08, // 001000 (octal 10)
    Boolean    = 0x09, // 001001 (octal 11)
    Ascii      = 0x10, // 010000 (octal 20)
    Jis        = 0x11, // 010001 (octal 21)
    Char2      = 0x12, // 010010 (octal 22) 2 byte character
    Int8       = 0x18, // 011000 (octal 30) 8‑byte signed integer
    Int1       = 0x19, // 011001 (octal 31) 1‑byte signed integer
    Int2       = 0x1A, // 011010 (octal 32) 2‑byte signed integer
    Int4       = 0x1C, // 011100 (octal 34) 4‑byte signed integer
    Float8     = 0x20, // 100000 (octal 40) 8‑byte floating point
    Float4     = 0x24, // 100100 (octal 44) 4‑byte floating point
    Uint8      = 0x28, // 101000 (octal 50) 8‑byte unsigned integer
    Uint1      = 0x29, // 101001 (octal 51) 1‑byte unsigned integer
    Uint2      = 0x2A, // 101010 (octal 52) 2‑byte unsigned integer
    Uint4      = 0x2C  // 101100 (octal 54) 4‑byte unsigned integer
}

