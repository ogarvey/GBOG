using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GBOG.Cheats;

public static class CheatsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string GetCheatsDirectory()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dir = Path.Combine(root, "GBOG", "cheats");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }
        return new string(chars);
    }

    public static string GetCheatFilePath(string romIdentifier)
    {
        string safeName = SanitizeFileName(romIdentifier);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "default";
        }
        return Path.Combine(GetCheatsDirectory(), safeName + ".json");
    }

    public static List<CheatEntry> LoadCheats(string romIdentifier)
    {
        try
        {
            string path = GetCheatFilePath(romIdentifier);
            if (!File.Exists(path))
            {
                return new List<CheatEntry>();
            }

            string json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<CheatEntry>>(json, JsonOptions);
            if (list == null) return new List<CheatEntry>();

            // Re-validate and populate addresses/values for loaded cheats
            foreach (var cheat in list)
            {
                cheat.EnsureLinesFromLegacy();
                if (cheat.Lines.Count == 0 && !string.IsNullOrWhiteSpace(cheat.Code))
                {
                    if (CheatDecoder.TryDecodeMultiLine(cheat.Code, cheat.Description, out var decoded, out _))
                    {
                        cheat.Lines = decoded.Lines;
                        cheat.IsValid = true;
                    }
                }
            }

            return list;
        }
        catch
        {
            return new List<CheatEntry>();
        }
    }

    public static void SaveCheats(string romIdentifier, List<CheatEntry> cheats)
    {
        try
        {
            string path = GetCheatFilePath(romIdentifier);
            string tmp = path + ".tmp";
            string json = JsonSerializer.Serialize(cheats, JsonOptions);
            File.WriteAllText(tmp, json);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(tmp, path);
        }
        catch
        {
            // best-effort persistence
        }
    }
}
