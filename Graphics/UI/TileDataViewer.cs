using GBOG.CPU;
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

		private void DisplayTileData()
		{

			byte[] tiledata = _gb._memory.GetTileData();
			List<byte[]> tileList = GraphicUtils.ConvertGBTileData(tiledata);

			int tileWidth = 8; // Width of each tile
			int tileHeight = 8; // Height of each tile
			int tilesPerRow = 16;
			int tilesPerColumn = tileList.Count / tilesPerRow;

			Bitmap bmp = new Bitmap(tileWidth * tilesPerRow, tileHeight * tilesPerColumn, PixelFormat.Format32bppArgb);

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
					}
				}
			}

			pbTileData.Image = bmp;

		}

		private void btnRefreshData_Click(object sender, EventArgs e)
		{
			DisplayTileData();
		}
	}
}
