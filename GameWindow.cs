using GBOG.CPU;
using GBOG.Controls.ImGuiHexEditor;
using Hexa.NET.GLFW;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.GLFW;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.ImGui.Widgets;
using Hexa.NET.ImGui.Widgets.Dialogs;
using Hexa.NET.OpenGL;
using HexaGen.Runtime;
using GBOG.Utils;
using GBOG.Graphics.UI;
using System.Reflection;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GBOG
{
    public unsafe class GameWindow
    {
        private AppSettings _settings = new();

        private Hexa.NET.GLFW.GLFWwindowPtr _window;
        private Gameboy? _gb;
        private uint _textureId;
        private int _width = 1280;
        private int _height = 720;
        private string _serialOutput = "";
        private string? _loadedRomName;
        private string? _loadedRomPath;
        private bool _gameRunning = false;
        private bool _gamePaused = false;
        private bool _frameAdvanceJustRequested = false;
        private int _saveStateSlot = 0;

        private EventHandler<char>? _serialHandler;

        private static readonly byte[] _blankFrame = new byte[160 * 144 * 4];

        private bool _audioEnabled = true;
        private float _audioVolume = 1.0f;

        private bool _preferCgbWhenSupported;

        private string _displayPalettePreset = DisplayPalettes.PresetDmg;
        private Vector4 _customShade0 = new(1f, 1f, 1f, 1f);
        private Vector4 _customShade1 = new(0.6667f, 0.6667f, 0.6667f, 1f);
        private Vector4 _customShade2 = new(0.3333f, 0.3333f, 0.3333f, 1f);
        private Vector4 _customShade3 = new(0f, 0f, 0f, 1f);

        private static readonly string[] _paletteKeys =
        [
            DisplayPalettes.PresetDmg,
            DisplayPalettes.PresetPocket,
            DisplayPalettes.PresetGreen,
            DisplayPalettes.PresetBlue,
            DisplayPalettes.PresetCustom,
        ];

        private static readonly string[] _paletteLabels =
        [
            "DMG (Default)",
            "Pocket",
            "Green",
            "Blue",
            "Custom",
        ];

        private GL _gl = null!;
        private ImGuiContextPtr _guiContext;
        private const float UiScale = 1.25f;

        private float _fontSizePixels = 16.0f;
        private float? _requestedFontSizePixels;
        private const float MinFontSizePixels = 10.0f;
        private const float MaxFontSizePixels = 32.0f;
        private int _lastFlushedSaveVersion;
        private int _lastSeenSaveVersion;
        private long _lastSeenSaveDirtyTick;
        private const int SaveDebounceMs = 2000;
        
        private FileOpenDialog _fileOpenDialog = null!;

        private bool _showCpuStateWindow;
        private bool _showMemoryViewerWindow;
        private bool _showTileDataViewerWindow;

        private readonly ImGuiTileMapViewerWindow _tileViewer = new();

        private sealed class MemoryRegion
        {
            public required string Name { get; init; }
            public required ushort BaseAddress { get; init; }
            public required int Size { get; init; }

            // When non-zero, reads for this region are served from a snapshot buffer instead of live memory.
            // This prevents live updates from clobbering in-progress edits (e.g. between hex nibbles).
            public int FreezeRefreshFrames { get; set; }
        }

        private readonly MemoryRegion[] _memoryRegions =
        [
            new MemoryRegion { Name = "ROM0 (0000-3FFF)", BaseAddress = 0x0000, Size = 0x4000 },
            new MemoryRegion { Name = "ROMX (4000-7FFF)", BaseAddress = 0x4000, Size = 0x4000 },
            new MemoryRegion { Name = "VRAM (8000-9FFF)", BaseAddress = 0x8000, Size = 0x2000 },
            new MemoryRegion { Name = "ERAM (A000-BFFF)", BaseAddress = 0xA000, Size = 0x2000 },
            new MemoryRegion { Name = "WRAM (C000-DFFF)", BaseAddress = 0xC000, Size = 0x2000 },
            new MemoryRegion { Name = "OAM (FE00-FE9F)", BaseAddress = 0xFE00, Size = 0x00A0 },
            new MemoryRegion { Name = "IO (FF00-FF7F)", BaseAddress = 0xFF00, Size = 0x0080 },
            new MemoryRegion { Name = "HRAM (FF80-FFFE)", BaseAddress = 0xFF80, Size = 0x007F },
            new MemoryRegion { Name = "IE (FFFF)", BaseAddress = 0xFFFF, Size = 0x0001 },
        ];

        private readonly Dictionary<string, HexEditorState> _memoryViewerStates = new();

        private string _wramSearchHex = string.Empty;
        private string _wramSearchError = string.Empty;
        private byte[]? _wramSearchPattern;
        private readonly List<int> _wramSearchResults = new();
        private int _wramSearchSelectedIndex = -1;
        private readonly byte[] _wramSearchBuffer = new byte[0x2000];

        public void Run()
        {
            _settings = SettingsStore.Load();
            ApplySettingsToFields();
            _tileViewer.ApplySettings(_settings);

            if (GLFW.Init() == 0)
            {
                Console.WriteLine("Failed to initialize GLFW");
                return;
            }

            GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MAJOR, 3);
            GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MINOR, 3);
            GLFW.WindowHint(GLFW.GLFW_OPENGL_PROFILE, GLFW.GLFW_OPENGL_CORE_PROFILE);

            _window = GLFW.CreateWindow(_width, _height, "GBOG ImGui", null, null);
            if (_window.IsNull)
            {
                Console.WriteLine("Failed to create GLFW window");
                GLFW.Terminate();
                return;
            }

            GLFW.MakeContextCurrent(_window);
            GLFW.SwapInterval(1); // Enable vsync

            _gl = new GL(new GLFWContext(_window));

            // Setup ImGui context
            _guiContext = ImGui.CreateContext();
            ImGui.SetCurrentContext(_guiContext);
            var io = ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;     // Enable Keyboard Controls
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;         // Enable Docking
            io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;       // Enable Multi-Viewport / Platform Windows

            ImGui.StyleColorsDark();
            ImGui.GetStyle().ScaleAllSizes(UiScale);
            ApplyFontSize(_fontSizePixels, rebuildBackend: false);

            ImGuiImplGLFW.SetCurrentContext(_guiContext);
            // BitCast to convert between the two GLFWwindowPtr types (Hexa.NET.GLFW and Hexa.NET.ImGui.Backends.GLFW)
            var imguiWindowPtr = Unsafe.BitCast<Hexa.NET.GLFW.GLFWwindowPtr, Hexa.NET.ImGui.Backends.GLFW.GLFWwindowPtr>(_window);
            if (!ImGuiImplGLFW.InitForOpenGL(imguiWindowPtr, true))
            {
                 Console.WriteLine("Failed to init ImGui Impl GLFW");
                 GLFW.Terminate();
                 return;
            }
            
            ImGuiImplOpenGL3.SetCurrentContext(_guiContext);
            if (!ImGuiImplOpenGL3.Init("#version 330"))
            {
                 Console.WriteLine("Failed to init ImGui Impl OpenGL3");
                 GLFW.Terminate();
                 return;
            }

            // Initialize ImGuiTexInspect render-state/shader integration.
            // Without this, inspectors still render (via ImGui.Image), but grid/alpha/background features won't work.
            if (!GBOG.ImGuiTexInspect.Backend.OpenGL.RenderState.Initialize(_gl, "#version 330"))
            {
                Console.WriteLine("WARNING: Failed to init ImGuiTexInspect RenderState (grid/alpha modes may not work).");
            }

            _textureId = CreateTexture();
            ClearDisplayTexture();
            _fileOpenDialog = new FileOpenDialog();
            ApplySettingsToFileDialog();
            EnsureMemoryViewerStates();

            while (GLFW.WindowShouldClose(_window) == 0)
            {
                GLFW.PollEvents();

                UpdateJoypadFromKeyboard();
                HandleGlobalShortcuts();

                if (_requestedFontSizePixels.HasValue)
                {
                    var requested = _requestedFontSizePixels.Value;
                    _requestedFontSizePixels = null;
                    ApplyFontSize(requested, rebuildBackend: true);
                    SaveSettings();
                }

                ImGuiImplOpenGL3.NewFrame();
                ImGuiImplGLFW.NewFrame();
                ImGui.NewFrame();

                // Dockspace
                var dockspaceId = ImGui.GetID("MyDockSpace");
                ImGui.DockSpaceOverViewport(dockspaceId, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

                RenderUI();

                UpdateBatterySaveAutosave();

                ImGui.Render();
                int display_w, display_h;
                GLFW.GetFramebufferSize(_window, &display_w, &display_h);
                _gl.Viewport(0, 0, display_w, display_h);
                _gl.ClearColor(0.45f, 0.55f, 0.60f, 1.00f);
                _gl.Clear(GLClearBufferMask.ColorBufferBit);
                ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

                if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
                {
                    ImGui.UpdatePlatformWindows();
                    ImGui.RenderPlatformWindowsDefault();
                    GLFW.MakeContextCurrent(_window);
                }

                GLFW.SwapBuffers(_window);
            }

            // Persist window size for next run.
            try
            {
                int w, h;
                GLFW.GetWindowSize(_window, &w, &h);
                if (w > 0 && h > 0)
                {
                    _width = w;
                    _height = h;
                }
            }
            catch
            {
                // ignore
            }

            SaveSettings();

            // Cleanup
            if (_gb != null)
            {
                TryFlushBatterySave(_gb, force: true);
                _gb.EndGame();
            }

            // Cleanup ImGuiTexInspect resources (safe to call even if init failed).
            GBOG.ImGuiTexInspect.Backend.OpenGL.RenderState.Shutdown();
            
            ImGuiImplOpenGL3.Shutdown();
            ImGuiImplGLFW.Shutdown();
            ImGui.DestroyContext();

            GLFW.DestroyWindow(_window);
            GLFW.Terminate();
        }

        private void UpdateJoypadFromKeyboard()
        {
            if (_gb == null || !_gameRunning)
            {
                return;
            }

            var keys = _gb._memory._joyPadKeys;
            if (keys == null || keys.Length < 8)
            {
                return;
            }

            if (GLFW.GetWindowAttrib(_window, GLFW.GLFW_FOCUSED) == 0)
            {
                Array.Clear(keys, 0, keys.Length);
                _gb._memory.NotifyJoypadStateChanged();
                return;
            }

            keys[0] = IsKeyDown(GlfwKey.Right) || IsKeyDown(GlfwKey.D); // Right
            keys[1] = IsKeyDown(GlfwKey.Left) || IsKeyDown(GlfwKey.A);  // Left
            keys[2] = IsKeyDown(GlfwKey.Up) || IsKeyDown(GlfwKey.W);    // Up
            keys[3] = IsKeyDown(GlfwKey.Down) || IsKeyDown(GlfwKey.S);  // Down

            keys[4] = IsKeyDown(GlfwKey.Z);         // A
            keys[5] = IsKeyDown(GlfwKey.X);         // B
            keys[6] = IsKeyDown(GlfwKey.Backspace); // Select
            keys[7] = IsKeyDown(GlfwKey.Enter);     // Start

            // Force JOYP refresh so edge-triggered interrupt can fire even if the game
            // doesn't read FF00 this frame.
            _gb._memory.NotifyJoypadStateChanged();
        }

        private void HandleGlobalShortcuts()
        {
            // Only process shortcuts when our window is focused.
            if (GLFW.GetWindowAttrib(_window, GLFW.GLFW_FOCUSED) == 0)
            {
                return;
            }

            var io = ImGui.GetIO();
            if (io.WantTextInput)
            {
                return;
            }

            bool changed = false;

            // Emulation shortcuts
            if (ImGui.IsKeyPressed(ImGuiKey.F5))
            {
                if (io.KeyShift)
                {
                    SaveState();
                }
                else if (_gb != null && _gameRunning && _gamePaused)
                {
                    _gb.Resume();
                    _gamePaused = false;
                }
                else
                {
                    EnsureGameboyLoaded();
                    if (_gb != null)
                    {
                        _gb.ConfigureAudioOutput(_audioEnabled);
                        _gb.SetAudioVolume(_audioVolume);
                        _ = _gb.RunGame();
                        _gameRunning = true;
                        _gamePaused = false;
                    }
                }
            }

            if (ImGui.IsKeyPressed(ImGuiKey.F6))
            {
                if (io.KeyShift)
                {
                    LoadState();
                }
                else if (_gb != null && _gameRunning && !_gamePaused)
                {
                    _gb.Pause();
                    _gamePaused = true;
                }
            }

            if (ImGui.IsKeyPressed(ImGuiKey.F7))
            {
                if (_gb != null && _gameRunning)
                {
                    StopEmulation(clearLoadedRom: false);
                }
            }

            if (ImGui.IsKeyPressed(ImGuiKey.F8))
            {
                if (_gamePaused)
                {
                    _gb?.RequestFrameAdvance();
                    _frameAdvanceJustRequested = true;
                }
            }

            // Audio shortcuts (avoid overlapping with gameplay keys).
            if (ImGui.IsKeyPressed(ImGuiKey.F9))
            {
                ToggleAudioEnabled();
                changed = true;
            }

            const float step = 0.05f;
            if (ImGui.IsKeyPressed(ImGuiKey.F10, true))
            {
                AdjustAudioVolume(-step);
                changed = true;
            }
            if (ImGui.IsKeyPressed(ImGuiKey.F11, true))
            {
                AdjustAudioVolume(step);
                changed = true;
            }

            if (changed)
            {
                SaveSettings();
            }
        }

        private void ToggleAudioEnabled()
        {
            _audioEnabled = !_audioEnabled;
            if (_gb != null)
            {
                _gb.ConfigureAudioOutput(_audioEnabled);
                if (_audioEnabled)
                {
                    _gb.SetAudioVolume(_audioVolume);
                }
            }
        }

        private void AdjustAudioVolume(float delta)
        {
            _audioVolume = Math.Clamp(_audioVolume + delta, 0f, 1f);
            _gb?.SetAudioVolume(_audioVolume);
        }

        private bool IsKeyDown(GlfwKey key)
        {
            return GLFW.GetKey(_window, (int)key) == GLFW.GLFW_PRESS;
        }

        private uint CreateTexture()
        {
            uint textureId;
            _gl.GenTextures(1, &textureId);
            _gl.BindTexture(GLTextureTarget.Texture2D, textureId);
            _gl.TexParameteri(GLTextureTarget.Texture2D, GLTextureParameterName.MinFilter, (int)GLTextureMinFilter.Nearest);
            _gl.TexParameteri(GLTextureTarget.Texture2D, GLTextureParameterName.MagFilter, (int)GLTextureMagFilter.Nearest);
            _gl.TexImage2D(GLTextureTarget.Texture2D, 0, GLInternalFormat.Rgba, 160, 144, 0, GLPixelFormat.Rgba, GLPixelType.UnsignedByte, null);
            return textureId;
        }

        private uint CreateTexture(int width, int height)
        {
            uint textureId;
            _gl.GenTextures(1, &textureId);
            _gl.BindTexture(GLTextureTarget.Texture2D, textureId);
            _gl.TexParameteri(GLTextureTarget.Texture2D, GLTextureParameterName.MinFilter, (int)GLTextureMinFilter.Nearest);
            _gl.TexParameteri(GLTextureTarget.Texture2D, GLTextureParameterName.MagFilter, (int)GLTextureMagFilter.Nearest);
            _gl.TexImage2D(GLTextureTarget.Texture2D, 0, GLInternalFormat.Rgba, width, height, 0, GLPixelFormat.Rgba, GLPixelType.UnsignedByte, null);
            return textureId;
        }

        private void UpdateTexture()
        {
            if (_gb == null) return;
            var pixels = _gb.GetDisplayArray();
            if (pixels == null) return;

            fixed (byte* p = pixels)
            {
                _gl.BindTexture(GLTextureTarget.Texture2D, _textureId);
                _gl.TexSubImage2D(GLTextureTarget.Texture2D, 0, 0, 0, 160, 144, GLPixelFormat.Rgba, GLPixelType.UnsignedByte, p);
            }
        }

        private void ClearDisplayTexture()
        {
            fixed (byte* p = _blankFrame)
            {
                _gl.BindTexture(GLTextureTarget.Texture2D, _textureId);
                _gl.TexSubImage2D(GLTextureTarget.Texture2D, 0, 0, 0, 160, 144, GLPixelFormat.Rgba, GLPixelType.UnsignedByte, p);
            }
        }

        private void EnsureGameboyLoaded()
        {
            if (_gb != null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_loadedRomPath) || !File.Exists(_loadedRomPath))
            {
                return;
            }

            var gb = new Gameboy();
            gb.SetDisplayPalette(GetCurrentDisplayPalette());
            gb.ConfigureAudioOutput(_audioEnabled);
            gb.SetAudioVolume(_audioVolume);
            gb.LimitSpeed = true;

            // Reset UI-facing state for a fresh run.
            _serialOutput = string.Empty;

            _serialHandler = (_, data) => _serialOutput += data;
            gb._memory.SerialDataReceived += _serialHandler;

            gb.LoadRom(_loadedRomPath, _preferCgbWhenSupported);

            TryLoadBatterySave(gb);
            _lastFlushedSaveVersion = gb._memory.SaveDirtyVersion;
            _lastSeenSaveVersion = _lastFlushedSaveVersion;
            _lastSeenSaveDirtyTick = 0;

            _gb = gb;

            _tileViewer.InvalidateAll();
            ClearDisplayTexture();
        }

        private void StopEmulation(bool clearLoadedRom)
        {
            var gb = _gb;
            if (gb != null)
            {
                try
                {
                    TryFlushBatterySave(gb, force: true);
                    gb.EndGame();
                }
                catch
                {
                    // ignore
                }

                try
                {
                    if (_serialHandler != null)
                    {
                        gb._memory.SerialDataReceived -= _serialHandler;
                    }
                }
                catch
                {
                    // ignore
                }
            }

            _serialHandler = null;
            _gb = null;
            _gameRunning = false;
            _gamePaused = false;

            _lastFlushedSaveVersion = 0;
            _lastSeenSaveVersion = 0;
            _lastSeenSaveDirtyTick = 0;

            // Clear UI so we don't show stale pixels/state.
            ClearDisplayTexture();
            _tileViewer.InvalidateAll();

            if (clearLoadedRom)
            {
                _loadedRomName = null;
                _loadedRomPath = null;
                _serialOutput = string.Empty;
            }
        }

        private void RenderUI()
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Load ROM"))
                    {
                        ApplySettingsToFileDialog();
                        _fileOpenDialog.Show(LoadRomCallback);
                    }
                    if (ImGui.MenuItem("Exit"))
                    {
                        GLFW.SetWindowShouldClose(_window, 1);
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("View"))
                {
                    var canIncrease = _fontSizePixels < MaxFontSizePixels;
                    var canDecrease = _fontSizePixels > MinFontSizePixels;

                    if (ImGui.MenuItem("CPU State", string.Empty, _showCpuStateWindow, true))
                    {
                        _showCpuStateWindow = !_showCpuStateWindow;
                    }

                    if (ImGui.MenuItem("Memory Viewer", string.Empty, _showMemoryViewerWindow, true))
                    {
                        _showMemoryViewerWindow = !_showMemoryViewerWindow;
                    }

                    if (ImGui.MenuItem("Tile Data Viewer", string.Empty, _showTileDataViewerWindow, true))
                    {
                        _showTileDataViewerWindow = !_showTileDataViewerWindow;
                    }

                    ImGui.Separator();

                    if (ImGui.MenuItem("Increase Font Size", string.Empty, false, canIncrease))
                    {
                        RequestFontSize(_fontSizePixels + 1.0f);
                    }

                    if (ImGui.MenuItem("Decrease Font Size", string.Empty, false, canDecrease))
                    {
                        RequestFontSize(_fontSizePixels - 1.0f);
                    }

                    ImGui.MenuItem($"Font Size: {_fontSizePixels:0} px", string.Empty, false, false);
                    ImGui.EndMenu();
                }
                
                if (ImGui.BeginMenu("Emulation"))
                {
                    bool preferCgb = _preferCgbWhenSupported;
                    if (ImGui.MenuItem("Prefer CGB for dual-mode ROMs", string.Empty, preferCgb, true))
                    {
                        _preferCgbWhenSupported = !_preferCgbWhenSupported;
                        SaveSettings();
                    }

                    ImGui.Separator();

                    bool canStart = !_gameRunning && (_gb != null || !string.IsNullOrWhiteSpace(_loadedRomPath));
                    if (ImGui.MenuItem("Start", "F5", _gameRunning && !_gamePaused, canStart))
                    {
                        EnsureGameboyLoaded();
                        if (_gb != null)
                        {
                            _gb.ConfigureAudioOutput(_audioEnabled);
                            _gb.SetAudioVolume(_audioVolume);
                            _ = _gb.RunGame();
                            _gameRunning = true;
                            _gamePaused = false;
                        }
                    }

                    bool canPause = _gb != null && _gameRunning && !_gamePaused;
                    if (ImGui.MenuItem("Pause", "F6", _gamePaused, canPause))
                    {
                        _gb?.Pause();
                        _gamePaused = true;
                    }

                    bool canResume = _gb != null && _gameRunning && _gamePaused;
                    if (ImGui.MenuItem("Resume", "F5", false, canResume))
                    {
                        _gb?.Resume();
                        _gamePaused = false;
                    }

                    if (ImGui.MenuItem("Frame Advance", "F8", false, _gamePaused))
                    {
                        _gb?.RequestFrameAdvance();
                        _frameAdvanceJustRequested = true;
                    }

                    bool canStop = _gb != null && _gameRunning;
                    if (ImGui.MenuItem("Stop", "F7", false, canStop))
                    {
                        StopEmulation(clearLoadedRom: false);
                    }

                    ImGui.Separator();

                    if (ImGui.BeginMenu("Save State Slot"))
                    {
                        for (int i = 0; i <= 9; i++)
                        {
                            if (ImGui.MenuItem($"Slot {i}", string.Empty, _saveStateSlot == i))
                            {
                                _saveStateSlot = i;
                            }
                        }
                        ImGui.EndMenu();
                    }

                    bool canSaveLoad = _gb != null;
                    if (ImGui.MenuItem("Save State", "Shift+F5", false, canSaveLoad))
                    {
                        SaveState();
                    }
                    if (ImGui.MenuItem("Load State", "Shift+F6", false, canSaveLoad))
                    {
                        LoadState();
                    }

                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Audio"))
                {
                    bool enabled = _audioEnabled;
                    if (ImGui.MenuItem("Enabled", "F9", enabled, true))
                    {
                        ToggleAudioEnabled();

                        SaveSettings();
                    }

                    ImGui.Separator();

                    const float step = 0.05f;

                    // Keep the menu open while clicking volume adjustments.
                    ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);

                    if (ImGui.MenuItem("Volume -", "F10", false, true))
                    {
                        AdjustAudioVolume(-step);

                        SaveSettings();
                    }
                    if (ImGui.MenuItem("Volume +", "F11", false, true))
                    {
                        AdjustAudioVolume(step);

                        SaveSettings();
                    }

                    ImGui.PopItemFlag();

                    ImGui.MenuItem($"Volume: {(int)MathF.Round(_audioVolume * 100f)}%", string.Empty, false, false);
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Video"))
                {
                    RenderPaletteMenu();
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

            ImGui.Begin("Game View");
            ImGui.Text($"ROM: {(_loadedRomName ?? "None")}");

            if (_gb != null)
            {
                var mem = _gb._memory;
                string cartType = mem.CartRequiresCgb
                    ? "CGB-only"
                    : (mem.CartSupportsCgb ? "Dual-mode" : "DMG-only");
                string mode = mem.IsCgb ? "CGB" : "DMG";
                string modeReason = mem.CartRequiresCgb
                    ? "(forced by ROM)"
                    : (mem.CartSupportsCgb
                        ? (_preferCgbWhenSupported ? "(preferred by setting)" : "(defaulting to DMG)"
                        )
                        : string.Empty);

                ImGui.Text($"Emulation: {mode}  |  Cart: {cartType}  |  Prefer CGB: {(_preferCgbWhenSupported ? "On" : "Off")} {modeReason}");

                // Display palette affects DMG rendering only. In CGB mode we use CGB palettes.
                ImGui.Text($"Display palette: {_displayPalettePreset} (DMG-only)");

                if (mem.IsCgb)
                {
                    var bg0c0 = mem.GetCgbBgPaletteColor(0, 0);
                    var bg0c1 = mem.GetCgbBgPaletteColor(0, 1);
                    var bg0c2 = mem.GetCgbBgPaletteColor(0, 2);
                    var bg0c3 = mem.GetCgbBgPaletteColor(0, 3);
                    var obj0c1 = mem.GetCgbObjPaletteColor(0, 1);
                    ImGui.Text($"CGB palette writes: BG={mem.CgbBgPaletteWriteCount} OBJ={mem.CgbObjPaletteWriteCount}");
                    ImGui.Text($"BG0: ({bg0c0.R},{bg0c0.G},{bg0c0.B}) ({bg0c1.R},{bg0c1.G},{bg0c1.B}) ({bg0c2.R},{bg0c2.G},{bg0c2.B}) ({bg0c3.R},{bg0c3.G},{bg0c3.B})");
                    ImGui.Text($"OBJ0 col1: ({obj0c1.R},{obj0c1.G},{obj0c1.B})");
                }
            }

            if (_gb != null && _gameRunning)
            {
                if (!_gamePaused || (_gb.IsPaused && _frameAdvanceJustRequested))
                {
                    UpdateTexture();
                    _frameAdvanceJustRequested = false;
                }
            }

            {
                var avail = ImGui.GetContentRegionAvail();
                float aspect = 160f / 144f;
                float w = avail.X;
                float h = w / aspect;
                if (h > avail.Y)
                {
                    h = avail.Y;
                    w = h * aspect;
                }

                ImGui.Image(new ImTextureRef(null, _textureId), new Vector2(w, h));
            }
            ImGui.End();

            ImGui.Begin("Serial Output");
            ImGui.Text(_serialOutput);
            ImGui.End();

            _fileOpenDialog.Draw(ImGuiWindowFlags.None);

            RenderCpuStateWindow();
            RenderMemoryViewerWindow();
            RenderTileDataViewerWindow();
        }

        private void RenderPaletteMenu()
        {
            if (!ImGui.BeginMenu("Palette"))
            {
                return;
            }

            int current = Array.IndexOf(_paletteKeys, _displayPalettePreset);
            if (current < 0)
            {
                current = 0;
            }

            int selected = current;
            if (ImGui.Combo("Preset", ref selected, _paletteLabels, _paletteLabels.Length))
            {
                _displayPalettePreset = _paletteKeys[Math.Clamp(selected, 0, _paletteKeys.Length - 1)];
                ApplyCurrentDisplayPalette();
                SaveSettings();
            }

            if (_displayPalettePreset == DisplayPalettes.PresetCustom)
            {
                ImGui.Separator();
                var flags = ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoAlpha;

                bool changed = false;
                changed |= ImGui.ColorEdit4("Shade 0", ref _customShade0, flags);
                changed |= ImGui.ColorEdit4("Shade 1", ref _customShade1, flags);
                changed |= ImGui.ColorEdit4("Shade 2", ref _customShade2, flags);
                changed |= ImGui.ColorEdit4("Shade 3", ref _customShade3, flags);

                if (changed)
                {
                    ForceOpaque(ref _customShade0);
                    ForceOpaque(ref _customShade1);
                    ForceOpaque(ref _customShade2);
                    ForceOpaque(ref _customShade3);
                    ApplyCurrentDisplayPalette();
                    SaveSettings();
                }
            }

            ImGui.EndMenu();
        }

        private static void ForceOpaque(ref Vector4 c)
        {
            c.W = 1f;
        }

        private void ApplyCurrentDisplayPalette()
        {
            var palette = GetCurrentDisplayPalette();
            _gb?.SetDisplayPalette(palette);
            _tileViewer.InvalidateAll();
        }

        private DisplayPalette GetCurrentDisplayPalette()
        {
            if (_displayPalettePreset == DisplayPalettes.PresetCustom)
            {
                return new DisplayPalette(
                    FromVector4(_customShade0),
                    FromVector4(_customShade1),
                    FromVector4(_customShade2),
                    FromVector4(_customShade3));
            }

            return DisplayPalettes.GetPreset(_displayPalettePreset);
        }

        private static Vector4 ToVector4(Color c)
        {
            return new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        }

        private static Color FromVector4(Vector4 v)
        {
            static byte ToByte(float x)
            {
                int i = (int)MathF.Round(Math.Clamp(x, 0f, 1f) * 255f);
                return (byte)Math.Clamp(i, 0, 255);
            }

            return new Color
            {
                R = ToByte(v.X),
                G = ToByte(v.Y),
                B = ToByte(v.Z),
                A = 255,
            };
        }

        private static uint PackRgba(Color c)
        {
            return (uint)(c.R | (c.G << 8) | (c.B << 16) | (c.A << 24));
        }

        private static Color UnpackRgba(uint rgba)
        {
            return new Color
            {
                R = (byte)(rgba & 0xFF),
                G = (byte)((rgba >> 8) & 0xFF),
                B = (byte)((rgba >> 16) & 0xFF),
                A = (byte)((rgba >> 24) & 0xFF),
            };
        }

        private void RenderTileDataViewerWindow()
        {
            _tileViewer.Render(ref _showTileDataViewerWindow, _gb, _gl, GetCurrentDisplayPalette(), _settings, SaveSettings);
        }

        private void EnsureMemoryViewerStates()
        {
            if (_memoryViewerStates.Count != 0)
            {
                return;
            }

            foreach (var region in _memoryRegions)
            {
                _memoryViewerStates[region.Name] = new HexEditorState
                {
                    MaxBytes = region.Size,
                    BytesPerLine = 16,
                    ShowAddress = true,
                    ShowAscii = true,
                    // Keep ROM read-only by default. All other regions are writable.
                    ReadOnly = region.BaseAddress < 0x8000,
                    Separators = 8,
                    UserData = region,
                    Bytes = new byte[region.Size],
                    ReadCallback = ReadMemoryRegion,
                    WriteCallback = WriteMemoryRegion,
                    GetAddressNameCallback = GetRegionAddressName,
                };
            }
        }

        private int ReadMemoryRegion(HexEditorState state, int offset, byte[] buffer, int size)
        {
            if (_gb == null)
            {
                return 0;
            }

            if (state.UserData is not MemoryRegion region)
            {
                return 0;
            }

            // Ensure we have a snapshot buffer.
            if (state.Bytes == null || state.Bytes.Length != region.Size)
            {
                state.Bytes = new byte[region.Size];
            }

            int max = Math.Min(size, region.Size - offset);
            if (max <= 0)
            {
                return 0;
            }

            // If we recently edited this region, freeze refresh and serve reads from the snapshot.
            // This prevents the emulator's live writes from changing the byte between the first and
            // second nibble entry when editing in hex.
            if (region.FreezeRefreshFrames > 0)
            {
                region.FreezeRefreshFrames--;
                Array.Copy(state.Bytes, offset, buffer, 0, max);
                return max;
            }

            int addr0 = region.BaseAddress + offset;
            for (int i = 0; i < max; i++)
            {
                byte v = _gb._memory.PeekByte((ushort)(addr0 + i));
                buffer[i] = v;
                state.Bytes[offset + i] = v;
            }

            return max;
        }

        private int WriteMemoryRegion(HexEditorState state, int offset, byte[] buffer, int size)
        {
            if (_gb == null)
            {
                return 0;
            }

            if (state.UserData is not MemoryRegion region)
            {
                return 0;
            }

            // Ensure we have a snapshot buffer.
            if (state.Bytes == null || state.Bytes.Length != region.Size)
            {
                state.Bytes = new byte[region.Size];
            }

            // Do not allow editing ROM via this viewer.
            if (region.BaseAddress < 0x8000)
            {
                return 0;
            }

            int max = Math.Min(size, region.Size - offset);
            if (max <= 0)
            {
                return 0;
            }

            int addr0 = region.BaseAddress + offset;
            for (int i = 0; i < max; i++)
            {
                byte v = buffer[i];
                _gb._memory.WriteByte((ushort)(addr0 + i), v);
                state.Bytes[offset + i] = v;
            }

            // Freeze refresh for a short window so multi-nibble edits don't get clobbered by live updates.
            region.FreezeRefreshFrames = Math.Max(region.FreezeRefreshFrames, 90);

            return max;
        }

        private bool GetRegionAddressName(HexEditorState state, int offset, out string addressName)
        {
            if (state.UserData is MemoryRegion region)
            {
                addressName = (region.BaseAddress + offset).ToString("X4");
                return true;
            }

            addressName = offset.ToString("X4");
            return true;
        }

        private void RenderCpuStateWindow()
        {
            if (!_showCpuStateWindow)
            {
                return;
            }

            if (!ImGui.Begin("CPU State", ref _showCpuStateWindow))
            {
                ImGui.End();
                return;
            }

            if (_gb == null)
            {
                ImGui.Text("No ROM loaded.");
                ImGui.End();
                return;
            }

            var gb = _gb;

            ImGui.Text($"Running: {_gameRunning}");
            ImGui.Text($"IME: {gb.InterruptMasterEnabled}  HALT: {gb.Halt}  CGB DoubleSpeed: {gb.DoubleSpeed}");

            var stats = gb.Stats;
            if (stats != null)
            {
                ImGui.Text($"Frame: target {stats.TargetFrameMs:0.00} ms, host {stats.HostFrameMs:0.00} ms, work {stats.EmuWorkMs:0.00} ms, wait {stats.ThrottleWaitMs:0.00} ms");
                ImGui.Text($"Speed: {stats.SpeedMultiplier:0.00}x  Hot PC: {stats.HotPc:X4} (repeats {stats.HotPcRepeats})");
                ImGui.Text($"Ops: {stats.InstructionsThisFrame}");
                ImGui.Text($"GC: gen0 +{stats.Gc0Collections}, gen1 +{stats.Gc1Collections}, gen2 +{stats.Gc2Collections}");

                if (double.IsNaN(stats.EmuWorkCpuMs))
                {
                    ImGui.Text($"CPU: n/a, alloc {stats.AllocBytesThisFrame / 1024.0:0.0} KB");
                }
                else
                {
                    double preemptMs = stats.EmuWorkMs - stats.EmuWorkCpuMs;
                    if (preemptMs < 0) preemptMs = 0;
                    ImGui.Text($"CPU: {stats.EmuWorkCpuMs:0.00} ms, preempt {preemptMs:0.00} ms, alloc {stats.AllocBytesThisFrame / 1024.0:0.0} KB");
                }

                if (stats.HasThreadCpuCycles)
                {
                    ImGui.Text($"CPU cycles: {stats.ThreadCpuCyclesThisFrame:N0}");
                }
            }
            ImGui.Separator();

            if (ImGui.BeginTable("##cpu_regs", 4, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text($"AF: {gb.AF:X4}");
                ImGui.TableNextColumn(); ImGui.Text($"BC: {gb.BC:X4}");
                ImGui.TableNextColumn(); ImGui.Text($"DE: {gb.DE:X4}");
                ImGui.TableNextColumn(); ImGui.Text($"HL: {gb.HL:X4}");

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text($"A: {gb.A:X2}  F: {gb.F:X2}");
                ImGui.TableNextColumn(); ImGui.Text($"B: {gb.B:X2}  C: {gb.C:X2}");
                ImGui.TableNextColumn(); ImGui.Text($"D: {gb.D:X2}  E: {gb.E:X2}");
                ImGui.TableNextColumn(); ImGui.Text($"H: {gb.H:X2}  L: {gb.L:X2}");

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text($"PC: {gb.PC:X4}");
                ImGui.TableNextColumn(); ImGui.Text($"SP: {gb.SP:X4}");
                ImGui.TableNextColumn(); ImGui.Text($"Z:{(gb.Z ? 1 : 0)} N:{(gb.N ? 1 : 0)} H:{(gb.HC ? 1 : 0)} C:{(gb.CF ? 1 : 0)}");
                ImGui.TableNextColumn(); ImGui.Text($"IE: {gb._memory.InterruptEnableRegister:X2}  IF: {gb._memory.IF:X2}");

                ImGui.EndTable();
            }

            ImGui.End();
        }

        private void RenderMemoryViewerWindow()
        {
            if (!_showMemoryViewerWindow)
            {
                return;
            }

            if (!ImGui.Begin("Memory Viewer", ref _showMemoryViewerWindow))
            {
                ImGui.End();
                return;
            }

            if (_gb == null)
            {
                ImGui.Text("No ROM loaded.");
                ImGui.End();
                return;
            }

            if (ImGui.BeginTabBar("##mem_tabs"))
            {
                foreach (var region in _memoryRegions)
                {
                    if (ImGui.BeginTabItem(region.Name))
                    {
                        var state = _memoryViewerStates[region.Name];

                        // WRAM search (minimal: hex byte pattern -> results -> click to jump)
                        if (region.BaseAddress == 0xC000 && region.Size == 0x2000)
                        {
                            RenderWramSearchControls(region, state);
                        }

                        var avail = ImGui.GetContentRegionAvail();
                        if (HexEditor.BeginHexEditor($"##hex_{region.BaseAddress:X4}", state, avail))
                        {
                            HexEditor.EndHexEditor();
                        }
                        ImGui.EndTabItem();
                    }
                }
                ImGui.EndTabBar();
            }

            ImGui.End();
        }

        private void RenderWramSearchControls(MemoryRegion region, HexEditorState state)
        {
            ImGui.Text("Search (hex bytes):");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(240);
            ImGui.InputText("##wram_search", ref _wramSearchHex, 256);
            ImGui.SameLine();
            bool doSearch = ImGui.Button("Search##wram");
            ImGui.SameLine();
            if (ImGui.Button("Clear##wram"))
            {
                _wramSearchError = string.Empty;
                _wramSearchPattern = null;
                _wramSearchResults.Clear();
                _wramSearchSelectedIndex = -1;
            }

            if (doSearch)
            {
                if (!TryParseHexPattern(_wramSearchHex, out var pattern, out var error))
                {
                    _wramSearchError = error;
                    _wramSearchPattern = null;
                    _wramSearchResults.Clear();
                    _wramSearchSelectedIndex = -1;
                }
                else
                {
                    _wramSearchError = string.Empty;
                    _wramSearchPattern = pattern;
                    DoWramSearch(region, state, pattern);
                }
            }

            if (!string.IsNullOrEmpty(_wramSearchError))
            {
                ImGui.TextDisabled(_wramSearchError);
            }

            if (_wramSearchPattern != null)
            {
                ImGui.Text($"Results: {_wramSearchResults.Count}  (pattern {_wramSearchPattern.Length} byte(s))");
            }

            if (_wramSearchResults.Count > 0)
            {
                // Keep the results list compact so the hex view remains usable.
                float listHeight = Math.Min(160, ImGui.GetTextLineHeightWithSpacing() * 8);
                if (ImGui.BeginChild("##wram_search_results", new Vector2(0, listHeight), ImGuiChildFlags.Borders))
                {
                    int maxToShow = Math.Min(_wramSearchResults.Count, 512);
                    for (int i = 0; i < maxToShow; i++)
                    {
                        int off = _wramSearchResults[i];
                        ushort addr = (ushort)(region.BaseAddress + off);
                        bool selected = i == _wramSearchSelectedIndex;
                        if (ImGui.Selectable($"{addr:X4} (+0x{off:X})##wram_res_{i}", selected))
                        {
                            _wramSearchSelectedIndex = i;
                            JumpHexEditorToMatch(state, off, _wramSearchPattern?.Length ?? 1);
                        }
                    }
                }
                ImGui.EndChild();
            }

            ImGui.Separator();
        }

        private void DoWramSearch(MemoryRegion region, HexEditorState state, byte[] pattern)
        {
            _wramSearchResults.Clear();
            _wramSearchSelectedIndex = -1;

            // Prefer searching the frozen snapshot while edits are in progress.
            byte[] haystack;
            if (region.FreezeRefreshFrames > 0 && state.Bytes != null && state.Bytes.Length == region.Size)
            {
                haystack = state.Bytes;
            }
            else
            {
                // Refresh the whole region from live memory (WRAM is small: 8KB).
                for (int i = 0; i < region.Size; i++)
                {
                    _wramSearchBuffer[i] = _gb!._memory.PeekByte((ushort)(region.BaseAddress + i));
                }
                // Keep the snapshot buffer in sync for subsequent browsing.
                if (state.Bytes != null && state.Bytes.Length == region.Size)
                {
                    Array.Copy(_wramSearchBuffer, 0, state.Bytes, 0, region.Size);
                }
                haystack = _wramSearchBuffer;
            }

            if (pattern.Length == 0 || pattern.Length > region.Size)
            {
                return;
            }

            int lastStart = region.Size - pattern.Length;
            for (int i = 0; i <= lastStart; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (haystack[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    _wramSearchResults.Add(i);
                    if (_wramSearchResults.Count >= 4096)
                    {
                        // Safety cap; keeps UI responsive.
                        break;
                    }
                }
            }

            if (_wramSearchResults.Count > 0)
            {
                _wramSearchSelectedIndex = 0;
                JumpHexEditorToMatch(state, _wramSearchResults[0], pattern.Length);
            }
        }

        private static void JumpHexEditorToMatch(HexEditorState state, int offset, int length)
        {
            int start = Math.Clamp(offset, 0, Math.Max(0, state.MaxBytes - 1));
            int end = Math.Clamp(offset + Math.Max(1, length) - 1, 0, Math.Max(0, state.MaxBytes - 1));
            state.SelectStartByte = start;
            state.SelectStartSubByte = 0;
            state.SelectEndByte = end;
            state.SelectEndSubByte = 1;
            state.LastSelectedByte = start;
            state.RequestScrollToByte = start;
        }

        private static bool TryParseHexPattern(string input, out byte[] pattern, out string error)
        {
            pattern = Array.Empty<byte>();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Enter hex bytes like 'DE AD BE EF' or 'DEADBEEF'.";
                return false;
            }

            string trimmed = input.Trim();

            // If it contains whitespace or separators, parse as tokens.
            bool tokenMode = trimmed.Any(char.IsWhiteSpace) || trimmed.Contains(',');
            if (tokenMode)
            {
                var bytes = new List<byte>();
                var tokens = trimmed
                    .Split(new[] { ' ', '\t', '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var t0 in tokens)
                {
                    string t = t0.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? t0[2..] : t0;
                    if (t.Length == 0)
                    {
                        continue;
                    }
                    if (t.Length > 2)
                    {
                        error = $"Invalid byte token '{t0}'. Use 1-2 hex digits per byte.";
                        return false;
                    }
                    if (!byte.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out var b))
                    {
                        error = $"Invalid hex byte '{t0}'.";
                        return false;
                    }
                    bytes.Add(b);
                }

                if (bytes.Count == 0)
                {
                    error = "Enter hex bytes like 'DE AD' or 'DEADBEEF'.";
                    return false;
                }

                pattern = bytes.ToArray();
                return true;
            }

            // Otherwise, parse as a continuous hex string.
            string s = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? trimmed[2..] : trimmed;
            s = s.Replace("_", string.Empty);
            if ((s.Length & 1) != 0)
            {
                error = "Hex string must have an even number of digits.";
                return false;
            }
            if (s.Length == 0)
            {
                error = "Enter hex bytes like 'DEADBEEF'.";
                return false;
            }

            int count = s.Length / 2;
            var outBytes = new byte[count];
            for (int i = 0; i < count; i++)
            {
                string byteStr = s.Substring(i * 2, 2);
                if (!byte.TryParse(byteStr, System.Globalization.NumberStyles.HexNumber, null, out outBytes[i]))
                {
                    error = $"Invalid hex byte '{byteStr}'.";
                    return false;
                }
            }
            pattern = outBytes;
            return true;
        }

        private void RequestFontSize(float sizePixels)
        {
            var clamped = Math.Clamp(sizePixels, MinFontSizePixels, MaxFontSizePixels);
            if (Math.Abs(clamped - _fontSizePixels) < 0.001f)
            {
                return;
            }

            _fontSizePixels = clamped;
            _requestedFontSizePixels = clamped;
        }

        private void ApplySettingsToFields()
        {
            _audioEnabled = _settings.AudioEnabled;
            _audioVolume = Math.Clamp(_settings.AudioVolume, 0f, 1f);
            _fontSizePixels = Math.Clamp(_settings.FontSizePixels, MinFontSizePixels, MaxFontSizePixels);

            _preferCgbWhenSupported = _settings.PreferCgbWhenSupported;

            _displayPalettePreset = string.IsNullOrWhiteSpace(_settings.DisplayPalettePreset)
                ? DisplayPalettes.PresetDmg
                : _settings.DisplayPalettePreset;

            _customShade0 = ToVector4(UnpackRgba(_settings.CustomPalette0));
            _customShade1 = ToVector4(UnpackRgba(_settings.CustomPalette1));
            _customShade2 = ToVector4(UnpackRgba(_settings.CustomPalette2));
            _customShade3 = ToVector4(UnpackRgba(_settings.CustomPalette3));
            ForceOpaque(ref _customShade0);
            ForceOpaque(ref _customShade1);
            ForceOpaque(ref _customShade2);
            ForceOpaque(ref _customShade3);

            if (_settings.WindowWidth >= 320 && _settings.WindowWidth <= 8192)
            {
                _width = _settings.WindowWidth;
            }
            if (_settings.WindowHeight >= 240 && _settings.WindowHeight <= 8192)
            {
                _height = _settings.WindowHeight;
            }
        }

        private void SaveSettings()
        {
            _settings.AudioEnabled = _audioEnabled;
            _settings.AudioVolume = Math.Clamp(_audioVolume, 0f, 1f);
            _settings.FontSizePixels = _fontSizePixels;
            _settings.WindowWidth = _width;
            _settings.WindowHeight = _height;

            _settings.PreferCgbWhenSupported = _preferCgbWhenSupported;

            _settings.DisplayPalettePreset = _displayPalettePreset;
            _settings.CustomPalette0 = PackRgba(FromVector4(_customShade0));
            _settings.CustomPalette1 = PackRgba(FromVector4(_customShade1));
            _settings.CustomPalette2 = PackRgba(FromVector4(_customShade2));
            _settings.CustomPalette3 = PackRgba(FromVector4(_customShade3));

            try
            {
                SettingsStore.Save(_settings);
            }
            catch
            {
                // ignore
            }
        }

        private void ApplySettingsToFileDialog()
        {
            string? dir = _settings.LastRomDirectory;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                return;
            }

            TrySetStringProperty(_fileOpenDialog, new[]
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

        private static void TrySetStringProperty(object target, string[] propertyNames, string value)
        {
            Type t = target.GetType();
            foreach (var name in propertyNames)
            {
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
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

        private void ApplyFontSize(float sizePixels, bool rebuildBackend)
        {
            var io = ImGui.GetIO();

            io.Fonts.Clear();

            var fontConfig = ImGui.ImFontConfig();
            fontConfig.SizePixels = sizePixels;
            io.FontDefault = ImGui.AddFontDefault(io.Fonts, fontConfig);

            // Let the backend handle texture creation/updates via ImGuiBackendFlags_RendererHasTextures.
            // We only mark the atlas texture as needing (re)creation.
            var texData = io.Fonts.TexData;
            if (!texData.IsNull)
            {
                texData.Status = ImTextureStatus.WantCreate;
                texData.WantDestroyNextFrame = false;
            }
        }

        private string? GetSaveFilePath()
        {
            if (string.IsNullOrWhiteSpace(_loadedRomPath))
            {
                return null;
            }

            return Path.ChangeExtension(_loadedRomPath, ".sav");
        }

        private void TryLoadBatterySave(Gameboy gb)
        {
            try
            {
                if (!gb._memory.HasBatteryBackedSave)
                {
                    return;
                }

                string? savePath = GetSaveFilePath();
                if (string.IsNullOrWhiteSpace(savePath) || !File.Exists(savePath))
                {
                    return;
                }

                byte[] data = File.ReadAllBytes(savePath);
                gb._memory.LoadBatterySaveFileImage(data);
            }
            catch
            {
                // ignore
            }
        }

        private void UpdateBatterySaveAutosave()
        {
            var gb = _gb;
            if (gb == null)
            {
                return;
            }

            if (!gb._memory.HasBatteryBackedSave)
            {
                return;
            }

            int version = gb._memory.SaveDirtyVersion;
            if (version == _lastFlushedSaveVersion)
            {
                return;
            }

            long now = Environment.TickCount64;
            if (version != _lastSeenSaveVersion)
            {
                _lastSeenSaveVersion = version;
                _lastSeenSaveDirtyTick = now;
            }

            if (_lastSeenSaveDirtyTick == 0)
            {
                _lastSeenSaveDirtyTick = now;
            }

            if (now - _lastSeenSaveDirtyTick < SaveDebounceMs)
            {
                return;
            }

            if (TryFlushBatterySave(gb, force: false))
            {
                _lastFlushedSaveVersion = version;
            }
        }

        private bool TryFlushBatterySave(Gameboy gb, bool force)
        {
            try
            {
                if (!gb._memory.HasBatteryBackedSave)
                {
                    return false;
                }

                int version = gb._memory.SaveDirtyVersion;
                if (!force && version == _lastFlushedSaveVersion)
                {
                    return false;
                }

                string? savePath = GetSaveFilePath();
                if (string.IsNullOrWhiteSpace(savePath))
                {
                    return false;
                }

                byte[] image = gb._memory.GetBatterySaveFileImage();
                if (image.Length == 0)
                {
                    return false;
                }

                string tmp = savePath + ".tmp";
                File.WriteAllBytes(tmp, image);
                File.Move(tmp, savePath, overwrite: true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void SaveState()
        {
            if (_gb == null || string.IsNullOrWhiteSpace(_loadedRomPath)) return;

            bool wasRunning = _gameRunning && !_gamePaused;
            if (wasRunning)
            {
                _gb.Pause();
                _gb.WaitForPause();
            }

            string path = GetSaveStatePath(_saveStateSlot);
            try
            {
                using var stream = File.Create(path);
                using var writer = new BinaryWriter(stream);
                _gb.SaveState(writer);
                Console.WriteLine($"Saved state to {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save state: {ex.Message}");
            }
            finally
            {
                if (wasRunning) _gb.Resume();
            }
        }

        private void LoadState()
        {
            if (_gb == null || string.IsNullOrWhiteSpace(_loadedRomPath)) return;

            string path = GetSaveStatePath(_saveStateSlot);
            if (!File.Exists(path))
            {
                Console.WriteLine($"Save state file not found: {path}");
                return;
            }

            bool wasRunning = _gameRunning && !_gamePaused;
            if (wasRunning)
            {
                _gb.Pause();
                _gb.WaitForPause();
            }

            try
            {
                using var stream = File.OpenRead(path);
                using var reader = new BinaryReader(stream);
                _gb.LoadState(reader);
                Console.WriteLine($"Loaded state from {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load state: {ex.Message}");
            }
            finally
            {
                if (wasRunning) _gb.Resume();
            }
        }

        private string GetSaveStatePath(int slot)
        {
            if (string.IsNullOrWhiteSpace(_loadedRomPath)) return string.Empty;
            return Path.ChangeExtension(_loadedRomPath, $".s{slot}");
        }

        private void LoadRomCallback(object? sender, DialogResult result)
        {
            if (result == DialogResult.Ok)
            {
                var path = _fileOpenDialog.SelectedFile;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    // If a ROM is currently running/paused, stop and clear state before switching.
                    StopEmulation(clearLoadedRom: false);

                    _loadedRomName = Path.GetFileName(path);
                    _loadedRomPath = path;

                    try
                    {
                        _settings.LastRomDirectory = Path.GetDirectoryName(path);
                        SaveSettings();
                    }
                    catch
                    {
                        // ignore
                    }

                    // Load ROM into a fresh emulator instance (state reset).
                    EnsureGameboyLoaded();
                }
            }
        }
    }

    public unsafe class GLFWContext : IGLContext
    {
        private readonly Hexa.NET.GLFW.GLFWwindowPtr _window;

        public GLFWContext(Hexa.NET.GLFW.GLFWwindowPtr window)
        {
            _window = window;
        }

        public nint Handle => (nint)_window.Handle;

        public void MakeCurrent()
        {
            GLFW.MakeContextCurrent(_window);
        }

        public void SwapBuffers()
        {
            GLFW.SwapBuffers(_window);
        }

        public void SwapInterval(int interval)
        {
            GLFW.SwapInterval(interval);
        }
        
        public nint GetProcAddress(string procName)
        {
            return (nint)GLFW.GetProcAddress(procName);
        }

        public bool TryGetProcAddress(string procName, out nint procAddress)
        {
            procAddress = (nint)GLFW.GetProcAddress(procName);
            return procAddress != 0;
        }

        public bool IsExtensionSupported(string extensionName)
        {
            return GLFW.ExtensionSupported(extensionName) != 0;
        }
        
        public bool IsCurrent => GLFW.GetCurrentContext() == _window;
        public void Dispose() { }
    }
}
