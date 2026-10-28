using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using GBOG.Cheats;
using GBOG.CPU;
using Hexa.NET.ImGui;

namespace GBOG.Graphics.UI;

public sealed class ImGuiCheatsWindow
{
    private string _inputDescription = string.Empty;
    private string _inputCode = string.Empty;
    private string _statusMessage = string.Empty;
    private string? _lastLoadedRomKey;
    private string? _lastLoadedRomDisplayName;

    private readonly struct PresetCheat
    {
        public readonly string Game;
        public readonly string Name;
        public readonly string Code;
        public readonly string Description;

        public PresetCheat(string game, string name, string code, string description)
        {
            Game = game;
            Name = name;
            Code = code;
            Description = description;
        }
    }

    private static readonly PresetCheat[] CommonPresets = new[]
    {
        new PresetCheat("Zelda: Link's Awakening", "Max Rupees (GS, 2-line)", "01FF7DC4\n01FF7EC4", "Zelda: LA - 999 Rupees (16-bit)"),
        new PresetCheat("Zelda: Link's Awakening", "Infinite Hearts (GG)", "004-94B-3BE", "Zelda: LA - Infinite Health/Hearts"),
        new PresetCheat("Super Mario Land", "Infinite Lives (GG)", "21C-53E-4C1", "Super Mario Land - Infinite Lives"),
        new PresetCheat("Super Mario Land", "Start with 10 Lives (GG)", "09D-49C-E62", "Super Mario Land - Start with 10 Lives"),
        new PresetCheat("Super Mario Land", "Always Super Mario (GS)", "010214DA", "Super Mario Land - Always Super Mario"),
        new PresetCheat("Pokemon Red/Blue", "Max Money $999,999 (GS, 3-line)", "019946D3\n019947D3\n019948D3", "Pokemon Red/Blue - Max Money ($999,999)"),
        new PresetCheat("Pokemon Red/Blue", "Walk Through Walls (GS, 2-line)", "010138CD\n010139CD", "Pokemon Red/Blue - Walk Through Walls"),
        new PresetCheat("Tetris", "Always I-Tetromino (Line) (GS)", "010042C2", "Tetris - Always receive Line Piece"),
    };

    public void LoadCheatsForRom(Gameboy? gb, string? romPath, string? romName)
    {
        if (gb == null || string.IsNullOrWhiteSpace(romPath))
        {
            return;
        }

        string romKey = GetRomKey(gb, romPath, romName);
        _lastLoadedRomKey = romKey;
        _lastLoadedRomDisplayName = GetDisplayName(gb, romName);

        var savedCheats = CheatsStore.LoadCheats(romKey);
        gb.CheatManager.LoadCheats(savedCheats);
    }

    public void Render(ref bool show, Gameboy? gb, string? romName, string? romPath)
    {
        if (!show) return;

        ImGui.SetNextWindowSize(new Vector2(780, 620), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("Cheats (Game Genie & GameShark)###CheatsWindow", ref show))
        {
            ImGui.End();
            return;
        }

        // Auto-synchronize ROM switch if active
        if (gb != null && !string.IsNullOrWhiteSpace(romPath))
        {
            string currentKey = GetRomKey(gb, romPath, romName);
            if (currentKey != _lastLoadedRomKey)
            {
                LoadCheatsForRom(gb, romPath, romName);
            }
        }

        RenderHeader(gb);
        ImGui.Separator();

        RenderAddCheatSection(gb);
        ImGui.Separator();

        RenderCheatsTable(gb);
        ImGui.Separator();

        RenderBottomBar(gb);
        ImGui.Separator();

        RenderDocumentationSection();

        ImGui.End();
    }

    private void RenderHeader(Gameboy? gb)
    {
        string romDisplay = _lastLoadedRomDisplayName ?? "No ROM Loaded";
        bool hasRom = gb != null;

        ImGui.Text("Active Game:");
        ImGui.SameLine();
        if (hasRom)
        {
            ImGui.TextColored(new Vector4(0.3f, 0.85f, 1.0f, 1.0f), romDisplay);
        }
        else
        {
            ImGui.TextDisabled("No ROM Loaded (Cheats will apply automatically when game is started)");
        }

        ImGui.SameLine(Math.Max(300, ImGui.GetWindowWidth() - 260));

        if (gb != null)
        {
            bool master = gb.CheatManager.MasterEnable;
            if (ImGui.Checkbox("Master Enable Cheats", ref master))
            {
                gb.CheatManager.MasterEnable = master;
            }

            int activeCount = gb.CheatManager.ActiveCount;
            int totalCount = gb.CheatManager.TotalCount;
            ImGui.TextDisabled($"Active Patches: {activeCount} | Total Cheats: {totalCount}");
        }
        else
        {
            ImGui.TextDisabled("Emulation Inactive");
        }
    }

    private void RenderAddCheatSection(Gameboy? gb)
    {
        ImGui.TextColored(new Vector4(0.9f, 0.9f, 1.0f, 1.0f), "Add New Cheat:");

        // 1. CLEARLY LABELED DESCRIPTION INPUT
        ImGui.Text("1. Cheat Name / Description:");
        ImGui.SameLine();
        ImGui.TextDisabled("(e.g., Infinite Lives, Max Rupees, 99 Bombs)");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##cheat_desc_input", ref _inputDescription, 128);

        ImGui.Spacing();

        // 2. CLEARLY LABELED MULTI-LINE CODE INPUT
        ImGui.Text("2. Cheat Code(s) - One or multiple lines (Game Genie or GameShark):");
        ImGui.SameLine();
        ImGui.TextDisabled("(Paste single codes or multi-line sequences)");
        ImGui.InputTextMultiline("##cheat_code_multiline", ref _inputCode, 2048, new Vector2(-1, 75));

        // Analyze lines for validation & button state
        var previewList = CheatDecoder.ParseLinesPreview(_inputCode);
        var validCodeLines = previewList.Where(p => !p.IsCommentOrEmpty && p.ParsedLine != null).ToList();
        var errorLines = previewList.Where(p => !p.IsCommentOrEmpty && !string.IsNullOrEmpty(p.Error)).ToList();
        bool canAdd = validCodeLines.Count > 0 && errorLines.Count == 0;

        ImGui.Spacing();

        // Action Buttons Row
        if (!canAdd)
        {
            ImGui.BeginDisabled();
        }

        string addLabel = validCodeLines.Count > 1
            ? $"Add Cheat ({validCodeLines.Count} lines grouped)"
            : "Add Cheat";

        if (ImGui.Button(addLabel))
        {
            if (CheatDecoder.TryDecodeMultiLine(_inputCode, _inputDescription, out var entry, out _))
            {
                if (gb != null)
                {
                    gb.CheatManager.AddCheat(entry);
                    SaveCheatsForCurrentRom(gb);
                    _statusMessage = $"Added cheat '{entry.Description}' ({entry.Lines.Count} line(s)).";
                }
                else
                {
                    _statusMessage = "Cheat saved, will apply when ROM loads.";
                }
                _inputCode = string.Empty;
                _inputDescription = string.Empty;
            }
        }

        if (validCodeLines.Count > 1)
        {
            ImGui.SameLine();
            if (ImGui.Button($"Add as {validCodeLines.Count} Separate Cheats"))
            {
                if (CheatDecoder.TryDecodeMultipleEntries(_inputCode, _inputDescription, out var entries, out _))
                {
                    if (gb != null)
                    {
                        foreach (var e in entries)
                        {
                            gb.CheatManager.AddCheat(e);
                        }
                        SaveCheatsForCurrentRom(gb);
                        _statusMessage = $"Added {entries.Count} individual cheats.";
                    }
                    _inputCode = string.Empty;
                    _inputDescription = string.Empty;
                }
            }
        }

        if (!canAdd)
        {
            ImGui.EndDisabled();
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear Input"))
        {
            _inputCode = string.Empty;
            _inputDescription = string.Empty;
        }

        ImGui.SameLine();
        if (ImGui.BeginCombo("##presets_combo", "Quick Presets..."))
        {
            foreach (var preset in CommonPresets)
            {
                if (ImGui.Selectable($"{preset.Game}: {preset.Name}"))
                {
                    _inputCode = preset.Code;
                    _inputDescription = preset.Description;
                }
            }
            ImGui.EndCombo();
        }

        // Live validation display
        if (!string.IsNullOrWhiteSpace(_inputCode))
        {
            ImGui.Spacing();
            if (errorLines.Count > 0)
            {
                ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), $"✗ {errorLines.Count} error(s) in code input:");
                foreach (var err in errorLines)
                {
                    ImGui.BulletText($"Line {err.LineNumber} ('{err.RawText}'): {err.Error}");
                }
            }
            else if (validCodeLines.Count > 0)
            {
                string plural = validCodeLines.Count > 1 ? $"{validCodeLines.Count} code lines" : "1 code line";
                ImGui.TextColored(new Vector4(0.4f, 1.0f, 0.5f, 1.0f), $"✓ {plural} parsed successfully:");

                for (int i = 0; i < validCodeLines.Count; i++)
                {
                    var p = validCodeLines[i];
                    var line = p.ParsedLine!;
                    string region = CheatDecoder.GetAddressRegionName(line.Address);
                    Vector4 badgeColor = line.Type == CheatType.GameGenie
                        ? new Vector4(1.0f, 0.78f, 0.28f, 1.0f)
                        : new Vector4(0.2f, 0.8f, 1.0f, 1.0f);

                    ImGui.SameLine();
                    if (i == 0) ImGui.NewLine();

                    ImGui.TextColored(badgeColor, $"[{line.Type}]");
                    ImGui.SameLine();
                    ImGui.Text($"{line.Code}  ->  {region} (${line.Address:X4}) = ${line.Value:X2}");

                    if (line.Type == CheatType.GameGenie && line.HasCompare)
                    {
                        ImGui.SameLine();
                        ImGui.TextDisabled($"(Compare: ${line.CompareValue:X2})");
                    }
                    else if (line.Type == CheatType.GameShark)
                    {
                        ImGui.SameLine();
                        ImGui.TextDisabled($"(Bank: ${line.Bank:X2})");
                    }
                }
            }
        }
        else
        {
            ImGui.TextDisabled("Supported: Game Genie (ABC-DEF or ABC-DEF-GHI) | GameShark (TTNNAAAA or TTNN-AAAA)");
        }
    }

    private void RenderCheatsTable(Gameboy? gb)
    {
        if (gb == null)
        {
            ImGui.TextDisabled("Load a game to manage active cheats.");
            return;
        }

        var cheats = gb.CheatManager.GetCheatsSnapshot();
        if (cheats.Count == 0)
        {
            ImGui.TextDisabled("No cheats added for this game yet. Enter a code above or pick a quick preset.");
            return;
        }

        var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable;
        float availHeight = Math.Max(160, ImGui.GetContentRegionAvail().Y - 150);

        if (ImGui.BeginTable("##cheats_table", 8, tableFlags, new Vector2(-1, availHeight)))
        {
            ImGui.TableSetupColumn("Active", ImGuiTableColumnFlags.WidthFixed, 48);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 95);
            ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Code(s)", ImGuiTableColumnFlags.WidthFixed, 115);
            ImGui.TableSetupColumn("Target(s)", ImGuiTableColumnFlags.WidthFixed, 110);
            ImGui.TableSetupColumn("Value(s)", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Compare", ImGuiTableColumnFlags.WidthFixed, 65);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 65);
            ImGui.TableHeadersRow();

            string? idToDelete = null;

            for (int i = 0; i < cheats.Count; i++)
            {
                var cheat = cheats[i];
                cheat.EnsureLinesFromLegacy();
                ImGui.TableNextRow();

                // 1. Active checkbox
                ImGui.TableNextColumn();
                bool enabled = cheat.Enabled;
                if (ImGui.Checkbox($"##chk_{cheat.Id}", ref enabled))
                {
                    gb.CheatManager.SetCheatEnabled(cheat.Id, enabled);
                    SaveCheatsForCurrentRom(gb);
                }

                // 2. Type badge
                ImGui.TableNextColumn();
                int lineCount = cheat.Lines.Count;
                if (lineCount <= 1)
                {
                    if (cheat.Type == CheatType.GameGenie)
                    {
                        ImGui.TextColored(new Vector4(1.0f, 0.78f, 0.28f, 1.0f), "Game Genie");
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(0.2f, 0.8f, 1.0f, 1.0f), "GameShark");
                    }
                }
                else
                {
                    // Multi-line badge
                    var primaryType = cheat.Lines[0].Type;
                    Vector4 badgeColor = primaryType == CheatType.GameGenie
                        ? new Vector4(1.0f, 0.78f, 0.28f, 1.0f)
                        : new Vector4(0.2f, 0.8f, 1.0f, 1.0f);
                    ImGui.TextColored(badgeColor, $"{primaryType} ({lineCount})");
                }

                // 3. Description
                ImGui.TableNextColumn();
                ImGui.Text(string.IsNullOrWhiteSpace(cheat.Description) ? "-" : cheat.Description);

                // 4. Code(s)
                ImGui.TableNextColumn();
                if (lineCount <= 1)
                {
                    string singleCode = lineCount == 1 ? cheat.Lines[0].Code : cheat.Code;
                    ImGui.Text(singleCode);
                }
                else
                {
                    for (int l = 0; l < lineCount; l++)
                    {
                        ImGui.Text(cheat.Lines[l].Code);
                    }
                }

                // 5. Target Address(es)
                ImGui.TableNextColumn();
                if (lineCount <= 1)
                {
                    ushort addr = lineCount == 1 ? cheat.Lines[0].Address : cheat.Address;
                    string region = CheatDecoder.GetAddressRegionName(addr);
                    ImGui.Text($"{region} ${addr:X4}");
                }
                else
                {
                    for (int l = 0; l < lineCount; l++)
                    {
                        ushort addr = cheat.Lines[l].Address;
                        string region = CheatDecoder.GetAddressRegionName(addr);
                        ImGui.Text($"{region} ${addr:X4}");
                    }
                }

                // 6. Value(s)
                ImGui.TableNextColumn();
                if (lineCount <= 1)
                {
                    byte val = lineCount == 1 ? cheat.Lines[0].Value : cheat.Value;
                    ImGui.Text($"${val:X2}");
                }
                else
                {
                    for (int l = 0; l < lineCount; l++)
                    {
                        ImGui.Text($"${cheat.Lines[l].Value:X2}");
                    }
                }

                // 7. Compare
                ImGui.TableNextColumn();
                if (lineCount <= 1)
                {
                    bool hasComp = lineCount == 1 ? cheat.Lines[0].HasCompare : cheat.HasCompare;
                    byte compVal = lineCount == 1 ? cheat.Lines[0].CompareValue : cheat.CompareValue;
                    if (cheat.Type == CheatType.GameGenie && hasComp)
                    {
                        ImGui.Text($"${compVal:X2}");
                    }
                    else
                    {
                        ImGui.TextDisabled("-");
                    }
                }
                else
                {
                    for (int l = 0; l < lineCount; l++)
                    {
                        if (cheat.Lines[l].Type == CheatType.GameGenie && cheat.Lines[l].HasCompare)
                        {
                            ImGui.Text($"${cheat.Lines[l].CompareValue:X2}");
                        }
                        else
                        {
                            ImGui.TextDisabled("-");
                        }
                    }
                }

                // 8. Delete button
                ImGui.TableNextColumn();
                if (ImGui.Button($"Delete##{cheat.Id}"))
                {
                    idToDelete = cheat.Id;
                }
            }

            ImGui.EndTable();

            if (idToDelete != null)
            {
                gb.CheatManager.RemoveCheat(idToDelete);
                SaveCheatsForCurrentRom(gb);
                _statusMessage = "Cheat removed.";
            }
        }
    }

    private void RenderBottomBar(Gameboy? gb)
    {
        if (gb != null)
        {
            if (ImGui.Button("Enable All"))
            {
                gb.CheatManager.SetAllEnabled(true);
                SaveCheatsForCurrentRom(gb);
                _statusMessage = "Enabled all cheats.";
            }
            ImGui.SameLine();

            if (ImGui.Button("Disable All"))
            {
                gb.CheatManager.SetAllEnabled(false);
                SaveCheatsForCurrentRom(gb);
                _statusMessage = "Disabled all cheats.";
            }
            ImGui.SameLine();

            if (ImGui.Button("Clear All"))
            {
                gb.CheatManager.ClearCheats();
                SaveCheatsForCurrentRom(gb);
                _statusMessage = "Cleared all cheats.";
            }
            ImGui.SameLine();

            if (ImGui.Button("Copy Active Codes"))
            {
                var cheats = gb.CheatManager.GetCheatsSnapshot();
                var sb = new StringBuilder();
                foreach (var c in cheats)
                {
                    if (c.Enabled)
                    {
                        sb.AppendLine($"# {c.Description}");
                        c.EnsureLinesFromLegacy();
                        foreach (var line in c.Lines)
                        {
                            sb.AppendLine(line.Code);
                        }
                        sb.AppendLine();
                    }
                }
                if (sb.Length > 0)
                {
                    ImGui.SetClipboardText(sb.ToString().TrimEnd());
                    _statusMessage = "Copied active codes to clipboard.";
                }
                else
                {
                    _statusMessage = "No active codes to copy.";
                }
            }
        }

        if (!string.IsNullOrEmpty(_statusMessage))
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.4f, 1.0f, 0.4f, 1.0f), _statusMessage);
        }
    }

    private static void RenderDocumentationSection()
    {
        if (ImGui.CollapsingHeader("Game Genie & GameShark Technical Reference (Pan Docs)"))
        {
            ImGui.TextWrapped("Pan Docs Reference: https://gbdev.io/pandocs/Shark_Cheats.html");
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(1.0f, 0.78f, 0.28f, 1.0f), "Game Genie (ROM Patches):");
            ImGui.BulletText("Consists of 6 or 9 hexadecimal characters formatted as ABC-DEF or ABC-DEF-GHI.");
            ImGui.BulletText("AB: New replacement data byte injected onto the cartridge bus.");
            ImGui.BulletText("FCDE: Memory address XORed with $F000 (must be in ROM range $0000-$7FFF, F is 8-F).");
            ImGui.BulletText("GI: Old compare data byte (XORed with $BA and rotated left by 2).");
            ImGui.BulletText("H: Checksum digit (computed as G XOR 8).");
            ImGui.BulletText("Why Compare Byte Matters: Because Game Boy ROMs use bank switching (MBC1/2/3/5), multiple banks occupy $4000-$7FFF. The compare byte ensures only the intended ROM bank is patched.");

            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 1.0f, 1.0f), "GameShark (RAM Patches):");
            ImGui.BulletText("Consists of 8 hexadecimal characters formatted as TTNN-AAAA.");
            ImGui.BulletText("TT: SRAM/WRAM bank or cheat type.");
            ImGui.BulletText("NN: New value to write into memory.");
            ImGui.BulletText("AAAA: Memory address in little-endian format (e.g., 010238CD writes $02 to address $CD38).");
            ImGui.BulletText("Multi-line codes: In games where stats or items span multiple bytes (e.g. 16-bit rupees 01FF7DC4 + 01FF7EC4), enter both codes on separate lines to toggle them together.");
        }
    }

    private void SaveCheatsForCurrentRom(Gameboy gb)
    {
        if (string.IsNullOrWhiteSpace(_lastLoadedRomKey)) return;
        var list = gb.CheatManager.GetCheatsSnapshot();
        CheatsStore.SaveCheats(_lastLoadedRomKey, list);
    }

    private static string GetRomKey(Gameboy gb, string romPath, string? romName)
    {
        string? internalTitle = gb._memory.GetRomTitle();
        if (!string.IsNullOrWhiteSpace(internalTitle))
        {
            return internalTitle;
        }

        if (!string.IsNullOrWhiteSpace(romName))
        {
            return Path.GetFileNameWithoutExtension(romName);
        }

        return Path.GetFileNameWithoutExtension(romPath);
    }

    private static string GetDisplayName(Gameboy gb, string? romName)
    {
        string? internalTitle = gb._memory.GetRomTitle();
        if (!string.IsNullOrWhiteSpace(internalTitle) && !string.IsNullOrWhiteSpace(romName))
        {
            return $"{internalTitle} ({romName})";
        }
        if (!string.IsNullOrWhiteSpace(internalTitle))
        {
            return internalTitle;
        }
        return romName ?? "Unknown ROM";
    }
}
