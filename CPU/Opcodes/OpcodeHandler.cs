using GBOG.Utils;

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
  }
}
