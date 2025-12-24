using GBOG.CPU;
using GBOG.Utils;

namespace GBOG.Graphics
{
	// The PPU (which stands for Picture Processing Unit) is the part of the Gameboy that’s responsible for everything you see on screen.
	public class PPU
	{
		private int _scanlineCounter;
		private readonly Gameboy _gb;

		private int _prevMode = -1;
		private bool _prevCoincidence;
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

		internal int GetOamScanRowForCurrentMCycle()
		{
			if (!_gb._memory.LCDEnabled)
			{
				return -1;
			}
			// Visible scanlines only.
			if (_gb._memory.LY >= 144)
			{
				return -1;
			}
			// Mode 2 is the first 80 t-cycles of the scanline (20 M-cycles).
			if (_scanlineCounter <= _lcdMode2Bounds)
			{
				return -1;
			}
			int tCyclesIntoLine = 456 - _scanlineCounter;
			if (tCyclesIntoLine < 0 || tCyclesIntoLine >= 80)
			{
				return -1;
			}
			return tCyclesIntoLine / 4; // 0..19
		}

		internal void ApplyOamCorruptionWrite(int row)
		{
			ApplyOamCorruptionWriteOrRead(row, isRead: false);
		}

		internal void ApplyOamCorruptionRead(int row)
		{
			ApplyOamCorruptionWriteOrRead(row, isRead: true);
		}

		internal void ApplyOamCorruptionReadDuringIncDec(int row)
		{
			// Pan Docs: doesn't happen if accessed row is one of first four, or last row.
			if (row < 4 || row >= 19)
			{
				ApplyOamCorruptionRead(row);
				return;
			}

			// (1) Corrupt first word of preceding row, then copy preceding row to current row and two rows before.
			ushort a = ReadOamWord(row - 2, 0);
			ushort b = ReadOamWord(row - 1, 0);
			ushort c = ReadOamWord(row, 0);
			ushort d = ReadOamWord(row - 1, 2);
			ushort newB = (ushort)((b & (a | c | d)) | (a & c & d));
			WriteOamWord(row - 1, 0, newB);

			for (int w = 0; w < 4; w++)
			{
				ushort src = ReadOamWord(row - 1, w);
				WriteOamWord(row, w, src);
				WriteOamWord(row - 2, w, src);
			}

			// (2) Then apply normal read corruption.
			ApplyOamCorruptionRead(row);
		}

		private void ApplyOamCorruptionWriteOrRead(int row, bool isRead)
		{
			// OAM is 20 rows of 8 bytes (4 words). Row 0 (objects 0-1) is not affected.
			if (row <= 0 || row >= 20)
			{
				return;
			}

			ushort a = ReadOamWord(row, 0);
			ushort b = ReadOamWord(row - 1, 0);
			ushort c = ReadOamWord(row - 1, 2);

			ushort newFirst = isRead
				? (ushort)(b | (a & c))
				: (ushort)(((a ^ c) & (b ^ c)) ^ c);

			WriteOamWord(row, 0, newFirst);
			for (int w = 1; w < 4; w++)
			{
				WriteOamWord(row, w, ReadOamWord(row - 1, w));
			}
		}

		private ushort ReadOamWord(int row, int wordIndex)
		{
			int baseIndex = (row * 8) + (wordIndex * 2);
			byte lo = ReadOamByte(baseIndex);
			byte hi = ReadOamByte(baseIndex + 1);
			return (ushort)((hi << 8) | lo);
		}

		private void WriteOamWord(int row, int wordIndex, ushort value)
		{
			int baseIndex = (row * 8) + (wordIndex * 2);
			WriteOamByte(baseIndex, (byte)(value & 0xFF));
			WriteOamByte(baseIndex + 1, (byte)(value >> 8));
		}

		private byte ReadOamByte(int oamIndex)
		{
			if ((uint)oamIndex >= (uint)OAM.Length)
			{
				return 0;
			}
			return OAM[oamIndex];
		}

		private void WriteOamByte(int oamIndex, byte value)
		{
			if ((uint)oamIndex >= (uint)OAM.Length)
			{
				return;
			}
			OAM[oamIndex] = value;
			_gb._memory.WriteOamByteDirect(oamIndex, value);
		}

		internal void OnLcdEnabled()
		{
			// When LCD is turned on from off, hardware synchronizes to the start of the
			// first visible scanline. Tests expect LY to increment at a specific cycle
			// offset relative to the enabling write, so we start slightly into the line.
			Screen.Clear(Color.White);
			_gb._memory.LY = 0;
			Scanline = 0;
			WindowScanline = 0;
			_prevMode = -1;
			_prevCoincidence = false;
			_scanlineCounter = 456 - 4;
		}

		internal void OnLcdDisabled()
		{
			// When LCD is turned off, DMG shows a blank (very bright) screen and LY resets.
			// Do this once on the transition; doing it every PPU step is extremely slow.
			Screen.Clear(Color.White);
			_gb._memory.LY = 0;
			Scanline = 0;
			WindowScanline = 0;
			_prevMode = 0;
			_prevCoincidence = false;
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
					// Frame is complete; publish it for the renderer.
					Screen.SwapBuffers();
					_gb.RequestInterrupt(Interrupt.VBlank);
				}
			}
		}

		// Renders a scanline to the screen.
		private void RenderScanline()
		{
			// Keep track of whether the BG/WIN pixel at each X uses color number 0.
			// This is needed for OBJ-to-BG priority (sprite bit 7).
			Span<bool> bgIsColor0 = stackalloc bool[160];

			// Always fill the scanline with BG palette color 0 (even if BG is disabled).
			byte bgp = _gb._memory.BGP;
			int bgShade0 = bgp & 0x03;
			Color baseColor = MapColor(bgShade0);
			for (int x = 0; x < 160; x++)
			{
				bgIsColor0[x] = true;
				Screen.DrawPixel(x, Scanline, baseColor);
			}

			// Render BG layer (if enabled)
			if (_gb._memory.BGDisplay)
			{
				RenderBackground(bgIsColor0);
			}

			// Render Window (if enabled and active on this line)
			if (_gb._memory.WindowDisplayEnable)
			{
				RenderWindow(bgIsColor0);
			}

			// Render sprites on top (if enabled)
			if (_gb._memory.SpriteDisplay)
			{
				RenderSprites(bgIsColor0);
			}
		}

		internal void WarmupJit()
		{
			// Force JIT of the main rendering path early (e.g., at ROM load) so the first
			// visible frame doesn't hitch due to one-time compilation cost.
			int savedScanline = Scanline;
			int savedWindowScanline = WindowScanline;
			try
			{
				Scanline = 0;
				WindowScanline = 0;

				Span<bool> bgIsColor0 = stackalloc bool[160];
				for (int x = 0; x < 160; x++)
				{
					bgIsColor0[x] = true;
					Screen.DrawPixel(x, 0, Color.White);
				}

				RenderBackground(bgIsColor0);
				RenderWindow(bgIsColor0);
				RenderSprites(bgIsColor0);
			}
			finally
			{
				Scanline = savedScanline;
				WindowScanline = savedWindowScanline;
			}
		}

		private void RenderBackground(Span<bool> bgIsColor0)
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

				// Get the color number directly from VRAM (avoid per-pixel allocations)
				int colorNum = GetTileColorNumber(tileAddr, xPos & 7, yPos & 7);

				bgIsColor0[pixel] = colorNum == 0;
				// Map the color number to actual color using the background palette
				int colorBits = (bgp >> (colorNum * 2)) & 0x03;
				Color actualColor = MapColor(colorBits);

				// Draw the pixel to the screen
				Screen.DrawPixel(pixel, Scanline, actualColor);
			}
		}

		private int GetTileColorNumber(ushort tileAddr, int xInTile, int yInTile)
		{
			int baseIndex = tileAddr - 0x8000;
			int rowIndex = baseIndex + (yInTile * 2);
			if ((uint)(rowIndex + 1) >= (uint)VideoRam.Length)
			{
				return 0;
			}

			byte byte1 = VideoRam[rowIndex];
			byte byte2 = VideoRam[rowIndex + 1];
			int bit = 7 - xInTile;
			return ((byte1 >> bit) & 1) | (((byte2 >> bit) & 1) << 1);
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

		private void RenderWindow(Span<bool> bgIsColor0)
		{
			byte lcdControl = _gb._memory.LCDC;
			byte wx = _gb._memory.WX;
			byte wy = _gb._memory.WY;
			byte bgp = _gb._memory.BGP; // Background Palette Data

			// If the current scanline is below the window, exit
			if (Scanline < wy) return;
			// WX has a hardware offset of 7; WX >= 167 means the window is not visible.
			if (wx >= 167) return;

			// Calculate the address of the window map
			ushort windowMapAddr = (ushort)((lcdControl & 0x40) == 0x40 ? 0x9C00 : 0x9800);

			// Calculate the base address for tile data
			bool useSignedTileNumbers = (lcdControl & 0x10) == 0;
			ushort tileDataBaseAddr = useSignedTileNumbers ? (ushort)0x8800 : (ushort)0x8000;

			// Window uses an internal line counter that increments only on lines where the window is actually rendered.
			int yPos = WindowScanline;
			int mapRow = yPos / 8; // The row of the tile in the window map

			bool drewAny = false;
			for (int pixel = 0; pixel < 160; pixel++) // 160 pixels in a scanline
			{
				// If the pixel is before the starting X position of the window, continue to the next pixel
				if (pixel < wx - 7) continue; // wx register has an offset of 7
				drewAny = true;

				int xPos = pixel - (wx - 7); // Window's X position in relation to the current pixel
				int mapCol = xPos / 8; // The column of the tile in the window map

				// Get the tile number from the window map
				byte tileNum = ReadByte((ushort)(windowMapAddr + mapRow * 32 + mapCol));

				// Calculate the tile data address
				ushort tileAddr = useSignedTileNumbers ? (ushort)(tileDataBaseAddr + ((sbyte)tileNum + 128) * 16)
																							 : (ushort)(tileDataBaseAddr + tileNum * 16);

				// Fetch the tile data
				int colorNum = GetTileColorNumber(tileAddr, xPos & 7, yPos & 7);

				bgIsColor0[pixel] = colorNum == 0;
				// Map the color number to actual color using the background palette
				int colorBits = (bgp >> (colorNum * 2)) & 0x03;
				Color actualColor = MapColor(colorBits);

				// Draw the pixel to the screen
				Screen.DrawPixel(pixel, Scanline, actualColor);
			}
			if (drewAny)
			{
				WindowScanline++;
			}
		}

		private void RenderSprites(Span<bool> bgIsColor0)
		{
			byte lcdControl = _gb._memory.LCDC;
			bool use8x16 = (lcdControl & 0x04) == 0x04; // 0x00 for 8x8 sprites, 0x04 for 8x16 sprites

			// Avoid per-scanline allocations by scanning OAM directly.
			Span<SpriteEntry> visibleSprites = stackalloc SpriteEntry[10];
			int visibleCount = 0;
			int height = use8x16 ? 16 : 8;
			for (int i = 0; i < 40; i++)
			{
				int oam = i * 4;
				int y = OAM[oam] - 16;
				if (Scanline < y || Scanline >= y + height)
				{
					continue;
				}
				int x = OAM[oam + 1] - 8;
				byte tile = OAM[oam + 2];
				byte flags = OAM[oam + 3];
				visibleSprites[visibleCount++] = new SpriteEntry
				{
					X = x,
					Y = y,
					TileNumber = tile,
					Flags = flags,
					OamIndex = i,
				};
				if (visibleCount == 10)
				{
					break;
				}
			}

			// For overlapping pixels: lower X wins; if equal X, lower OAM index wins.
			// Since later draws overwrite earlier ones, draw from lowest priority to highest.
			// Sort by X descending then OAM index descending.
			for (int i = 1; i < visibleCount; i++)
			{
				SpriteEntry key = visibleSprites[i];
				int j = i - 1;
				while (j >= 0 && CompareSpriteEntries(visibleSprites[j], key) > 0)
				{
					visibleSprites[j + 1] = visibleSprites[j];
					j--;
				}
				visibleSprites[j + 1] = key;
			}

			for (int i = 0; i < visibleCount; i++)
			{
				RenderSprite(visibleSprites[i], use8x16, bgIsColor0);
			}
		}

		private struct SpriteEntry
		{
			public int X;
			public int Y;
			public int OamIndex;
			public byte TileNumber;
			public byte Flags;
		}

		private static int CompareSpriteEntries(SpriteEntry a, SpriteEntry b)
		{
			int xCmp = b.X.CompareTo(a.X);
			if (xCmp != 0) return xCmp;
			return b.OamIndex.CompareTo(a.OamIndex);
		}

		private void RenderSprite(SpriteEntry sprite, bool use8x16, Span<bool> bgIsColor0)
		{
			byte obp0 = _gb._memory.OBP0;
			byte obp1 = _gb._memory.OBP1;

			// Determine the palette to use
			byte palette = (sprite.Flags & 0x10) == 0x10 ? obp1 : obp0;

			int spriteX = sprite.X;
			int spriteY = sprite.Y;
			int height = use8x16 ? 16 : 8;
			int rowInSprite = Scanline - spriteY;
			if (rowInSprite < 0 || rowInSprite >= height)
			{
				return;
			}

			// Determine tile index and row within tile.
			int tileRow = rowInSprite;
			int tileIndex = sprite.TileNumber;
			bool xFlip = (sprite.Flags & 0x20) != 0;
			bool yFlip = (sprite.Flags & 0x40) != 0;
			bool behindBg = (sprite.Flags & 0x80) != 0;
			if (use8x16)
			{
				tileIndex &= 0xFE;
				if (yFlip)
				{
					tileRow = 15 - rowInSprite;
				}
				if (tileRow >= 8)
				{
					tileIndex += 1;
					tileRow -= 8;
				}
			}
			else
			{
				if (yFlip)
				{
					tileRow = 7 - rowInSprite;
				}
			}

			ushort tileAddr = (ushort)(0x8000 + tileIndex * 16);
			for (int col = 0; col < 8; col++)
			{
				int x = spriteX + col;
				if (x < 0 || x >= 160)
				{
					continue;
				}
				int tileCol = xFlip ? (7 - col) : col;
				int colorNum = GetTileColorNumber(tileAddr, tileCol, tileRow);
				if (colorNum == 0)
				{
					continue;
				}
				if (behindBg && !bgIsColor0[x])
				{
					continue;
				}
				int colorBits = (palette >> (colorNum * 2)) & 0x03;
				Color actualColor = MapColor(colorBits);
				Screen.DrawPixel(x, Scanline, actualColor);
			}
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
				int y = OAM[i] - 16; // signed screen-space Y
				int x = OAM[i + 1] - 8; // signed screen-space X
				byte tileNumber = OAM[i + 2];
				byte attributes = OAM[i + 3];
				int oamIndex = i / 4;

				sprites.Add(new SpriteAttributes(x, y, tileNumber, attributes, oamIndex));
			}

			return sprites;
		}

		// Handles GPU registers and updates GPU mode.
		private void SetLCDStatus()
		{
			var lcdStatus = _gb._memory.LCDStatus;

			if (!_gb._memory.LCDEnabled)
			{
				// When LCD is off, STAT mode is 0 (HBlank) and coincidence is cleared.
				// Side effects like LY reset and blanking are handled on the LCD on/off transition.
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

			// STAT mode interrupts trigger on transitions into Mode 0/1/2.
			if (mode != currentMode)
			{
				if (reqInt)
				{
					_gb.RequestInterrupt(Interrupt.LCDStat);
				}
				_prevMode = mode;
			}
			else if (_prevMode < 0)
			{
				_prevMode = mode;
			}

			// LYC=LY coincidence flag is constantly updated; interrupt triggers on 0->1 edge.
			bool coincidence = Scanline == _gb._memory.LYC;
			if (coincidence)
			{
				lcdStatus |= 0b0000_0100;
				if (!_prevCoincidence && lcdStatus.TestBit(6))
				{
					_gb.RequestInterrupt(Interrupt.LCDStat);
				}
			}
			else
			{
				lcdStatus &= 0b1111_1011;
			}
			_prevCoincidence = coincidence;
			
			_gb._memory.LCDStatus = lcdStatus;
		}
	}
}
