using GBOG.CPU;

namespace GBOG.Utils;

public readonly struct DisplayPalette
{
    public readonly Color Shade0;
    public readonly Color Shade1;
    public readonly Color Shade2;
    public readonly Color Shade3;

    public DisplayPalette(Color shade0, Color shade1, Color shade2, Color shade3)
    {
        Shade0 = shade0;
        Shade1 = shade1;
        Shade2 = shade2;
        Shade3 = shade3;
    }

    public Color GetShade(int index)
    {
        return index switch
        {
            0 => Shade0,
            1 => Shade1,
            2 => Shade2,
            3 => Shade3,
            _ => Color.Fallback,
        };
    }
}

public static class DisplayPalettes
{
    // Keys used for settings/UI.
    public const string PresetDmg = "DMG";
    public const string PresetPocket = "Pocket";
    public const string PresetGreen = "Green";
    public const string PresetBlue = "Blue";
    public const string PresetCustom = "Custom";

    // Default palette keeps the exact grayscale levels used historically.
    public static readonly DisplayPalette Dmg = new(
        Color.White,
        Color.LightGray,
        Color.DarkGray,
        Color.Black);

    // Predefined palettes are common "DMG-style" screen tints.
    public static readonly DisplayPalette Pocket = new(
        new Color { R = 0xE0, G = 0xE0, B = 0xD0, A = 0xFF },
        new Color { R = 0xB0, G = 0xB0, B = 0xA0, A = 0xFF },
        new Color { R = 0x70, G = 0x70, B = 0x60, A = 0xFF },
        new Color { R = 0x20, G = 0x20, B = 0x18, A = 0xFF });

    public static readonly DisplayPalette Green = new(
        new Color { R = 0xE0, G = 0xF8, B = 0xD0, A = 0xFF },
        new Color { R = 0x88, G = 0xC0, B = 0x70, A = 0xFF },
        new Color { R = 0x34, G = 0x68, B = 0x56, A = 0xFF },
        new Color { R = 0x08, G = 0x18, B = 0x20, A = 0xFF });

    public static readonly DisplayPalette Blue = new(
        new Color { R = 0xE8, G = 0xF4, B = 0xFF, A = 0xFF },
        new Color { R = 0xA8, G = 0xC8, B = 0xF0, A = 0xFF },
        new Color { R = 0x58, G = 0x78, B = 0xA8, A = 0xFF },
        new Color { R = 0x18, G = 0x28, B = 0x48, A = 0xFF });

    public static DisplayPalette GetPreset(string? key)
    {
        return key switch
        {
            PresetPocket => Pocket,
            PresetGreen => Green,
            PresetBlue => Blue,
            _ => Dmg,
        };
    }
}
