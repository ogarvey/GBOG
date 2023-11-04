using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBOG.Memory.MBC
{
  public class MBC1Cartridge : Cartridge, IMBCHandler
  {
    private Memory _memory;
    private int _mode;
    private int _currentRamBank;
    private int _currentRomBank;
    private int _currentROMAddress;
    private int _currentRAMAddress;
    private bool _ramEnabled;

    private byte _higherRomBankBits;
    private byte[] _ramBanks;

    private const int _MBC1_RAM_BANK_SIZE = 0x8000;

    public MBC1Cartridge()
    {
      _ramBanks = new byte[_MBC1_RAM_BANK_SIZE];
      Reset();
    }

    public void Reset()
    {
      _mode = 0;
      _currentRomBank = 1;
      _currentRamBank = 0;
      _higherRomBankBits = 0;
      _ramEnabled = false;
      for (int i = 0; i < _MBC1_RAM_BANK_SIZE; i++)
        _ramBanks[i] = 0xFF;
      _currentROMAddress = 0x4000;
      _currentRAMAddress = 0;

    }

    public int GetCurrentRamBankIndex()
    {
      return _currentRamBank;
    }

    public int GetCurrentRomBank0Index()
    {
      return 0;
    }

    public int GetCurrentRomBank1Index()
    {
      return _currentRomBank;
    }

    public byte[] GetRamBanks()
    {
      return _ramBanks;
    }

    public int GetRamSize()
    {
      return GetRAMBankCount() * 0x2000;
    }
    
    public byte ReadByte(ushort address)
    {
      switch (address & 0xE000)
      {
        case 0x4000:
        case 0x6000:
          {
            var pROM = GetTheROM();
            return pROM[(address - 0x4000) + _currentROMAddress];
          }
        case 0xA000:
          {
            if (_ramEnabled)
            {
              if (_mode == 0)
              {
                if ((GetRAMSize() == 1) && (address >= 0xA800))
                {
                  // only 2KB of ram
                  Log.Warning("--> ** Attempting to read from invalid RAM %X", address);
                }
                return _ramBanks[address - 0xA000];
              }
              else
                return _ramBanks[(address - 0xA000) + _currentRAMAddress];
            }
            else
            {
              Log.Warning("--> ** Attempting to read from disabled ram %X", address);
              return 0xFF;
            }
          }
        default:
          {
            return _memory.Retrieve(address);
          }
      }
    }

    public void WriteByte(ushort address, byte value)
    {
      switch (address & 0xE000)
      {
        case 0x0000:
          {
            if (GetRAMSize() > 0)
            {
              bool previous = _ramEnabled;
              _ramEnabled = ((value & 0x0F) == 0x0A);

              //if (IsValidPointer(m_pRamChangedCallback) && previous && !_ramEnabled)
              //{
              //  (*m_pRamChangedCallback)();
              //}
            }
            break;
          }
        case 0x2000:
          {
            if (_mode == 0)
            {
              _currentRomBank = (value & 0x1F) | (_higherRomBankBits << 5);
            }
            else
            {
              _currentRomBank = value & 0x1F;
            }

            if (_currentRomBank == 0x00 || _currentRomBank == 0x20
                    || _currentRomBank == 0x40 || _currentRomBank == 0x60)
              _currentRomBank++;

            _currentRomBank &= (GetROMBankCount() - 1);
            _currentROMAddress = _currentRomBank * 0x4000;
            break;
          }
        case 0x4000:
          {
            if (_mode == 1)
            {
              _currentRamBank = value & 0x03;
              _currentRamBank &= (GetRAMBankCount() - 1);
              _currentRAMAddress = _currentRamBank * 0x2000;
            }
            else
            {
              _higherRomBankBits = (byte)(value & 0x03);
              _currentRomBank = (_currentRomBank & 0x1F) | (_higherRomBankBits << 5);

              if (_currentRomBank == 0x00 || _currentRomBank == 0x20
                      || _currentRomBank == 0x40 || _currentRomBank == 0x60)
                _currentRomBank++;

              _currentRomBank &= (GetROMBankCount() - 1);
              _currentROMAddress = _currentRomBank * 0x4000;
            }
            break;
          }
        case 0x6000:
          {
            if ((GetRAMSize() != 3) && (value & 0x01) > 0)
            {
              Log.Warning("--> ** Attempting to change MBC1 to mode 1 with incorrect RAM banks %X %X", address, value);
            }
            else
            {
              _mode = value & 0x01;
            }
            break;
          }
        case 0xA000:
          {
            if (_ramEnabled)
            {
              if (_mode == 0)
              {
                if ((GetRAMSize() == 1) && (address >= 0xA800))
                {
                  // only 2KB of ram
                  Log.Warning("--> ** Attempting to write on invalid RAM %X %X", address, value);
                }

                _ramBanks[address - 0xA000] = value;
              }
              else
                _ramBanks[(address - 0xA000) + _currentRAMAddress] = value;
            }
            else
            {
              Log.Warning("--> ** Attempting to write on RAM when ram is disabled %X %X", address, value);
            }
            break;
          }
        default:
          {
            _memory.Load(address, value);
            break;
          }
      }
    }

    public void SaveRam(string path)
    {
      throw new NotImplementedException();
    }

    public void SaveState(string path)
    {
      throw new NotImplementedException();
    }
    
    public bool LoadRam(string path)
    {
      throw new NotImplementedException();
    }

    public void LoadState(string path)
    {
      throw new NotImplementedException();
    }
  }
}
