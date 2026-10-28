using System;
using System.Collections.Generic;
using System.Linq;

namespace GBOG.Cheats;

public static class CheatDecoder
{
    public sealed class LinePreviewResult
    {
        public int LineNumber { get; set; }
        public string RawText { get; set; } = string.Empty;
        public bool IsCommentOrEmpty { get; set; }
        public CheatLine? ParsedLine { get; set; }
        public string? Error { get; set; }
    }

    public static bool TryDecodeSingleLine(string rawLine, out CheatLine line, out string? error)
    {
        line = new CheatLine();
        error = null;

        if (string.IsNullOrWhiteSpace(rawLine))
        {
            error = "Line is empty.";
            line.IsValid = false;
            line.ErrorMessage = error;
            return false;
        }

        string clean = new string(rawLine.Where(char.IsLetterOrDigit).ToArray());

        if (clean.Length == 6 || clean.Length == 9)
        {
            if (GameGenieDecoder.TryDecode(
                clean,
                out ushort address,
                out byte newValue,
                out bool hasCompare,
                out byte compareValue,
                out string formattedCode,
                out string? warning,
                out string? ggError))
            {
                line.Type = CheatType.GameGenie;
                line.Code = formattedCode;
                line.Address = address;
                line.Value = newValue;
                line.HasCompare = hasCompare;
                line.CompareValue = compareValue;
                line.IsValid = true;
                line.WarningMessage = warning;
                return true;
            }
            else
            {
                error = ggError;
                line.IsValid = false;
                line.ErrorMessage = error;
                return false;
            }
        }
        else if (clean.Length == 8)
        {
            if (GameSharkDecoder.TryDecode(
                clean,
                out ushort address,
                out byte newValue,
                out byte bank,
                out string formattedCode,
                out string? gsError))
            {
                line.Type = CheatType.GameShark;
                line.Code = formattedCode;
                line.Address = address;
                line.Value = newValue;
                line.Bank = bank;
                line.HasCompare = false;
                line.IsValid = true;
                return true;
            }
            else
            {
                error = gsError;
                line.IsValid = false;
                line.ErrorMessage = error;
                return false;
            }
        }

        error = $"Unknown cheat code format ({clean.Length} hex chars). Expected 6 or 9 chars for Game Genie (e.g. 21C-53E-4C1) or 8 chars for GameShark (e.g. 010238CD).";
        line.IsValid = false;
        line.ErrorMessage = error;
        return false;
    }

    /// <summary>
    /// Parses a multi-line input text as a single grouped CheatEntry.
    /// Blank lines and lines starting with #, //, or ; are ignored.
    /// </summary>
    public static bool TryDecodeMultiLine(
        string rawInput,
        string description,
        out CheatEntry entry,
        out List<string> errors)
    {
        entry = new CheatEntry
        {
            Description = description.Trim()
        };
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(rawInput))
        {
            errors.Add("Input code is empty.");
            entry.IsValid = false;
            return false;
        }

        var rawLines = rawInput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int lineNum = 0;

        foreach (var rawLine in rawLines)
        {
            lineNum++;
            string trimmed = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            if (trimmed.StartsWith("//") || trimmed.StartsWith("#") || trimmed.StartsWith(";")) continue;

            if (TryDecodeSingleLine(trimmed, out var cheatLine, out var err))
            {
                entry.Lines.Add(cheatLine);
            }
            else
            {
                errors.Add($"Line {lineNum} ('{trimmed}'): {err}");
            }
        }

        if (entry.Lines.Count == 0 && errors.Count == 0)
        {
            errors.Add("No cheat codes found in input.");
            entry.IsValid = false;
            return false;
        }

        if (errors.Count > 0)
        {
            entry.IsValid = false;
            entry.ErrorMessage = string.Join("; ", errors);
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.Description))
        {
            entry.Description = entry.Lines.Count == 1
                ? $"{entry.Lines[0].Type} Cheat ({entry.Lines[0].Code})"
                : $"Multi-line Cheat ({entry.Lines.Count} lines)";
        }

        entry.IsValid = true;
        return true;
    }

    /// <summary>
    /// Parses each line of a multi-line input as an independent CheatEntry.
    /// </summary>
    public static bool TryDecodeMultipleEntries(
        string rawInput,
        string baseDescription,
        out List<CheatEntry> entries,
        out List<string> errors)
    {
        entries = new List<CheatEntry>();
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(rawInput))
        {
            errors.Add("Input code is empty.");
            return false;
        }

        var rawLines = rawInput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int lineNum = 0;

        foreach (var rawLine in rawLines)
        {
            lineNum++;
            string trimmed = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            if (trimmed.StartsWith("//") || trimmed.StartsWith("#") || trimmed.StartsWith(";")) continue;

            if (TryDecodeSingleLine(trimmed, out var cheatLine, out var err))
            {
                string desc = string.IsNullOrWhiteSpace(baseDescription)
                    ? $"{cheatLine.Type} ({cheatLine.Code})"
                    : (entries.Count == 0 ? baseDescription.Trim() : $"{baseDescription.Trim()} (Line {lineNum})");

                var item = new CheatEntry
                {
                    Description = desc,
                    Enabled = true,
                    IsValid = true
                };
                item.Lines.Add(cheatLine);
                entries.Add(item);
            }
            else
            {
                errors.Add($"Line {lineNum} ('{trimmed}'): {err}");
            }
        }

        return entries.Count > 0 && errors.Count == 0;
    }

    /// <summary>
    /// Analyzes each line for real-time UI preview feedback.
    /// </summary>
    public static List<LinePreviewResult> ParseLinesPreview(string rawInput)
    {
        var results = new List<LinePreviewResult>();
        if (string.IsNullOrWhiteSpace(rawInput)) return results;

        var rawLines = rawInput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int lineNum = 0;

        foreach (var rawLine in rawLines)
        {
            lineNum++;
            string trimmed = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//") || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
            {
                results.Add(new LinePreviewResult
                {
                    LineNumber = lineNum,
                    RawText = trimmed,
                    IsCommentOrEmpty = true
                });
                continue;
            }

            if (TryDecodeSingleLine(trimmed, out var line, out var err))
            {
                results.Add(new LinePreviewResult
                {
                    LineNumber = lineNum,
                    RawText = trimmed,
                    ParsedLine = line
                });
            }
            else
            {
                results.Add(new LinePreviewResult
                {
                    LineNumber = lineNum,
                    RawText = trimmed,
                    Error = err
                });
            }
        }

        return results;
    }

    public static bool TryDecode(string rawInput, string description, out CheatEntry entry, out string? error)
    {
        bool success = TryDecodeMultiLine(rawInput, description, out entry, out var errors);
        error = errors.Count > 0 ? string.Join("\n", errors) : null;
        return success;
    }

    public static string GetAddressRegionName(ushort address)
    {
        if (address < 0x4000) return "ROM0";
        if (address < 0x8000) return "ROMX";
        if (address < 0xA000) return "VRAM";
        if (address < 0xC000) return "SRAM";
        if (address < 0xD000) return "WRAM0";
        if (address < 0xE000) return "WRAMX";
        if (address < 0xFE00) return "ECHO";
        if (address < 0xFEA0) return "OAM";
        if (address < 0xFF80) return "IO";
        if (address < 0xFFFF) return "HRAM";
        return "IE";
    }
}
