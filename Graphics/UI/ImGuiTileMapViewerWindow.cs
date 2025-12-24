using GBOG.CPU;
using GBOG.Memory;
using GBOG.Utils;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Widgets.Dialogs;
using Hexa.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using System.Numerics;

namespace GBOG.Graphics.UI;

public sealed unsafe class ImGuiTileMapViewerWindow
{
    private uint _tileDataTextureId;
    private uint _bgMapTextureId;
    private uint _windowMapTextureId;

    private readonly OpenFolderDialog _exportTilesFolderDialog = new();
    private readonly SaveFileDialog _exportBgDialog = new();
    private readonly SaveFileDialog _exportWindowDialog = new();

    private Gameboy? _exportGb;
    private DisplayPalette _exportPalette;
    private ExportRequest _exportRequest;

    private int _tileDataAtlasWidth;
    private int _tileDataAtlasHeight;
    private bool _tileDataAtlasValid;

    private const int TileMapSizePixels = 256;
    private bool _tileMapsValid;

    public bool AutoRefresh { get; set; } = true;

    // When enabled, map BG/WIN color indices through FF47 (BGP) first,
    // matching the DMG "final shaded output".
    public bool ApplyBgpShading { get; set; } = false;

    // Pan Docs: color index 0 is transparent for OBJs (sprites), but not for BG/WIN.
    // This is a debug viewer toggle for convenience.
    public bool TreatColor0AsTransparent { get; set; } = false;

    private static void DebugLog(string message)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine(message);
            Console.WriteLine(message);
        }
        catch
        {
            // ignore
        }
    }

    private enum ExportRequest
    {
        None = 0,
        TilesToFolder,
        BackgroundToFile,
        WindowToFile,
    }

    public void InvalidateAll()
    {
        _tileDataAtlasValid = false;
        _tileMapsValid = false;
    }

    public void Render(ref bool show, Gameboy? gb, GL gl, DisplayPalette displayPalette)
    {
        if (!show)
        {
            return;
        }

        if (!ImGui.Begin("Tile Data Viewer", ref show))
        {
            ImGui.End();
            return;
        }

        if (gb == null)
        {
            ImGui.Text("No ROM loaded.");
            ImGui.End();
            return;
        }

        bool auto = AutoRefresh;
        if (ImGui.Checkbox("Auto", ref auto))
        {
            AutoRefresh = auto;
        }

        ImGui.SameLine();
        bool applyBgp = ApplyBgpShading;
        if (ImGui.Checkbox("Apply BGP", ref applyBgp))
        {
            ApplyBgpShading = applyBgp;
            InvalidateAll();
        }

        ImGui.SameLine();
        bool ci0Transparent = TreatColor0AsTransparent;
        if (ImGui.Checkbox("CI0 Transparent", ref ci0Transparent))
        {
            TreatColor0AsTransparent = ci0Transparent;
            InvalidateAll();
        }

        ImGui.SameLine();
        if (ImGui.Button("Refresh"))
        {
            InvalidateAll();
        }

        if (ImGui.Button("Export Tiles..."))
        {
            _exportGb = gb;
            _exportPalette = displayPalette;
            _exportRequest = ExportRequest.TilesToFolder;
            _exportTilesFolderDialog.Show(ExportDialogCallback);
        }

        ImGui.SameLine();
        if (ImGui.Button("Export Background..."))
        {
            _exportGb = gb;
            _exportPalette = displayPalette;
            _exportRequest = ExportRequest.BackgroundToFile;
            _exportBgDialog.Show(ExportDialogCallback);
        }

        ImGui.SameLine();
        if (ImGui.Button("Export Window..."))
        {
            _exportGb = gb;
            _exportPalette = displayPalette;
            _exportRequest = ExportRequest.WindowToFile;
            _exportWindowDialog.Show(ExportDialogCallback);
        }

        if (AutoRefresh)
        {
            var dirty = gb._memory.ConsumeVideoDebugDirty();
            if (dirty != VideoDebugDirtyFlags.None)
            {
                if ((dirty & VideoDebugDirtyFlags.Palette) != 0)
                {
                    _tileDataAtlasValid = false;
                    _tileMapsValid = false;
                }

                if ((dirty & VideoDebugDirtyFlags.TileData) != 0)
                {
                    _tileDataAtlasValid = false;
                }

                if ((dirty & (VideoDebugDirtyFlags.TileMaps | VideoDebugDirtyFlags.Lcdc)) != 0)
                {
                    _tileMapsValid = false;
                }
            }
        }

        if (!_tileDataAtlasValid)
        {
            TryUpdateTileDataAtlas(gb, gl, displayPalette);
        }

        if (!_tileMapsValid)
        {
            TryUpdateTileMaps(gb, gl, displayPalette);
        }

        if (_tileDataTextureId == 0 || _tileDataAtlasWidth <= 0 || _tileDataAtlasHeight <= 0)
        {
            ImGui.Text("Tile atlas not available.");
            ImGui.End();
            return;
        }

        if (ImGui.BeginTable("##tileDataViewerTable", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            float atlasAspect = _tileDataAtlasWidth / (float)_tileDataAtlasHeight;

            ImGui.TableNextColumn();
            ImGui.Text("Tile Data (8000-97FF)");
            {
                var avail = ImGui.GetContentRegionAvail();
                float w = MathF.Max(1f, avail.X);
                float h = w / atlasAspect;
                ImGui.Image(new ImTextureRef(null, _tileDataTextureId), new Vector2(w, h));
            }

            ImGui.TableNextColumn();
            ImGui.Text("Background + Window (Full 256x256)");
            {
                if (_bgMapTextureId == 0 || _windowMapTextureId == 0)
                {
                    ImGui.Text("Tile maps not available.");
                }
                else
                {
                    var avail = ImGui.GetContentRegionAvail();
                    float width = MathF.Max(1f, avail.X);

                    // Two square previews stacked vertically.
                    float each = width;
                    float gap = ImGui.GetStyle().ItemSpacing.Y;
                    float needed = (each * 2f) + gap;
                    if (needed > avail.Y && avail.Y > 1f)
                    {
                        each = MathF.Max(1f, (avail.Y - gap) / 2f);
                        width = each;
                    }

                    ImGui.Text("Background");
                    ImGui.Image(new ImTextureRef(null, _bgMapTextureId), new Vector2(width, each));

                    ImGui.Spacing();
                    ImGui.Text("Window");
                    ImGui.Image(new ImTextureRef(null, _windowMapTextureId), new Vector2(width, each));
                }
            }

            ImGui.EndTable();
        }

        // Dialogs must be drawn every frame.
        _exportTilesFolderDialog.Draw(ImGuiWindowFlags.None);
        _exportBgDialog.Draw(ImGuiWindowFlags.None);
        _exportWindowDialog.Draw(ImGuiWindowFlags.None);

        ImGui.End();
    }

    private void ExportDialogCallback(object? sender, DialogResult result)
    {
        if (result != DialogResult.Ok)
        {
            _exportRequest = ExportRequest.None;
            return;
        }

        var gb = _exportGb;
        if (gb == null)
        {
            _exportRequest = ExportRequest.None;
            return;
        }

        try
        {
            switch (_exportRequest)
            {
                case ExportRequest.TilesToFolder:
                {
                    var folder = GetSelectedPathForRequest(sender, _exportRequest);
                    if (!string.IsNullOrWhiteSpace(folder))
                    {
                        ExportTilesToFolder(gb, _exportPalette, folder, TreatColor0AsTransparent, ApplyBgpShading);
                    }
                    else
                    {
                        DebugLog("[TileMapViewer] Export tiles: no folder selected.");
                    }
                    break;
                }

                case ExportRequest.BackgroundToFile:
                {
                    var path = GetSelectedPathForRequest(sender, _exportRequest);
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        ExportTileMapToFile(gb, _exportPalette, isWindow: false, path, TreatColor0AsTransparent, ApplyBgpShading);
                    }
                    else
                    {
                        DebugLog("[TileMapViewer] Export BG: no file selected.");
                    }
                    break;
                }

                case ExportRequest.WindowToFile:
                {
                    var path = GetSelectedPathForRequest(sender, _exportRequest);
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        ExportTileMapToFile(gb, _exportPalette, isWindow: true, path, TreatColor0AsTransparent, ApplyBgpShading);
                    }
                    else
                    {
                        DebugLog("[TileMapViewer] Export WIN: no file selected.");
                    }
                    break;
                }
            }
        }
        catch
        {
            // best-effort; avoid crashing the UI if export fails.
            DebugLog("[TileMapViewer] Export failed (exception). See debugger output.");
        }
        finally
        {
            _exportRequest = ExportRequest.None;
        }
    }

    private string? GetSelectedPathForRequest(object? sender, ExportRequest request)
    {
        // Prefer reading from our dialog instance; sender isn't guaranteed to be the dialog.
        object? dialog = request switch
        {
            ExportRequest.TilesToFolder => _exportTilesFolderDialog,
            ExportRequest.BackgroundToFile => _exportBgDialog,
            ExportRequest.WindowToFile => _exportWindowDialog,
            _ => null,
        };

        return GetDialogSelectedPath(dialog) ?? GetDialogSelectedPath(sender);
    }

    private static string? GetDialogSelectedPath(object? dialog)
    {
        if (dialog == null)
        {
            return null;
        }

        var t = dialog.GetType();

        string? currentFolder = null;
        foreach (var folderProp in new[] { "CurrentFolder", "Folder", "CurrentDirectory", "Directory", "Path" })
        {
            var p = t.GetProperty(folderProp);
            if (p == null || p.PropertyType != typeof(string))
            {
                continue;
            }
            try
            {
                currentFolder = (string?)p.GetValue(dialog);
                if (!string.IsNullOrWhiteSpace(currentFolder))
                {
                    break;
                }
            }
            catch
            {
                // ignore
            }
        }

        foreach (var name in new[]
        {
            "SelectedFile",
            "FilePath",
            "FileName",
            "SelectionString",
            "SelectedPath",
            "SelectedFolder",
            "Path",
            "Folder",
        })
        {
            var p = t.GetProperty(name);
            if (p == null || p.PropertyType != typeof(string))
            {
                continue;
            }

            try
            {
                var raw = (string?)p.GetValue(dialog);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                raw = raw.Trim().Trim('"');

                // Some dialogs may return only a filename; combine with current folder when possible.
                if (!string.IsNullOrWhiteSpace(currentFolder) && !System.IO.Path.IsPathRooted(raw))
                {
                    raw = System.IO.Path.Combine(currentFolder, raw);
                }

                return raw;
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    private static void ExportTilesToFolder(Gameboy gb, DisplayPalette palette, string folder, bool treatCi0Transparent, bool applyBgpShading)
    {
        Directory.CreateDirectory(folder);

        const int tileSize = 8;
        var tileData = gb._memory.GetTileData();
        var tiles = GraphicUtils.ConvertGBTileData(tileData);

        for (int i = 0; i < tiles.Count; i++)
        {
            var src = tiles[i];
            using var image = new Image<Rgba32>(tileSize, tileSize);

            byte bgp = gb._memory.BGP;

            for (int y = 0; y < tileSize; y++)
            {
                for (int x = 0; x < tileSize; x++)
                {
                    byte ci = src[(y * tileSize) + x];
                    bool transparent = treatCi0Transparent && ci == 0;
                    int shadeIndex = applyBgpShading ? MapDmgPaletteToShade(bgp, ci) : ci;
                    image[x, y] = ToRgba32(palette, shadeIndex, transparent);
                }
            }

            string path = System.IO.Path.Combine(folder, $"tile_{i:D3}.png");
            image.Save(path);
        }
    }

    private static void ExportTileMapToFile(Gameboy gb, DisplayPalette palette, bool isWindow, string path, bool treatCi0Transparent, bool applyBgpShading)
    {
        path = EnsurePngExtension(path);

        string? dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tileData = gb._memory.GetTileData();
        byte bgp = gb._memory.BGP;
        bool useSignedTileNumbers = !gb._memory.BGWindowTileDataSelect;

        ushort mapBase;
        if (isWindow)
        {
            mapBase = gb._memory.WindowTileMapDisplaySelect ? (ushort)0x9C00 : (ushort)0x9800;
        }
        else
        {
            mapBase = gb._memory.BGTileMapDisplaySelect ? (ushort)0x9C00 : (ushort)0x9800;
        }

        var map = gb._memory.GetTileMap(mapBase);
        var rgba = RenderTileMapToRgba(map, tileData, useSignedTileNumbers, palette, treatCi0Transparent, applyBgpShading, bgp);

        using var image = Image.LoadPixelData<Rgba32>(rgba, TileMapSizePixels, TileMapSizePixels);
        image.Save(path);
    }

    private static Rgba32 ToRgba32(DisplayPalette palette, int shadeIndex, bool transparent)
    {
        var c = palette.GetShade(shadeIndex);
        byte a = transparent ? (byte)0 : c.A;
        return new Rgba32(c.R, c.G, c.B, a);
    }

    private static int MapDmgPaletteToShade(byte dmgPalette, int colorIndex)
    {
        return (dmgPalette >> (colorIndex * 2)) & 0x03;
    }

    private static string EnsurePngExtension(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return path + ".png";
    }

    private static uint CreateTexture(GL gl, int width, int height)
    {
        uint textureId;
        gl.GenTextures(1, &textureId);
        gl.BindTexture(GLTextureTarget.Texture2D, textureId);
        gl.TexParameteri(GLTextureTarget.Texture2D, GLTextureParameterName.MinFilter, (int)GLTextureMinFilter.Nearest);
        gl.TexParameteri(GLTextureTarget.Texture2D, GLTextureParameterName.MagFilter, (int)GLTextureMagFilter.Nearest);
        gl.TexImage2D(GLTextureTarget.Texture2D, 0, GLInternalFormat.Rgba, width, height, 0, GLPixelFormat.Rgba, GLPixelType.UnsignedByte, null);
        return textureId;
    }

    private void TryUpdateTileDataAtlas(Gameboy gb, GL gl, DisplayPalette palette)
    {
        // Match the old WinForms viewer layout: 16 tiles per row, each tile is 8x8.
        const int tilesPerRow = 16;
        const int tileSize = 8;

        try
        {
            var tileData = gb._memory.GetTileData();
            var tiles = GraphicUtils.ConvertGBTileData(tileData);
            int tileCount = tiles.Count;
            if (tileCount <= 0)
            {
                return;
            }

            int rows = (tileCount + tilesPerRow - 1) / tilesPerRow;
            int width = tilesPerRow * tileSize;
            int height = rows * tileSize;

            var rgba = new byte[width * height * 4];

            for (int i = 0; i < tileCount; i++)
            {
                int baseX = (i % tilesPerRow) * tileSize;
                int baseY = (i / tilesPerRow) * tileSize;

                var src = tiles[i];

                for (int y = 0; y < tileSize; y++)
                {
                    for (int x = 0; x < tileSize; x++)
                    {
                        int dstX = baseX + x;
                        int dstY = baseY + y;
                        int dst = (dstY * width + dstX) * 4;

                        byte ci = src[y * tileSize + x];
                        bool transparent = TreatColor0AsTransparent && ci == 0;
                        int shadeIndex = ApplyBgpShading ? MapDmgPaletteToShade(gb._memory.BGP, ci) : ci;
                        var c = ToRgba32(palette, shadeIndex, transparent);
                        rgba[dst + 0] = c.R;
                        rgba[dst + 1] = c.G;
                        rgba[dst + 2] = c.B;
                        rgba[dst + 3] = c.A;
                    }
                }
            }

            if (_tileDataTextureId == 0)
            {
                _tileDataTextureId = CreateTexture(gl, width, height);
            }
            else
            {
                gl.BindTexture(GLTextureTarget.Texture2D, _tileDataTextureId);
                gl.TexImage2D(GLTextureTarget.Texture2D, 0, GLInternalFormat.Rgba, width, height, 0, GLPixelFormat.Rgba, GLPixelType.UnsignedByte, null);
            }

            fixed (byte* p = rgba)
            {
                gl.BindTexture(GLTextureTarget.Texture2D, _tileDataTextureId);
                gl.TexSubImage2D(GLTextureTarget.Texture2D, 0, 0, 0, width, height, GLPixelFormat.Rgba, GLPixelType.UnsignedByte, p);
            }

            _tileDataAtlasWidth = width;
            _tileDataAtlasHeight = height;
            _tileDataAtlasValid = true;
        }
        catch
        {
            // best-effort
        }
    }

    private void TryUpdateTileMaps(Gameboy gb, GL gl, DisplayPalette palette)
    {
        try
        {
            var tileData = gb._memory.GetTileData();

            byte bgp = gb._memory.BGP;

            ushort bgMapBase = gb._memory.BGTileMapDisplaySelect ? (ushort)0x9C00 : (ushort)0x9800;
            ushort winMapBase = gb._memory.WindowTileMapDisplaySelect ? (ushort)0x9C00 : (ushort)0x9800;
            bool useSignedTileNumbers = !gb._memory.BGWindowTileDataSelect;

            var bgMap = gb._memory.GetTileMap(bgMapBase);
            var winMap = gb._memory.GetTileMap(winMapBase);

            var rgbaBg = RenderTileMapToRgba(bgMap, tileData, useSignedTileNumbers, palette, TreatColor0AsTransparent, ApplyBgpShading, bgp);
            var rgbaWin = RenderTileMapToRgba(winMap, tileData, useSignedTileNumbers, palette, TreatColor0AsTransparent, ApplyBgpShading, bgp);

            if (_bgMapTextureId == 0)
            {
                _bgMapTextureId = CreateTexture(gl, TileMapSizePixels, TileMapSizePixels);
            }
            if (_windowMapTextureId == 0)
            {
                _windowMapTextureId = CreateTexture(gl, TileMapSizePixels, TileMapSizePixels);
            }

            fixed (byte* pBg = rgbaBg)
            {
                gl.BindTexture(GLTextureTarget.Texture2D, _bgMapTextureId);
                gl.TexSubImage2D(GLTextureTarget.Texture2D, 0, 0, 0, TileMapSizePixels, TileMapSizePixels, GLPixelFormat.Rgba, GLPixelType.UnsignedByte, pBg);
            }

            fixed (byte* pWin = rgbaWin)
            {
                gl.BindTexture(GLTextureTarget.Texture2D, _windowMapTextureId);
                gl.TexSubImage2D(GLTextureTarget.Texture2D, 0, 0, 0, TileMapSizePixels, TileMapSizePixels, GLPixelFormat.Rgba, GLPixelType.UnsignedByte, pWin);
            }

            _tileMapsValid = true;
        }
        catch
        {
            // best-effort
        }
    }

    private static byte[] RenderTileMapToRgba(byte[] tileMap, byte[] tileData, bool useSignedTileNumbers, DisplayPalette palette, bool treatCi0Transparent, bool applyBgpShading, byte bgp)
    {
        var rgba = new byte[TileMapSizePixels * TileMapSizePixels * 4];

        for (int mapY = 0; mapY < 32; mapY++)
        {
            for (int mapX = 0; mapX < 32; mapX++)
            {
                byte tileId = tileMap[(mapY * 32) + mapX];
                int tileBase = GetTileOffset(tileId, useSignedTileNumbers);

                for (int y = 0; y < 8; y++)
                {
                    int rowIndex = tileBase + (y * 2);
                    byte b1 = (uint)rowIndex < (uint)tileData.Length ? tileData[rowIndex] : (byte)0;
                    byte b2 = (uint)(rowIndex + 1) < (uint)tileData.Length ? tileData[rowIndex + 1] : (byte)0;

                    for (int x = 0; x < 8; x++)
                    {
                        int bit = 7 - x;
                        int colorIndex = ((b1 >> bit) & 1) | (((b2 >> bit) & 1) << 1);
                        bool transparent = treatCi0Transparent && colorIndex == 0;
                        int shadeIndex = applyBgpShading ? MapDmgPaletteToShade(bgp, colorIndex) : colorIndex;
                        var c = ToRgba32(palette, shadeIndex, transparent);

                        int px = (mapX * 8) + x;
                        int py = (mapY * 8) + y;
                        int dst = (py * TileMapSizePixels + px) * 4;
                        rgba[dst + 0] = c.R;
                        rgba[dst + 1] = c.G;
                        rgba[dst + 2] = c.B;
                        rgba[dst + 3] = c.A;
                    }
                }
            }
        }

        return rgba;
    }

    private static int GetTileOffset(byte tileId, bool useSignedTileNumbers)
    {
        // tileData buffer is 0x8000-0x97FF, so offsets are relative to 0x8000.
        if (!useSignedTileNumbers)
        {
            return tileId * 16;
        }

        int signedIndex = unchecked((sbyte)tileId);
        int addr = 0x9000 + (signedIndex * 16);
        return addr - 0x8000;
    }
}
