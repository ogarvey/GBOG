using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using System.Threading.Tasks;
using Hexa.NET.ImGui;
using Hexa.NET.OpenGL;
using GBOG.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GBOG.Library
{
    public class LibraryWindow
    {
        private readonly LibraryManager _manager;
        private readonly GL _gl;
        private bool _isScanning = false;
        private bool _isDownloading = false;
        private string _statusMessage = string.Empty;
        private int _progressCurrent = 0;
        private int _progressTotal = 0;

        public LibraryWindow(LibraryManager manager, GL gl)
        {
            _manager = manager;
            _gl = gl;
        }

        public void Render(ref bool show, Action<string> onLaunchRom)
        {
            if (!show) return;

            if (ImGui.Begin("Game Library", ref show))
            {
                RenderToolbar();

                if (_isScanning || _isDownloading)
                {
                    ImGui.Text(_statusMessage);
                    if (_progressTotal > 0)
                    {
                        ImGui.ProgressBar((float)_progressCurrent / _progressTotal, new Vector2(-1, 0), $"{_progressCurrent}/{_progressTotal}");
                    }
                }

                ImGui.Separator();

                var avail = ImGui.GetContentRegionAvail();
                if (ImGui.BeginChild("LibraryGrid", avail))
                {
                    RenderGrid(onLaunchRom);
                }
                ImGui.EndChild();
            }
            ImGui.End();
        }

        private void RenderToolbar()
        {
            if (ImGui.Button("Scan Folder"))
            {
                _isScanning = true;
                _statusMessage = "Scanning...";
                _manager.ScanLibrary();
                _isScanning = false;
                _statusMessage = string.Empty;
            }
            ImGui.SameLine();
            if (ImGui.Button("Download All Covers"))
            {
                _isDownloading = true;
                _statusMessage = "Downloading covers...";
                _progressTotal = _manager.Entries.Count;
                _progressCurrent = 0;
                Task.Run(async () =>
                {
                    await _manager.DownloadAllCoversAsync((curr, total) =>
                    {
                        _progressCurrent = curr;
                        _progressTotal = total;
                    });
                    _isDownloading = false;
                    _statusMessage = string.Empty;
                });
            }
        }

        private unsafe void RenderGrid(Action<string> onLaunchRom)
        {
            float cardWidth = 120;
            float cardHeight = 180;
            var windowVisibleX2 = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
            var style = ImGui.GetStyle();

            for (int i = 0; i < _manager.Entries.Count; i++)
            {
                var entry = _manager.Entries[i];
                ImGui.PushID(i);

                if (entry.CoverTextureId == null && !string.IsNullOrEmpty(entry.CoverPath))
                {
                    entry.CoverTextureId = LoadTexture(entry.CoverPath);
                }

                ImGui.BeginGroup();
                
                if (entry.CoverTextureId.HasValue)
                {
                    if (ImGui.ImageButton("cover", new ImTextureRef(null, entry.CoverTextureId.Value), new Vector2(cardWidth, cardHeight)))
                    {
                        onLaunchRom(entry.RomPath);
                    }
                }
                else
                {
                    if (ImGui.Button("No Cover\n" + entry.Title, new Vector2(cardWidth, cardHeight)))
                    {
                        onLaunchRom(entry.RomPath);
                    }
                }

                ImGui.TextWrapped(entry.Title);
                
                if (ImGui.Button("Get Cover"))
                {
                    DownloadCover(entry);
                }

                ImGui.EndGroup();

                float lastButtonX2 = ImGui.GetItemRectMax().X;
                float nextButtonX2 = lastButtonX2 + style.ItemSpacing.X + cardWidth;
                if (i + 1 < _manager.Entries.Count && nextButtonX2 < windowVisibleX2)
                {
                    ImGui.SameLine();
                }

                ImGui.PopID();
            }
        }

        private void DownloadCover(LibraryEntry entry)
        {
            Task.Run(async () => await _manager.DownloadCoverAsync(entry));
        }

        private uint LoadTexture(string path)
        {
            try
            {
                using var image = Image.Load<Rgba32>(path);
                uint textureId;
                unsafe
                {
                    _gl.GenTextures(1, &textureId);
                    _gl.BindTexture(GLTextureTarget.Texture2D, textureId);
                    _gl.TexParameteri(GLTextureTarget.Texture2D, GLTextureParameterName.MinFilter, (int)GLTextureMinFilter.Linear);
                    _gl.TexParameteri(GLTextureTarget.Texture2D, GLTextureParameterName.MagFilter, (int)GLTextureMagFilter.Linear);

                    byte[] pixels = new byte[image.Width * image.Height * 4];
                    image.CopyPixelDataTo(pixels);

                    fixed (byte* p = pixels)
                    {
                        _gl.TexImage2D(GLTextureTarget.Texture2D, 0, GLInternalFormat.Rgba, image.Width, image.Height, 0, GLPixelFormat.Rgba, GLPixelType.UnsignedByte, p);
                    }
                }

                return textureId;
            }
            catch
            {
                return 0;
            }
        }
    }
}
