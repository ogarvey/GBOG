using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GBOG.Utils
{
	public static class GraphicUtils
	{
		public static List<byte[]> ConvertGBTileData(byte[] tileData)
		{
			// Each tile is 16 bytes where each line is represented by 2 bytes
			// We should extract these bytes into separate tile arrays before further processing
			List<byte[]> tiles = new List<byte[]>();

			for (int i = 0; i < tileData.Length; i += 16)
			{
				byte[] tile = new byte[16];
				Array.Copy(tileData, i, tile, 0, 16);
				tiles.Add(tile);
			}

			// Now we need to convert each tile into a 8x8 pixel array
			// Each pixel is represented by 2 bits
			// Each tile occupies 16 bytes, where each line is represented by 2 bytes
			// For each line, the first byte specifies the least significant bit of the color ID of each pixel, and the second byte specifies the most significant bit.
			// In both bytes, bit 7 represents the leftmost pixel, and bit 0 the rightmost. 
			// For example, the tile data $3C $7E $42 $42 $42 $42 $42 $42 $7E $5E $7E $0A $7C $56 $38 $7C
			// For the first row, the values $3C $7E are 00111100 and 01111110 in binary.
			// The leftmost bits are 0 and 0, thus the color ID is binary 00, or 0.
			// The next bits are 0 and 1, thus the color ID is binary 10, or 2(remember to flip the order of the bits!).
			// The full eight-pixel row evaluates to 0 2 3 3 3 3 2 0.

			List<byte[]> pixels = new List<byte[]>();

			foreach (byte[] tile in tiles)
			{
				byte[] pixel = new byte[64];
				for (int i = 0; i < 8; i++)
				{
					byte lsb = tile[i * 2];
					byte msb = tile[i * 2 + 1];
					for (int j = 0; j < 8; j++)
					{
						byte lsbBit = (byte)((lsb >> (7 - j)) & 1);
						byte msbBit = (byte)((msb >> (7 - j)) & 1);
						byte color = (byte)((msbBit << 1) | lsbBit);
						pixel[i * 8 + j] = color;
					}
				}
				pixels.Add(pixel);
			}

			return pixels;
		}

		internal static Bitmap CreateBitmapFromTileData(byte[] bytes)
		{
			// Tiles have already been processed so as to be an 8 * 8 array where each byte represents the pixel colour
			// We need to convert this into a bitmap
			Bitmap bitmap = new Bitmap(8, 8);
			for (int i = 0; i < bytes.Length; i++)
			{
				byte color = bytes[i];
				int x = i % 8;
				int y = i / 8;
				bitmap.SetPixel(x, y, GetColor(color));
			}
			return bitmap;
		}

		public static Color GetColor(byte color)
		{
			switch (color)
			{
				case 0:
					return Color.White;
				case 1:
					return Color.LightGray;
				case 2:
					return Color.DarkGray;
				case 3:
					return Color.Black;
				default:
					throw new Exception("Invalid color");
			}
		}
	}
}
