using GBOG.CPU;
using GBOG.Memory;
using GBOG.Utils;
using Serilog;
using static System.Windows.Forms.AxHost;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace GBOG.Graphics
{
  // The PPU (which stands for Picture Processing Unit) is the part of the Gameboy that’s responsible for everything you see on screen.
  public class PPU
  {
    private int _scanlineCounter;

    public int StatusMode = 1;
    private int _tileCycleCounter;
    private int _statusModeCounter = 0;
    private int _statusModeCounterAux = 0;
    private int _statusModeLYCounter = 144;
    private int _statusVBlankLine = 0;
    public byte IRQ48Signal = 0;

    private readonly Gameboy _gb;

    private static int _prevLy = 0;
    private bool _scanLineTransfered = false;
    private int _pixelCounter;
    private int _windowLine;
    private static readonly int _lcdMode2Bounds = 456 - 80;
    private readonly int _lcdMode3Bounds = _lcdMode2Bounds - 172;
    private readonly int GAMEBOY_WIDTH = 160;

    public int Scanline { get; private set; } // Represents the current scanline being processed.
    public int WindowScanline { get; private set; } // Represents the current window scanline being processed.
    public byte[] VideoRam { get; private set; } // Represents the Video RAM.
    public byte[] OAM { get; private set; } // Represents the Object Attribute Memory (OAM) for sprites.
    public Screen Screen { get; private set; } // Represents the Screen to render pixels.
    public Color[] FrameBuffer = new Color[160 * 144];
    private byte[] ColorCacheBuffer;
    private byte[] SpriteXCacheBuffer;

    public PPU(Gameboy gb)
    {
      _gb = gb;
      VideoRam = new byte[0x2000];
      OAM = new byte[0xA0];
      Screen = new Screen();
      Scanline = 0;
      _scanlineCounter = 0;
    }

    // Methods

    // Resets the GPU to its initial state.
    public void Reset()
    {
      // Implementation
    }
    public bool Tick(int cycles)
    {
      bool vBlank = false;
      _statusModeCounter += cycles;

      Log.Information($"--> **Tick:::Current Mode: {StatusMode}, StatusModeCounter: {_statusModeCounter}, LYCounter: {_statusModeLYCounter}");
      if (_gb._memory.LCDEnabled)
      {
        switch (StatusMode)
        {
          // During H-BLANK
          case 0:
            if (_statusModeCounter >= 204)
            {
              _statusModeCounter -= 204;
              _statusModeLYCounter++;
              _gb._memory.LY++;

              CompareLYToLYC();

              if (_statusModeLYCounter == 144)
              {
                StatusMode = 1;
                _statusVBlankLine = 0;
                _statusModeCounterAux = _statusModeCounter;

                _gb.RequestInterrupt(Interrupt.VBlank);

                IRQ48Signal &= 0x09;
                byte stat = _gb._memory.ReadByte(0xFF41);
                if (stat.TestBit(4))
                {
                  if (!stat.TestBit(3) && !IRQ48Signal.TestBit(0))
                  {
                    _gb.RequestInterrupt(Interrupt.LCDStat);
                  }
                  IRQ48Signal &= 0x0E;
                }
                vBlank = true;

              }
              else
              {
                byte stat = _gb._memory.ReadByte(0xFF41);
                IRQ48Signal &= 0x09;
                StatusMode = 2;
                if (stat.TestBit(5))
                {
                  if (IRQ48Signal == 0)
                  {
                    _gb.RequestInterrupt(Interrupt.LCDStat);
                  }
                  IRQ48Signal = IRQ48Signal.SetBit(2);
                }
                IRQ48Signal &= 0x0E;
              }
              // Update STAT register
              UpdateStatRegister();
            }
            break;
          // During V-BLANK
          case 1:
            _statusModeCounterAux += cycles;
            if (_statusModeCounterAux >= 456)
            {
              _statusModeCounterAux -= 456;
              _statusVBlankLine++;

              if (_statusVBlankLine <= 9)
              {
                _statusModeLYCounter++;
                _gb._memory.LY++;
                CompareLYToLYC();
              }
            }

            if (_statusModeCounter >= 4104 && _statusModeCounterAux >= 4 && _statusModeLYCounter == 153)
            {
              _statusModeLYCounter = 0;
              _gb._memory.LY = 0;
              CompareLYToLYC();
            }

            if (_statusModeCounter >= 4560)
            {
              _statusModeCounter -= 4560;
              StatusMode = 2;
              // Update STAT register
              UpdateStatRegister();
              IRQ48Signal &= 0x07;
              IRQ48Signal &= 0x0A;
              byte stat = _gb._memory.ReadByte(0xFF41);

              if (stat.TestBit(5))
              {
                if (IRQ48Signal == 0)
                {
                  _gb.RequestInterrupt(Interrupt.LCDStat);
                }
                IRQ48Signal = IRQ48Signal.SetBit(2);
              }
              IRQ48Signal &= 0x0D;
            }
            break;
          // During OAM Search
          case 2:
            if (_statusModeCounter >= 80)
            {
              _statusModeCounter -= 80;
              StatusMode = 3;
              _scanLineTransfered = false;
              IRQ48Signal &= 0x08;
              // Update STAT Register
              UpdateStatRegister();
            }
            break;
          // During LCD Data Transfer
          case 3:
            if (_pixelCounter < 160)
            {
              _tileCycleCounter += cycles;

              byte lcdc = _gb._memory.ReadByte(0xFF40);
              if (_gb._memory.LCDEnabled && lcdc.TestBit(7))
              {
                while (_tileCycleCounter >= 3)
                {
                  Log.Information($"--> **Tick:::Mode3 - Rendering BG for line: {_statusModeLYCounter}");
                  RenderBG(_statusModeLYCounter, _pixelCounter);
                  _pixelCounter += 4;
                  _tileCycleCounter -= 3;

                  if (_pixelCounter >= 160)
                  {
                    break;
                  }
                }
              }
            }

            if (_statusModeCounter >= 160 && !_scanLineTransfered)
            {
              ScanLine(_statusModeLYCounter);
              _scanLineTransfered = true;
            }

            if (_statusModeCounter >= 172)
            {
              _pixelCounter = 0;
              _statusModeCounter -= 172;
              StatusMode = 0;
              _tileCycleCounter = 0;
              UpdateStatRegister();

              byte stat = _gb._memory.ReadByte(0xFF41);
              IRQ48Signal &= 0x08;
              if (stat.TestBit(3))
              {
                if (!IRQ48Signal.TestBit(3))
                {
                  _gb.RequestInterrupt(Interrupt.LCDStat);
                }
                IRQ48Signal = IRQ48Signal.SetBit(0);
              }
            }
            break;
        }
      }
      else
      {
        StatusMode = 0;
        _statusModeCounter = 0;
        _statusModeCounterAux = 0;
        _statusModeLYCounter = 0;
        _windowLine = 0;
        _statusVBlankLine = 0;
        _pixelCounter = 0;
        _gb._memory.WriteByte(0xFF44, 0);
        IRQ48Signal = 0;


        byte stat = _gb._memory.ReadByte(0xFF41);
        if(stat.TestBit(5))
        {
          _gb.RequestInterrupt(Interrupt.LCDStat);
          IRQ48Signal = IRQ48Signal.SetBit(2);
        }
        CompareLYToLYC();
      }
      return vBlank;
    }

    private void ScanLine(int line)
    {
      byte lcdc = _gb._memory.ReadByte(0xFF40);
      if (_gb._memory.LCDEnabled && lcdc.TestBit(7))
      {
        //RenderBG(line, 0);
        RenderWindowNew(line);
        RenderSpritesNew(line);
      }
      else
      {
        Screen.ClearScanline(line);
      }
    }

    private void RenderSpritesNew(int line)
    {
      byte lcdc = _gb._memory.ReadByte(0xFF40);

      if (!lcdc.TestBit(1))
        return;

      int sprite_height = lcdc.TestBit(2) ? 16 : 8;
      int line_width = (line * GAMEBOY_WIDTH);

      bool[] visible_sprites = new bool[40];
      int sprite_limit = 0;

      for (int sprite = 0; sprite < 40; sprite++)
      {
        int sprite_4 = sprite << 2;
        int sprite_y = _gb._memory.ReadByte((uint)(0xFE00 + sprite_4)) - 16;

        if ((sprite_y > line) || ((sprite_y + sprite_height) <= line))
        {
          visible_sprites[sprite] = false;
          continue;
        }

        sprite_limit++;

        visible_sprites[sprite] = sprite_limit <= 10;
      }

      for (int sprite = 39; sprite >= 0; sprite--)
      {
        if (!visible_sprites[sprite])
          continue;

        int sprite_4 = sprite << 2;
        int sprite_x = _gb._memory.ReadByte((uint)(0xFE00 + sprite_4 + 1)) - 8;

        if ((sprite_x < -7) || (sprite_x >= GAMEBOY_WIDTH))
          continue;

        int sprite_y = _gb._memory.ReadByte((uint)(0xFE00 + sprite_4)) - 16;
        int sprite_tile_16 = _gb._memory.ReadByte((uint)(0xFE00 + sprite_4 + 2))
                & ((sprite_height == 16) ? 0xFE : 0xFF) << 4;
        byte sprite_flags = _gb._memory.ReadByte((uint)(0xFE00 + sprite_4 + 3));
        int sprite_pallette = sprite_flags.TestBit(4) ? 1 : 0;
        byte palette = _gb._memory.ReadByte((uint)((sprite_pallette > 0) ? 0xFF49 : 0xFF48));
        bool xflip = sprite_flags.TestBit(5);
        bool yflip = sprite_flags.TestBit(6);
        bool aboveBG = !sprite_flags.TestBit(7);
        bool cgb_tile_bank = sprite_flags.TestBit(3);
        int cgb_tile_pal = sprite_flags & 0x07;
        int tiles = 0x8000;
        int pixel_y = yflip ? ((sprite_height == 16) ? 15 : 7) - (line - sprite_y) : line - sprite_y;
        int pixel_y_2 = 0;
        int offset = 0;

        if (sprite_height == 16 && (pixel_y >= 8))
        {
          pixel_y_2 = (pixel_y - 8) << 1;
          offset = 16;
        }
        else
          pixel_y_2 = pixel_y << 1;

        int tile_address = tiles + sprite_tile_16 + pixel_y_2 + offset;


        int byte1 = _gb._memory.ReadByte((uint)tile_address);
        int byte2 = _gb._memory.ReadByte((uint)(tile_address + 1));


        for (int pixelx = 0; pixelx < 8; pixelx++)
        {
          int pixel = (byte1 & (0x01 << (xflip ? pixelx : 7 - pixelx))) > 0 ? 1 : 0;
          pixel |= (byte2 & (0x01 << (xflip ? pixelx : 7 - pixelx))) > 0 ? 2 : 0;

          if (pixel == 0)
            continue;

          int bufferX = (sprite_x + pixelx);

          if (bufferX < 0 || bufferX >= GAMEBOY_WIDTH)
            continue;

          int position = line_width + bufferX;
          byte color_cache = ColorCacheBuffer[position];

          int sprite_x_cache = SpriteXCacheBuffer[position];
          if (color_cache.TestBit(3) && (sprite_x_cache < sprite_x))
            continue;


          if (!aboveBG && (color_cache & 0x03) > 0)
            continue;

          ColorCacheBuffer[position] = color_cache.SetBit(3);
          SpriteXCacheBuffer[position] = (byte)sprite_x;

          byte color = (byte)((palette >> (pixel << 1)) & 0x03);
          FrameBuffer[position] = MapColor(color);
        }
      }
    }

    private void RenderWindowNew(int line)
    {
      if (_windowLine > 143)
        return;

      byte lcdc = _gb._memory.ReadByte(0xFF40);
      if (!lcdc.TestBit(5))
        return;

      int wx = _gb._memory.WX - 7;
      if (wx > 159)
        return;

      byte wy = _gb._memory.WY;
      if ((wy > 143) || (wy > line))
        return;

      int tiles = lcdc.TestBit(4) ? 0x8000 : 0x8800;
      int map = lcdc.TestBit(6) ? 0x9C00 : 0x9800;
      int lineAdjusted = _windowLine;
      int y_32 = (lineAdjusted >> 3) << 5;
      int pixely = lineAdjusted & 0x7;
      int pixely_2 = pixely << 1;
      int pixely_2_flip = (7 - pixely) << 1;
      int line_width = (line * GAMEBOY_WIDTH);
      byte palette = _gb._memory.BGP;

      for (int x = 0; x < 32; x++)
      {
        int tile = 0;

        if (tiles == 0x8800)
        {
          tile = _gb._memory.ReadByte((uint)(map + y_32 + x));
          tile += 128;
        }
        else
        {
          tile = _gb._memory.ReadByte((uint)(map + y_32 + x));
        }

        int mapOffsetX = x << 3;
        int tile_16 = tile << 4;

        int final_pixely_2 = pixely_2;
        int tile_address = tiles + tile_16 + final_pixely_2;

        int byte1 = _gb._memory.ReadByte((uint)tile_address);
        int byte2 = _gb._memory.ReadByte((uint)(tile_address + 1));


        for (int pixelx = 0; pixelx < 8; pixelx++)
        {
          int bufferX = (mapOffsetX + pixelx + wx);

          if (bufferX < 0 || bufferX >= GAMEBOY_WIDTH)
            continue;

          int pixelx_pos = pixelx;


          int pixel = (byte1 & (0x1 << (7 - pixelx_pos))) > 0 ? 1 : 0;
          pixel |= (byte2 & (0x1 << (7 - pixelx_pos))) > 0 ? 2 : 0;

          int position = line_width + bufferX;

          byte color = (byte)((palette >> (pixel << 1)) & 0x03);
          FrameBuffer[position] = MapColor(color);

        }
      }
      _windowLine++;
    }

    private void RenderBG(int line, int pixel)
    {
      byte lcdc = _gb._memory.ReadByte(0xFF40);
      int line_width = (line * GAMEBOY_WIDTH);

      if (lcdc.TestBit(0))
      {
        int pixels_to_render = 4;
        int offset_x_init = pixel & 0x7;
        int offset_x_end = offset_x_init + pixels_to_render;
        int screen_tile = pixel >> 3;
        int tile_start_addr = lcdc.TestBit(4) ? 0x8000 : 0x8800;
        int map_start_addr = lcdc.TestBit(3) ? 0x9C00 : 0x9800;
        byte scroll_x = _gb._memory.SCX;
        byte scroll_y = _gb._memory.SCY;
        byte line_scrolled = (byte)(line + scroll_y);
        int line_scrolled_32 = (line_scrolled >> 3) << 5;
        int tile_pixel_y = line_scrolled & 0x7;
        int tile_pixel_y_2 = tile_pixel_y << 1;
        int tile_pixel_y_flip_2 = (7 - tile_pixel_y) << 1;
        byte palette = _gb._memory.BGP;

        for (int offset_x = offset_x_init; offset_x < offset_x_end; offset_x++)
        {
          int screen_pixel_x = (screen_tile << 3) + offset_x;
          byte map_pixel_x = (byte)(screen_pixel_x + scroll_x);
          int map_tile_x = map_pixel_x >> 3;
          int map_tile_offset_x = map_pixel_x & 0x7;
          ushort map_tile_addr = (ushort)(map_start_addr + line_scrolled_32 + map_tile_x);

          int map_tile = _gb._memory.ReadByte(map_tile_addr);

          if (tile_start_addr == 0x8800)
          {
            map_tile += 128;
          }

          bool cgb_tile_xflip = false;
          bool cgb_tile_yflip = false;
          int map_tile_16 = map_tile << 4;

          int final_pixely_2 = cgb_tile_yflip ? tile_pixel_y_flip_2 : tile_pixel_y_2;
          int tile_address = tile_start_addr + map_tile_16 + final_pixely_2;

          byte byte1 = _gb._memory.ReadByte((uint)tile_address);
          byte byte2 = _gb._memory.ReadByte((uint)(tile_address + 1));

          int pixel_x_in_tile = map_tile_offset_x;

          if (cgb_tile_xflip)
          {
            pixel_x_in_tile = 7 - pixel_x_in_tile;
          }
          int pixel_x_in_tile_bit = 0x1 << (7 - pixel_x_in_tile);
          int pixel_data = (byte1 & pixel_x_in_tile_bit) > 0 ? 1 : 0;
          pixel_data |= (byte2 & pixel_x_in_tile_bit) > 0 ? 2 : 0;

          int index = line_width + screen_pixel_x;
          byte color = (byte)((palette >> (pixel_data << 1)) & 0x03);
          FrameBuffer[index] = MapColor(color);
        }
      }
    }

    public void ResetWindowLine()
    {
      if ((_windowLine == 0) && (_statusModeLYCounter < 144) && (_statusModeLYCounter > _gb._memory.WY))
        _windowLine = 144;
    }
    private void UpdateStatRegister()
    {
      byte stat = _gb._memory.ReadByte(0xFF41);
      _gb._memory.WriteByte(0xFF41, (byte)((stat & 0xFC) | (StatusMode & 0x3)));
    }

    public void CompareLYToLYC()
    {
      if (_gb._memory.LCDEnabled)
      {
        byte stat = _gb._memory.ReadByte(0xFF41);
        byte lyc = _gb._memory.LYC;

        if (lyc == _statusModeLYCounter)
        {
          stat = stat.SetBit(2);
          if (stat.TestBit(6))
          {
            if (IRQ48Signal == 0)
            {
              _gb.RequestInterrupt(Interrupt.LCDStat);
            }
            IRQ48Signal = IRQ48Signal.SetBit(3);
          }
        }
        else
        {
          stat = stat.UnsetBit(2);
          IRQ48Signal = IRQ48Signal.UnsetBit(3);
        }

        _gb._memory.WriteByteDirect(0xFF41, stat);
      }
    }

    // Reads a byte from a memory address within the GPU's address space.
    public byte ReadByte(ushort address)
    {
      return VideoRam[address - 0x8000];
    }

    // Steps the GPU a given number of cycles, performing rendering tasks.
    public void Step(int cycles)
    {
      if (!_gb._memory.LCDEnabled)
      {
        return;
      }

      _scanlineCounter -= cycles;

      SetLCDStatus();

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

        // Render the window tiles to the screen if the window is enabled
        if (_gb._memory.WindowDisplayEnable)
        {
          RenderWindow();
        }
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
      byte lcdControl = _gb._memory.ReadByte(0xFF40);
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
      int mapRow = yPos / 8; // * 32?

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
        0 => Color.FromArgb(255, 0x9B, 0xBC, 0x0F),
        1 => Color.FromArgb(255, 0x8B, 0xAC, 0x0F),
        2 => Color.FromArgb(255, 0x30, 0x62, 0x30),
        3 => Color.FromArgb(255, 0x0F, 0x38, 0x0F),
        _ => Color.Aquamarine
      };
    }
    
    private int UnmapColor(Color color)
    {
      if (color == Color.FromArgb(255, 0x9B, 0xBC, 0x0F))
      {
        return 0;
      }
      else if (color == Color.FromArgb(255, 0x8B, 0xAC, 0x0F)) { return 1; }
      else if (color == Color.FromArgb(255, 0x30, 0x62, 0x30)) { return 2; }
      else if (color == Color.FromArgb(255, 0x0F, 0x38, 0x0F)) { return 3; }
      else { return 0; }
    }
    private void RenderWindow()
    {
      byte lcdControl = _gb._memory.ReadByte(0xFF40);
      byte wx = _gb._memory.WX;
      byte wy = _gb._memory.WY;
      byte bgp = _gb._memory.BGP; // Background Palette Data

      // If the current scanline is below the window, exit
      if (wx - 7 > 159 || wy > 143 || Scanline < wy) return;

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
      byte lcdControl = _gb._memory.ReadByte(0xFF40);
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
      byte palette = (sprite.Flags & 0x10) != 0 ? obp1 : obp0;

      // Fetch the tile data
      ushort tileNumber = sprite.TileNumber;
      bool xFlip = sprite.XFlip;
      bool yFlip = sprite.YFlip;
      bool priority = sprite.Priority;

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
            colorNum = tile.GetPixel(8 - col - 1, height - row - 1);
          }
          else if (xFlip)
          {
            colorNum = tile.GetPixel(8 - col - 1, row);
          }
          else if (yFlip)
          {
            colorNum = tile.GetPixel(col, height - row - 1);
          }
          else
          {
            colorNum = tile.GetPixel(col, row);
          }

          // Map the color number to actual color using the sprite's palette
          int colorBits = (palette >> (colorNum * 2)) & 0x03;
          var currentPixel = Screen.GetPixel(xPos, Scanline);
          Color actualColor = MapColor(colorBits);

          //if (!priority && (currentPixel == actualColor))
          //  continue;

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
      var lcdStatus = _gb._memory.ReadByte(0xFF41); ;

      if (!_gb._memory.LCDEnabled)
      {
        Screen.Clear(Color.Black);
        _scanlineCounter = 456;
        _gb._memory.LY = 0;
        lcdStatus &= 0b1111_1100;
        lcdStatus |= 0b01;
        _gb._memory.WriteByte(0xFF41,lcdStatus);
        return;
      }

      Scanline = _gb._memory.LY;
      var currentMode = lcdStatus & 0b11;
      int mode;
      bool reqInt = false;

      if (Scanline >= 144)
      {
        mode = (int)GraphicsMode.Mode1_VBlank;
        lcdStatus &= 0b1111_1100;
        reqInt = lcdStatus.TestBit(4);
        WindowScanline = 0;
      }
      else if (_scanlineCounter >= _lcdMode2Bounds)
      {
        mode = (int)GraphicsMode.Mode2_OAMRead;
        lcdStatus &= 0b1111_1100;
        reqInt = lcdStatus.TestBit(5);
      }
      else if (_scanlineCounter >= _lcdMode3Bounds)
      {
        mode = (int)GraphicsMode.Mode3_VRAMReadWrite;
        lcdStatus |= 0b11;
        if (mode != currentMode)
        {
          RenderScanline();
        }
      }
      else
      {
        mode = (int)GraphicsMode.Mode0_HBlank;
        lcdStatus &= 0b1111_1100;
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

      _gb._memory.WriteByte(0xFF41, lcdStatus);
    }
  }
}
