using GBOG.CPU.Opcodes;
using GBOG.Graphics;
using GBOG.Memory;
using GBOG.Utils;
using Microsoft.VisualBasic.Logging;
using Serilog;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using Color = System.Drawing.Color;
using Log = Serilog.Log;

namespace GBOG.CPU
{
  public class Gameboy
  {
    #region Registers
    // The CPU has eight 8-bit registers.
    // These registers are named A, B, C, D, E, F, H and L.
    // Since they are 8-bit registers, they can only hold 8-bit values. 
    private byte[] _registers = new byte[8];
    private OpcodeHandler _opcodeHandler;
    private int _scanlineCounter;
    private byte[,,] _display;
    private byte[] _pixels;
    private RenderWindow _window;
    private Texture _bgTexture;

    public GBMemory _memory { get; }
    private int _mClockCount { get; set; }

    // The registers can be accessed individually:
    public byte A { get => _registers[0]; set => _registers[0] = value; }
    public byte B { get => _registers[1]; set => _registers[1] = value; }
    public byte C { get => _registers[2]; set => _registers[2] = value; }
    public byte D { get => _registers[3]; set => _registers[3] = value; }
    public byte E { get => _registers[4]; set => _registers[4] = value; }
    public byte F { get => _registers[5]; set => _registers[5] = value; }
    public byte H { get => _registers[6]; set => _registers[6] = value; }
    public byte L { get => _registers[7]; set => _registers[7] = value; }
    // However, the GameBoy can “combine” two registers in order to read and write 16-bit values. 
    // The valid combinations are AF, BC, DE and HL.
    public ushort AF
    {
      get => (ushort)((A << 8) | F);
      set
      {
        A = (byte)((value & 0xFF00) >> 8);
        F = (byte)(value & 0b11110000);
      }
    }

    public ushort BC
    {
      get => (ushort)((B << 8) | C);
      set
      {
        B = (byte)((value & 0xFF00) >> 8);
        C = (byte)(value & 0xFF);
      }
    }

    public ushort DE
    {
      get => (ushort)((D << 8) | E);
      set
      {
        D = (byte)((value & 0xFF00) >> 8);
        E = (byte)(value & 0xFF);
      }
    }

    public ushort HL
    {
      get => (ushort)((H << 8) | L);
      set
      {
        H = (byte)((value & 0xFF00) >> 8);
        L = (byte)(value & 0xFF);
      }
    }
    // The F register is a special register that is used for flags.
    // The flags are used to indicate the result of a comparison or operation.
    // The flags are:
    // Z - Zero flag
    // N - Subtract flag
    // HC - Half Carry flag
    // CF - Carry flag
    public bool Z { get => (F & 0b1000_0000) != 0; set => F = (byte)((F & 0b0111_1111) | (value ? 0b1000_0000 : 0)); }
    public bool N { get => (F & 0b0100_0000) != 0; set => F = (byte)((F & 0b1011_1111) | (value ? 0b0100_0000 : 0)); }
    public bool HC { get => (F & 0b0010_0000) != 0; set => F = (byte)((F & 0b1101_1111) | (value ? 0b0010_0000 : 0)); }
    public bool CF { get => (F & 0b0001_0000) != 0; set => F = (byte)((F & 0b1110_1111) | (value ? 0b0001_0000 : 0)); }

    // The GameBoy has two 16-bit registers: the stack pointer and the program counter.
    // The stack pointer is used to keep track of the current stack position.
    public ushort SP { get; set; }
    // The program counter is used to keep track of the current position in the program.
    public ushort PC { get; set; }
    public bool InterruptMasterEnabled { get; set; }
    public bool Halt { get; set; }
    public int DIVCounter { get; set; }
    #endregion

    public Gameboy()
    {
      Log.Logger = new LoggerConfiguration()
        .WriteTo.File("log.txt",
        outputTemplate: "{Message:lj}{NewLine}{Exception}")
        .CreateLogger();
      _display = new byte[160, 144, 3];
      _pixels = new byte[160 * 144];
      _window = new RenderWindow(new VideoMode(160, 144), "GameBoy")
      {
        Size = new Vector2u(160 * 4, 144 * 4)
      };
      _window.Closed += (sender, e) => _window.Close();
      _window.KeyPressed += (sender, e) =>
      {
        switch (e.Code)
        {
          case Keyboard.Key.Escape:
            _window.Close();
            break;
          case Keyboard.Key.Space:
            break;
          case Keyboard.Key.Left:
            break;
          case Keyboard.Key.Right:
            break;
          case Keyboard.Key.Up:
            break;
          case Keyboard.Key.Down:
            break;
        }
      };

      _bgTexture = new Texture(160, 144);

      _opcodeHandler = new OpcodeHandler(this);
      _memory = new GBMemory(this);
      AF = 0x01B0;
      BC = 0x0013;
      DE = 0x00D8;
      HL = 0x014D;
      SP = 0xFFFE;
      PC = 0x0100;
    }

    private void DoLoop()
    {
      const int MAX_CYCLES = 69905;
      var i = 0;
      int cycles = 4;
      while (i < 300)
      {
        int cyclesThisUpdate = 0;

        while (cyclesThisUpdate < MAX_CYCLES)
        {

          byte opcode;

          if (IsInterruptRequested())
          {
            Halt = false;
            HandleInterrupts();
          }
          if (!Halt)
          {
            opcode = _memory.ReadByte(PC++);
            var op = _opcodeHandler.GetOpcode(opcode);
            var steps = op?.steps;
            if (steps != null)
            {
              foreach (var step in steps)
              {
                if (step(this))
                {
                  cyclesThisUpdate++;
                  UpdateTimer(cycles);
                  UpdateGraphics(cycles);
                }
                else
                {
                  break;
                }
              }
            }
            //cycles = _opcodeHandler.HandleOpcode(opcode);
          }
          else
          {
            cyclesThisUpdate++;
            UpdateTimer(cycles);
            UpdateGraphics(cycles);
          }



          //LogSystemState();
        }
        _bgTexture.Update(FlattenDisplayArray());
        _window.Clear();
        _window.Draw(new Sprite(_bgTexture));
        _window.Display();
      }
      i++;
    }

    private byte[] FlattenDisplayArray()
    {
      var flattened = new byte[160 * 144 * 4];
      for (int i = 0; i < 160; i++)
      {
        for (int j = 0; j < 144; j++)
        {
          flattened[(i * 144 + j) * 4] = _display[i, j, 0];
          flattened[(i * 144 + j) * 4 + 1] = _display[i, j, 1];
          flattened[(i * 144 + j) * 4 + 2] = _display[i, j, 2];
          flattened[(i * 144 + j) * 4 + 3] = 255;
        }
      }

      return flattened;
    }

    private void HandleInterrupts()
    {
      if (InterruptMasterEnabled)
      {
        if (_memory.IFVBlank && _memory.IEVBlank)
        {
          _memory.IFVBlank = false;
          InterruptMasterEnabled = false;
          _memory.WriteByte(--SP, (byte)(PC >> 8));
          _memory.WriteByte(--SP, (byte)(PC & 0xFF));
          PC = 0x40;
        }
        else if (_memory.IFLCDStat && _memory.IELCDStat)
        {
          _memory.IFLCDStat = false;
          InterruptMasterEnabled = false;
          _memory.WriteByte(--SP, (byte)(PC >> 8));
          _memory.WriteByte(--SP, (byte)(PC & 0xFF));
          PC = 0x48;
        }
        else if (_memory.IFTimer && _memory.IETimer)
        {
          _memory.IFTimer = false;
          InterruptMasterEnabled = false;
          _memory.WriteByte(--SP, (byte)(PC >> 8));
          _memory.WriteByte(--SP, (byte)(PC & 0xFF));
          PC = 0x50;
        }
        else if (_memory.IFSerial && _memory.IESerial)
        {
          _memory.IFSerial = false;
          InterruptMasterEnabled = false;
          _memory.WriteByte(--SP, (byte)(PC >> 8));
          _memory.WriteByte(--SP, (byte)(PC & 0xFF));
          PC = 0x58;
        }
        else if (_memory.IFJoypad && _memory.IEJoypad)
        {
          _memory.IFJoypad = false;
          InterruptMasterEnabled = false;
          _memory.WriteByte(--SP, (byte)(PC >> 8));
          _memory.WriteByte(--SP, (byte)(PC & 0xFF));
          PC = 0x60;
        }
      }
    }

    private bool IsInterruptRequested()
    {
      return (_memory.IF & _memory.InterruptEnableRegister) != 0;
    }
    private void UpdateTimer(int cycles)
    {
      // increment timer
      DIVCounter += (byte)cycles;
      // is clock enabled?
      if (_memory.TimerEnabled)
      {
        _mClockCount -= cycles;
        if (_mClockCount <= 0)
        {
          SetClockFrequency();
          // increment timer
          _memory.TIMA++;
          // check if timer overflows
          if (_memory.TIMA == 255)
          {
            _memory.TIMA = _memory.TMA;
            _memory.IFTimer = true;
          }
        }
      }
      if (DIVCounter > 255)
      {
        DIVCounter = 0;
        _memory.DIV++;
      }
    }

    public byte GetClockFrequency()
    {
      return (byte)(_memory.TAC & 0x3);
    }

    public void SetClockFrequency()
    {
      var frequency = GetClockFrequency();
      _mClockCount = frequency switch
      {
        0 => 1024,
        1 => 16,
        2 => 64,
        3 => 256,
        _ => throw new Exception("Invalid clock frequency")
      };
    }

    public void UpdateGraphics(int cycles)
    {
      SetLCDStatus();

      if (_memory.LCD)
      {
        _scanlineCounter -= cycles;
      }
      else
      {
        return;
      }

      if (_scanlineCounter <= 0)
      {
        _memory.LY++;
        byte currentLine = _memory.LY;
        _scanlineCounter = 456;

        if (currentLine == 144)
        {
          // VBlank
          _memory.IFVBlank = true;
          _memory.IF = 0x01;
        }
        else if (currentLine > 153)
        {
          _memory.LY = 0;
        }
        else if (currentLine < 144)
        {
          DrawScanline();
        }
      }
    }

    private void DrawScanline()
    {
      if (_memory.BGDisplay)
      {
        DrawBackground();
      }

      if (_memory.SpriteDisplay)
      {
        DrawSprites();
      }
    }

    private void DrawSprites()
    {
      bool use8x16 = _memory.OBJSize;

      for (int sprite = 0; sprite < 40; sprite++)
      {
        byte index = (byte)(sprite * 4);
        byte yPos = (byte)(_memory.ReadByte((ushort)(0xFE00 + index)) - 16);
        byte xPos = (byte)(_memory.ReadByte((ushort)(0xFE00 + index + 1)) - 8);
        byte tileLocation = _memory.ReadByte((ushort)(0xFE00 + index + 2));
        byte attributes = _memory.ReadByte((ushort)(0xFE00 + index + 3));

        bool yFlip = attributes.TestBit(6);
        bool xFlip = attributes.TestBit(5);

        int ySize = use8x16 ? 16 : 8;

        int scanline = _memory.LY;

        if (scanline >= yPos && scanline < (yPos + ySize))
        {
          int line = scanline - yPos;

          if (yFlip)
          {
            line -= ySize;
            line *= -1;
          }

          line *= 2;
          ushort dataAddress = (ushort)(0x8000 + (tileLocation * 16) + line);
          byte data1 = _memory.ReadByte(dataAddress);
          byte data2 = _memory.ReadByte((ushort)(dataAddress + 1));

          for (int tilePixel = 7; tilePixel >= 0; tilePixel--)
          {
            int colorBit = tilePixel;

            if (xFlip)
            {
              colorBit -= 7;
              colorBit *= -1;
            }

            int colorNum = 0;
            colorNum |= data2.TestBit(colorBit) ? 1 : 0;
            colorNum <<= 1;
            colorNum |= data1.TestBit(colorBit) ? 1 : 0;

            Color color = GetColor((byte)colorNum, 0xFF48);

            int red = 0;
            int green = 0;
            int blue = 0;

            switch (color)
            {
              case Color color1 when color1 == Color.White:
                red = 255;
                green = 255;
                blue = 255;
                break;
              case Color color2 when color2 == Color.LightGray:
                red = 0xCC;
                green = 0xCC;
                blue = 0xCC;
                break;
              case Color color3 when color3 == Color.DarkGray:
                red = 0x77;
                green = 0x77;
                blue = 0x77;
                break;
              case Color color4 when color4 == Color.Black:
                red = 0;
                green = 0;
                blue = 0;
                break;
            }

            int xPix = 0 - tilePixel;
            xPix += 7;

            int pixel = xPos + xPix;

            if (scanline < 0 || scanline > 143 || pixel < 0 || pixel > 159)
            {
              continue;
            }

            _display[pixel, scanline, 0] = (byte)red;
            _display[pixel, scanline, 1] = (byte)green;
            _display[pixel, scanline, 2] = (byte)blue;

            // alternatively, use the colorNum directly in a 160*144 array to represent the pixel
            // use pixel and finalY to determine the position in the array
            _pixels[pixel + scanline * 160] = (byte)colorNum;
          }
        }
      }
    }

    private void DrawBackground()
    {
      ushort tileData = 0;
      ushort backgroundMemory = 0;
      bool unsigned = true;

      byte scrollX = _memory.SCX;
      byte scrollY = _memory.SCY;
      byte windowX = _memory.WX;
      byte windowY = _memory.WY;

      bool usingWindow = false;

      if (_memory.WindowDisplayEnable)
      {
        if (windowY <= _memory.LY)
          usingWindow = true;
      }

      if (_memory.BGWindowTileDataSelect)
      {
        tileData = 0x8000;
      }
      else
      {
        tileData = 0x8800;
        unsigned = false;
      }

      if (usingWindow)
      {
        if (_memory.WindowTileMapDisplaySelect)
        {
          backgroundMemory = 0x9C00;
        }
        else
        {
          backgroundMemory = 0x9800;
        }
      }
      else
      {
        if (_memory.BGWindowTileDataSelect)
        {
          backgroundMemory = 0x9C00;
        }
        else
        {
          backgroundMemory = 0x9800;
        }
      }

      byte yPos;

      if (usingWindow)
      {
        yPos = (byte)(_memory.LY - windowY);
      }
      else
      {
        yPos = (byte)(scrollY + _memory.LY);
      }

      ushort tileRow = (ushort)((yPos / 8) * 32);

      for (int pixel = 0; pixel < 160; pixel++)
      {
        byte xPos = (byte)(pixel + scrollX);

        if (usingWindow)
        {
          if (pixel >= windowX)
          {
            xPos = (byte)(pixel - windowX);
          }
        }

        ushort tileCol = (ushort)(xPos / 8);
        int tileNum;
        ushort tileAddress = (ushort)(backgroundMemory + tileRow + tileCol);

        if (unsigned)
        {
          tileNum = _memory.ReadByte(tileAddress);
        }
        else
        {
          tileNum = (sbyte)_memory.ReadByte(tileAddress);
        }

        ushort tileLocation = tileData;

        if (unsigned)
        {
          tileLocation += (ushort)(tileNum * 16);
        }
        else
        {
          tileLocation += (ushort)((tileNum + 128) * 16);
        }

        byte line = (byte)(yPos % 8);
        line *= 2;

        byte data1 = _memory.ReadByte((ushort)(tileLocation + line));
        byte data2 = _memory.ReadByte((ushort)(tileLocation + line + 1));

        int colorBit = xPos % 8;
        colorBit -= 7;
        colorBit *= -1;

        int colorNum = 0;
        colorNum |= data2.TestBit(colorBit) ? 1 : 0;
        colorNum <<= 1;
        colorNum |= data1.TestBit(colorBit) ? 1 : 0;

        Color color = GetColor((byte)colorNum, 0xFF47);

        int red = 0;
        int green = 0;
        int blue = 0;

        switch (color)
        {
          case Color color1 when color1 == Color.White:
            red = 255;
            green = 255;
            blue = 255;
            break;
          case Color color2 when color2 == Color.LightGray:
            red = 0xCC;
            green = 0xCC;
            blue = 0xCC;
            break;
          case Color color3 when color3 == Color.DarkGray:
            red = 0x77;
            green = 0x77;
            blue = 0x77;
            break;
          case Color color4 when color4 == Color.Black:
            red = 0;
            green = 0;
            blue = 0;
            break;
        }

        int finalY = _memory.LY;

        if (finalY < 0 || finalY > 143 || pixel < 0 || pixel > 159)
        {
          continue;
        }

        _display[pixel, finalY, 0] = (byte)red;
        _display[pixel, finalY, 1] = (byte)green;
        _display[pixel, finalY, 2] = (byte)blue;

        // alternatively, use the colorNum directly in a 160*144 array to represent the pixel
        // use pixel and finalY to determine the position in the array
        _pixels[pixel + finalY * 160] = (byte)colorNum;
      }
    }

    private Color GetColor(byte colorNum, ushort address)
    {
      byte palette = _memory.ReadByte(address);
      byte hi = 0;
      byte lo = 0;

      switch (colorNum)
      {
        case 0:
          hi = 1;
          lo = 0;
          break;
        case 1:
          hi = 3;
          lo = 2;
          break;
        case 2:
          hi = 5;
          lo = 4;
          break;
        case 3:
          hi = 7;
          lo = 6;
          break;
      }

      int color = 0;
      color |= palette.TestBit(hi) ? 1 : 0;
      color <<= 1;
      color |= palette.TestBit(lo) ? 1 : 0;

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
      }

      return Color.Black;
    }

    private void SetLCDStatus()
    {
      byte status = _memory.STAT;

      if (!_memory.LCD)
      {
        _scanlineCounter = 456;
        _memory.LY = 0;
        status &= 0b1111_1100;
        status |= 0b01;
        _memory.STAT = status;
        return;
      }

      byte currentLine = _memory.LY;
      byte currentMode = (byte)(status & 0b11);
      byte mode;
      bool reqInt = false;

      if (currentLine >= 144)
      {
        mode = (byte)GraphicsMode.OAM_access;
        status |= 0b01;
        reqInt = status.TestBit(4);
      }
      else
      {
        int mode2Bounds = 456 - 80;
        int mode3Bounds = mode2Bounds - 172;

        if (_scanlineCounter >= mode2Bounds)
        {
          mode = (byte)GraphicsMode.HBlank;
          status |= 0b10;
          reqInt = status.TestBit(5);
        }
        else if (_scanlineCounter >= mode3Bounds)
        {
          mode = (byte)GraphicsMode.VBlank;
          status |= 0b11;
        }
        else
        {
          mode = (byte)GraphicsMode.OAM_access;
          status &= 0b1111_1100;
          reqInt = status.TestBit(3);
        }
      }

      if (reqInt && mode != currentMode)
      {
        _memory.IFLCDStat = true;
        _memory.IF = 0x02;
      }

      if (_memory.LY == _memory.LYC)
      {
        status |= 0b100;
        if (status.TestBit(6))
        {
          _memory.IFLCDStat = true;
          _memory.IF = 0x02;
        }
      }
      else
      {
        status &= 0b1111_1011;
      }

      _memory.STAT = status;
    }

    public void LoadRom(string path)
    {
      // Open the file as a stream of bytes
      var rom = File.ReadAllBytes(path);

      // Create a buffer to hold the contents
      // Read the file into the buffer
      _memory.InitialiseGame(rom);
      LogSystemState();
      DoLoop();
    }

    private void LogSystemState()
    {

      // Format A: 01 F: B0 B: 00 C: 13 D: 00 E: D8 H: 01 L: 4D SP: FFFE PC: 00:0100 (00 C3 13 02)
      // Format: [registers] (mem[pc] mem[pc+1] mem[pc+2] mem[pc+3])
      // All of the values between A and PC are the hex-encoded values of the corresponding registers. 
      // The final values in brackets (00 C3 13 02) are the 4 bytes stored in the memory locations near PC (ie. the values at pc,pc+1,pc+2,pc+3).
      // The values in brackets are useful for debugging, as they show the next few bytes of the program.
      Log.Information($"A: {A:X2} F: {F:X2} B: {B:X2} C: {C:X2} D: {D:X2} E: {E:X2} H: {H:X2} L: {L:X2} SP: {SP:X4} PC: 00:{PC:X4} ({_memory.ReadByte(PC):X2} {_memory.ReadByte((ushort)(PC + 1)):X2} {_memory.ReadByte((ushort)(PC + 2)):X2} {_memory.ReadByte((ushort)(PC + 3)):X2})");
    }
  }
}
