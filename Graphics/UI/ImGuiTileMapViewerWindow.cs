using GBOG.CPU;
using GBOG.ImGuiTexInspect;
using GBOG.ImGuiTexInspect.Core;
using GBOG.Memory;
using GBOG.Utils;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Widgets.Dialogs;
using Hexa.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace GBOG.Graphics.UI;

public sealed unsafe class ImGuiTileMapViewerWindow
{
  private uint _tileDataTextureId;
  private uint _bgMapTextureId;
  private uint _windowMapTextureId;
  private uint _spriteLayerTextureId;

  private readonly OpenFolderDialog _exportTilesFolderDialog = new();
  private readonly SaveFileDialog _exportBgDialog = new();
  private readonly SaveFileDialog _exportWindowDialog = new();
  private readonly SaveFileDialog _exportSpritesDialog = new();
  private readonly SaveFileDialog _exportSpriteTileDialog = new();

  private Gameboy? _exportGb;
  private DisplayPalette _exportPalette;
  private ExportRequest _exportRequest;

  private SpriteTileExportInfo _spriteTileExport;

  private int _tileDataAtlasWidth;
  private int _tileDataAtlasHeight;
  private bool _tileDataAtlasValid;

  private const int TileMapSizePixels = 256;
  private bool _tileMapsValid;

  private const int ScreenWidth = 160;
  private const int ScreenHeight = 144;
  private bool _spriteLayerValid;

  private int _tileAtlasSnapshotVersion;
  private int _tileMapsSnapshotVersion;
  private int _spriteLayerSnapshotVersion;

  private Gameboy? _lastGbForInspectors;

  private enum TileMapSelectMode
  {
    FollowLcdc = 0,
    Map9800 = 1,
    Map9C00 = 2,
  }

  private TileMapSelectMode _bgMapSelectMode = TileMapSelectMode.FollowLcdc;

  private int _cgbTileAtlasPaletteIndex = 0; // For CGB: which palette to display (0-7)
  private bool _cgbTileAtlasPaletteIsObj = false; // For CGB: whether to use BG or OBJ palettes

  public bool AutoRefresh { get; set; } = true;

  private bool _inspectShowGrid = true;
  private bool _inspectShowTooltip = true;
  private bool _inspectAutoReadTexture = true;
  private bool _inspectForceNearest = true;
  private InspectorAlphaMode _inspectAlphaMode = InspectorAlphaMode.ImGui;

  private bool _inspectorSettingsApplied;

  private AppSettings? _exportSettings;
  private Action? _exportSaveSettings;

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
    SpritesToFile,
    SpriteTileToFile,
  }

  private struct SpriteTileExportInfo
  {
    public int SpriteIndex;
    public int TileIndex;
    public bool XFlip;
    public bool YFlip;
    public byte DmgPalette;

    // CGB fields (ignored on DMG)
    public int CgbPalette;
    public int VramBank;

    // Screen position of the 8x8 tile region to export (top-left corner)
    public int ScreenX;
    public int ScreenY;
  }

  public void InvalidateAll()
  {
    _tileDataAtlasValid = false;
    _tileMapsValid = false;
    _spriteLayerValid = false;
  }

  public void ApplySettings(AppSettings settings)
  {
    _inspectShowGrid = settings.InspectorShowGrid;
    _inspectShowTooltip = settings.InspectorShowTooltip;
    _inspectAutoReadTexture = settings.InspectorAutoReadTexture;
    _inspectForceNearest = settings.InspectorForceNearest;

    _inspectAlphaMode = (InspectorAlphaMode)Math.Clamp(settings.InspectorAlphaMode, 0, 4);
    _inspectorSettingsApplied = true;
  }

  public void Render(ref bool show, Gameboy? gb, GL gl, DisplayPalette displayPalette, AppSettings settings, Action saveSettings)
  {
    if (!show)
    {
      return;
    }

    if (!_inspectorSettingsApplied)
    {
      ApplySettings(settings);
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

    // When switching ROMs, the Gameboy instance changes. ImGuiTexInspect keeps per-panel state
    // (pan/zoom/flags) in a global context keyed by panel ID, so without resetting you'll carry
    // over the previous ROM's pan/zoom which can make hover/tooltips appear "wrong".
    if (!ReferenceEquals(_lastGbForInspectors, gb))
    {
      _lastGbForInspectors = gb;
      InspectorPanel.Shutdown();
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

    if (gb._memory.IsCgb)
    {
      ImGui.SameLine();
      bool isObj = _cgbTileAtlasPaletteIsObj;
      if (ImGui.Checkbox("OBJ Pal", ref isObj))
      {
        _cgbTileAtlasPaletteIsObj = isObj;
        _tileDataAtlasValid = false;
      }

      ImGui.SameLine();
      int paletteIndex = _cgbTileAtlasPaletteIndex;
      string label = _cgbTileAtlasPaletteIsObj ? "OBJ Pal##atlas" : "BG Pal##atlas";
      if (ImGui.SliderInt(label, ref paletteIndex, 0, 7))
      {
        _cgbTileAtlasPaletteIndex = paletteIndex;
        _tileDataAtlasValid = false;
      }
    }

    ImGui.SameLine();
    if (ImGui.Button("Refresh"))
    {
      InvalidateAll();
    }

    if (ImGui.CollapsingHeader("Inspector Options"))
    {
      bool showTooltip = _inspectShowTooltip;
      if (ImGui.Checkbox("Tooltip", ref showTooltip))
      {
        _inspectShowTooltip = showTooltip;
        PersistInspectorSettings(settings, saveSettings);
      }

      ImGui.SameLine();
      bool showGrid = _inspectShowGrid;
      if (ImGui.Checkbox("Grid", ref showGrid))
      {
        _inspectShowGrid = showGrid;
        PersistInspectorSettings(settings, saveSettings);
      }

      ImGui.SameLine();
      string[] alphaModes = { "ImGui Background", "Black", "White", "Checkered", "Custom Color" };
      int alphaMode = (int)_inspectAlphaMode;
      ImGui.SetNextItemWidth(200);
      if (ImGui.Combo("Alpha Mode", ref alphaMode, alphaModes, alphaModes.Length))
      {
        _inspectAlphaMode = (InspectorAlphaMode)Math.Clamp(alphaMode, 0, 4);
        PersistInspectorSettings(settings, saveSettings);
      }

      bool autoRead = _inspectAutoReadTexture;
      if (ImGui.Checkbox("Auto Read Texture", ref autoRead))
      {
        _inspectAutoReadTexture = autoRead;
        PersistInspectorSettings(settings, saveSettings);
      }

      ImGui.SameLine();
      bool forceNearest = _inspectForceNearest;
      if (ImGui.Checkbox("Force Nearest", ref forceNearest))
      {
        _inspectForceNearest = forceNearest;
        PersistInspectorSettings(settings, saveSettings);
      }
    }

    if (ImGui.Button("Export Tiles..."))
    {
      ApplyExportDirectoryToDialogs(settings.LastExportDirectory);
      _exportGb = gb;
      _exportPalette = displayPalette;
      _exportRequest = ExportRequest.TilesToFolder;
      _exportSettings = settings;
      _exportSaveSettings = saveSettings;
      _exportTilesFolderDialog.Show(ExportDialogCallback);
    }

    ImGui.SameLine();
    if (ImGui.Button("Export Background..."))
    {
      ApplyExportDirectoryToDialogs(settings.LastExportDirectory);
      _exportGb = gb;
      _exportPalette = displayPalette;
      _exportRequest = ExportRequest.BackgroundToFile;
      _exportSettings = settings;
      _exportSaveSettings = saveSettings;
      _exportBgDialog.Show(ExportDialogCallback);
    }

    ImGui.SameLine();
    if (ImGui.Button("Export Window..."))
    {
      ApplyExportDirectoryToDialogs(settings.LastExportDirectory);
      _exportGb = gb;
      _exportPalette = displayPalette;
      _exportRequest = ExportRequest.WindowToFile;
      _exportSettings = settings;
      _exportSaveSettings = saveSettings;
      _exportWindowDialog.Show(ExportDialogCallback);
    }

    ImGui.SameLine();
    if (ImGui.Button("Export Sprites..."))
    {
      ApplyExportDirectoryToDialogs(settings.LastExportDirectory);
      _exportGb = gb;
      _exportPalette = displayPalette;
      _exportRequest = ExportRequest.SpritesToFile;
      _exportSettings = settings;
      _exportSaveSettings = saveSettings;
      _exportSpritesDialog.Show(ExportDialogCallback);
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
          _spriteLayerValid = false;
        }

        if ((dirty & VideoDebugDirtyFlags.TileData) != 0)
        {
          _tileDataAtlasValid = false;
          _spriteLayerValid = false;
        }

        if ((dirty & (VideoDebugDirtyFlags.TileMaps | VideoDebugDirtyFlags.Lcdc)) != 0)
        {
          _tileMapsValid = false;
          _spriteLayerValid = false;
        }

        if ((dirty & VideoDebugDirtyFlags.Oam) != 0)
        {
          _spriteLayerValid = false;
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

    if (!_spriteLayerValid)
    {
      TryUpdateSpriteLayer(gb, gl, displayPalette);
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
      ImGui.Text("Layers");
      {
        if (_bgMapTextureId == 0 || _windowMapTextureId == 0)
        {
          ImGui.Text("Tile maps not available.");
        }
        else
        {
          var avail = ImGui.GetContentRegionAvail();

          var inspectorFlags = GetInspectorFlags();

          if (ImGui.BeginTabBar("##layerTabs"))
          {
            if (ImGui.BeginTabItem("Background"))
            {
              string[] bgModes = { "Follow LCDC", "0x9800", "0x9C00" };
              int bgMode = (int)_bgMapSelectMode;
              ImGui.SetNextItemWidth(120);
              if (ImGui.Combo("BG Map", ref bgMode, bgModes, bgModes.Length))
              {
                _bgMapSelectMode = (TileMapSelectMode)Math.Clamp(bgMode, 0, 2);
                _tileMapsValid = false;
              }

              ApplyNextInspectorSettings(inspectorFlags, _inspectAlphaMode);
              if (InspectorPanel.BeginInspectorPanel(
                  "Background##texinspect",
                  (nint)_bgMapTextureId,
                  new Vector2(TileMapSizePixels, TileMapSizePixels),
                  flags: inspectorFlags,
                  size: null,
                  extraTooltip: info => RenderBgWinExtraTooltip(gb, isWindow: false, (int)info.Texel.X, (int)info.Texel.Y)))
              {
                InspectorPanel.EndInspectorPanel();
              }
              ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Window"))
            {
              ApplyNextInspectorSettings(inspectorFlags, _inspectAlphaMode);
              if (InspectorPanel.BeginInspectorPanel(
                  "Window##texinspect",
                  (nint)_windowMapTextureId,
                  new Vector2(TileMapSizePixels, TileMapSizePixels),
                  flags: inspectorFlags,
                  size: null,
                  extraTooltip: info => RenderBgWinExtraTooltip(gb, isWindow: true, (int)info.Texel.X, (int)info.Texel.Y)))
              {
                InspectorPanel.EndInspectorPanel();
              }
              ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Sprites"))
            {
              if (_spriteLayerTextureId == 0)
              {
                ImGui.Text("Sprite layer not available.");
              }
              else
              {
                ApplyNextInspectorSettings(inspectorFlags, _inspectAlphaMode);
                if (InspectorPanel.BeginInspectorPanel(
                    "Sprites##texinspect",
                    (nint)_spriteLayerTextureId,
                    new Vector2(ScreenWidth, ScreenHeight),
                    flags: inspectorFlags,
                    size: null,
                    extraTooltip: info => RenderSpriteExtraTooltip(gb, (int)info.Texel.X, (int)info.Texel.Y)))
                {
                  // Right-click a sprite pixel to export the contributing 8x8 tile.
                  if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                  {
                    TryStartSpriteTileExportAtMouse(gb, displayPalette, settings, saveSettings);
                  }

                  InspectorPanel.EndInspectorPanel();
                }
              }

              ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
          }
        }
      }

      ImGui.EndTable();
    }

    // Dialogs must be drawn every frame.
    _exportTilesFolderDialog.Draw(ImGuiWindowFlags.None);
    _exportBgDialog.Draw(ImGuiWindowFlags.None);
    _exportWindowDialog.Draw(ImGuiWindowFlags.None);
    _exportSpritesDialog.Draw(ImGuiWindowFlags.None);
    _exportSpriteTileDialog.Draw(ImGuiWindowFlags.None);

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
              UpdateLastExportDirectory(folder);
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
              UpdateLastExportDirectory(path);
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
              UpdateLastExportDirectory(path);
              ExportTileMapToFile(gb, _exportPalette, isWindow: true, path, TreatColor0AsTransparent, ApplyBgpShading);
            }
            else
            {
              DebugLog("[TileMapViewer] Export WIN: no file selected.");
            }
            break;
          }

        case ExportRequest.SpritesToFile:
          {
            var path = GetSelectedPathForRequest(sender, _exportRequest);
            if (!string.IsNullOrWhiteSpace(path))
            {
              UpdateLastExportDirectory(path);
              ExportSpritesToFile(gb, _exportPalette, path);
            }
            else
            {
              DebugLog("[TileMapViewer] Export OBJ: no file selected.");
            }
            break;
          }

        case ExportRequest.SpriteTileToFile:
          {
            var path = GetSelectedPathForRequest(sender, _exportRequest);
            if (!string.IsNullOrWhiteSpace(path))
            {
              UpdateLastExportDirectory(path);
              ExportSpriteTileToFile(gb, _exportPalette, path, _spriteTileExport);
            }
            else
            {
              DebugLog("[TileMapViewer] Export OBJ tile: no file selected.");
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
      ExportRequest.SpritesToFile => _exportSpritesDialog,
      ExportRequest.SpriteTileToFile => _exportSpriteTileDialog,
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

    bool isCgb = gb._memory.IsCgb;
    var tileData = gb._memory.GetTileData();
    var tileData1 = isCgb ? gb._memory.GetTileDataBank(1) : Array.Empty<byte>();
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

    gb._memory.GetVideoDebugSnapshot(out _, out var bgPalRam, out _, out _, out _);
    var rgba = isCgb
      ? RenderCgbTileMapToRgba(map, gb._memory.GetCgbTileMapAttributes(mapBase), tileData, tileData1, useSignedTileNumbers, treatCi0Transparent, bgPalRam)
      : RenderTileMapToRgba(map, tileData, useSignedTileNumbers, palette, treatCi0Transparent, applyBgpShading, bgp);

    using var image = Image.LoadPixelData<Rgba32>(rgba, TileMapSizePixels, TileMapSizePixels);
    image.Save(path);
  }

  private static void ExportSpritesToFile(Gameboy gb, DisplayPalette palette, string path)
  {
    path = EnsurePngExtension(path);

    string? dir = System.IO.Path.GetDirectoryName(path);
    if (!string.IsNullOrWhiteSpace(dir))
    {
      Directory.CreateDirectory(dir);
    }

    bool isCgb = gb._memory.IsCgb;
    gb._memory.GetVideoDebugSnapshot(out var vram, out _, out var objPal, out _, out _);
    var tileData = new byte[0x1800];
    Array.Copy(vram, 0x0000, tileData, 0, 0x1800);
    var tileData1 = isCgb ? new byte[0x1800] : Array.Empty<byte>();
    if (isCgb)
    {
      Array.Copy(vram, 0x2000, tileData1, 0, 0x1800);
    }
    var oam = gb._memory.OAMRam;
    bool use8x16 = gb._memory.OBJSize;
    byte obp0 = gb._memory.OBP0;
    byte obp1 = gb._memory.OBP1;
    var rgba = isCgb
      ? RenderCgbSpriteLayerToRgba(oam, tileData, tileData1, use8x16, objPal)
      : RenderSpriteLayerToRgba(oam, tileData, use8x16, palette, obp0, obp1);

    using var image = Image.LoadPixelData<Rgba32>(rgba, ScreenWidth, ScreenHeight);
    image.Save(path);
  }

  private static void ExportSpriteTileToFile(Gameboy gb, DisplayPalette palette, string path, SpriteTileExportInfo info)
  {
    path = EnsurePngExtension(path);

    string? dir = System.IO.Path.GetDirectoryName(path);
    if (!string.IsNullOrWhiteSpace(dir))
    {
      Directory.CreateDirectory(dir);
    }

    bool isCgb = gb._memory.IsCgb;
    // Get the snapshot once to ensure consistency between tile data and palette
    gb._memory.GetVideoDebugSnapshot(out var vram, out _, out var objPalRam, out var oam, out _);
    
    var tileData0 = new byte[0x1800];
    Array.Copy(vram, 0x0000, tileData0, 0, 0x1800);
    
    var tileData1 = isCgb ? new byte[0x1800] : Array.Empty<byte>();
    if (isCgb)
    {
      Array.Copy(vram, 0x2000, tileData1, 0, 0x1800);
    }
    
    // Render the full sprite layer using the same logic as the sprite viewer.
    // This ensures we get exactly what's displayed, including overlapping sprites.
    bool use8x16 = gb._memory.OBJSize;
    byte obp0 = gb._memory.OBP0;
    byte obp1 = gb._memory.OBP1;
    
    var spriteLayerRgba = isCgb
      ? RenderCgbSpriteLayerToRgba(oam, tileData0, tileData1, use8x16, objPalRam)
      : RenderSpriteLayerToRgba(oam, tileData0, use8x16, palette, obp0, obp1);

    // Extract the 8x8 region at the sprite tile's screen position
    using var image = new Image<Rgba32>(8, 8);
    for (int y = 0; y < 8; y++)
    {
      int srcY = info.ScreenY + y;
      if ((uint)srcY >= (uint)ScreenHeight)
      {
        continue;
      }
      
      for (int x = 0; x < 8; x++)
      {
        int srcX = info.ScreenX + x;
        if ((uint)srcX >= (uint)ScreenWidth)
        {
          continue;
        }
        
        int srcIdx = (srcY * ScreenWidth + srcX) * 4;
        image[x, y] = new Rgba32(
          spriteLayerRgba[srcIdx + 0],
          spriteLayerRgba[srcIdx + 1],
          spriteLayerRgba[srcIdx + 2],
          spriteLayerRgba[srcIdx + 3]);
      }
    }

    image.Save(path);
  }

  private void TryStartSpriteTileExportAtMouse(Gameboy gb, DisplayPalette palette, AppSettings settings, Action saveSettings)
  {
    try
    {
      var ctx = InspectorPanel.GetCurrentContext();
      var inspector = ctx.CurrentInspector;
      if (inspector == null)
      {
        return;
      }

      var mousePos = ImGui.GetMousePos();
      var texel = inspector.PixelsToTexels * mousePos;
      int px = Math.Clamp((int)MathF.Floor(texel.X), 0, ScreenWidth - 1);
      int py = Math.Clamp((int)MathF.Floor(texel.Y), 0, ScreenHeight - 1);

      if (!TryGetTopmostSpriteTileAtPixel(gb, px, py, out var info))
      {
        return;
      }

      ApplyExportDirectoryToDialogs(settings.LastExportDirectory);
      _exportGb = gb;
      _exportPalette = palette;
      _exportRequest = ExportRequest.SpriteTileToFile;
      _exportSettings = settings;
      _exportSaveSettings = saveSettings;
      _spriteTileExport = info;
      _exportSpriteTileDialog.Show(ExportDialogCallback);
    }
    catch
    {
      // ignore
    }
  }

  private static bool TryGetTopmostSpriteTileAtPixel(Gameboy gb, int px, int py, out SpriteTileExportInfo info)
  {
    info = default;
    try
    {
      bool isCgb = gb._memory.IsCgb;
      // Use snapshot data to ensure consistency between OAM, tile data, and palettes
      gb._memory.GetVideoDebugSnapshot(out var vram, out _, out _, out var oam, out _);
      
      var tileData0 = new byte[0x1800];
      Array.Copy(vram, 0x0000, tileData0, 0, 0x1800);
      
      var tileData1 = isCgb ? new byte[0x1800] : Array.Empty<byte>();
      if (isCgb)
      {
        Array.Copy(vram, 0x2000, tileData1, 0, 0x1800);
      }
      
      bool use8x16 = gb._memory.OBJSize;
      int spriteHeight = use8x16 ? 16 : 8;
      byte obp0 = gb._memory.OBP0;
      byte obp1 = gb._memory.OBP1;

      // Iterate in reverse (39 to 0) to find the topmost sprite, matching RenderCgbSpriteLayerToRgba order.
      // Lower OAM indices are drawn last (on top).
      for (int spriteIndex = 39; spriteIndex >= 0; spriteIndex--)
      {
        int baseAddr = spriteIndex * 4;
        if (baseAddr + 3 >= oam.Length)
        {
          continue;
        }

        int y = oam[baseAddr + 0] - 16;
        int x = oam[baseAddr + 1] - 8;
        int tile = oam[baseAddr + 2];
        byte attr = oam[baseAddr + 3];

        if (px < x || px >= x + 8 || py < y || py >= y + spriteHeight)
        {
          continue;
        }

        bool yFlip = (attr & 0x40) != 0;
        bool xFlip = (attr & 0x20) != 0;
        bool useObp1 = (attr & 0x10) != 0;
        byte dmgPal = useObp1 ? obp1 : obp0;

        int vramBank = isCgb && (attr & 0x08) != 0 ? 1 : 0;
        int cgbPal = isCgb ? (attr & 0x07) : 0;
        var tileData = vramBank == 0 ? tileData0 : tileData1;

        int localY = py - y;
        int localX = px - x;
        if (yFlip) localY = (spriteHeight - 1 - localY);
        if (xFlip) localX = (7 - localX);

        int baseTile = use8x16 ? (tile & 0xFE) : tile;
        int tileIndex = baseTile;
        int tileY = localY;
        if (use8x16)
        {
          tileIndex = baseTile + (tileY >= 8 ? 1 : 0);
          tileY &= 7;
        }

        int rowIndex = (tileIndex * 16) + (tileY * 2);
        byte b1 = (uint)rowIndex < (uint)tileData.Length ? tileData[rowIndex] : (byte)0;
        byte b2 = (uint)(rowIndex + 1) < (uint)tileData.Length ? tileData[rowIndex + 1] : (byte)0;
        int bit = 7 - localX;
        int colorIndex = ((b1 >> bit) & 1) | (((b2 >> bit) & 1) << 1);
        if (colorIndex == 0)
        {
          continue;
        }

        info = new SpriteTileExportInfo
        {
          SpriteIndex = spriteIndex,
          TileIndex = tileIndex,
          XFlip = xFlip,
          YFlip = yFlip,
          DmgPalette = dmgPal,
          CgbPalette = cgbPal,
          VramBank = vramBank,
          // Store the screen position of the 8x8 tile (not the click position)
          ScreenX = x,
          ScreenY = use8x16 ? (y + (localY >= 8 ? 8 : 0)) : y,
        };
        return true;
      }
    }
    catch
    {
      // ignore
    }

    return false;
  }

  private static Vector2 FitAspect(Vector2 avail, float aspect)
  {
    float w = MathF.Max(1f, avail.X);
    float h = w / MathF.Max(0.0001f, aspect);
    if (h > avail.Y && avail.Y > 1f)
    {
      h = MathF.Max(1f, avail.Y);
      w = h * aspect;
    }
    return new Vector2(w, h);
  }

  private InspectorFlags GetInspectorFlags()
  {
    InspectorFlags flags = InspectorFlags.None;
    if (!_inspectShowTooltip)
    {
      flags |= InspectorFlags.NoTooltip;
    }

    if (!_inspectShowGrid)
    {
      flags |= InspectorFlags.NoGrid;
    }

    if (!_inspectAutoReadTexture)
    {
      flags |= InspectorFlags.NoAutoReadTexture;
    }

    if (!_inspectForceNearest)
    {
      flags |= InspectorFlags.NoForceFilterNearest;
    }

    return flags;
  }

  private static readonly InspectorFlags ManagedInspectorFlags =
      InspectorFlags.NoTooltip |
      InspectorFlags.NoGrid |
      InspectorFlags.ShowWrap |
      InspectorFlags.NoAutoReadTexture |
      InspectorFlags.NoForceFilterNearest;

  private static void ApplyNextInspectorSettings(InspectorFlags desired, InspectorAlphaMode alphaMode)
  {
    var toSet = desired;
    var toClear = ManagedInspectorFlags & ~desired;
    InspectorPanel.SetNextPanelFlags(toSet, toClear);
    InspectorPanel.SetNextPanelAlphaMode(alphaMode);

    // Use a tile-sized grid: each Game Boy tile is 8x8 pixels.
    InspectorPanel.SetNextPanelGridCellSize(new Vector2(8, 8));
    // Show tile grid once a tile is at least ~8px (scale >= 1).
    InspectorPanel.SetNextPanelMinimumGridScale(1.0f);
  }

  private void PersistInspectorSettings(AppSettings settings, Action saveSettings)
  {
    settings.InspectorShowGrid = _inspectShowGrid;
    settings.InspectorShowTooltip = _inspectShowTooltip;
    settings.InspectorAutoReadTexture = _inspectAutoReadTexture;
    settings.InspectorForceNearest = _inspectForceNearest;
    settings.InspectorAlphaMode = (int)_inspectAlphaMode;

    try
    {
      saveSettings();
    }
    catch
    {
      // ignore
    }
  }

  private void ApplyExportDirectoryToDialogs(string? dir)
  {
    if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
    {
      return;
    }

    foreach (var dialog in new object[] { _exportTilesFolderDialog, _exportBgDialog, _exportWindowDialog, _exportSpritesDialog, _exportSpriteTileDialog })
    {
      TrySetStringProperty(dialog, new[]
      {
                "InitialDirectory",
                "InitialPath",
                "CurrentDirectory",
                "CurrentPath",
                "CurrentFolder",
                "Directory",
                "Folder",
                "Path",
            }, dir);
    }
  }

  private void UpdateLastExportDirectory(string pathOrFolder)
  {
    if (_exportSettings == null)
    {
      return;
    }

    string? dir = pathOrFolder;
    try
    {
      if (File.Exists(pathOrFolder) || Path.HasExtension(pathOrFolder))
      {
        dir = Path.GetDirectoryName(pathOrFolder);
      }
    }
    catch
    {
      // ignore
    }

    if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
    {
      return;
    }

    _exportSettings.LastExportDirectory = dir;

    try
    {
      _exportSaveSettings?.Invoke();
    }
    catch
    {
      // ignore
    }
  }

  private static void TrySetStringProperty(object target, string[] propertyNames, string value)
  {
    var t = target.GetType();
    foreach (var name in propertyNames)
    {
      var p = t.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
      if (p == null || !p.CanWrite || p.PropertyType != typeof(string))
      {
        continue;
      }

      try
      {
        p.SetValue(target, value);
        return;
      }
      catch
      {
        // try next
      }
    }
  }

  private void RenderBgWinExtraTooltip(Gameboy gb, bool isWindow, int px, int py)
  {
    px = Math.Clamp(px, 0, TileMapSizePixels - 1);
    py = Math.Clamp(py, 0, TileMapSizePixels - 1);

    int tileX = px / 8;
    int tileY = py / 8;
    int inTileX = px & 7;
    int inTileY = py & 7;

    ushort mapBase = isWindow
        ? (gb._memory.WindowTileMapDisplaySelect ? (ushort)0x9C00 : (ushort)0x9800)
        : (gb._memory.BGTileMapDisplaySelect ? (ushort)0x9C00 : (ushort)0x9800);

    bool useSignedTileNumbers = !gb._memory.BGWindowTileDataSelect;
    var map = gb._memory.GetTileMap(mapBase);
    int mapIndex = (tileY * 32) + tileX;
    byte tileId = (uint)mapIndex < (uint)map.Length ? map[mapIndex] : (byte)0;

    int colorIndex;
    int shadeIndex;
    string extra = string.Empty;

    if (gb._memory.IsCgb)
    {
      gb._memory.GetVideoDebugSnapshot(out _, out var bgPalRam, out _, out _, out var lcdc);
      useSignedTileNumbers = (lcdc & 0x10) == 0;
      var attrMap = gb._memory.GetCgbTileMapAttributes(mapBase);
      byte attr = (uint)mapIndex < (uint)attrMap.Length ? attrMap[mapIndex] : (byte)0;
      int pal = attr & 0x07;
      int vramBank = (attr & 0x08) != 0 ? 1 : 0;
      bool xFlip = (attr & 0x20) != 0;
      bool yFlip = (attr & 0x40) != 0;
      bool pri = (attr & 0x80) != 0;

      var tileData0 = gb._memory.GetTileDataBank(0);
      var tileData1 = gb._memory.GetTileDataBank(1);
      var tileData = vramBank == 0 ? tileData0 : tileData1;

      int tileOffset = GetTileOffset(tileId, useSignedTileNumbers);
      int localX = xFlip ? (7 - inTileX) : inTileX;
      int localY = yFlip ? (7 - inTileY) : inTileY;
      int tileRowIndex = tileOffset + (localY * 2);
      byte b1 = (uint)tileRowIndex < (uint)tileData.Length ? tileData[tileRowIndex] : (byte)0;
      byte b2 = (uint)(tileRowIndex + 1) < (uint)tileData.Length ? tileData[tileRowIndex + 1] : (byte)0;
      int bit = 7 - localX;
      colorIndex = ((b1 >> bit) & 1) | (((b2 >> bit) & 1) << 1);
      shadeIndex = colorIndex;

      var rgb = GBMemory.DecodeCgbPaletteColorFromRam(bgPalRam, pal, colorIndex);
      extra = $"  Attr=0x{attr:X2} pal={pal} vbk={vramBank} flip={(xFlip ? "X" : "-")}{(yFlip ? "Y" : "-")} pri={(pri ? 1 : 0)} rgb=({rgb.R},{rgb.G},{rgb.B})";
    }
    else
    {
      int tileOffset = GetTileOffset(tileId, useSignedTileNumbers);
      int tileRowIndex = tileOffset + (inTileY * 2);
      var tileData = gb._memory.GetTileData();
      byte b1 = (uint)tileRowIndex < (uint)tileData.Length ? tileData[tileRowIndex] : (byte)0;
      byte b2 = (uint)(tileRowIndex + 1) < (uint)tileData.Length ? tileData[tileRowIndex + 1] : (byte)0;
      int bit = 7 - inTileX;
      colorIndex = ((b1 >> bit) & 1) | (((b2 >> bit) & 1) << 1);
      shadeIndex = ApplyBgpShading ? MapDmgPaletteToShade(gb._memory.BGP, colorIndex) : colorIndex;
    }

    ImGui.Text($"Pixel: ({px},{py})");
    ImGui.Text($"Tile: ({tileX},{tileY})  InTile: ({inTileX},{inTileY})");
    ImGui.Text($"MapBase: 0x{mapBase:X4}  TileId: 0x{tileId:X2} ({tileId})");
    ImGui.Text($"TileData: {(useSignedTileNumbers ? "signed" : "unsigned")}");
    ImGui.Text($"CI: {colorIndex}  Shade: {shadeIndex}  BGP: 0x{gb._memory.BGP:X2}{extra}");
  }

  private static void RenderSpriteExtraTooltip(Gameboy gb, int px, int py)
  {
    px = Math.Clamp(px, 0, ScreenWidth - 1);
    py = Math.Clamp(py, 0, ScreenHeight - 1);

    string info = DescribeSpritesAtPixel(gb, px, py);
    if (!string.IsNullOrWhiteSpace(info))
    {
      ImGui.TextWrapped(info);
    }
    else
    {
      ImGui.Text($"OBJ @ ({px},{py}): none");
    }
  }

  private static Rgba32 ToRgba32(DisplayPalette palette, int shadeIndex, bool transparent)
  {
    var c = palette.GetShade(shadeIndex);
    byte a = transparent ? (byte)0 : c.A;
    return new Rgba32(c.R, c.G, c.B, a);
  }

  private static Rgba32 ToRgba32(GBOG.CPU.Color c, bool transparent)
  {
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
      bool isCgb = gb._memory.IsCgb;

      int snapVer = gb._memory.VideoDebugSnapshotVersion;
      if (snapVer <= 0)
      {
        return;
      }

      gb._memory.GetVideoDebugSnapshot(out var vram, out var bgPal, out var objPal, out _, out _);
      _tileAtlasSnapshotVersion = snapVer;

      // Tile pattern data is 0x8000-0x97FF (384 tiles) per VRAM bank.
      var tileData0 = new byte[0x1800];
      Array.Copy(vram, 0x0000, tileData0, 0, 0x1800);
      var tiles0 = GraphicUtils.ConvertGBTileData(tileData0);
      if (tiles0.Count <= 0)
      {
        return;
      }

      List<byte[]>? tiles1 = null;
      if (isCgb)
      {
        var tileData1 = new byte[0x1800];
        Array.Copy(vram, 0x2000, tileData1, 0, 0x1800);
        tiles1 = GraphicUtils.ConvertGBTileData(tileData1);
      }

      int tilesPerBank = tiles0.Count;
      int tileCount = isCgb ? (tilesPerBank + (tiles1?.Count ?? 0)) : tilesPerBank;
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

        byte[] src;
        if (isCgb && i >= tilesPerBank)
        {
          int j = i - tilesPerBank;
          src = (tiles1 != null && (uint)j < (uint)tiles1.Count) ? tiles1[j] : tiles0[0];
        }
        else
        {
          src = tiles0[i];
        }

        for (int y = 0; y < tileSize; y++)
        {
          for (int x = 0; x < tileSize; x++)
          {
            int dstX = baseX + x;
            int dstY = baseY + y;
            int dst = (dstY * width + dstX) * 4;

            byte ci = src[y * tileSize + x];
            bool transparent = TreatColor0AsTransparent && ci == 0;

            if (isCgb)
            {
              // In CGB mode, colorize using the selected palette from the per-frame snapshot.
              // Clamp the palette index to valid range (0-7).
              int palIndex = Math.Max(0, Math.Min(7, _cgbTileAtlasPaletteIndex));
              byte[] activePal = _cgbTileAtlasPaletteIsObj ? objPal : bgPal;
              var c = GBOG.Memory.GBMemory.DecodeCgbPaletteColorFromRam(activePal, palIndex, ci);
              var px = ToRgba32(c, transparent);
              rgba[dst + 0] = px.R;
              rgba[dst + 1] = px.G;
              rgba[dst + 2] = px.B;
              rgba[dst + 3] = px.A;
            }
            else
            {
              int shadeIndex = ApplyBgpShading ? MapDmgPaletteToShade(gb._memory.BGP, ci) : ci;
              var c = ToRgba32(palette, shadeIndex, transparent);
              rgba[dst + 0] = c.R;
              rgba[dst + 1] = c.G;
              rgba[dst + 2] = c.B;
              rgba[dst + 3] = c.A;
            }
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
      bool isCgb = gb._memory.IsCgb;

      int snapVer = gb._memory.VideoDebugSnapshotVersion;
      if (snapVer <= 0)
      {
        return;
      }

      gb._memory.GetVideoDebugSnapshot(out var vram, out var bgPal, out _, out _, out var lcdc);
      _tileMapsSnapshotVersion = snapVer;

      var tileData = new byte[0x1800];
      Array.Copy(vram, 0x0000, tileData, 0, 0x1800);
      var tileData1 = isCgb ? new byte[0x1800] : Array.Empty<byte>();
      if (isCgb)
      {
        Array.Copy(vram, 0x2000, tileData1, 0, 0x1800);
      }

      byte bgp = gb._memory.BGP;

      bool bgMapSelect = (lcdc & 0x08) != 0;
      bool winMapSelect = (lcdc & 0x40) != 0;
      bool useSignedTileNumbers = (lcdc & 0x10) == 0;

      ushort bgMapBase = _bgMapSelectMode switch
      {
        TileMapSelectMode.Map9800 => (ushort)0x9800,
        TileMapSelectMode.Map9C00 => (ushort)0x9C00,
        _ => (bgMapSelect ? (ushort)0x9C00 : (ushort)0x9800),
      };
      ushort winMapBase = winMapSelect ? (ushort)0x9C00 : (ushort)0x9800;

      var bgMap = new byte[0x400];
      var winMap = new byte[0x400];
      Array.Copy(vram, bgMapBase - 0x8000, bgMap, 0, 0x400);
      Array.Copy(vram, winMapBase - 0x8000, winMap, 0, 0x400);

      byte[] rgbaBg;
      byte[] rgbaWin;
      if (isCgb)
      {
        var bgAttr = new byte[0x400];
        var winAttr = new byte[0x400];
        Array.Copy(vram, 0x2000 + (bgMapBase - 0x8000), bgAttr, 0, 0x400);
        Array.Copy(vram, 0x2000 + (winMapBase - 0x8000), winAttr, 0, 0x400);
        rgbaBg = RenderCgbTileMapToRgba(bgMap, bgAttr, tileData, tileData1, useSignedTileNumbers, TreatColor0AsTransparent, bgPal);
        rgbaWin = RenderCgbTileMapToRgba(winMap, winAttr, tileData, tileData1, useSignedTileNumbers, TreatColor0AsTransparent, bgPal);
      }
      else
      {
        rgbaBg = RenderTileMapToRgba(bgMap, tileData, useSignedTileNumbers, palette, TreatColor0AsTransparent, ApplyBgpShading, bgp);
        rgbaWin = RenderTileMapToRgba(winMap, tileData, useSignedTileNumbers, palette, TreatColor0AsTransparent, ApplyBgpShading, bgp);
      }

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

  private void TryUpdateSpriteLayer(Gameboy gb, GL gl, DisplayPalette palette)
  {
    try
    {
      bool isCgb = gb._memory.IsCgb;

      int snapVer = gb._memory.VideoDebugSnapshotVersion;
      if (snapVer <= 0)
      {
        return;
      }

      gb._memory.GetVideoDebugSnapshot(out var vram, out _, out var objPal, out _, out _);
      _spriteLayerSnapshotVersion = snapVer;

      var tileData = new byte[0x1800];
      Array.Copy(vram, 0x0000, tileData, 0, 0x1800);
      var tileData1 = isCgb ? new byte[0x1800] : Array.Empty<byte>();
      if (isCgb)
      {
        Array.Copy(vram, 0x2000, tileData1, 0, 0x1800);
      }
      var oam = gb._memory.OAMRam;
      bool use8x16 = gb._memory.OBJSize;
      byte obp0 = gb._memory.OBP0;
      byte obp1 = gb._memory.OBP1;

      var rgba = isCgb
        ? RenderCgbSpriteLayerToRgba(oam, tileData, tileData1, use8x16, objPal)
        : RenderSpriteLayerToRgba(oam, tileData, use8x16, palette, obp0, obp1);

      if (_spriteLayerTextureId == 0)
      {
        _spriteLayerTextureId = CreateTexture(gl, ScreenWidth, ScreenHeight);
      }

      fixed (byte* p = rgba)
      {
        gl.BindTexture(GLTextureTarget.Texture2D, _spriteLayerTextureId);
        gl.TexSubImage2D(GLTextureTarget.Texture2D, 0, 0, 0, ScreenWidth, ScreenHeight, GLPixelFormat.Rgba, GLPixelType.UnsignedByte, p);
      }

      _spriteLayerValid = true;
    }
    catch
    {
      // best-effort
    }
  }

  private static byte[] RenderCgbSpriteLayerToRgba(byte[] oam, byte[] tileData0, byte[] tileData1, bool use8x16, byte[] objPal)
  {
    var rgba = new byte[ScreenWidth * ScreenHeight * 4];
    int spriteHeight = use8x16 ? 16 : 8;

    // Draw in reverse OAM order so lower OAM index wins on overlap.
    for (int spriteIndex = 39; spriteIndex >= 0; spriteIndex--)
    {
      int baseAddr = spriteIndex * 4;
      if (baseAddr + 3 >= oam.Length)
      {
        continue;
      }

      int y = oam[baseAddr + 0] - 16;
      int x = oam[baseAddr + 1] - 8;
      int tile = oam[baseAddr + 2];
      byte attr = oam[baseAddr + 3];

      bool yFlip = (attr & 0x40) != 0;
      bool xFlip = (attr & 0x20) != 0;

      int vramBank = (attr & 0x08) != 0 ? 1 : 0;
      int pal = attr & 0x07;
      var bankTileData = vramBank == 0 ? tileData0 : tileData1;

      int baseTile = use8x16 ? (tile & 0xFE) : tile;

      for (int sy = 0; sy < spriteHeight; sy++)
      {
        int py = y + sy;
        if ((uint)py >= (uint)ScreenHeight)
        {
          continue;
        }

        int localY = yFlip ? (spriteHeight - 1 - sy) : sy;
        int tileY = localY;
        int tileIndex = baseTile;
        if (use8x16)
        {
          tileIndex = baseTile + (tileY >= 8 ? 1 : 0);
          tileY &= 7;
        }

        int rowIndex = (tileIndex * 16) + (tileY * 2);
        byte b1 = (uint)rowIndex < (uint)bankTileData.Length ? bankTileData[rowIndex] : (byte)0;
        byte b2 = (uint)(rowIndex + 1) < (uint)bankTileData.Length ? bankTileData[rowIndex + 1] : (byte)0;

        for (int sx = 0; sx < 8; sx++)
        {
          int px = x + sx;
          if ((uint)px >= (uint)ScreenWidth)
          {
            continue;
          }

          int localX = xFlip ? (7 - sx) : sx;
          int bit = 7 - localX;
          int colorIndex = ((b1 >> bit) & 1) | (((b2 >> bit) & 1) << 1);
          if (colorIndex == 0)
          {
            continue;
          }

          var c = GBOG.Memory.GBMemory.DecodeCgbPaletteColorFromRam(objPal, pal, colorIndex);
          int dst = (py * ScreenWidth + px) * 4;
          rgba[dst + 0] = c.R;
          rgba[dst + 1] = c.G;
          rgba[dst + 2] = c.B;
          rgba[dst + 3] = c.A;
        }
      }
    }

    return rgba;
  }

  private static byte[] RenderSpriteLayerToRgba(byte[] oam, byte[] tileData, bool use8x16, DisplayPalette palette, byte obp0, byte obp1)
  {
    var rgba = new byte[ScreenWidth * ScreenHeight * 4];
    // Start fully transparent.

    int spriteHeight = use8x16 ? 16 : 8;

    // Draw in reverse OAM order so lower OAM index wins on overlap.
    for (int spriteIndex = 39; spriteIndex >= 0; spriteIndex--)
    {
      int baseAddr = spriteIndex * 4;
      if (baseAddr + 3 >= oam.Length)
      {
        continue;
      }

      int y = oam[baseAddr + 0] - 16;
      int x = oam[baseAddr + 1] - 8;
      int tile = oam[baseAddr + 2];
      byte attr = oam[baseAddr + 3];

      bool yFlip = (attr & 0x40) != 0;
      bool xFlip = (attr & 0x20) != 0;
      bool useObp1 = (attr & 0x10) != 0;
      byte dmgPal = useObp1 ? obp1 : obp0;

      // 8x16 sprites use two stacked tiles, and the tile index is forced even.
      int baseTile = use8x16 ? (tile & 0xFE) : tile;

      for (int sy = 0; sy < spriteHeight; sy++)
      {
        int py = y + sy;
        if ((uint)py >= (uint)ScreenHeight)
        {
          continue;
        }

        int localY = yFlip ? (spriteHeight - 1 - sy) : sy;
        int tileY = localY;
        int tileIndex = baseTile;
        if (use8x16)
        {
          tileIndex = baseTile + (tileY >= 8 ? 1 : 0);
          tileY &= 7;
        }

        int rowIndex = (tileIndex * 16) + (tileY * 2);
        byte b1 = (uint)rowIndex < (uint)tileData.Length ? tileData[rowIndex] : (byte)0;
        byte b2 = (uint)(rowIndex + 1) < (uint)tileData.Length ? tileData[rowIndex + 1] : (byte)0;

        for (int sx = 0; sx < 8; sx++)
        {
          int px = x + sx;
          if ((uint)px >= (uint)ScreenWidth)
          {
            continue;
          }

          int localX = xFlip ? (7 - sx) : sx;
          int bit = 7 - localX;
          int colorIndex = ((b1 >> bit) & 1) | (((b2 >> bit) & 1) << 1);
          if (colorIndex == 0)
          {
            // OBJ color index 0 is always transparent.
            continue;
          }

          int shadeIndex = MapDmgPaletteToShade(dmgPal, colorIndex);
          var c = ToRgba32(palette, shadeIndex, transparent: false);

          int dst = (py * ScreenWidth + px) * 4;
          rgba[dst + 0] = c.R;
          rgba[dst + 1] = c.G;
          rgba[dst + 2] = c.B;
          rgba[dst + 3] = c.A;
        }
      }
    }

    return rgba;
  }

  private static string DescribeSpritesAtPixel(Gameboy gb, int sx, int sy)
  {
    try
    {
      var oam = gb._memory.OAMRam;
      var tileData = gb._memory.GetTileData();
      bool use8x16 = gb._memory.OBJSize;

      int spriteHeight = use8x16 ? 16 : 8;
      int found = 0;
      var sb = new StringBuilder();

      sb.Append($"OBJ @ ({sx},{sy})\n");

      for (int spriteIndex = 0; spriteIndex < 40; spriteIndex++)
      {
        int baseAddr = spriteIndex * 4;
        if (baseAddr + 3 >= oam.Length)
        {
          break;
        }

        int y = oam[baseAddr + 0] - 16;
        int x = oam[baseAddr + 1] - 8;
        int tile = oam[baseAddr + 2];
        byte attr = oam[baseAddr + 3];

        if (sx < x || sx >= x + 8 || sy < y || sy >= y + spriteHeight)
        {
          continue;
        }

        bool yFlip = (attr & 0x40) != 0;
        bool xFlip = (attr & 0x20) != 0;
        bool useObp1 = (attr & 0x10) != 0;
        bool behindBg = (attr & 0x80) != 0;

        int localY = sy - y;
        int localX = sx - x;
        if (yFlip) localY = (spriteHeight - 1 - localY);
        if (xFlip) localX = (7 - localX);

        int baseTile = use8x16 ? (tile & 0xFE) : tile;
        int tileIndex = baseTile;
        int tileY = localY;
        if (use8x16)
        {
          tileIndex = baseTile + (tileY >= 8 ? 1 : 0);
          tileY &= 7;
        }

        int rowIndex = (tileIndex * 16) + (tileY * 2);
        byte b1 = (uint)rowIndex < (uint)tileData.Length ? tileData[rowIndex] : (byte)0;
        byte b2 = (uint)(rowIndex + 1) < (uint)tileData.Length ? tileData[rowIndex + 1] : (byte)0;
        int bit = 7 - localX;
        int colorIndex = ((b1 >> bit) & 1) | (((b2 >> bit) & 1) << 1);
        if (colorIndex == 0)
        {
          continue;
        }

        found++;
        if (found <= 8)
        {
          sb.Append($"- OAM#{spriteIndex:00} pos=({x},{y}) tile={tile} (rowTile={tileIndex}) pal={(useObp1 ? "OBP1" : "OBP0")} flip={(xFlip ? "X" : "-")}{(yFlip ? "Y" : "-")} pri={(behindBg ? "BehindBG" : "Front")}\n");
        }
      }

      if (found == 0)
      {
        return string.Empty;
      }

      if (found > 8)
      {
        sb.Append($"(+{found - 8} more)\n");
      }

      sb.Append("Tip: sprites that form a character will usually be a cluster of OAM entries near the same area; the tile IDs shown above are the ones you want to inspect in the Tile Data atlas.");
      return sb.ToString();
    }
    catch
    {
      return string.Empty;
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

  private static byte[] RenderCgbTileMapToRgba(byte[] tileMap, byte[] attrMap, byte[] tileData0, byte[] tileData1, bool useSignedTileNumbers, bool treatCi0Transparent, byte[] bgPalRam)
  {
    var rgba = new byte[TileMapSizePixels * TileMapSizePixels * 4];

    for (int mapY = 0; mapY < 32; mapY++)
    {
      for (int mapX = 0; mapX < 32; mapX++)
      {
        int mapIndex = (mapY * 32) + mapX;
        byte tileId = (uint)mapIndex < (uint)tileMap.Length ? tileMap[mapIndex] : (byte)0;
        byte attr = (uint)mapIndex < (uint)attrMap.Length ? attrMap[mapIndex] : (byte)0;

        int pal = attr & 0x07;
        int vramBank = (attr & 0x08) != 0 ? 1 : 0;
        bool xFlip = (attr & 0x20) != 0;
        bool yFlip = (attr & 0x40) != 0;

        int tileBase = GetTileOffset(tileId, useSignedTileNumbers);
        var tileData = vramBank == 0 ? tileData0 : tileData1;

        for (int y = 0; y < 8; y++)
        {
          int localY = yFlip ? (7 - y) : y;
          int rowIndex = tileBase + (localY * 2);
          byte b1 = (uint)rowIndex < (uint)tileData.Length ? tileData[rowIndex] : (byte)0;
          byte b2 = (uint)(rowIndex + 1) < (uint)tileData.Length ? tileData[rowIndex + 1] : (byte)0;

          for (int x = 0; x < 8; x++)
          {
            int localX = xFlip ? (7 - x) : x;
            int bit = 7 - localX;
            int colorIndex = ((b1 >> bit) & 1) | (((b2 >> bit) & 1) << 1);
            bool transparent = treatCi0Transparent && colorIndex == 0;
            var c = GBOG.Memory.GBMemory.DecodeCgbPaletteColorFromRam(bgPalRam, pal, colorIndex);
            var pxColor = ToRgba32(c, transparent);

            int px = (mapX * 8) + x;
            int py = (mapY * 8) + y;
            int dst = (py * TileMapSizePixels + px) * 4;
            rgba[dst + 0] = pxColor.R;
            rgba[dst + 1] = pxColor.G;
            rgba[dst + 2] = pxColor.B;
            rgba[dst + 3] = pxColor.A;
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
