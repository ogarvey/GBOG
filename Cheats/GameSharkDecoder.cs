using System;
using System.Linq;

namespace GBOG.Cheats;

public static class GameSharkDecoder
{
    /// <summary>
    /// Attempts to decode a Game Boy GameShark code (8 hexadecimal characters, e.g. "010238CD" or "0102-38CD").
    /// According to Pan Docs (https://gbdev.io/pandocs/Shark_Cheats.html):
    /// - 0..1: SRAM bank (or type)
    /// - 2..3: New value to write
    /// - 4..7: Address in little-endian format (4..5 low byte, 6..7 high byte)
    /// Example: 010238CD writes 0x02 to address 0xCD38 (bank 0x01).
    /// </summary>
    public static bool TryDecode(
        string input,
        out ushort address,
        out byte newValue,
        out byte bank,
        out string formattedCode,
        out string? error)
    {
        address = 0;
        newValue = 0;
        bank = 0;
        formattedCode = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Code is empty.";
            return false;
        }

        string clean = new string(input.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        if (clean.Length != 8)
        {
            error = $"Invalid GameShark length ({clean.Length} chars). Expected 8 hexadecimal chars (TTNNAAAA or TTNN-AAAA).";
            return false;
        }

        for (int i = 0; i < clean.Length; i++)
        {
            if (!IsHexChar(clean[i]))
            {
                error = $"Invalid hexadecimal character '{clean[i]}' at position {i + 1}.";
                return false;
            }
        }

        bank = (byte)((HexVal(clean[0]) << 4) | HexVal(clean[1]));
        newValue = (byte)((HexVal(clean[2]) << 4) | HexVal(clean[3]));

        byte addrLow = (byte)((HexVal(clean[4]) << 4) | HexVal(clean[5]));
        byte addrHigh = (byte)((HexVal(clean[6]) << 4) | HexVal(clean[7]));
        address = (ushort)((addrHigh << 8) | addrLow);

        formattedCode = $"{clean[0]}{clean[1]}{clean[2]}{clean[3]}-{clean[4]}{clean[5]}{clean[6]}{clean[7]}";
        return true;
    }

    /// <summary>
    /// Encodes a RAM patch into Game Boy GameShark format.
    /// </summary>
    public static string Encode(ushort address, byte newValue, byte bank = 1)
    {
        byte addrLow = (byte)(address & 0xFF);
        byte addrHigh = (byte)((address >> 8) & 0xFF);
        return $"{bank:X2}{newValue:X2}-{addrLow:X2}{addrHigh:X2}";
    }

    private static bool IsHexChar(char c) =>
        (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');

    private static int HexVal(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        return 0;
    }
}
