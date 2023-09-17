using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBOG.CPU.Opcodes
{
  public delegate bool Step(Gameboy gb);
  public class GBOpcode
  {
    public byte value { get; set; }    // for instance 0xC3
    public string label { get; set; } // JP {0:x4}
    public int length { get; set; } // in bytes
    public int tcycles { get; set; }  // clock cycles
    public int mcycles { get; set; }  // machine cycles
    public Step[]? steps { get; set; } // function array

    public GBOpcode(byte value, string label, int length, int tcycles, Step[]? steps)
    {
      this.value = value;
      this.label = label;
      this.length = length;
      this.tcycles = tcycles;
      this.mcycles = tcycles / 4;
      this.steps = steps;
    }

    // override tostring
    public override string ToString()
    {
      return $"{label} {value:x2}";
    }
  }
}
