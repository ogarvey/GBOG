using GBOG.CPU;
// using Microsoft.Xna.Framework.Graphics;

namespace GBOG.Graphics
{
	public class Screen
	{ 
		// Constants representing the screen dimensions
		private const int Width = 160;
		private const int Height = 144;

		// Flat array representing the pixel buffer (RGBA)
		private byte[] _pixels;

		public Screen()
		{
			// Initialize the pixel buffer
			_pixels = new byte[Width * Height * 4];
		}

		// Method to draw a pixel to the buffer
		public void DrawPixel(int x, int y, Color color)
		{
			if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                int index = (y * Width + x) * 4;
                _pixels[index] = color.R;
                _pixels[index + 1] = color.G;
                _pixels[index + 2] = color.B;
                _pixels[index + 3] = color.A;
            }
		}

		public void ClearScanline(int y)
		{
            int startIndex = y * Width * 4;
            int endIndex = startIndex + Width * 4;
			for (int i = startIndex; i < endIndex; i += 4)
            {
                _pixels[i] = 0;
                _pixels[i + 1] = 0;
                _pixels[i + 2] = 0;
                _pixels[i + 3] = 255;
            }
		}

		// Method to clear the pixel buffer
		public void Clear(Color color)
		{
			for (int i = 0; i < _pixels.Length; i += 4)
            {
                _pixels[i] = color.R;
                _pixels[i + 1] = color.G;
                _pixels[i + 2] = color.B;
                _pixels[i + 3] = color.A;
            }
		}
		
		// Method to get the pixel buffer as flat array
		public byte[] GetBuffer()
		{
			return _pixels;
		}
	}
}
