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
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GBOG
{
    public unsafe class GameWindow
    {
        private Hexa.NET.GLFW.GLFWwindowPtr _window;
        private Gameboy? _gb;
        private uint _textureId;
        private int _width = 1280;
        private int _height = 720;
        private string _serialOutput = "";
        private string? _loadedRomName;
        private bool _gameRunning = false;
        private GL _gl = null!;
        private ImGuiContextPtr _guiContext;
        private const float UiScale = 1.25f;

        private float _fontSizePixels = 16.0f;
        private float? _requestedFontSizePixels;
        private const float MinFontSizePixels = 10.0f;
        private const float MaxFontSizePixels = 32.0f;
        
        private FileOpenDialog _fileOpenDialog = null!;

        private bool _showCpuStateWindow;
        private bool _showMemoryViewerWindow;

        private sealed class MemoryRegion
        {
            public required string Name { get; init; }
            public required ushort BaseAddress { get; init; }
            public required int Size { get; init; }
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

        public void Run()
        {
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

            _textureId = CreateTexture();
            _fileOpenDialog = new FileOpenDialog();
            EnsureMemoryViewerStates();

            while (GLFW.WindowShouldClose(_window) == 0)
            {
                GLFW.PollEvents();

                UpdateJoypadFromKeyboard();

                if (_requestedFontSizePixels.HasValue)
                {
                    var requested = _requestedFontSizePixels.Value;
                    _requestedFontSizePixels = null;
                    ApplyFontSize(requested, rebuildBackend: true);
                }

                ImGuiImplOpenGL3.NewFrame();
                ImGuiImplGLFW.NewFrame();
                ImGui.NewFrame();

                // Dockspace
                var dockspaceId = ImGui.GetID("MyDockSpace");
                ImGui.DockSpaceOverViewport(dockspaceId, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

                RenderUI();

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

            // Cleanup
            if (_gb != null)
            {
                _gb.EndGame();
            }
            
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
                _ = _gb._memory.Joypad;
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
            _ = _gb._memory.Joypad;
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

        private void RenderUI()
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Load ROM"))
                    {
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
                    if (ImGui.MenuItem("Start", string.Empty, _gameRunning, _gb != null))
                    {
                        _gameRunning = true;
                        if (_gb != null)
                        {
                            _ = _gb.RunGame();
                        }
                    }
                    if (ImGui.MenuItem("Stop", string.Empty, !_gameRunning, _gb != null))
                    {
                        _gb?.EndGame();
                        _gameRunning = false;
                    }
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

            ImGui.Begin("Game View");
            ImGui.Text($"ROM: {(_loadedRomName ?? "None")}");
            if (_gb != null)
            {
                UpdateTexture();
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
                    ReadOnly = true,
                    Separators = 8,
                    UserData = region,
                    ReadCallback = ReadMemoryRegion,
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

            int max = Math.Min(size, region.Size - offset);
            if (max <= 0)
            {
                return 0;
            }

            int addr0 = region.BaseAddress + offset;
            for (int i = 0; i < max; i++)
            {
                buffer[i] = _gb._memory.PeekByte((ushort)(addr0 + i));
            }

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

        private void LoadRomCallback(object? sender, DialogResult result)
        {
            if (result == DialogResult.Ok)
            {
                var path = _fileOpenDialog.SelectedFile;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    _loadedRomName = Path.GetFileName(path);
                    _gb = new Gameboy();
                    _gb.ConfigureAudioOutput(true);
                    _gb.LimitSpeed = true;
                    _gb._memory.SerialDataReceived += (sender, data) => _serialOutput += data;
                    _gb.LoadRom(path);
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
