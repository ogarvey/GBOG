using GBOG.CPU;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBOG.Utils
{
	public static class GraphicUtils
	{
		const int Ymask = 0x00ff0000;
		const int Umask = 0x0000ff00;
		const int Vmask = 0x000000ff;
		
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

		//internal static Bitmap CreateBitmapFromTileData(byte[] bytes)
		//{
		//	// Tiles have already been processed so as to be an 8 * 8 array where each byte represents the pixel colour
		//	// We need to convert this into a bitmap
		//	Bitmap bitmap = new Bitmap(8, 8);
		//	for (int i = 0; i < bytes.Length; i++)
		//	{
		//		byte color = bytes[i];
		//		int x = i % 8;
		//		int y = i / 8;
		//		bitmap.SetPixel(x, y, GetColor(color));
		//	}
		//	return bitmap;
		//}
		private static bool Diff(uint c1, uint c2, uint trY, uint trU, uint trV, uint trA)
		{
			int YUV1 = (int)RgbYuv.GetYuv(c1);
			int YUV2 = (int)RgbYuv.GetYuv(c2);

			return ((Math.Abs((YUV1 & Ymask) - (YUV2 & Ymask)) > trY) ||
			(Math.Abs((YUV1 & Umask) - (YUV2 & Umask)) > trU) ||
			(Math.Abs((YUV1 & Vmask) - (YUV2 & Vmask)) > trV) ||
			(Math.Abs(((int)((uint)c1 >> 24) - (int)((uint)c2 >> 24))) > trA));
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
					return Color.Fallback;
			}
		}

		public static unsafe void Scale4(uint* sp, uint* dp, int Xres, int Yres, uint trY = 48, uint trU = 7, uint trV = 6, uint trA = 0, bool wrapX = false, bool wrapY = false)
		{
			//Don't shift trA, as it uses shift right instead of a mask for comparisons.
			trY <<= 2 * 8;
			trU <<= 1 * 8;
			int dpL = Xres * 4;

			int prevline, nextline;
			var w = new uint[9];

			for (int j = 0; j < Yres; j++)
			{
				if (j > 0)
				{
					prevline = -Xres;
				}
				else
				{
					if (wrapY)
					{
						prevline = Xres * (Yres - 1);
					}
					else
					{
						prevline = 0;
					}
				}
				if (j < Yres - 1)
				{
					nextline = Xres;
				}
				else
				{
					if (wrapY)
					{
						nextline = -(Xres * (Yres - 1));
					}
					else
					{
						nextline = 0;
					}
				}

				for (int i = 0; i < Xres; i++)
				{
					w[1] = *(sp + prevline);
					w[4] = *sp;
					w[7] = *(sp + nextline);

					if (i > 0)
					{
						w[0] = *(sp + prevline - 1);
						w[3] = *(sp - 1);
						w[6] = *(sp + nextline - 1);
					}
					else
					{
						if (wrapX)
						{
							w[0] = *(sp + prevline + Xres - 1);
							w[3] = *(sp + Xres - 1);
							w[6] = *(sp + nextline + Xres - 1);
						}
						else
						{
							w[0] = w[1];
							w[3] = w[4];
							w[6] = w[7];
						}
					}

					if (i < Xres - 1)
					{
						w[2] = *(sp + prevline + 1);
						w[5] = *(sp + 1);
						w[8] = *(sp + nextline + 1);
					}
					else
					{
						if (wrapX)
						{
							w[2] = *(sp + prevline - Xres + 1);
							w[5] = *(sp - Xres + 1);
							w[8] = *(sp + nextline - Xres + 1);
						}
						else
						{
							w[2] = w[1];
							w[5] = w[4];
							w[8] = w[7];
						}
					}

					int pattern = 0;
					int flag = 1;

					for (int k = 0; k < 9; k++)
					{
						if (k == 4) continue;

						if (w[k] != w[4])
						{
							if (Diff(w[4], w[k], trY, trU, trV, trA))
								pattern |= flag;
						}
						flag <<= 1;
					}

					switch (pattern)
					{
						case 0:
						case 1:
						case 4:
						case 32:
						case 128:
						case 5:
						case 132:
						case 160:
						case 33:
						case 129:
						case 36:
						case 133:
						case 164:
						case 161:
						case 37:
						case 165:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 2:
						case 34:
						case 130:
						case 162:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 16:
						case 17:
						case 48:
						case 49:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 64:
						case 65:
						case 68:
						case 69:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 8:
						case 12:
						case 136:
						case 140:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 3:
						case 35:
						case 131:
						case 163:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 6:
						case 38:
						case 134:
						case 166:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 20:
						case 21:
						case 52:
						case 53:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 144:
						case 145:
						case 176:
						case 177:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 192:
						case 193:
						case 196:
						case 197:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 96:
						case 97:
						case 100:
						case 101:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 40:
						case 44:
						case 168:
						case 172:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 9:
						case 13:
						case 137:
						case 141:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 18:
						case 50:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 80:
						case 81:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 72:
						case 76:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 10:
						case 138:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + 1) = w[4];
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 66:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 24:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 7:
						case 39:
						case 135:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 148:
						case 149:
						case 180:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 224:
						case 228:
						case 225:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 41:
						case 169:
						case 45:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 22:
						case 54:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 208:
						case 209:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 104:
						case 108:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 11:
						case 139:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 19:
						case 51:
							{
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[3]);
									*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 1) = Interpolation.Mix3To1(w[1], w[4]);
									*(dp + 2) = Interpolation.Mix5To3(w[1], w[5]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
									*(dp + dpL + 3) = Interpolation.Mix2To1To1(w[5], w[4], w[1]);
								}
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 146:
						case 178:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix2To1To1(w[1], w[4], w[5]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
									*(dp + dpL + 3) = Interpolation.Mix5To3(w[5], w[1]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 84:
						case 85:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
									*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[5], w[4]);
									*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[5], w[7]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix2To1To1(w[7], w[4], w[5]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 112:
						case 113:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[5], w[4], w[7]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[7], w[5]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								break;
							}
						case 200:
						case 204:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix2To1To1(w[3], w[4], w[7]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[7]);
								}
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 73:
						case 77:
							{
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL) = Interpolation.Mix3To1(w[3], w[4]);
									*(dp + dpL + dpL) = Interpolation.Mix5To3(w[3], w[7]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix2To1To1(w[7], w[4], w[3]);
								}
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 42:
						case 170:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
									*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.Mix2To1To1(w[1], w[4], w[3]);
									*(dp + dpL) = Interpolation.Mix5To3(w[3], w[1]);
									*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 14:
						case 142:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.Mix5To3(w[1], w[3]);
									*(dp + 2) = Interpolation.Mix3To1(w[1], w[4]);
									*(dp + 3) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix2To1To1(w[3], w[4], w[1]);
									*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								}
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 67:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 70:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 28:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 152:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 194:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 98:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 56:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 25:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 26:
						case 31:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 82:
						case 214:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 88:
						case 248:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								break;
							}
						case 74:
						case 107:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 27:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 86:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 216:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 106:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 30:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 210:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 120:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 75:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 29:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 198:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 184:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 99:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 57:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 71:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 156:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 226:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 60:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 195:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 102:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 153:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 58:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + 1) = w[4];
								}
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 83:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 92:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[7]);
								}
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
						case 202:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + 1) = w[4];
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[7]);
								}
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 78:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + 1) = w[4];
								}
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[7]);
								}
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 154:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + 1) = w[4];
								}
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 114:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								break;
							}
						case 89:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[7]);
								}
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
						case 90:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + 1) = w[4];
								}
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[7]);
								}
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
						case 55:
						case 23:
							{
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[3]);
									*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 1) = Interpolation.Mix3To1(w[1], w[4]);
									*(dp + 2) = Interpolation.Mix5To3(w[1], w[5]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
									*(dp + dpL + 3) = Interpolation.Mix2To1To1(w[5], w[4], w[1]);
								}
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 182:
						case 150:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix2To1To1(w[1], w[4], w[5]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
									*(dp + dpL + 3) = Interpolation.Mix5To3(w[5], w[1]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 213:
						case 212:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
									*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[5], w[4]);
									*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[5], w[7]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix2To1To1(w[7], w[4], w[5]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 241:
						case 240:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[5], w[4], w[7]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[7], w[5]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								break;
							}
						case 236:
						case 232:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix2To1To1(w[3], w[4], w[7]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[7]);
								}
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 109:
						case 105:
							{
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL) = Interpolation.Mix3To1(w[3], w[4]);
									*(dp + dpL + dpL) = Interpolation.Mix5To3(w[3], w[7]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix2To1To1(w[7], w[4], w[3]);
								}
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 171:
						case 43:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
									*(dp + dpL + 1) = w[4];
									*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.Mix2To1To1(w[1], w[4], w[3]);
									*(dp + dpL) = Interpolation.Mix5To3(w[3], w[1]);
									*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 143:
						case 15:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
									*(dp + dpL) = w[4];
									*(dp + dpL + 1) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.Mix5To3(w[1], w[3]);
									*(dp + 2) = Interpolation.Mix3To1(w[1], w[4]);
									*(dp + 3) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix2To1To1(w[3], w[4], w[1]);
									*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								}
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 124:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 203:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 62:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 211:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 118:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 217:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 110:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 155:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 188:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 185:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 61:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 157:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 103:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 227:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 230:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 199:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 220:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[7]);
								}
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								break;
							}
						case 158:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + 1) = w[4];
								}
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 234:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + 1) = w[4];
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 242:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								break;
							}
						case 59:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 121:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
						case 87:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 79:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[7]);
								}
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 122:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + 1) = w[4];
								}
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
						case 94:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + 1) = w[4];
								}
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL + 2) = w[4];
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[7]);
								}
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
						case 218:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + 1) = w[4];
								}
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[7]);
								}
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								break;
							}
						case 91:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								*(dp + dpL + 1) = w[4];
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[7]);
								}
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
						case 229:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 167:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 173:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 181:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 186:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + 1) = w[4];
								}
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 115:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								break;
							}
						case 93:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[7]);
								}
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
						case 206:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + 1) = w[4];
								}
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[7]);
								}
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 205:
						case 201:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[7]);
								}
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 174:
						case 46:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[0]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
									*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
									*(dp + 1) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL + 1) = w[4];
								}
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 179:
						case 147:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
									*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 117:
						case 116:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								}
								else
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								break;
							}
						case 189:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 231:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 126:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = w[4];
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 219:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 125:
							{
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp) = Interpolation.Mix3To1(w[4], w[3]);
									*(dp + dpL) = Interpolation.Mix3To1(w[3], w[4]);
									*(dp + dpL + dpL) = Interpolation.Mix5To3(w[3], w[7]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix2To1To1(w[7], w[4], w[3]);
								}
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 221:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
									*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 3) = Interpolation.Mix3To1(w[4], w[5]);
									*(dp + dpL + 3) = Interpolation.Mix3To1(w[5], w[4]);
									*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[5], w[7]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix2To1To1(w[7], w[4], w[5]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 207:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
									*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
									*(dp + dpL) = w[4];
									*(dp + dpL + 1) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.Mix5To3(w[1], w[3]);
									*(dp + 2) = Interpolation.Mix3To1(w[1], w[4]);
									*(dp + 3) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + dpL) = Interpolation.Mix2To1To1(w[3], w[4], w[1]);
									*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								}
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 238:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.Mix2To1To1(w[3], w[4], w[7]);
									*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[7]);
								}
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 190:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = w[4];
									*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								}
								else
								{
									*(dp + 2) = Interpolation.Mix2To1To1(w[1], w[4], w[5]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
									*(dp + dpL + 3) = Interpolation.Mix5To3(w[5], w[1]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[5]);
								}
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 187:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
									*(dp + dpL + 1) = w[4];
									*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.Mix2To1To1(w[1], w[4], w[3]);
									*(dp + dpL) = Interpolation.Mix5To3(w[3], w[1]);
									*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
									*(dp + dpL + dpL) = Interpolation.Mix3To1(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix3To1(w[4], w[3]);
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 243:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
									*(dp + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[5], w[4], w[7]);
									*(dp + dpL + dpL + dpL) = Interpolation.Mix3To1(w[4], w[7]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[7], w[5]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								break;
							}
						case 119:
							{
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp) = Interpolation.Mix5To3(w[4], w[3]);
									*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 2) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp) = Interpolation.Mix3To1(w[4], w[1]);
									*(dp + 1) = Interpolation.Mix3To1(w[1], w[4]);
									*(dp + 2) = Interpolation.Mix5To3(w[1], w[5]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
									*(dp + dpL + 3) = Interpolation.Mix2To1To1(w[5], w[4], w[1]);
								}
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 237:
						case 233:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[5]);
								*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[1]);
								*(dp + dpL + dpL) = w[4];
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + dpL) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								}
								*(dp + dpL + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 175:
						case 47:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								}
								*(dp + 1) = w[4];
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = w[4];
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix6To1To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								break;
							}
						case 183:
						case 151:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = w[4];
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 3) = w[4];
								}
								else
								{
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								}
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + 3) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[3]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 245:
						case 244:
							{
								*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[3]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix6To1To1(w[4], w[3], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = w[4];
								*(dp + dpL + dpL + 3) = w[4];
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
						case 250:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								break;
							}
						case 123:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 95:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 222:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 252:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix5To2To1(w[4], w[1], w[0]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = w[4];
								*(dp + dpL + dpL + 3) = w[4];
								*(dp + dpL + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
						case 249:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To2To1(w[4], w[1], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = w[4];
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + dpL) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								}
								*(dp + dpL + dpL + dpL + 1) = w[4];
								break;
							}
						case 235:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[2]);
								*(dp + dpL + dpL) = w[4];
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + dpL) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								}
								*(dp + dpL + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 111:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								}
								*(dp + 1) = w[4];
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = w[4];
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To2To1(w[4], w[5], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 63:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								}
								*(dp + 1) = w[4];
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL) = w[4];
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To2To1(w[4], w[7], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 159:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								*(dp + 2) = w[4];
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 3) = w[4];
								}
								else
								{
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								}
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + 3) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To2To1(w[4], w[7], w[6]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 215:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = w[4];
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 3) = w[4];
								}
								else
								{
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								}
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + 3) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 246:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL) = Interpolation.Mix5To2To1(w[4], w[3], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = w[4];
								*(dp + dpL + dpL + 3) = w[4];
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
						case 254:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[0]);
								*(dp + 1) = Interpolation.Mix3To1(w[4], w[0]);
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL) = Interpolation.Mix3To1(w[4], w[0]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[0]);
								*(dp + dpL + 2) = w[4];
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = w[4];
								*(dp + dpL + dpL + 3) = w[4];
								*(dp + dpL + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
						case 253:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 1) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 2) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[1]);
								*(dp + dpL) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + 3) = Interpolation.Mix7To1(w[4], w[1]);
								*(dp + dpL + dpL) = w[4];
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = w[4];
								*(dp + dpL + dpL + 3) = w[4];
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + dpL) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								}
								*(dp + dpL + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
						case 251:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								*(dp + 2) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[2]);
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[2]);
								*(dp + dpL + 3) = Interpolation.Mix3To1(w[4], w[2]);
								*(dp + dpL + dpL) = w[4];
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + dpL) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								}
								*(dp + dpL + dpL + dpL + 1) = w[4];
								break;
							}
						case 239:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								}
								*(dp + 1) = w[4];
								*(dp + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL) = w[4];
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								*(dp + dpL + dpL) = w[4];
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + dpL) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								}
								*(dp + dpL + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[5]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[5]);
								break;
							}
						case 127:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								}
								*(dp + 1) = w[4];
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 2) = w[4];
									*(dp + 3) = w[4];
									*(dp + dpL + 3) = w[4];
								}
								else
								{
									*(dp + 2) = Interpolation.MixEven(w[1], w[4]);
									*(dp + 3) = Interpolation.MixEven(w[1], w[5]);
									*(dp + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
								}
								*(dp + dpL) = w[4];
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = w[4];
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL) = w[4];
									*(dp + dpL + dpL + dpL + 1) = w[4];
								}
								else
								{
									*(dp + dpL + dpL) = Interpolation.MixEven(w[3], w[4]);
									*(dp + dpL + dpL + dpL) = Interpolation.MixEven(w[7], w[3]);
									*(dp + dpL + dpL + dpL + 1) = Interpolation.MixEven(w[7], w[4]);
								}
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[8]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix3To1(w[4], w[8]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[8]);
								break;
							}
						case 191:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								}
								*(dp + 1) = w[4];
								*(dp + 2) = w[4];
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 3) = w[4];
								}
								else
								{
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								}
								*(dp + dpL) = w[4];
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + 3) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 2) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + 3) = Interpolation.Mix7To1(w[4], w[7]);
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 2) = Interpolation.Mix5To3(w[4], w[7]);
								*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix5To3(w[4], w[7]);
								break;
							}
						case 223:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
									*(dp + 1) = w[4];
									*(dp + dpL) = w[4];
								}
								else
								{
									*(dp) = Interpolation.MixEven(w[1], w[3]);
									*(dp + 1) = Interpolation.MixEven(w[1], w[4]);
									*(dp + dpL) = Interpolation.MixEven(w[3], w[4]);
								}
								*(dp + 2) = w[4];
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 3) = w[4];
								}
								else
								{
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								}
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + 3) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix3To1(w[4], w[6]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[6]);
								*(dp + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + 3) = w[4];
									*(dp + dpL + dpL + dpL + 2) = w[4];
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + 3) = Interpolation.MixEven(w[5], w[4]);
									*(dp + dpL + dpL + dpL + 2) = Interpolation.MixEven(w[7], w[4]);
									*(dp + dpL + dpL + dpL + 3) = Interpolation.MixEven(w[7], w[5]);
								}
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[6]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix3To1(w[4], w[6]);
								break;
							}
						case 247:
							{
								*(dp) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + 2) = w[4];
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 3) = w[4];
								}
								else
								{
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								}
								*(dp + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + 3) = w[4];
								*(dp + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + 2) = w[4];
								*(dp + dpL + dpL + 3) = w[4];
								*(dp + dpL + dpL + dpL) = Interpolation.Mix5To3(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 1) = Interpolation.Mix7To1(w[4], w[3]);
								*(dp + dpL + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
						case 255:
							{
								if (Diff(w[3], w[1], trY, trU, trV, trA))
								{
									*dp = w[4];
								}
								else
								{
									*(dp) = Interpolation.Mix2To1To1(w[4], w[1], w[3]);
								}
								*(dp + 1) = w[4];
								*(dp + 2) = w[4];
								if (Diff(w[1], w[5], trY, trU, trV, trA))
								{
									*(dp + 3) = w[4];
								}
								else
								{
									*(dp + 3) = Interpolation.Mix2To1To1(w[4], w[1], w[5]);
								}
								*(dp + dpL) = w[4];
								*(dp + dpL + 1) = w[4];
								*(dp + dpL + 2) = w[4];
								*(dp + dpL + 3) = w[4];
								*(dp + dpL + dpL) = w[4];
								*(dp + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + 2) = w[4];
								*(dp + dpL + dpL + 3) = w[4];
								if (Diff(w[7], w[3], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + dpL) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + dpL) = Interpolation.Mix2To1To1(w[4], w[7], w[3]);
								}
								*(dp + dpL + dpL + dpL + 1) = w[4];
								*(dp + dpL + dpL + dpL + 2) = w[4];
								if (Diff(w[5], w[7], trY, trU, trV, trA))
								{
									*(dp + dpL + dpL + dpL + 3) = w[4];
								}
								else
								{
									*(dp + dpL + dpL + dpL + 3) = Interpolation.Mix2To1To1(w[4], w[7], w[5]);
								}
								break;
							}
					}
					sp++;
					dp += 4;
				}
				dp += (dpL * 3);
			}
		}

	}
}
