using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBOG.Memory
{
  internal interface IMBCHandler
  {
    byte ReadByte(ushort address);
    void WriteByte(ushort address, byte value);
    void Reset();
    void SaveRam(string path);
    bool LoadRam(string path);
    int GetRamSize();
    byte[] GetRamBanks();
    int GetCurrentRamBankIndex();
    void SaveState(string path);
    void LoadState(string path);
  }
}
