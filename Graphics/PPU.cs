using GBOG.CPU;
using GBOG.Utils;

namespace GBOG.Graphics
{
	// The PPU (which stands for Picture Processing Unit) is the part of the Gameboy that’s responsible for everything you see on screen.
	public class PPU
	{
		private int _scanlineCounter;
		private readonly Gameboy _gb;

		private static int _prevLy = 0;
		private static readonly int _lcdMode2Bounds = 456 - 80;
		private readonly int _lcdMode3Bounds = _lcdMode2Bounds - 172;
		
		public int Scanline { get; private set; } // Represents the current scanline being processed.
		public int WindowScanline { get; private set; } // Represents the current window scanline being processed.
		public byte[] VideoRam { get; private set; } // Represents the Video RAM.
		public byte[] OAM { get; private set; } // Represents the Object Attribute Memory (OAM) for sprites.
		public Screen Screen { get; private set; } // Represents the Screen to render pixels.

		public PPU(Gameboy gb)
		{
			_gb = gb;
			VideoRam = new byte[0x2000];
			OAM = new byte[0xA0];
			Screen = new Screen();
			Scanline = 0;
			_scanlineCounter = 456;
		}

		// Methods

		// Resets the GPU to its initial state.
		public void Reset()
		{
			// Implementation
		}

		// Reads a byte from a memory address within the GPU's address space.
		public byte ReadByte(ushort address)
		{
			return VideoRam[address - 0x8000];
		}

		// Steps the GPU a given number of cycles, performing rendering tasks.
		public void Step(int cycles)
		{
			_scanlineCounter -= cycles;
			SetLCDStatus();

			if (!_gb._memory.LCDEnabled)
			{
				return;
			}

			if (_scanlineCounter <= 0)
			{
				_gb._memory.LY++;
				if (_gb._memory.LY > 153)
				{
					_gb._memory.LY = 0;
				}

				_scanlineCounter += 456;
				
				if (_gb._memory.LY == 144)
				{
					_gb.RequestInterrupt(Interrupt.VBlank);
				}
			}
		}

		// Renders a scanline to the screen.
		private void RenderScanline()
		{
			// Clear the current scanline on the screen
			//Screen.ClearScanline(Scanline);

			// Render the background tiles to the screen
			if (_gb._memory.BGDisplay)
			{
				RenderBackground();
			}

			// Render the window tiles to the screen if the window is enabled
			if (_gb._memory.WindowDisplayEnable)
			{
				RenderWindow();
			}

			// Render the sprites to the screen if sprites are enabled
			if (_gb._memory.SpriteDisplay)
			{
				RenderSprites();
			}
		}

		private void RenderBackground()
		{
			// Calculate which tiles are visible in the current scanline.
			// Fetch the tile data using FetchTile method.
			// Draw the pixels to the Screen object considering SCX and SCY.
			byte lcdControl = _gb._memory.LCDC;
			byte scx = _gb._memory.SCX;
			byte scy = _gb._memory.SCY;
			byte bgp = _gb._memory.BGP; // Background Palette Data

			// Calculate the address of the background map
			ushort bgMapAddr = (ushort)((lcdControl & 0x08) == 0x08 ? 0x9C00 : 0x9800);

			// Calculate the base address for tile data
			bool useSignedTileNumbers = (lcdControl & 0x10) == 0;
			ushort tileDataBaseAddr = useSignedTileNumbers ? (ushort)0x8800 : (ushort)0x8000;

			// Calculate the Y coordinate in the background map
			int yPos = (Scanline + scy) % 256;

			// Calculate the row of the tile in the background map
			int mapRow = yPos / 8;

			for (int pixel = 0; pixel < 160; pixel++) // 160 pixels in a scanline
			{
				// Calculate the X coordinate in the background map
				int xPos = (pixel + scx) % 256;

				// Calculate the column of the tile in the background map
				int mapCol = xPos / 8;

				// Get the tile number from the background map
				byte tileNum = ReadByte((ushort)(bgMapAddr + mapRow * 32 + mapCol));

				// Calculate the tile data address
				ushort tileAddr;
				if (useSignedTileNumbers)
				{
					sbyte signedTileNum = (sbyte)tileNum; // Treat tileNum as signed
					tileAddr = (ushort)(tileDataBaseAddr + (signedTileNum + 128) * 16);
				}
				else
				{
					tileAddr = (ushort)(tileDataBaseAddr + tileNum * 16); // Treat tileNum as unsigned
				}

				// Fetch the tile data
				Tile tile = FetchTile(tileAddr);

				// Get the color number from the tile data
				int colorNum = tile.GetPixel(xPos % 8, yPos % 8);

				// Map the color number to actual color using the background palette
				int colorBits = (bgp >> (colorNum * 2)) & 0x03;
				Color actualColor = MapColor(colorBits);

				// Draw the pixel to the screen
				Screen.DrawPixel(pixel, Scanline, actualColor);
			}
		}

		private Color MapColor(int colorBits)
		{
			return colorBits switch
			{
				0 => Color.White,
				1 => Color.LightGray,
				2 => Color.DarkGray,
				3 => Color.Black,
				_ => Color.Fallback
			};
		}

		private void RenderWindow()
		{
			byte lcdControl = _gb._memory.LCDC;
			byte wx = _gb._memory.WX;
			byte wy = _gb._memory.WY;
			byte bgp = _gb._memory.BGP; // Background Palette Data

			// If the current scanline is below the window, exit
			if (Scanline < wy) return;

			// Calculate the address of the window map
			ushort windowMapAddr = (ushort)((lcdControl & 0x40) == 0x40 ? 0x9C00 : 0x9800);

			// Calculate the base address for tile data
			bool useSignedTileNumbers = (lcdControl & 0x10) == 0;
			ushort tileDataBaseAddr = useSignedTileNumbers ? (ushort)0x8800 : (ushort)0x8000;

			// Calculate the Y coordinate in the window map
			int yPos = Scanline - wy; // Window's Y position in relation to the current scanline
			int mapRow = yPos / 8; // The row of the tile in the window map

			for (int pixel = 0; pixel < 160; pixel++) // 160 pixels in a scanline
			{
				// If the pixel is before the starting X position of the window, continue to the next pixel
				if (pixel < wx - 7) continue; // wx register has an offset of 7

				int xPos = pixel - (wx - 7); // Window's X position in relation to the current pixel
				int mapCol = xPos / 8; // The column of the tile in the window map

				// Get the tile number from the window map
				byte tileNum = ReadByte((ushort)(windowMapAddr + mapRow * 32 + mapCol));

				// Calculate the tile data address
				ushort tileAddr = useSignedTileNumbers ? (ushort)(tileDataBaseAddr + ((sbyte)tileNum + 128) * 16)
																							 : (ushort)(tileDataBaseAddr + tileNum * 16);

				// Fetch the tile data
				Tile tile = FetchTile(tileAddr);

				// Get the color number from the tile data
				int colorNum = tile.GetPixel(xPos % 8, yPos % 8);

				// Map the color number to actual color using the background palette
				int colorBits = (bgp >> (colorNum * 2)) & 0x03;
				Color actualColor = MapColor(colorBits);

				// Draw the pixel to the screen
				Screen.DrawPixel(pixel, Scanline, actualColor);
			}
		}

		private void RenderSprites()
		{
			byte lcdControl = _gb._memory.LCDC;
			bool use8x16 = (lcdControl & 0x04) == 0x04; // 0x00 for 8x8 sprites, 0x04 for 8x16 sprites

			var sprites = GetSpriteAttributes();
			var visibleSprites = new List<SpriteAttributes>();

			// Filter the sprites visible on the current scanline
			foreach (var sprite in sprites)
			{
				int height = use8x16 ? 16 : 8;

				// Check if the sprite is visible on the current scanline
				if (Scanline >= sprite.YPosition && Scanline < sprite.YPosition + height)
				{
					visibleSprites.Add(sprite);
				}
			}

			// The Game Boy can only render 10 sprites per scanline
			int count = Math.Min(visibleSprites.Count, 10);

			// Sort the visible sprites by X position and OAM index
			visibleSprites.Sort((s1, s2) => s1.XPosition != s2.XPosition ? s1.XPosition - s2.XPosition : sprites.FindIndex(s => s == s1) - sprites.FindIndex(s => s == s2));

			for (int i = 0; i < count; i++)
			{
				var sprite = visibleSprites[i];
				RenderSprite(sprite, use8x16);
			}
		}
		private void RenderSprite(SpriteAttributes sprite, bool use8x16)
		{
			byte obp0 = _gb._memory.OBP0;
			byte obp1 = _gb._memory.OBP1;

			// Determine the palette to use
			byte palette = (sprite.Flags & 0x10) == 0x10 ? obp1 : obp0;

			// Fetch the tile data
			ushort tileNumber = sprite.TileNumber;
			bool xFlip = sprite.XFlip;
			bool yFlip = sprite.YFlip;
			var priority = sprite.Priority;

			if (use8x16)
			{
				// Check whether the current scanline is in the upper or lower tile of the sprite
				bool isUpperTile = Scanline < (sprite.YPosition + 8);

				// If the sprite is 8x16, the least significant bit of the tile number is ignored,
				// and each sprite is represented by two vertical tiles.
				tileNumber &= 0xFE; // Clear the least significant bit

				if (!isUpperTile)
				{
					// If the current scanline is in the lower tile of the sprite, increment the tile number
					tileNumber++;
				}
			}

			// Fetch the tile data from the calculated address
			Tile tile = FetchTile((ushort)(0x8000 + tileNumber * 16));
			int height = use8x16 ? 16 : 8;

			for (int row = 0; row < height; row++)
			{
				int yPos = sprite.YPosition + row;

				if (yPos < 0 || yPos >= 144) continue; // Skip rows outside the screen

				// Skip rows outside the sprite
				if (Scanline < yPos || Scanline >= yPos + height) continue;

				for (int col = 0; col < 8; col++)
				{
					int xPos = sprite.XPosition + col;
					if (xPos < 0 || xPos >= 160) continue; // Skip columns outside the screen

					// check priority

					// Get the color number from the tile data
					int colorNum;

					if (xFlip && yFlip)
					{
						colorNum = tile.GetPixel(8 - col-1, height - row-1);
					}
					else if (xFlip)
					{
						colorNum = tile.GetPixel(8 - col-1, row);
					}
					else if (yFlip)
					{
						colorNum = tile.GetPixel(col, height - row-1);
					}
					else {
						colorNum = tile.GetPixel(col, row);
					}

					// Map the color number to actual color using the sprite's palette
					int colorBits = (palette >> (colorNum * 2)) & 0x03;

					Color actualColor = MapColor(colorBits);

					// Draw the pixel to the screen
					Screen.DrawPixel(xPos, Scanline, actualColor);
				}
			}
		}

		// Fetches tile data from memory.
		private Tile FetchTile(ushort address)
		{
			Tile tile = new Tile(); // A placeholder for your Tile class or structure.

			// Iterate over each row in the tile.
			for (int row = 0; row < 8; row++)
			{
				// Each row is represented by two bytes.
				byte byte1 = VideoRam[(address - 0x8000) + row * 2];
				byte byte2 = VideoRam[(address - 0x8000) + row * 2 + 1];

				// Iterate over each pixel in the row.
				for (int col = 0; col < 8; col++)
				{
					// Calculate the color number for the pixel using the bits from byte1 and byte2.
					int colorNum = ((byte1 >> (7 - col)) & 1) + (((byte2 >> (7 - col)) & 1) * 2);

					// Store the color number in the tile (replace with your actual implementation).
					tile.SetPixel(col, row, colorNum);
				}
			}

			return tile;
		}

		public byte[] GetTileData()
		{
			// 8000-97FF
			var tileData = new byte[0x1800];
			Array.Copy(VideoRam, 0, tileData, 0, 0x1800);
			return tileData;
		}
		// Updates the palette with the new colors.
		private void UpdatePalette(byte value)
		{
			// Implementation
		}

		// Gets the sprite attributes from OAM.
		public List<SpriteAttributes> GetSpriteAttributes()
		{
			List<SpriteAttributes> sprites = new List<SpriteAttributes>();

			// Each sprite has 4 bytes of attributes in the OAM.
			for (int i = 0; i < OAM.Length; i += 4)
			{
				byte y = (byte)(OAM[i] - 16); // Subtract 16 to get the actual Y position.
				byte x = (byte)(OAM[i + 1] - 8); // Subtract 8 to get the actual X position.
				byte tileNumber = OAM[i + 2];
				byte attributes = OAM[i + 3];

				sprites.Add(new SpriteAttributes(x, y, tileNumber, attributes));
			}

			return sprites;
		}

		// Handles GPU registers and updates GPU mode.
		private void SetLCDStatus()
		{
			var lcdStatus = _gb._memory.LCDStatus;

			if (!_gb._memory.LCDEnabled)
			{
				Screen.Clear(Color.Black);
				_scanlineCounter = 456;
				_gb._memory.LY = 0;
				// When LCD is off, STAT mode is 0 (HBlank) and coincidence is cleared.
				lcdStatus &= 0b1111_1000;
				_gb._memory.LCDStatus = lcdStatus;
				return;
			}

			Scanline = _gb._memory.LY;
			var currentMode = lcdStatus & 0b11;
			int mode;
			bool reqInt = false;
			
			if (Scanline >= 144)
			{
				mode = (int)GraphicsMode.Mode1_VBlank;
				lcdStatus = (byte)((lcdStatus & 0b1111_1100) | mode);
				reqInt = lcdStatus.TestBit(4);
				WindowScanline = 0;
			}
			else if (_scanlineCounter >= _lcdMode2Bounds)
			{
				mode = (int)GraphicsMode.Mode2_OAMRead;
				lcdStatus = (byte)((lcdStatus & 0b1111_1100) | mode);
				reqInt = lcdStatus.TestBit(5);
			}
			else if (_scanlineCounter >= _lcdMode3Bounds)
			{
				mode = (int)GraphicsMode.Mode3_VRAMReadWrite;
				lcdStatus = (byte)((lcdStatus & 0b1111_1100) | mode);
				if (mode != currentMode)
				{
					RenderScanline();
				}
			}
			else
			{
				mode = (int)GraphicsMode.Mode0_HBlank;
				lcdStatus = (byte)((lcdStatus & 0b1111_1100) | mode);
				reqInt = lcdStatus.TestBit(3);
			}

			if (reqInt && mode != currentMode && _prevLy != Scanline)
			{
				_gb.RequestInterrupt(Interrupt.LCDStat);
			}

			if (Scanline == _gb._memory.LYC)
			{
				lcdStatus |= 0b100;
				if (lcdStatus.TestBit(6) && _prevLy != Scanline)
				{
					_gb.RequestInterrupt(Interrupt.LCDStat);
				}
			}
			else
			{
				lcdStatus &= 0b1111_1011;
			}

			if (_prevLy != Scanline)
			{
				_prevLy = Scanline;
			}
			
			_gb._memory.LCDStatus = lcdStatus;
		}
	}
}
