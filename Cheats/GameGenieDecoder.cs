using System;
using System.Linq;

namespace GBOG.Cheats;

public static class GameGenieDecoder
{
    /// <summary>
    /// Attempts to decode a Game Boy Game Genie code (6 or 9 hexadecimal characters, e.g. "ABC-DEF" or "ABC-DEF-GHI").
    /// According to Pan Docs (https://gbdev.io/pandocs/Shark_Cheats.html):
    /// - AB: new replacement data
    /// - FCDE: memory address XORed by $F000 (address must be in ROM range $0000-$7FFF, so F must be 8-F)
    /// - GI: original compare data (XORed by $BA and rotated left by two)
    /// - H: checksum digit (usually G ^ 8)
    /// </summary>
    public static bool TryDecode(
        string input,
        out ushort address,
        out byte newValue,
        out bool hasCompare,
        out byte compareValue,
        out string formattedCode,
        out string? warning,
        out string? error)
    {
        address = 0;
        newValue = 0;
        hasCompare = false;
        compareValue = 0;
        formattedCode = string.Empty;
        warning = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Code is empty.";
            return false;
        }

        string clean = new string(input.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        if (clean.Length != 6 && clean.Length != 9)
        {
            error = $"Invalid Game Genie length ({clean.Length} chars). Expected 6 chars (ABC-DEF) or 9 chars (ABC-DEF-GHI).";
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

        int valA = HexVal(clean[0]);
        int valB = HexVal(clean[1]);
        int valC = HexVal(clean[2]);
        int valD = HexVal(clean[3]);
        int valE = HexVal(clean[4]);
        int valF = HexVal(clean[5]);

        newValue = (byte)((valA << 4) | valB);

        int addrTop = valF ^ 0xF;
        if (addrTop > 7)
        {
            error = $"6th character '{clean[5]}' maps to address high nibble 0x{addrTop:X1}, which exceeds Game Boy ROM space $0000-$7FFF. Character 6 must be 8-F.";
            return false;
        }

        address = (ushort)((addrTop << 12) | (valC << 8) | (valD << 4) | valE);

        if (clean.Length == 6)
        {
            hasCompare = false;
            compareValue = 0;
            formattedCode = $"{clean[0]}{clean[1]}{clean[2]}-{clean[3]}{clean[4]}{clean[5]}";
            return true;
        }

        // 9-digit format with compare byte
        hasCompare = true;
        int valG = HexVal(clean[6]);
        int valH = HexVal(clean[7]);
        int valI = HexVal(clean[8]);

        byte num = (byte)((valG << 4) | valI);
        byte rotated = (byte)((num >> 2) | ((num & 3) << 6));
        compareValue = (byte)(rotated ^ 0xBA);

        formattedCode = $"{clean[0]}{clean[1]}{clean[2]}-{clean[3]}{clean[4]}{clean[5]}-{clean[6]}{clean[7]}{clean[8]}";

        int checksum = valG ^ valH;
        if (checksum != 8 && checksum != 0)
        {
            warning = $"Checksum nibble '{clean[7]}' does not match standard G^8 check (expected 0x{(valG ^ 8):X1}). Code may contain a typo.";
        }

        return true;
    }

    /// <summary>
    /// Encodes a ROM patch into Game Boy Game Genie format.
    /// </summary>
    public static string Encode(ushort address, byte newValue, byte? compareValue = null)
    {
        if (address >= 0x8000)
        {
            throw new ArgumentOutOfRangeException(nameof(address), "Game Genie addresses must be in ROM range 0x0000-0x7FFF.");
        }

        char a = HexChar((newValue >> 4) & 0xF);
        char b = HexChar(newValue & 0xF);
        char c = HexChar((address >> 8) & 0xF);
        char d = HexChar((address >> 4) & 0xF);
        char e = HexChar(address & 0xF);
        char f = HexChar(0xF - ((address >> 12) & 0xF));

        if (!compareValue.HasValue)
        {
            return $"{a}{b}{c}-{d}{e}{f}";
        }

        byte orig = compareValue.Value;
        byte temp = (byte)(orig ^ 0x45);
        byte num = (byte)(((temp << 2) | (temp >> 6)) ^ 0xFF);
        byte gNib = (byte)((num >> 4) & 0xF);
        byte hNib = (byte)(gNib ^ 0x08);
        byte iNib = (byte)(num & 0xF);

        return $"{a}{b}{c}-{d}{e}{f}-{HexChar(gNib)}{HexChar(hNib)}{HexChar(iNib)}";
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

    private static char HexChar(int nibble)
    {
        nibble &= 0xF;
        return nibble < 10 ? (char)('0' + nibble) : (char)('A' + (nibble - 10));
    }
}
