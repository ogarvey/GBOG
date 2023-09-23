using GBOG.CPU;
using GBOG.Utils;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using Color = System.Drawing.Color;

namespace GBOG.Graphics.UI
{
	public partial class Display : Form
	{
		private Gameboy _gb;
		private RenderWindow _renderWindow;

		public Display(Gameboy gb)
		{
			InitializeComponent();
			_gb = gb;
			RunDisplay();
		}

		private async void RunDisplay()
		{
			await RunDisplayAsync();
		}

		private async Task RunDisplayAsync()
		{
			_renderWindow = new RenderWindow(new VideoMode(160, 144), "GBOG");
			_renderWindow.Closed += (sender, e) => _renderWindow.Close();
			_renderWindow.KeyPressed += (sender, e) =>
			{
				switch (e.Code)
				{
					case Keyboard.Key.Escape:
						_renderWindow.Close();
						break;
				}
			};
			TileMap tileMap = new();
			if (!tileMap.Load("./Resources/gb-tileset.png", new Vector2u(32, 32), _gb.GetDisplayArray(), 160, 144))
			{
				Console.WriteLine("Failed to load tilemap");
				return;
			}

			await Task.Run(() =>
			{
				while (_renderWindow.IsOpen)
				{
					tileMap.Update(_gb.GetDisplayArray());
					_renderWindow.DispatchEvents();
					_renderWindow.Clear();
					_renderWindow.Draw(tileMap);
					_renderWindow.Display();
				}
			});
		}

		private void ParsePixelArray(byte[] pixels)
		{
			var _rgbaPixels = new byte[pixels.Length * 4];
			for (int i = 0; i < pixels.Length; i++)
			{
				byte[] rgba = ColorToRGBA(GraphicUtils.GetColor(pixels[i]));
				_rgbaPixels[i * 4] = rgba[0];
				_rgbaPixels[i * 4 + 1] = rgba[1];
				_rgbaPixels[i * 4 + 2] = rgba[2];
				_rgbaPixels[i * 4 + 3] = rgba[3];
			}
		}

		private byte[] ColorToRGBA(Color color)
		{
			byte[] rgba = new byte[4];
			rgba[0] = color.R;
			rgba[1] = color.G;
			rgba[2] = color.B;
			rgba[3] = color.A;
			return rgba;
		}
	}
	public class DrawingSurface : System.Windows.Forms.Control
	{
		protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
		{
			// don't call base.OnPaint(e) to prevent forground painting
			// base.OnPaint(e);
		}
		protected override void OnPaintBackground(System.Windows.Forms.PaintEventArgs pevent)
		{
			// don't call base.OnPaintBackground(e) to prevent background painting
			//base.OnPaintBackground(pevent);
		}
	}
}
