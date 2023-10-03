using GBOG.CPU;
using GBOG.Graphics.MonoGame;
using GBOG.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GBOG.Graphics.UI
{
	public partial class TileDataViewer : Form
	{
		private Gameboy _gb;

		public TileDataViewer(Gameboy gb)
		{
			InitializeComponent();
			_gb = gb;
			DisplayTileData();
		}

		private void DisplayScreenData()
		{
			byte[] screenData = _gb.GetDisplayArray();
			// screenData contains a 160*144 byte array where each byte is a color index
			// representing the color of a pixel on the screen.
			// We need to convert this to a bitmap to display it.
			// The bitmap will be 160*144 pixels, each pixel will be 4 bytes (ARGB)

			int screenWidth = 160; // Width of the screen
			int screenHeight = 144; // Height of the screen

			Bitmap bmp = new Bitmap(screenWidth, screenHeight, PixelFormat.Format32bppArgb);

			for (int row = 0; row < screenHeight; row++)
			{
				for (int col = 0; col < screenWidth; col++)
				{
					byte colorIndex = screenData[row * screenWidth + col];
					Color color = GraphicUtils.GetColor(colorIndex);
					bmp.SetPixel(col, row, color);
				}
			}

			var scaled = GraphicUtils.Scale4(bmp);

			pbTileData.Image = scaled;
		}

		private void DisplayTileData()
		{

			byte[] tiledata = _gb._memory.GetTileData();
			byte[] tiledata2 = _gb._ppu.GetTileData();

			List<byte[]> tileList = GraphicUtils.ConvertGBTileData(tiledata);
			List<byte[]> tileList2 = GraphicUtils.ConvertGBTileData(tiledata2);

			int tileWidth = 8; // Width of each tile
			int tileHeight = 8; // Height of each tile
			int tilesPerRow = 16;
			int tilesPerColumn = tileList.Count / tilesPerRow;

			Bitmap bmp = new Bitmap(tileWidth * tilesPerRow, tileHeight * tilesPerColumn, PixelFormat.Format32bppArgb);
			Bitmap bmp2 = new Bitmap(tileWidth * tilesPerRow, tileHeight * tilesPerColumn, PixelFormat.Format32bppArgb);

			for (int i = 0; i < tileList.Count; i++)
			{
				int x = (i % tilesPerRow) * tileWidth;
				int y = (i / tilesPerRow) * tileHeight;

				for (int row = 0; row < tileHeight; row++)
				{
					for (int col = 0; col < tileWidth; col++)
					{
						byte colorIndex = tileList[i][row * tileWidth + col];
						Color color = GraphicUtils.GetColor(colorIndex);
						bmp.SetPixel(x + col, y + row, color);
						colorIndex = tileList2[i][row * tileWidth + col];
						color = GraphicUtils.GetColor(colorIndex);
						bmp2.SetPixel(x + col, y + row, color);
					}
				}
			}

			pbTileData.Image = bmp;
			pbTileData2.Image = bmp2;

		}

		private void btnRefreshData_Click(object sender, EventArgs e)
		{
			DisplayTileData();
		}

		private void btnDisplayScreenData_Click(object sender, EventArgs e)
		{
			DisplayScreenData();

			using var gbGame = new GameboyGame(_gb);
			gbGame.Run();
		}
	}
}
