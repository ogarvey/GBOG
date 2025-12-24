using GBOG.CPU;
// using Microsoft.Xna.Framework.Graphics;

namespace GBOG.Graphics
{
	public class Screen
	{ 
		// Constants representing the screen dimensions
		private const int Width = 160;
		private const int Height = 144;

		// Double-buffered pixel storage (RGBA).
		// The PPU draws into the back buffer, and the UI reads the front buffer.
		private byte[] _frontPixels;
		private byte[] _backPixels;

		public Screen()
		{
			_frontPixels = new byte[Width * Height * 4];
			_backPixels = new byte[Width * Height * 4];
		}

		public void SwapBuffers()
		{
			// Swap references; arrays themselves are never mutated by the UI.
			(_frontPixels, _backPixels) = (_backPixels, _frontPixels);
		}

		// Method to draw a pixel to the buffer
		public void DrawPixel(int x, int y, Color color)
		{
			if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                int index = (y * Width + x) * 4;
				_backPixels[index] = color.R;
				_backPixels[index + 1] = color.G;
				_backPixels[index + 2] = color.B;
				_backPixels[index + 3] = color.A;
            }
		}

		public void ClearScanline(int y)
		{
            int startIndex = y * Width * 4;
            int endIndex = startIndex + Width * 4;
			for (int i = startIndex; i < endIndex; i += 4)
            {
				_backPixels[i] = 0;
				_backPixels[i + 1] = 0;
				_backPixels[i + 2] = 0;
				_backPixels[i + 3] = 255;
            }
		}

		// Method to clear the pixel buffer
		public void Clear(Color color)
		{
			// Clear both buffers so the UI can't show stale pixels.
			for (int i = 0; i < _frontPixels.Length; i += 4)
            {
				_frontPixels[i] = color.R;
				_frontPixels[i + 1] = color.G;
				_frontPixels[i + 2] = color.B;
				_frontPixels[i + 3] = color.A;
				_backPixels[i] = color.R;
				_backPixels[i + 1] = color.G;
				_backPixels[i + 2] = color.B;
				_backPixels[i + 3] = color.A;
            }
		}
		
		// Method to get the pixel buffer as flat array
		public byte[] GetBuffer()
		{
			return _frontPixels;
		}
	}
}
