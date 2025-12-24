using GBOG.Utils;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GBOG.CPU.Opcodes
{
  public class OpcodeHandler
  {
    private Dictionary<byte, GBOpcode> _opcodes;
    private Dictionary<byte, GBOpcode> _cbOpcodes;
    private Gameboy _gb;

    public OpcodeHandler(Gameboy gb)
    {
      _gb = gb;
      _opcodes = new Dictionary<byte, GBOpcode>();
      _opcodes = _opcodes.AddMultiple(ALUOpcodes.X8opcodes);
      _opcodes = _opcodes.AddMultiple(ALUOpcodes.X16opcodes);
      _opcodes = _opcodes.AddMultiple(LSMOpcodes.X8opcodes);
      _opcodes = _opcodes.AddMultiple(LSMOpcodes.X16opcodes);
      _opcodes = _opcodes.AddMultiple(ControlOpcodes.MiscOpcodes);
      _opcodes = _opcodes.AddMultiple(ControlOpcodes.BROpcodes);
      _opcodes = _opcodes.AddMultiple(RSBOpcodes.X8opcodes);
      _cbOpcodes = RSBOpcodes.CBOpcodes;
    }

    public GBOpcode? GetOpcode(byte opcode, bool cb = false)
    {
      byte cbOp = 0;
      if (cb) {
        cbOp = _gb._memory.ReadByte(_gb.PC++);
			}
      return cb ? _cbOpcodes.GetValueOrDefault(cbOp) : _opcodes.GetValueOrDefault(opcode);
    }

    internal void WarmupJit()
    {
      // The emulator is a long-running loop; .NET JITting opcode lambdas on-demand can
      // cause noticeable single-frame hitches (especially at the start of a game).
      // Pre-JIT all opcode step methods + common opcode helper methods at ROM load.

      var seen = new HashSet<IntPtr>();

      void PrepareDelegate(Step step)
      {
        var mh = step.Method.MethodHandle;
        if (mh.Value == IntPtr.Zero)
        {
          return;
        }
        if (!seen.Add(mh.Value))
        {
          return;
        }
        RuntimeHelpers.PrepareMethod(mh);
      }

      void PrepareOpcodeDict(Dictionary<byte, GBOpcode> dict)
      {
        foreach (var op in dict.Values)
        {
          var steps = op.steps;
          if (steps == null)
          {
            continue;
          }
          for (int i = 0; i < steps.Length; i++)
          {
            PrepareDelegate(steps[i]);
          }
        }
      }

      void PrepareType(Type t)
      {
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
          if (m.IsAbstract || m.ContainsGenericParameters)
          {
            continue;
          }
          if (m.MethodHandle.Value == IntPtr.Zero)
          {
            continue;
          }
          if (!seen.Add(m.MethodHandle.Value))
          {
            continue;
          }
          RuntimeHelpers.PrepareMethod(m.MethodHandle);
        }
      }

      PrepareOpcodeDict(_opcodes);
      PrepareOpcodeDict(_cbOpcodes);

      // Also prepare common helper methods used by those lambdas.
      PrepareType(typeof(ALUOpcodes));
      PrepareType(typeof(LSMOpcodes));
      PrepareType(typeof(ControlOpcodes));
      PrepareType(typeof(RSBOpcodes));
    }
  }
}
