using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBOG.Graphics
{
	internal class Tile
	{// A 2D array to hold the color numbers for each pixel in the tile.
		private int[,] pixels;

		public Tile()
		{
			// Initialize the pixel array for 8x8 pixels.
			pixels = new int[8, 8];
		}

		// Method to set the color number for a specific pixel in the tile.
		public void SetPixel(int x, int y, int colorNum)
		{
			if (x >= 0 && x < 8 && y >= 0 && y < 8)
				pixels[x, y] = colorNum;
		}

		// Method to get the color number for a specific pixel in the tile.
		public int GetPixel(int x, int y)
		{
			if (x >= 0 && x < 8 && y >= 0 && y < 8)
				return pixels[x, y];

			// Return a default value (e.g., 0) if the indices are out of bounds.
			return 0;
		}
	}
}
