using GBOG.CPU;
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
        private Gameboy _gb;
        private uint _textureId;
        private int _width = 1280;
        private int _height = 720;
        private string _serialOutput = "";
        private bool _gameRunning = false;
        private GL _gl;
        private ImGuiContextPtr _guiContext;
        
        private FileOpenDialog _fileOpenDialog;

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

            while (GLFW.WindowShouldClose(_window) == 0)
            {
                GLFW.PollEvents();

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
                
                if (ImGui.BeginMenu("Emulation"))
                {
                    if (ImGui.MenuItem("Start", (string)null, _gameRunning, _gb != null))
                    {
                        _gameRunning = true;
                        _gb.RunGame();
                    }
                    if (ImGui.MenuItem("Stop", (string)null, !_gameRunning, _gb != null))
                    {
                        _gb.EndGame();
                        _gameRunning = false;
                    }
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

            ImGui.Begin("Game View");
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
        }

        private void LoadRomCallback(object sender, DialogResult result)
        {
            if (result == DialogResult.Ok)
            {
                var path = _fileOpenDialog.SelectedFile;
                if (File.Exists(path))
                {
                    _gb = new Gameboy();
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
