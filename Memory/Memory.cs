using GBOG.CPU;
using GBOG.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBOG.Memory
{
  internal class Memory
  {
    private Gameboy _cpu;
    private PPU _ppu;
    //CommonMemoryRule* m_pCommonMemoryRule;
    //IORegistersMemoryRule* m_pIORegistersMemoryRule;
    IMBCHandler _currentMBCHandler; // _m_pCurrentMemoryRule
    private byte[] _map;

    public Memory(Gameboy cpu, PPU ppu)
    {
      _cpu = cpu;
      _ppu = ppu;
    }
    
    public byte Retrieve(ushort address)
    {
      return _map[address];
    }

    public void Load(ushort address, byte value)
    {
      throw new NotImplementedException();
    }
  }
}
