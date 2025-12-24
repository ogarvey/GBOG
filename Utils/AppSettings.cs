using System;
using System.IO;
using System.Text.Json;

namespace GBOG.Utils;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    public string? LastRomDirectory { get; set; }

    public bool AudioEnabled { get; set; } = true;
    public float AudioVolume { get; set; } = 1.0f;

    public float FontSizePixels { get; set; } = 16.0f;

    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;
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
