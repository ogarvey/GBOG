using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBOG.Memory
{
  public class Cartridge
  {
    private byte[] _rom;
    private int _totalSize;
    private string _name;
    private int _romSize;
    private int _ramSize;
    private CartridgeType _type;
    private bool _validROM;
    // private bool m_bSGB;
    private int _version;
    private bool _loaded;
    private long _rtcCurrentTime;
    private bool _battery;
    private string _filePath;
    private string _fileName;
    private bool _rtcPresent;
    private bool _rumblePresent;
    private int _romBankCount;
    private int _ramBankCount;
    private List<GameGenieCode> _gameGenieList;

    public Cartridge()
    {
      _rom = new byte[0x200000];
      _totalSize = 0;
      _name = "";
      _romSize = 0;
      _ramSize = 0;
      _type = CartridgeType.CartridgeUnsupported;
      _validROM = false;
      // m_bSGB = false;
      _version = 0;
      _loaded = false;
      _rtcCurrentTime = 0;
      _battery = false;
      _filePath = "";
      _fileName = "";
      _rtcPresent = false;
      _rumblePresent = false;
      _romBankCount = 0;
      _ramBankCount = 0;
      _gameGenieList = new List<GameGenieCode>();
    }

    public void Init()
    {
      Reset();
    }

    void Reset()
    {
      Array.Clear(_rom, 0, _rom.Length);
      _totalSize = 0;
      _name = "";
      _romSize = 0;
      _ramSize = 0;
      _type = CartridgeType.CartridgeUnsupported;
      _validROM = false;
      // m_bSGB = false;
      _version = 0;
      _loaded = false;
      _rtcCurrentTime = 0;
      _battery = false;
      _filePath = "";
      _fileName = "";
      _rtcPresent = false;
      _rumblePresent = false;
      _romBankCount = 0;
      _ramBankCount = 0;
      _gameGenieList = new List<GameGenieCode>();
    }
    public bool IsValidROM() { return _validROM; }
    public bool IsLoadedROM() { return _loaded; }
    public CartridgeType GetType() { return _type; }
    public int GetRAMSize() { return _ramSize; }
    public int GetROMSize() { return _romSize; }
    public int GetROMBankCount() { return _romBankCount; }
    public int GetRAMBankCount() { return _ramBankCount; }
    public string GetName() { return _name; }
    public string GetFilePath() { return _filePath; }
    public string GetFileName() { return _fileName; }
    public int GetTotalSize() { return _totalSize; }
    public bool HasBattery() { return _battery; }
    public byte[] GetTheROM() { return _rom; }

    public bool LoadFromFile(string path)
    {
      Reset();
      _filePath = path;
      _fileName = Path.GetFileName(path);
      var buffer = File.ReadAllBytes(path);
      var extension = Path.GetExtension(path);

      if (extension == "zip")
      {

      }
      else
      {
        _loaded = LoadFromBuffer(buffer);
      }

      if (!_loaded) Reset();

      return _loaded;
    }

    bool LoadFromBuffer(byte[] buffer)
    {
      if (buffer != null)
      {
        _totalSize = buffer.Length;
        _rom = buffer;
        _loaded = true;
        return GatherMetadata();
      }
      return false;
    }

    bool GatherMetadata()
    {
      char[] name = new char[12];
      name[11] = (char)0;

      for (int i = 0; i < 11; i++)
      {
        name[i] = (char)_rom[0x0134 + i];
        if (name[i] == 0)
        {
          break;
        }
      }

      int type = _rom[0x147];
      _romSize = _rom[0x148];
      _ramSize = _rom[0x149];
      _version = _rom[0x14C];

      CheckCartridgeType(type);
      switch (_ramSize)
      {
        case 0x00:
          _ramBankCount = (_type == CartridgeType.CartridgeMBC2) ? 1 : 0;
          break;
        case 0x01:
        case 0x02:
          _ramBankCount = 1;
          break;
        case 0x04:
          _ramBankCount = 16;
          break;
        default:
          _ramBankCount = 4;
          break;
      }

      _romBankCount = (int)Math.Max(Pow2Ceil((uint)_totalSize / 0x4000), 2u);

      bool presumeMultiMBC1 = type == 1 && _ramSize == 0 && _romBankCount == 64;

      if (_type == CartridgeType.CartridgeMBC1 && presumeMultiMBC1)
      {
        _type = CartridgeType.CartridgeMBC1Multi;
      }

      int checksum = 0;

      for (int j = 0x134; j < 0x14E; j++)
      {
        checksum += _rom[j];
      }

      _validROM = ((checksum + 25) & 0xFF) == 0;

      return _type != CartridgeType.CartridgeUnsupported;
    }

    private void CheckCartridgeType(int type)
    {
      if ((type != 0xEA) && (GetROMSize() == 0))
        type = 0;

      switch (type)
      {
        case 0x00:
        // NO MBC
        case 0x08:
        // ROM
        // SRAM
        case 0x09:
          // ROM
          // SRAM
          // BATT
          _type = CartridgeType.CartridgeNoMBC;
          break;
        case 0x01:
        // MBC1
        case 0x02:
        // MBC1
        // SRAM
        case 0x03:
        // MBC1
        // SRAM
        // BATT
        case 0xEA:
        // Hack to accept 0xEA as a MBC1 (Sonic 3D Blast 5)
        case 0xFF:
          // Hack to accept HuC1 as a MBC1
          _type = CartridgeType.CartridgeMBC1;
          break;
        case 0x05:
        // MBC2
        // SRAM
        case 0x06:
          // MBC2
          // SRAM
          // BATT
          _type = CartridgeType.CartridgeMBC2;
          break;
        case 0x0F:
        // MBC3
        // TIMER
        // BATT
        case 0x10:
        // MBC3
        // TIMER
        // BATT
        // SRAM
        case 0x11:
        // MBC3
        case 0x12:
        // MBC3
        // SRAM
        case 0x13:
        // MBC3
        // BATT
        // SRAM
        case 0xFC:
          // Game Boy Camera
          _type = CartridgeType.CartridgeMBC3;
          break;
        case 0x19:
        // MBC5
        case 0x1A:
        // MBC5
        // SRAM
        case 0x1B:
        // MBC5
        // BATT
        // SRAM
        case 0x1C:
        // RUMBLE
        case 0x1D:
        // RUMBLE
        // SRAM
        case 0x1E:
          // RUMBLE
          // BATT
          // SRAM
          _type = CartridgeType.CartridgeMBC5;
          break;
        case 0x0B:
        // MMMO1
        case 0x0C:
        // MMM01
        // SRAM
        case 0x0D:
        // MMM01
        // SRAM
        // BATT
        case 0x15:
        // MBC4
        case 0x16:
        // MBC4
        // SRAM
        case 0x17:
        // MBC4
        // SRAM
        // BATT
        case 0x22:
        // MBC7
        // BATT
        // SRAM
        case 0x55:
        // GG
        case 0x56:
        // GS3
        case 0xFD:
        // TAMA 5
        case 0xFE:
          // HuC3
          _type = CartridgeType.CartridgeUnsupported;
          Log.Warning("--> ** This cartridge is not supported. Type: %d", type);
          break;
        default:
          _type = CartridgeType.CartridgeUnsupported;
          Log.Warning("--> ** Unknown cartridge type: %d", type);
          break;
      }

      switch (type)
      {
        case 0x03:
        case 0x06:
        case 0x09:
        case 0x0D:
        case 0x0F:
        case 0x10:
        case 0x13:
        case 0x17:
        case 0x1B:
        case 0x1E:
        case 0x22:
        case 0xFD:
        case 0xFF:
          _battery = true;
          break;
        default:
          _battery = false;
          break;
      }

      switch (type)
      {
        case 0x0F:
        case 0x10:
          _rtcPresent = true;
          break;
        default:
          _rtcPresent = false;
          break;
      }

      switch (type)
      {
        case 0x1C:
        case 0x1D:
        case 0x1E:
          _rumblePresent = true;
          break;
        default:
          _rumblePresent = false;
          break;
      }
    }

    public int GetVersion() { return _version; }
    //bool IsSGB() { return false; }
    public void UpdateCurrentRTC() {
      _rtcCurrentTime = DateTime.Now.Ticks;
    }
    public long GetCurrentRTC() { return _rtcCurrentTime; }
    public bool IsRTCPresent() { return _rtcPresent; }
    public bool IsRumblePresent() { return _rumblePresent; }
    public void SetGameGenieCheat(string cheat) { }
    public void ClearGameGenieCheats() { }

    uint Pow2Ceil(uint n)
    {
      --n;
      n |= n >> 1;
      n |= n >> 2;
      n |= n >> 4;
      n |= n >> 8;
      ++n;
      return n;
    }
  }

  public enum CartridgeType
  {
    CartridgeNoMBC,
    CartridgeMBC1,
    CartridgeMBC2,
    CartridgeMBC3,
    CartridgeMBC5,
    CartridgeMBC1Multi,
    CartridgeUnsupported
  }

  public struct GameGenieCode
  {
    int address { get; set; }
    byte old_value { get; set; }
  }
}
