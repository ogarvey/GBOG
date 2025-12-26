using System;
using System.IO;
using System.Text.Json;

namespace GBOG.Utils;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    public string? LastRomDirectory { get; set; }

    // Separate from LastRomDirectory; used for exports (tiles/BG/WIN/OBJ).
    public string? LastExportDirectory { get; set; }

    public bool AudioEnabled { get; set; } = true;
    public float AudioVolume { get; set; } = 1.0f;

    public float FontSizePixels { get; set; } = 16.0f;

    // Display palette affects only rendering (not emulation correctness).
    // Preset keys: DMG, Pocket, Green, Blue, Custom
    public string DisplayPalettePreset { get; set; } = DisplayPalettes.PresetDmg;

    // For dual-mode (CGB-supported, DMG-compatible) cartridges, prefer running in CGB mode.
    // CGB-only (0xC0) cartridges always run in CGB mode regardless of this setting.
    public bool PreferCgbWhenSupported { get; set; } = false;

    // Packed RGBA32: R in lowest byte, then G, B, A.
    public uint CustomPalette0 { get; set; } = 0xFFFFFFFF;
    public uint CustomPalette1 { get; set; } = 0xFFAAAAAA;
    public uint CustomPalette2 { get; set; } = 0xFF555555;
    public uint CustomPalette3 { get; set; } = 0xFF000000;

    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;

    // ImGuiTexInspect options (debug viewer)
    public bool InspectorShowGrid { get; set; } = true;
    public bool InspectorShowTooltip { get; set; } = true;
    public bool InspectorAutoReadTexture { get; set; } = true;
    public bool InspectorForceNearest { get; set; } = true;

    // 0=ImGui, 1=Black, 2=White, 3=Checkered, 4=CustomColor
    public int InspectorAlphaMode { get; set; } = 0;
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string GetSettingsPath()
    {
        // Use LocalAppData so settings follow the user but stay machine-local.
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dir = Path.Combine(root, "GBOG");
        return Path.Combine(dir, "settings.json");
    }

    public static AppSettings Load()
    {
        string path = GetSettingsPath();
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        string path = GetSettingsPath();
        string dir = Path.GetDirectoryName(path) ?? ".";
        Directory.CreateDirectory(dir);

        string tmp = path + ".tmp";
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(tmp, json);

        // Replace atomically when possible.
        if (File.Exists(path))
        {
            File.Replace(tmp, path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tmp, path);
        }
    }
}
