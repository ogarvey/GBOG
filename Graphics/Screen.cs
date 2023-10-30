using Microsoft.Xna.Framework.Graphics;

namespace GBOG.Graphics
{
	public class Screen
	{
		// Constants representing the screen dimensions
		private const int Width = 160;
		private const int Height = 144;

		// 2D array representing the pixel buffer
		public Color[,] buffer;

		public Screen()
		{
			// Initialize the pixel buffer
			buffer = new Color[Width, Height];
		}

		// Method to draw a pixel to the buffer
		public void DrawPixel(int x, int y, Color color)
		{
			if (x >= 0 && x < Width && y >= 0 && y < Height)
				buffer[x, y] = color;
		}

		public Color GetPixel(int x, int y)
		{
      return buffer[x, y];
    }

		public void ClearScanline(int y)
		{
			for (int x = 0; x < Width; x++)
				buffer[x, y] = Color.Black;
		}

		// Method to clear the pixel buffer
		public void Clear(Color color)
		{
			for (int x = 0; x < Width; x++)
				for (int y = 0; y < Height; y++)
					buffer[x, y] = color;
		}
		
		// Method to get the pixel buffer as flat array
		public byte[] GetBuffer()
		{
			byte[] result = new byte[Width * Height * 4];
			int i = 0;
			for (int y = 0; y < Height; y++)
				for (int x = 0; x < Width; x++)
				{
					result[i++] = buffer[x, y].R;
					result[i++] = buffer[x, y].G;
					result[i++] = buffer[x, y].B;
					result[i++] = buffer[x, y].A;
				}
			return result;
		}
	}
}
