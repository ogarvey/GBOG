namespace GBOG.CPU.Opcodes
{
  public class ALUOpcodes
  {
    private static int initial;
		private static int offset;
    
		public static Dictionary<byte, GBOpcode> X8opcodes = new Dictionary<byte, GBOpcode>()
    {
      {0x04, new GBOpcode(0x04, "INC B",1,4,null)},
      {0x05, new GBOpcode(0x05, "DEC B",1,4,null)},
      {0x0C, new GBOpcode(0x0C, "INC C",1,4,null)},
      {0x0D, new GBOpcode(0x0D, "DEC C",1,4,null)},
      {0x14, new GBOpcode(0x14, "INC D",1,4,null)},
      {0x15, new GBOpcode(0x15, "DEC D",1,4,null)},
      {0x1C, new GBOpcode(0x1C, "INC E",1,4,null)},
      {0x1D, new GBOpcode(0x1D, "DEC E",1,4,null)},
      {0x24, new GBOpcode(0x24, "INC H",1,4,null)},
      {0x25, new GBOpcode(0x25, "DEC H",1,4,null)},
      {0x27, new GBOpcode(0x27, "DAA",1,4,null)},
      {0x2C, new GBOpcode(0x2C, "INC L",1,4,null)},
      {0x2D, new GBOpcode(0x2D, "DEC L",1,4,null)},
      {0x2F, new GBOpcode(0x2F, "CPL",1,4,null)},
      {0x34, new GBOpcode(0x34, "INC (HL)",1,12,null)},
      {0x35, new GBOpcode(0x35, "DEC (HL)",1,12,null)},
      {0x37, new GBOpcode(0x37, "SCF",1,4,null)},
      {0x3C, new GBOpcode(0x3C, "INC A",1,4,null)},
      {0x3D, new GBOpcode(0x3D, "DEC A",1,4,null)},
      {0x3F, new GBOpcode(0x3F, "CCF",1,4,null)},
      {0x80, new GBOpcode(0x80, "ADD A,B",1,4,null)},
      {0x81, new GBOpcode(0x81, "ADD A,C",1,4,null)},
      {0x82, new GBOpcode(0x82, "ADD A,D",1,4,null)},
      {0x83, new GBOpcode(0x83, "ADD A,E",1,4,null)},
      {0x84, new GBOpcode(0x84, "ADD A,H",1,4,null)},
      {0x85, new GBOpcode(0x85, "ADD A,L",1,4,null)},
      {0x86, new GBOpcode(0x86, "ADD A,(HL)",1,8,null)},
      {0x87, new GBOpcode(0x87, "ADD A,A",1,4,null)},
      {0x88, new GBOpcode(0x88, "ADC A,B",1,4,null)},
      {0x89, new GBOpcode(0x89, "ADC A,C",1,4,null)},
      {0x8A, new GBOpcode(0x8A, "ADC A,D",1,4,null)},
      {0x8B, new GBOpcode(0x8B, "ADC A,E",1,4,null)},
      {0x8C, new GBOpcode(0x8C, "ADC A,H",1,4,null)},
      {0x8D, new GBOpcode(0x8D, "ADC A,L",1,4,null)},
      {0x8E, new GBOpcode(0x8E, "ADC A,(HL)",1,8,null)},
      {0x8F, new GBOpcode(0x8F, "ADC A,A",1,4,null)},
      {0x90, new GBOpcode(0x90, "SUB B",1,4,null)},
      {0x91, new GBOpcode(0x91, "SUB C",1,4,null)},
      {0x92, new GBOpcode(0x92, "SUB D",1,4,null)},
      {0x93, new GBOpcode(0x93, "SUB E",1,4,null)},
      {0x94, new GBOpcode(0x94, "SUB H",1,4,null)},
      {0x95, new GBOpcode(0x95, "SUB L",1,4,null)},
      {0x96, new GBOpcode(0x96, "SUB (HL)",1,8,null)},
      {0x97, new GBOpcode(0x97, "SUB A",1,4,null)},
      {0x98, new GBOpcode(0x98, "SBC A,B",1,4,null)},
      {0x99, new GBOpcode(0x99, "SBC A,C",1,4,null)},
      {0x9A, new GBOpcode(0x9A, "SBC A,D",1,4,null)},
      {0x9B, new GBOpcode(0x9B, "SBC A,E",1,4,null)},
      {0x9C, new GBOpcode(0x9C, "SBC A,H",1,4,null)},
      {0x9D, new GBOpcode(0x9D, "SBC A,L",1,4,null)},
      {0x9E, new GBOpcode(0x9E, "SBC A,(HL)",1,8,null)},
      {0x9F, new GBOpcode(0x9F, "SBC A,A",1,4,null)},
      {0xA0, new GBOpcode(0xA0, "AND B",1,4,null)},
      {0xA1, new GBOpcode(0xA1, "AND C",1,4,null)},
      {0xA2, new GBOpcode(0xA2, "AND D",1,4,null)},
      {0xA3, new GBOpcode(0xA3, "AND E",1,4,null)},
      {0xA4, new GBOpcode(0xA4, "AND H",1,4,null)},
      {0xA5, new GBOpcode(0xA5, "AND L",1,4,null)},
      {0xA6, new GBOpcode(0xA6, "AND (HL)",1,8,null)},
      {0xA7, new GBOpcode(0xA7, "AND A",1,4,null)},
      {0xA8, new GBOpcode(0xA8, "XOR B",1,4,null)},
      {0xA9, new GBOpcode(0xA9, "XOR C",1,4,null)},
      {0xAA, new GBOpcode(0xAA, "XOR D",1,4,null)},
      {0xAB, new GBOpcode(0xAB, "XOR E",1,4,null)},
      {0xAC, new GBOpcode(0xAC, "XOR H",1,4,null)},
      {0xAD, new GBOpcode(0xAD, "XOR L",1,4,null)},
      {0xAE, new GBOpcode(0xAE, "XOR (HL)",1,8,null)},
      {0xAF, new GBOpcode(0xAF, "XOR A",1,4,null)},
      {0xB0, new GBOpcode(0xB0, "OR B",1,4,null)},
      {0xB1, new GBOpcode(0xB1, "OR C",1,4,null)},
      {0xB2, new GBOpcode(0xB2, "OR D",1,4,null)},
      {0xB3, new GBOpcode(0xB3, "OR E",1,4,null)},
      {0xB4, new GBOpcode(0xB4, "OR H",1,4,null)},
      {0xB5, new GBOpcode(0xB5, "OR L",1,4,null)},
      {0xB6, new GBOpcode(0xB6, "OR (HL)",1,8,null)},
      {0xB7, new GBOpcode(0xB7, "OR A",1,4,null)},
      {0xB8, new GBOpcode(0xB8, "CP B",1,4,null)},
      {0xB9, new GBOpcode(0xB9, "CP C",1,4,null)},
      {0xBA, new GBOpcode(0xBA, "CP D",1,4,null)},
      {0xBB, new GBOpcode(0xBB, "CP E",1,4,null)},
      {0xBC, new GBOpcode(0xBC, "CP H",1,4,null)},
      {0xBD, new GBOpcode(0xBD, "CP L",1,4,null)},
      {0xBE, new GBOpcode(0xBE, "CP (HL)",1,8,null)},
      {0xBF, new GBOpcode(0xBF, "CP A",1,4,null)},
      {0xC6, new GBOpcode(0xC6, "ADD A,{0:x2}",2,8,null)},
      {0xCE, new GBOpcode(0xCE, "ADC A,{0:x2}",2,8,null)},
      {0xD6, new GBOpcode(0xD6, "SUB {0:x2}",2,8,null)},
      {0xDE, new GBOpcode(0xDE, "SBC A,{0:x2}",2,8,null)},
      {0xE6, new GBOpcode(0xE6, "AND {0:x2}",2,8,null)},
      {0xEE, new GBOpcode(0xEE, "XOR {0:x2}",2,8,null)},
      {0xF6, new GBOpcode(0xF6, "OR {0:x2}",2,8,null)},
      {0xFE, new GBOpcode(0xFE, "CP {0:x2}",2,8,null)},
    };

    public static Dictionary<byte, GBOpcode> X16opcodes = new Dictionary<byte, GBOpcode>()
    {
      {0x03, new GBOpcode(0x03, "INC BC",1,8,new Step[] {
				(Gameboy gb) => {
          return true;
        },
				(Gameboy gb) => {
          gb.BC++;
					return true;
				},
			})},
      {0x09, new GBOpcode(0x09, "ADD HL,BC",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
				  gb.HL = Add(gb, gb.HL, gb.BC);
					return true;
				},
			})},
      {0x0B, new GBOpcode(0x0B, "DEC BC",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
          gb.BC--;
					return true;
				},
			})},
      {0x13, new GBOpcode(0x13, "INC DE",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
				  gb.DE++;
					return true;
				},
			})},
      {0x19, new GBOpcode(0x19, "ADD HL,DE",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.HL = Add(gb, gb.HL, gb.DE);
					return true;
				},
			})},
      {0x1B, new GBOpcode(0x1B, "DEC DE",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.DE--;
					return true;
				},
			})},
      {0x23, new GBOpcode(0x23, "INC HL",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
				  gb.HL++;
					return true;
				},
			})},
      {0x29, new GBOpcode(0x29, "ADD HL,HL",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.HL = Add(gb, gb.HL, gb.HL);
					return true;
				},
			})},
      {0x2B, new GBOpcode(0x2B, "DEC HL",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
				  gb.HL--;
					return true;
				},
			})},
      {0x33, new GBOpcode(0x33, "INC SP",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.SP++;
					return true;
				},
			})},
      {0x39, new GBOpcode(0x39, "ADD HL,SP",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.HL = Add(gb, gb.HL, gb.SP);
					return true;
				},
			})},
      {0x3B, new GBOpcode(0x3B, "DEC SP",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
				  gb.SP--;
					return true;
				},
			})},
      {0xE8, new GBOpcode(0xE8, "ADD SP,{0:x2}",2,16,new Step[] {
				(Gameboy gb) => {
					gb.N = false;
					gb.Z = false;
					return true;
				},
				(Gameboy gb) => {
					initial = gb.SP;
					return true;
				},
				(Gameboy gb) => {
					offset = gb._memory.ReadSByte(gb.PC++);
					return true;
				},
				(Gameboy gb) => {
					gb.SP += (ushort)offset;
					gb.CF = (initial & 0xFF) + (offset & 0xFF) > 0xFF;
					gb.HC = (initial & 0x0F) + (offset & 0x0F) > 0x0F;
					return true;
				},
			})},
      {0xF8, new GBOpcode(0xF8, "LD HL,SP+{0:x2}",2,12,new Step[] {
				(Gameboy gb) => {
					gb.N = false;
					gb.Z = false;
					return true;
				},
				(Gameboy gb) => {
					initial = gb.SP;
					return true;
				},
				(Gameboy gb) => {
					offset = gb._memory.ReadSByte(gb.PC++);
					return true;
				},
				(Gameboy gb) => {
					gb.HL = (ushort)(initial + offset);
					gb.CF = (initial & 0xFF) + (offset & 0xFF) > 0xFF;
					gb.HC = (initial & 0x0F) + (offset & 0x0F) > 0x0F;
					return true;
				},
			})},
    };

		private static ushort Add(Gameboy gb, ushort a, ushort b)
		{
			gb.N = false;
			gb.HC = (a & 0x0FFF) + (b & 0x0FFF) > 0x0FFF;
			gb.CF = a + b > 0xFFFF;
			return (ushort)(a + b);
		}
	}
}
