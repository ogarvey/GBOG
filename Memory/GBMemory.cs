﻿using GBOG.CPU;
using System.Diagnostics;

namespace GBOG.Memory
{
  public class GBMemory
  {
  
		public event EventHandler<char> SerialDataReceived;
    
		// The GameBoy has 8kB of RAM.
		// The RAM is used to store the program and the data.
		// The RAM is divided into different sections:
		// 0x0000 - 0x3FFF: ROM bank 0
		// 0x4000 - 0x7FFF: Switchable ROM bank
		// 0x8000 - 0x97FF: Character RAM 
		// 0x9800 - 0x9BFF: BG map data 1
		// 0x9C00 - 0x9FFF: BG map data 2
		// 0xA000 - 0xBFFF: Cartridge RAM 
		// 0xC000 - 0xCFFF: Internal RAM bank 0 (fixed)
		// 0xD000 - 0xDFFF: Internal RAM bank 1-7 (switchable - CGB only)
		// 0xE000 - 0xFDFF: Echo RAM (reserved, do not use)
		// 0xFE00 - 0xFE9F: OAM (sprite) RAM
		// 0xFEA0 - 0xFEFF: Unusable RAM
		// 0xFF00 - 0xFF7F: I/O Registers
		// 0xFF80 - 0xFFFE: Zero Page
		// 0xFFFF - 0xFFFF: Interrupt enable register
		private byte[] _memory = new byte[0x10000];
    private readonly Gameboy _gameBoy;

    // Rom bank 0
    // The first 16kB of the ROM are always mapped to the memory address 0x0000 - 0x3FFF.
    // The first 256 bytes of the ROM are reserved for the interrupt vector.
    // The next 256 bytes are reserved for the Nintendo logo.
    // The next 48 bytes are reserved for the title of the game.
    // The next 2 bytes are reserved for the new license code.
    // The next byte is reserved for the SGB flag.
    // The next byte is reserved for the cartridge type.
    // The next byte is reserved for the ROM size.
    // The next byte is reserved for the RAM size.
    // The next byte is reserved for the destination code.
    // The next byte is reserved for the old license code.
    // The next byte is reserved for the mask ROM version number.
    // The next byte is reserved for the header checksum.
    // The next 2 bytes are reserved for the global checksum.
    // The next 16kB of the ROM are available for the program.
    public byte[] RomBank0
    {
      get
      {
        byte[] romBank0 = new byte[0x4000];
        Array.Copy(_memory, 0, romBank0, 0, 0x4000);
        return romBank0;
      }
      set
      {
        Array.Copy(value, 0, _memory, 0, 0x4000);
      }
    }
    // Rom bank 1
    // The next 16kB of the ROM can be mapped to the memory address 0x4000 - 0x7FFF.
    public byte[] SwitchableRomBank
    {
      get
      {
        byte[] romBank1 = new byte[0x4000];
        Array.Copy(_memory, 0x4000, romBank1, 0, 0x4000);
        return romBank1;
      }
      set
      {
        Array.Copy(value, 0, _memory, 0x4000, 0x4000);
      }
    }

    // Character RAM
    // The character RAM is used to store the sprites.
    // The sprites are 8x8 pixels or 8x16 pixels.
    // The character RAM is mapped to the memory address 0x8000 - 0x97FF.
    public byte[] CharacterRam
    {
      get
      {
        byte[] characterRam = new byte[0x1800];
        Array.Copy(_memory, 0x8000, characterRam, 0, 0x1800);
        return characterRam;
      }
      set
      {
        Array.Copy(value, 0, _memory, 0x8000, 0x1800);
      }
    }

    // BG map data 1
    // The BG map data 1 is used to store the background map.
    // The background map is used to display the background.
    // The background map is mapped to the memory address 0x9800 - 0x9BFF.
    public byte[] BGMapData1
    {
      get
      {
        byte[] bgMapData1 = new byte[0x400];
        Array.Copy(_memory, 0x9800, bgMapData1, 0, 0x400);
        return bgMapData1;
      }
      set
      {
        Array.Copy(value, 0, _memory, 0x9800, 0x400);
      }
    }

    // BG map data 2
    // The BG map data 2 is used to store the background map.
    // The background map is used to display the background.
    // The background map is mapped to the memory address 0x9C00 - 0x9FFF.
    public byte[] BGMapData2
    {
      get
      {
        byte[] bgMapData2 = new byte[0x400];
        Array.Copy(_memory, 0x9C00, bgMapData2, 0, 0x400);
        return bgMapData2;
      }
      set
      {
        Array.Copy(value, 0, _memory, 0x9C00, 0x400);
      }
    }

    // Cartridge RAM
    // The cartridge RAM is used to store the save data.
    // The cartridge RAM is mapped to the memory address 0xA000 - 0xBFFF.
    public byte[] CartridgeRam
    {
      get
      {
        byte[] cartridgeRam = new byte[0x2000];
        Array.Copy(_memory, 0xA000, cartridgeRam, 0, 0x2000);
        return cartridgeRam;
      }
      set
      {
        Array.Copy(value, 0, _memory, 0xA000, 0x2000);
      }
    }

    // Internal RAM bank 0
    // The internal RAM bank 0 is used to store the program and the data.
    // The internal RAM bank 0 is mapped to the memory address 0xC000 - 0xCFFF.
    public byte[] InternalRamBank0
    {
      get
      {
        byte[] internalRamBank0 = new byte[0x1000];
        Array.Copy(_memory, 0xC000, internalRamBank0, 0, 0x1000);
        return internalRamBank0;
      }
      set
      {
        Array.Copy(value, 0, _memory, 0xC000, 0x1000);
      }
    }

    // Internal RAM bank 1-7
    // The internal RAM bank 1-7 is used to store the program and the data.
    // The internal RAM bank 1-7 is mapped to the memory address 0xD000 - 0xDFFF.
    public byte[] InternalRamBank1To7
    {
      get
      {
        byte[] internalRamBank1To7 = new byte[0x7000];
        Array.Copy(_memory, 0xD000, internalRamBank1To7, 0, 0x7000);
        return internalRamBank1To7;
      }
      set
      {
        Array.Copy(value, 0, _memory, 0xD000, 0x7000);
      }
    }

    // Echo RAM
    // The echo RAM is a copy of the internal RAM.
    // The echo RAM is mapped to the memory address 0xE000 - 0xFDFF.
    public byte[] EchoRam
    {
      get
      {
        byte[] echoRam = new byte[0x1E00];
        Array.Copy(_memory, 0xE000, echoRam, 0, 0x1E00);
        return echoRam;
      }
      set
      {
        Array.Copy(value, 0, _memory, 0xE000, 0x1E00);
      }
    }

    // OAM (sprite) RAM
    // The OAM RAM is used to store the sprites.
    // The sprites are 8x8 pixels or 8x16 pixels.
    // The OAM RAM is mapped to the memory address 0xFE00 - 0xFE9F.
    public byte[] OAMRam
    {
      get
      {
        byte[] oamRam = new byte[0xA0];
        Array.Copy(_memory, 0xFE00, oamRam, 0, 0xA0);
        return oamRam;
      }
      set
      {
        Array.Copy(value, 0, _memory, 0xFE00, 0xA0);
      }
    }

    // Unusable RAM
    // The unusable RAM is not used.
    // The unusable RAM is mapped to the memory address 0xFEA0 - 0xFEFF.
    public byte[] UnusableRam
    {
      get
      {
        byte[] unusableRam = new byte[0x60];
        Array.Copy(_memory, 0xFEA0, unusableRam, 0, 0x60);
        return unusableRam;
      }
      set
      {
        Array.Copy(value, 0, _memory, 0xFEA0, 0x60);
      }
    }

    // I/O Registers
    // The I/O registers are used to control the GameBoy.
    // The I/O registers are mapped to the memory address 0xFF00 - 0xFF7F.
    public byte[] IORegisters
    {
      get
      {
        byte[] ioRegisters = new byte[0x80];
        Array.Copy(_memory, 0xFF00, ioRegisters, 0, 0x80);
        return ioRegisters;
      }
      set
      {
        Array.Copy(value, 0, _memory, 0xFF00, 0x80);
      }
    }

    // SB 
    // The SB register is used to store the serial transfer data.
    // The SB register is mapped to the memory address 0xFF01.
    public byte SB
    {
      get
      {
        return _memory[0xFF01];
      }
      set
      {
        _memory[0xFF01] = value;
      }
    }

    // SC
    // The SC register is used to store the serial transfer data.
    // The SC register is mapped to the memory address 0xFF02.
    public byte SC
    {
      get
      {
        return _memory[0xFF02];
      }
      set
      {
        _memory[0xFF02] = value;
      }
    }

    // DIV
    // The DIV register is used to store the divider register.
    // The DIV register is mapped to the memory address 0xFF04.
    public byte DIV
    {
      get
      {
        return _memory[0xFF04];
      }
      set
      {
        _memory[0xFF04] = value;
      }
    }

    // TIMA
    // The TIMA register is used to store the timer counter.
    // The TIMA register is mapped to the memory address 0xFF05.
    public byte TIMA
    {
      get
      {
        return _memory[0xFF05];
      }
      set
      {
        _memory[0xFF05] = value;
      }
    }

    // TMA
    // The TMA register is used to store the timer modulo.
    // The TMA register is mapped to the memory address 0xFF06.
    public byte TMA
    {
      get
      {
        return _memory[0xFF06];
      }
      set
      {
        _memory[0xFF06] = value;
      }
    }

    // TAC
    // The TAC register is used to store the timer control.
    // The TAC register is mapped to the memory address 0xFF07.
    // Timer Control 
    // Bits 1-0 - Input Clock Select
    //            00: 4096   Hz 
    //            01: 262144 Hz
    //            10: 65536  Hz
    //            11: 16384  Hz
    // Bit  2   - Timer Enable

    public byte TAC
    {
      get
      {
        return _memory[0xFF07];
      }
      set
      {
        _memory[0xFF07] = value;
      }
    }

    public bool TimerEnabled
    {
      get
      {
        return (_memory[0xFF07] & 0b0000_0100) != 0;
      }
      set
      {
        _memory[0xFF07] = (byte)((_memory[0xFF07] & 0b1111_1011) | (value ? 0b0000_0100 : 0));
      }
    }
    // IF
    // The IF register is used to store the interrupt flags.
    // The IF register is mapped to the memory address 0xFF0F.
    // Interrupt Flag (R/W)
    // Bit 4: Transition from High to Low of Pin number P10-P13
    // Bit 3: Serial I/O transfer complete
    // Bit 2: Timer Overflow
    // Bit 1: LCDC (see STAT)
    // Bit 0: V-Blank
    public byte IF
    {
      get
      {
        return _memory[0xFF0F];
      }
      set
      {
        _memory[0xFF0F] = value;
      }
    }

    public bool IFVBlank
    {
      get
      {
        return (_memory[0xFF0F] & 0b0000_0001) != 0;
      }
      set
      {
        _memory[0xFF0F] = (byte)((_memory[0xFF0F] & 0b1111_1110) | (value ? 0b0000_0001 : 0));
      }
    }

    public bool IFLCDStat
    {
      get
      {
        return (_memory[0xFF0F] & 0b0000_0010) != 0;
      }
      set
      {
        _memory[0xFF0F] = (byte)((_memory[0xFF0F] & 0b1111_1101) | (value ? 0b0000_0010 : 0));
      }
    }

    public bool IFTimer
    {
      get
      {
        return (_memory[0xFF0F] & 0b0000_0100) != 0;
      }
      set
      {
        _memory[0xFF0F] = (byte)((_memory[0xFF0F] & 0b1111_1011) | (value ? 0b0000_0100 : 0));
      }
    }

    public bool IFSerial
    {
      get
      {
        return (_memory[0xFF0F] & 0b0000_1000) != 0;
      }
      set
      {
        _memory[0xFF0F] = (byte)((_memory[0xFF0F] & 0b1111_0111) | (value ? 0b0000_1000 : 0));
      }
    }

    public bool IFJoypad
    {
      get
      {
        return (_memory[0xFF0F] & 0b0001_0000) != 0;
      }
      set
      {
        _memory[0xFF0F] = (byte)((_memory[0xFF0F] & 0b1110_1111) | (value ? 0b0001_0000 : 0));
      }
    }

    // LCDC
    // The LCDC register is used to store the LCD control.
    // The LCDC register is mapped to the memory address 0xFF40.
    // LCD Control
    // Bit 7 - LCD Display Enable             (0=Off, 1=On)
    public bool LCD
    {
      get
      {
        return (_memory[0xFF40] & 0b1000_0000) != 0;
      }
      set
      {
        _memory[0xFF40] = (byte)((_memory[0xFF40] & 0b0111_1111) | (value ? 0b1000_0000 : 0));
      }
    }
    // Bit 6 - Window Tile Map Display Select (0=9800-9BFF, 1=9C00-9FFF)
    public bool WindowTileMapDisplaySelect
    {
      get
      {
        return (_memory[0xFF40] & 0b0100_0000) != 0;
      }
      set
      {
        _memory[0xFF40] = (byte)((_memory[0xFF40] & 0b1011_1111) | (value ? 0b0100_0000 : 0));
      }
    }
    // Bit 5 - Window Display Enable          (0=Off, 1=On)
    public bool WindowDisplayEnable
    {
      get
      {
        return (_memory[0xFF40] & 0b0010_0000) != 0;
      }
      set
      {
        _memory[0xFF40] = (byte)((_memory[0xFF40] & 0b1101_1111) | (value ? 0b0010_0000 : 0));
      }
    }
    // Bit 4 - BG & Window Tile Data Select   (0=8800-97FF, 1=8000-8FFF)
    public bool BGWindowTileDataSelect
    {
      get
      {
        return (_memory[0xFF40] & 0b0001_0000) != 0;
      }
      set
      {
        _memory[0xFF40] = (byte)((_memory[0xFF40] & 0b1110_1111) | (value ? 0b0001_0000 : 0));
      }
    }
    // Bit 3 - BG Tile Map Display Select     (0=9800-9BFF, 1=9C00-9FFF)
    public bool BGTileMapDisplaySelect
    {
      get
      {
        return (_memory[0xFF40] & 0b0000_1000) != 0;
      }
      set
      {
        _memory[0xFF40] = (byte)((_memory[0xFF40] & 0b1111_0111) | (value ? 0b0000_1000 : 0));
      }
    }
    // Bit 2 - OBJ (Sprite) Size              (0=8x8, 1=8x16)
    public bool OBJSize
    {
      get
      {
        return (_memory[0xFF40] & 0b0000_0100) != 0;
      }
      set
      {
        _memory[0xFF40] = (byte)((_memory[0xFF40] & 0b1111_1011) | (value ? 0b0000_0100 : 0));
      }
    }
    // Bit 1 - OBJ (Sprite) Display Enable    (0=Off, 1=On)
    public bool SpriteDisplay
    {
      get
      {
        return (_memory[0xFF40] & 0b0000_0010) != 0;
      }
      set
      {
        _memory[0xFF40] = (byte)((_memory[0xFF40] & 0b1111_1101) | (value ? 0b0000_0010 : 0));
      }
    }
    // Bit 0 - BG Display (for CGB see below) (0=Off, 1=On)
    public bool BGDisplay
    {
      get
      {
        return (_memory[0xFF40] & 0b0000_0001) != 0;
      }
      set
      {
        _memory[0xFF40] = (byte)((_memory[0xFF40] & 0b1111_1110) | (value ? 0b0000_0001 : 0));
      }
    }

    public byte LCDC
    {
      get
      {
        return _memory[0xFF40];
      }
      set
      {
        _memory[0xFF40] = value;
      }
    }

    // STAT
    // The STAT register is used to store the LCDC status.
    // The STAT register is mapped to the memory address 0xFF41.
    // LCDC Status
    // Bit 6 - LYC=LY Coincidence Interrupt (1=Enable) (Read/Write)
    // Bit 5 - Mode 2 OAM Interrupt         (1=Enable) (Read/Write)
    // Bit 4 - Mode 1 V-Blank Interrupt     (1=Enable) (Read/Write)
    // Bit 3 - Mode 0 H-Blank Interrupt     (1=Enable) (Read/Write)
    // Bit 2 - Coincidence Flag  (0:LYC<>LY, 1:LYC=LY) (Read Only)
    // Bit 1-0 - Mode Flag       (Mode 0-3, see below) (Read Only)
    //           0: During H-Blank
    //           1: During V-Blank
    //           2: During Searching OAM
    //           3: During Transfering Data to LCD Driver
    public byte STAT
    {
      get
      {
        return _memory[0xFF41];
      }
      set
      {
        _memory[0xFF41] = value;
      }
    }

    // SCY
    // The SCY register is used to store the scroll Y.
    // The SCY register is mapped to the memory address 0xFF42.
    public byte SCY
    {
      get
      {
        return _memory[0xFF42];
      }
      set
      {
        _memory[0xFF42] = value;
      }
    }

    // SCX
    // The SCX register is used to store the scroll X.
    // The SCX register is mapped to the memory address 0xFF43.
    public byte SCX
    {
      get
      {
        return _memory[0xFF43];
      }
      set
      {
        _memory[0xFF43] = value;
      }
    }

    // LY
    // The LY register is used to store the current line.
    // The LY register is mapped to the memory address 0xFF44.
    public byte LY
    {
      get
      {
        return 0x90;
      }
      set
      {
        _memory[0xFF44] = value;
      }
    }

    // LYC
    // The LYC register is used to store the LY compare.
    // The LYC register is mapped to the memory address 0xFF45.
    public byte LYC
    {
      get
      {
        return _memory[0xFF45];
      }
      set
      {
        _memory[0xFF45] = value;
      }
    }

    // DMA
    // The DMA register is used to store the DMA transfer and start address.
    // The DMA register is mapped to the memory address 0xFF46.
    public byte DMA
    {
      get
      {
        return _memory[0xFF46];
      }
      set
      {
        _memory[0xFF46] = value;
      }
    }

    // BGP
    // The BGP register is used to store the background and window palette data.
    // The BGP register is mapped to the memory address 0xFF47.
    // Bit 7-6 - Shade for Color Number 3
    // Bit 5-4 - Shade for Color Number 2
    // Bit 3-2 - Shade for Color Number 1
    // Bit 1-0 - Shade for Color Number 0
    //           0: White
    //           1: Light gray
    //           2: Dark gray
    //           3: Black
    public byte BGP
    {
      get
      {
        return _memory[0xFF47];
      }
      set
      {
        _memory[0xFF47] = value;
      }
    }

    // OBP0
    // The OBP0 register is used to store the object palette 0 data.
    // The OBP0 register is mapped to the memory address 0xFF48.
    // Bit 7-6 - Shade for Color Number 3
    // Bit 5-4 - Shade for Color Number 2
    // Bit 3-2 - Shade for Color Number 1
    // Bit 1-0 - Shade for Color Number 0
    //           0: White
    //           1: Light gray
    //           2: Dark gray
    //           3: Black
    public byte OBP0
    {
      get
      {
        return _memory[0xFF48];
      }
      set
      {
        _memory[0xFF48] = value;
      }
    }

    // OBP1
    // The OBP1 register is used to store the object palette 1 data.
    // The OBP1 register is mapped to the memory address 0xFF49.
    // Bit 7-6 - Shade for Color Number 3
    // Bit 5-4 - Shade for Color Number 2
    // Bit 3-2 - Shade for Color Number 1
    // Bit 1-0 - Shade for Color Number 0
    //           0: White
    //           1: Light gray
    //           2: Dark gray
    //           3: Black
    public byte OBP1
    {
      get
      {
        return _memory[0xFF49];
      }
      set
      {
        _memory[0xFF49] = value;
      }
    }

    // WY
    // The WY register is used to store the window Y position.
    // The WY register is mapped to the memory address 0xFF4A.
    public byte WY
    {
      get
      {
        return _memory[0xFF4A];
      }
      set
      {
        _memory[0xFF4A] = value;
      }
    }

    // WX
    // The WX register is used to store the window X position.
    // The WX register is mapped to the memory address 0xFF4B.
    public byte WX
    {
      get
      {
        return _memory[0xFF4B];
      }
      set
      {
        _memory[0xFF4B] = value;
      }
    }

    // KEY1
    // The KEY1 register is used to store the CGB mode.
    // The KEY1 register is mapped to the memory address 0xFF4D.
    public byte KEY1
    {
      get
      {
        return _memory[0xFF4D];
      }
      set
      {
        _memory[0xFF4D] = value;
      }
    }

    // VBK
    // The VBK register is used to store the VRAM bank.
    // The VBK register is mapped to the memory address 0xFF4F.
    public byte VBK
    {
      get
      {
        return _memory[0xFF4F];
      }
      set
      {
        _memory[0xFF4F] = value;
      }
    }

    // HDMA1
    // The HDMA1 register is used to store the HDMA source, high.
    // The HDMA1 register is mapped to the memory address 0xFF51.
    public byte HDMA1
    {
      get
      {
        return _memory[0xFF51];
      }
      set
      {
        _memory[0xFF51] = value;
      }
    }

    // HDMA2
    // The HDMA2 register is used to store the HDMA source, low.
    // The HDMA2 register is mapped to the memory address 0xFF52.
    public byte HDMA2
    {
      get
      {
        return _memory[0xFF52];
      }
      set
      {
        _memory[0xFF52] = value;
      }
    }

    // HDMA3
    // The HDMA3 register is used to store the HDMA destination, high.
    // The HDMA3 register is mapped to the memory address 0xFF53.
    public byte HDMA3
    {
      get
      {
        return _memory[0xFF53];
      }
      set
      {
        _memory[0xFF53] = value;
      }
    }

    // HDMA4
    // The HDMA4 register is used to store the HDMA destination, low.
    // The HDMA4 register is mapped to the memory address 0xFF54.
    public byte HDMA4
    {
      get
      {
        return _memory[0xFF54];
      }
      set
      {
        _memory[0xFF54] = value;
      }
    }

    // HDMA5
    // The HDMA5 register is used to store the HDMA length/mode/start.
    // The HDMA5 register is mapped to the memory address 0xFF55.
    public byte HDMA5
    {
      get
      {
        return _memory[0xFF55];
      }
      set
      {
        _memory[0xFF55] = value;
      }
    }

    // RP
    // The RP register is used to store the infrared communications port.
    // The RP register is mapped to the memory address 0xFF56.
    public byte RP
    {
      get
      {
        return _memory[0xFF56];
      }
      set
      {
        _memory[0xFF56] = value;
      }
    }

    // BCPS
    // The BCPS register is used to store the background palette specification.
    // The BCPS register is mapped to the memory address 0xFF68.
    public byte BCPS
    {
      get
      {
        return _memory[0xFF68];
      }
      set
      {
        _memory[0xFF68] = value;
      }
    }

    // BCPD
    // The BCPD register is used to store the background palette data.
    // The BCPD register is mapped to the memory address 0xFF69.
    public byte BCPD
    {
      get
      {
        return _memory[0xFF69];
      }
      set
      {
        _memory[0xFF69] = value;
      }
    }

    // OCPS
    // The OCPS register is used to store the object palette specification.
    // The OCPS register is mapped to the memory address 0xFF6A.
    public byte OCPS
    {
      get
      {
        return _memory[0xFF6A];
      }
      set
      {
        _memory[0xFF6A] = value;
      }
    }

    // OCPD
    // The OCPD register is used to store the object palette data.
    // The OCPD register is mapped to the memory address 0xFF6B.
    public byte OCPD
    {
      get
      {
        return _memory[0xFF6B];
      }
      set
      {
        _memory[0xFF6B] = value;
      }
    }

    // SVBK
    // The SVBK register is used to store the WRAM bank.
    // The SVBK register is mapped to the memory address 0xFF70.
    public byte SVBK
    {
      get
      {
        return _memory[0xFF70];
      }
      set
      {
        _memory[0xFF70] = value;
      }
    }

    // Zero Page
    // The zero page is used to store the stack.
    // The zero page is mapped to the memory address 0xFF80 - 0xFFFE.
    public byte[] ZeroPage
    {
      get
      {
        byte[] zeroPage = new byte[0x7F];
        Array.Copy(_memory, 0xFF80, zeroPage, 0, 0x7F);
        return zeroPage;
      }
      set
      {
        Array.Copy(value, 0, _memory, 0xFF80, 0x7F);
      }
    }

    // Interrupt enable register
    // The last byte of the RAM is always mapped to the memory address 0xFFFF.
    // The interrupt enable register is used to enable and disable the interrupts.
    public byte InterruptEnableRegister
    {
      get
      {
        return _memory[0xFFFF];
      }
      set
      {
        _memory[0xFFFF] = value;
      }
    }

    public bool IEVBlank
    {
      get
      {
        return (_memory[0xFFFF] & 0b0000_0001) != 0;
      }
      set
      {
        _memory[0xFFFF] = (byte)((_memory[0xFFFF] & 0b1111_1110) | (value ? 0b0000_0001 : 0));
      }
    }

    public bool IELCDStat
    {
      get
      {
        return (_memory[0xFFFF] & 0b0000_0010) != 0;
      }
      set
      {
        _memory[0xFFFF] = (byte)((_memory[0xFFFF] & 0b1111_1101) | (value ? 0b0000_0010 : 0));
      }
    }

    public bool IETimer
    {
      get
      {
        return (_memory[0xFFFF] & 0b0000_0100) != 0;
      }
      set
      {
        _memory[0xFFFF] = (byte)((_memory[0xFFFF] & 0b1111_1011) | (value ? 0b0000_0100 : 0));
      }
    }

    public bool IESerial
    {
      get
      {
        return (_memory[0xFFFF] & 0b0000_1000) != 0;
      }
      set
      {
        _memory[0xFFFF] = (byte)((_memory[0xFFFF] & 0b1111_0111) | (value ? 0b0000_1000 : 0));
      }
    }

    public bool IEJoypad
    {
      get
      {
        return (_memory[0xFFFF] & 0b0001_0000) != 0;
      }
      set
      {
        _memory[0xFFFF] = (byte)((_memory[0xFFFF] & 0b1110_1111) | (value ? 0b0001_0000 : 0));
      }
    }
    
    private byte[] _cartRom;
    private byte _currentROMBank = 1;
    private byte[] RamBanks;
    private bool _romBankingMode;
    private byte _currentRamBank = 0;
    private bool _ramEnabled;
    private bool _mbc1;
    private bool _mbc2;

		public GBMemory(Gameboy gameBoy)
    {
      _gameBoy = gameBoy;
			_cartRom = new byte[0x8000];
			RamBanks = new byte[0x8000];
      
			//InitialiseBootROM();
			InitialiseIORegisters();
      _memory[0xFF44] = 0x90;
    }

    private void InitialiseBootROM()
    {
      // The boot ROM is used to initialise the GameBoy.
      // The boot ROM is mapped to the memory address 0x0000 - 0x00FF.
      // The boot ROM is disabled after the GameBoy has been initialised.
      var rom = File.ReadAllBytes("DMG_ROM.bin");
      Array.Copy(rom, 0, _memory, 0, 0x100);
    }

    public byte ReadByte(ushort address)
    {
      ushort newAddress;
      //if ((address >= 0x4000) && (address <= 0x7FFF))
      //{
      //  newAddress = (ushort)(address - 0x4000);
      //  return _cartRom[newAddress + (_currentROMBank * 0x4000)];
      //}
      //else if ((address >= 0xA000) && (address <= 0xBFFF))
      //{
      //  newAddress = (ushort)(address - 0xA000);
      //  return RamBanks[newAddress + (_currentRamBank * 0x2000)];
      //}
      //else
      //
      if (address == 0xFF44)
      {
        return 0x90;
      }
			else
      {
        return _memory[address];
      }
    }

    public sbyte ReadSByte(ushort address)
    {
      return (sbyte)_memory[address];
    }

    public void WriteByte(ushort address, byte value)
		{
			if (address < 0x8000)
      {
        HandleBanking(address, value);
      }
      // 0x8000 - 0x97FF
      else if ((address >= 0x8000) && (address < 0x9800))
      {
        _memory[address] = value;
        //_gameBoy.UpdateTile(address, value);
      }
      else if (address >= 0xA000 && address <= 0xBFFF)
      {
        if (_ramEnabled)
        {
          var newAddress = (ushort)(address - 0xA000);
          RamBanks[newAddress + (_currentRamBank * 0x2000)] = value;
        }
      }
      else if ((address >= 0xE000) && (address < 0xFE00))
      {
        _memory[address] = value;
        _memory[address - 0x2000] = value;
      }
      else if (address == 0xFF00)
      {
        _memory[address] = (byte)((_memory[address] & 0b1111_0000) | (value & 0b0000_1111));
      }
      else if (address == 0xFF02 && value == 0x81)
      {
        Console.Write((char)_memory[0xFF01]);
				SerialDataReceived?.Invoke(this, (char)_memory[0xFF01]);
			}
      else if (address == 0xFF04)
      {
        _gameBoy.DIVCounter = 0;
        _memory[address] = 0;
      }
      else if (address == TIMA)
      {
        var currentFrequency = _gameBoy.GetClockFrequency();
        _memory[address] = value;
        var newFrequency = _gameBoy.GetClockFrequency();

        if (currentFrequency != newFrequency)
        {
          _gameBoy.SetClockFrequency();
        }
      }
      else if (address == DIV)
      {
        _memory[address] = 0;
      }
      else if (address == TAC)
      {
        _memory[address] = value;
        _gameBoy.SetClockFrequency();
      }
      else if (address == 0xFF44)
      {
        _memory[address] = 0;
      }
      else if (address == 0xFF46)
      {
        var sourceAddress = (ushort)(value << 8);
        for (int i = 0; i < 0xA0; i++)
        {
          _memory[0xFE00 + i] = _memory[sourceAddress + i];
        }
      }
      else
      {
        _memory[address] = value;
      }

    }

    private void HandleBanking(ushort address, byte value)
    {
      switch (address)
      {
        case < 0x2000:
          if (_mbc1 || _mbc2)
          {
            RamBankEnable(address, value);
          }
          break;
        case < 0x4000:
          if (_mbc1 || _mbc2)
          {
            LowRomBankSelect(address, value);
          }
          break;
        case < 0x6000:
          if (_mbc1)
          {
            if (_romBankingMode)
            {
              HighRomBankSelect(address, value);
            }
            else
            {
              RamBankSelect(address, value);
            }
          }
          break;
        case < 0x8000:
          if (_mbc1)
          {
            RomRamModeSelect(address, value);
          }
          break;
      }
    }

    private void RomRamModeSelect(ushort address, byte value)
    {
      var newData = (byte)(value & 0x01);
      if (_mbc1)
      {
        // if bit 4 of address is set, ignore
        if ((address & 0x10) == 0x10)
        {
          return;
        }
      }
      if (newData == 0x00)
      {
        // ROM banking mode
        _romBankingMode = true;
        _currentRamBank = 0;
      }
      else
      {
        // RAM banking mode
        _currentRamBank = _currentROMBank;
      }
    }

    private void RamBankSelect(ushort address, byte value)
    {
      if (_mbc1)
      {
        _currentRamBank = (byte)(value & 0x03);
      }
    }

    private void HighRomBankSelect(ushort address, byte value)
    {
      _currentROMBank = (byte)((_currentROMBank & 0x1F) | ((value & 0x03) << 5));
      if (_currentROMBank == 0)
      {
        _currentROMBank = 1;
      }
    }

    private void LowRomBankSelect(ushort address, byte value)
    {
      if (_mbc2)
      {
        _currentROMBank = (byte)(value & 0x0F);
        if (_currentROMBank == 0)
        {
          _currentROMBank = 1;
        }
      }
      else
      {
        _currentROMBank = (byte)(value & 0x1F);
        if (_currentROMBank == 0)
        {
          _currentROMBank = 1;
        }
      }
    }

    private void RamBankEnable(ushort address, byte value)
    {
      if (_mbc2)
      {
        // if bit 4 of address is set, ignore
        if ((address & 0x10) == 0x10)
        {
          return;
        }
      }
      if ((value & 0x0F) == 0x0A)
      {
        _ramEnabled = true;
      }
      else
      {
        _ramEnabled = false;
      }
    }

    public ushort ReadUShort(ushort address)
    {
      return (ushort)((_memory[address + 1] << 8) | _memory[address]);
    }

    public void WriteUShort(ushort address, ushort value)
    {
			_memory[address] = (byte)(value & 0xFF);
      _memory[address + 1] = (byte)((value & 0xFF00) >> 8);
    }

    private void InitialiseIORegisters()
    {
      // The I/O registers are used to control the GameBoy.
      _memory[0xFF00] = 0xCF; // JOYP
      _memory[0xFF01] = 0x00; // SB
      _memory[0xFF02] = 0x7E; // SC
      _memory[0xFF04] = 0x00; // DIV

      _memory[0xFF08] = 0xF8; // TAC
      _memory[0xFF0F] = 0xE1; // IF

      // Sound 1
      _memory[0xFF10] = 0x80; // ENT1
      _memory[0xFF11] = 0xBF; // LEN1
      _memory[0xFF12] = 0xF3; // ENV1
      _memory[0xFF13] = 0xC1; // FREQ1
      _memory[0xFF14] = 0xBF; // KIK1

      _memory[0xFF15] = 0xFF; // N/A
      _memory[0xFF16] = 0x3F; // LEN2
      _memory[0xFF19] = 0xBF; // KIK2

      _memory[0xFF1A] = 0x7F;
      _memory[0xFF1B] = 0xFF;
      _memory[0xFF1C] = 0x9F;
      _memory[0xFF1E] = 0xBF;
      _memory[0xFF20] = 0xFF;

      _memory[0xFF23] = 0xBF;
      _memory[0xFF24] = 0x77;
      _memory[0xFF25] = 0xF3;
      _memory[0xFF26] = 0xF1;

      // graphics
      _memory[0xFF40] = 0x91; // LCDC
      _memory[0xFF41] = 0x85; // STAT
      _memory[0xFF46] = 0xFF; // DMA
      _memory[0xFF47] = 0xFC; // BGP
      _memory[0xFF48] = 0xFF; // OBP0
      _memory[0xFF49] = 0xFF; // OBP1
      _memory[0xFF4D] = 0xFF; // KEY1
      _memory[0xFF4F] = 0xFF; // VBK
      _memory[0xFF70] = 0xFF; // SVBK

      _memory[0xFFFF] = 0x00;
    }

    public void InitialiseGame(byte[] rom)
    {
      _cartRom = rom;
      // Load the ROM into the memory at the right location
      switch (rom[0x147])
      {
        case 0:
        // ROM only
        case 1:
        // MBC1
        case 2:
        // MBC1+RAM
        case 3:
          // MBC1+RAM+BATTERY
          _mbc1 = true;
          Array.Copy(rom, 0x0000, _memory, 0x0000, 0x8000);
          break;
        case 5:
        // MBC2
        case 6:
          // MBC2+RAM
          _mbc2 = true;
          Array.Copy(rom, 0x0000, _memory, 0x0000, 0x4000);
          Array.Copy(rom, 0x4000, _memory, 0x4000, 0x4000);
          break;
      }
    }

    public byte[] GetTileData()
    {
      // 8000-8FFF
      var tileData = new byte[0x1800];
      Array.Copy(_memory, 0x8000, tileData, 0, 0x1800);
      return tileData;
    }

    public byte[] GetTileMap()
    {
      // 9800-9BFF
      var tileMap = new byte[0x400];
      Array.Copy(_memory, 0x9800, tileMap, 0, 0x400);
      return tileMap;
    }
  }
}
