namespace GBOG.CPU.Opcodes
{
  public class LSMOpcodes
	{
		private static int value;
		private static ushort address;
		
		public static Dictionary<byte, GBOpcode> X8opcodes = new Dictionary<byte, GBOpcode>()
    {
      {0x02, new GBOpcode(0x02, "LD (BC),A", 1,8, new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.BC, gb.A);
					return true;
				},
			})},
      {0x06, new GBOpcode(0x06, "LD B,u8",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = gb._memory.ReadByte(gb.PC++);
					return true;
				},
			})},
      {0x0A, new GBOpcode(0x0A, "LD A,(BC)",1,8,new Step[] {
				(Gameboy gb) => {
					gb.A = gb._memory.ReadByte(gb.BC);
					return true;
				},
			})},
      {0x0E, new GBOpcode(0x0E, "LD C,u8",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = gb._memory.ReadByte(gb.PC++);
					return true;
				},
			})},
      {0x12, new GBOpcode(0x12, "LD (DE),A",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.DE, gb.A);
					return true;
				},
			})},
      {0x16, new GBOpcode(0x16, "LD D,u8",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = gb._memory.ReadByte(gb.PC++);
					return true;
				},
			})},
      {0x1A, new GBOpcode(0x1A, "LD A,(DE)",1,8,new Step[] {
				(Gameboy gb) => {
					gb.A = gb._memory.ReadByte(gb.DE);
					return true;
				},
			})},
      {0x1E, new GBOpcode(0x1E, "LD E,u8",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = gb._memory.ReadByte(gb.PC++);
					return true;
				},
			})},
      {0x22, new GBOpcode(0x22, "LD (HL+),A",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL++, gb.A);
					return true;
				},
			})},
      {0x26, new GBOpcode(0x26, "LD H,u8",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = gb._memory.ReadByte(gb.PC++);
					return true;
				},
			})},
      {0x2A, new GBOpcode(0x2A, "LD A,(HL+)",1,8,new Step[] {
				(Gameboy gb) => {
					var h = gb.H;
					var l = gb.L;
					address = (ushort)((h << 8) | l);
					gb.A = gb._memory.ReadByte(address++);
					gb.HL++;
					return true;
				},
			})},
      {0x2E, new GBOpcode(0x2E, "LD L,u8",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = gb._memory.ReadByte(gb.PC++);
					return true;
				},
			})},
      {0x32, new GBOpcode(0x32, "LD (HL-),A",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL--, gb.A);
					return true;
				},
			})},
      {0x36, new GBOpcode(0x36, "LD (HL),u8",2,12,new Step[] {
				(Gameboy gb) => {
					value = gb._memory.ReadByte(gb.PC++);
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0x3A, new GBOpcode(0x3A, "LD A,(HL-)",1,8,new Step[] {
				(Gameboy gb) => {
					gb.A = gb._memory.ReadByte(gb.HL--);
					return true;
				},
			})},
      {0x3E, new GBOpcode(0x3E, "LD A,u8",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = gb._memory.ReadByte(gb.PC++);
					return true;
				},
			})},
      {0x40, new GBOpcode(0x40, "LD B,B",1,4,new Step[] {
				(Gameboy gb) => {
					gb.B = gb.B;
					return true;
				},
			})},
      {0x41, new GBOpcode(0x41, "LD B,C",1,4,new Step[] {
				(Gameboy gb) => {
					gb.B = gb.C;
					return true;
				},
			})},
      {0x42, new GBOpcode(0x42, "LD B,D",1,4,new Step[] {
				(Gameboy gb) => {
					gb.B = gb.D;
					return true;
				},
			})},
      {0x43, new GBOpcode(0x43, "LD B,E",1,4,new Step[] {
				(Gameboy gb) => {
					gb.B = gb.E;
					return true;
				},
			})},
      {0x44, new GBOpcode(0x44, "LD B,H",1,4,new Step[] {
				(Gameboy gb) => {
					gb.B = gb.H;
					return true;
				},
			})},
      {0x45, new GBOpcode(0x45, "LD B,L",1,4,new Step[] {
				(Gameboy gb) => {
					gb.B = gb.L;
					return true;
				},
			})},
      {0x46, new GBOpcode(0x46, "LD B,(HL)",1,8,new Step[] {
				(Gameboy gb) => {
					gb.B = gb._memory.ReadByte(gb.HL);
					return true;
				},
			})},
      {0x47, new GBOpcode(0x47, "LD B,A",1,4,new Step[] {
				(Gameboy gb) => {
					gb.B = gb.A;
					return true;
				},
			})},
      {0x48, new GBOpcode(0x48, "LD C,B",1,4,new Step[] {
				(Gameboy gb) => {
					gb.C = gb.B;
					return true;
				},
			})},
      {0x49, new GBOpcode(0x49, "LD C,C",1,4,new Step[] {
				(Gameboy gb) => {
					gb.C = gb.C;
					return true;
				},
			})},
      {0x4A, new GBOpcode(0x4A, "LD C,D",1,4,new Step[] {
				(Gameboy gb) => {
					gb.C = gb.D;
					return true;
				},
			})},
      {0x4B, new GBOpcode(0x4B, "LD C,E",1,4,new Step[] {
				(Gameboy gb) => {
					gb.C = gb.E;
					return true;
				},
			})},
      {0x4C, new GBOpcode(0x4C, "LD C,H",1,4,new Step[] {
				(Gameboy gb) => {
					gb.C = gb.H;
					return true;
				},
			})},
      {0x4D, new GBOpcode(0x4D, "LD C,L",1,4,new Step[] {
				(Gameboy gb) => {
					gb.C = gb.L;
					return true;
				},
			})},
      {0x4E, new GBOpcode(0x4E, "LD C,(HL)",1,8,new Step[] {
				(Gameboy gb) => {
					gb.C = gb._memory.ReadByte(gb.HL);
					return true;
				},
			})},
      {0x4F, new GBOpcode(0x4F, "LD C,A",1,4,new Step[] {
				(Gameboy gb) => {
					gb.C = gb.A;
					return true;
				},
			})},
      {0x50, new GBOpcode(0x50, "LD D,B",1,4,new Step[] {
				(Gameboy gb) => {
					gb.D = gb.B;
					return true;
				},
			})},
      {0x51, new GBOpcode(0x51, "LD D,C",1,4,new Step[] {
				(Gameboy gb) => {
					gb.D = gb.C;
					return true;
				},
			})},
      {0x52, new GBOpcode(0x52, "LD D,D",1,4,new Step[] {
				(Gameboy gb) => {
					gb.D = gb.D;
					return true;
				},
			})},
      {0x53, new GBOpcode(0x53, "LD D,E",1,4,new Step[] {
				(Gameboy gb) => {
					gb.D = gb.E;
					return true;
				},
			})},
      {0x54, new GBOpcode(0x54, "LD D,H",1,4,new Step[] {
				(Gameboy gb) => {
					gb.D = gb.H;
					return true;
				},
			})},
      {0x55, new GBOpcode(0x55, "LD D,L",1,4,new Step[] {
				(Gameboy gb) => {
					gb.D = gb.L;
					return true;
				},
			})},
      {0x56, new GBOpcode(0x56, "LD D,(HL)",1,8,new Step[] {
				(Gameboy gb) => {
					gb.D = gb._memory.ReadByte(gb.HL);
					return true;
				},
			})},
      {0x57, new GBOpcode(0x57, "LD D,A",1,4,new Step[] {
				(Gameboy gb) => {
					gb.D = gb.A;
					return true;
				},
			})},
      {0x58, new GBOpcode(0x58, "LD E,B",1,4,new Step[] {
				(Gameboy gb) => {
					gb.E = gb.B;
					return true;
				},
			})},
      {0x59, new GBOpcode(0x59, "LD E,C",1,4,new Step[] {
				(Gameboy gb) => {
					gb.E = gb.C;
					return true;
				},
			})},
      {0x5A, new GBOpcode(0x5A, "LD E,D",1,4,new Step[] {
				(Gameboy gb) => {
					gb.E = gb.D;
					return true;
				},
			})},
      {0x5B, new GBOpcode(0x5B, "LD E,E",1,4,new Step[] {
				(Gameboy gb) => {
					gb.E = gb.E;
					return true;
				},
			})},
      {0x5C, new GBOpcode(0x5C, "LD E,H",1,4,new Step[] {
				(Gameboy gb) => {
					gb.E = gb.H;
					return true;
				},
			})},
      {0x5D, new GBOpcode(0x5D, "LD E,L",1,4,new Step[] {
				(Gameboy gb) => {
					gb.E = gb.L;
					return true;
				},
			})},
      {0x5E, new GBOpcode(0x5E, "LD E,(HL)",1,8,new Step[] {
				(Gameboy gb) => {
					gb.E = gb._memory.ReadByte(gb.HL);
					return true;
				},
			})},
      {0x5F, new GBOpcode(0x5F, "LD E,A",1,4,new Step[] {
				(Gameboy gb) => {
					gb.E = gb.A;
					return true;
				},
			})},
      {0x60, new GBOpcode(0x60, "LD H,B",1,4,new Step[] {
				(Gameboy gb) => {
					gb.H = gb.B;
					return true;
				},
			})},
      {0x61, new GBOpcode(0x61, "LD H,C",1,4,new Step[] {
				(Gameboy gb) => {
					gb.H = gb.C;
					return true;
				},
			})},
      {0x62, new GBOpcode(0x62, "LD H,D",1,4,new Step[] {
				(Gameboy gb) => {
					gb.H = gb.D;
					return true;
				},
			})},
      {0x63, new GBOpcode(0x63, "LD H,E",1,4,new Step[] {
				(Gameboy gb) => {
					gb.H = gb.E;
					return true;
				},
			})},
      {0x64, new GBOpcode(0x64, "LD H,H",1,4,new Step[] {
				(Gameboy gb) => {
					gb.H = gb.H;
					return true;
				},
			})},
      {0x65, new GBOpcode(0x65, "LD H,L",1,4,new Step[] {
				(Gameboy gb) => {
					gb.H = gb.L;
					return true;
				},
			})},
      {0x66, new GBOpcode(0x66, "LD H,(HL)",1,8,new Step[] {
				(Gameboy gb) => {
					gb.H = gb._memory.ReadByte(gb.HL);
					return true;
				},
			})},
      {0x67, new GBOpcode(0x67, "LD H,A",1,4,new Step[] {
				(Gameboy gb) => {
					gb.H = gb.A;
					return true;
				},
			})},
      {0x68, new GBOpcode(0x68, "LD L,B",1,4,new Step[] {
				(Gameboy gb) => {
					gb.L = gb.B;
					return true;
				},
			})},
      {0x69, new GBOpcode(0x69, "LD L,C",1,4,new Step[] {
				(Gameboy gb) => {
					gb.L = gb.C;
					return true;
				},
			})},
      {0x6A, new GBOpcode(0x6A, "LD L,D",1,4,new Step[] {
				(Gameboy gb) => {
					gb.L = gb.D;
					return true;
				},
			})},
      {0x6B, new GBOpcode(0x6B, "LD L,E",1,4,new Step[] {
				(Gameboy gb) => {
					gb.L = gb.E;
					return true;
				},
			})},
      {0x6C, new GBOpcode(0x6C, "LD L,H",1,4,new Step[] {
				(Gameboy gb) => {
					gb.L = gb.H;
					return true;
				},
			})},
      {0x6D, new GBOpcode(0x6D, "LD L,L",1,4,new Step[] {
				(Gameboy gb) => {
					gb.L = gb.L;
					return true;
				},
			})},
      {0x6E, new GBOpcode(0x6E, "LD L,(HL)",1,8,new Step[] {
				(Gameboy gb) => {
					gb.L = gb._memory.ReadByte(gb.HL);
					return true;
				},
			})},
      {0x6F, new GBOpcode(0x6F, "LD L,A",1,4,new Step[] {
				(Gameboy gb) => {
					gb.L = gb.A;
					return true;
				},
			})},
      {0x70, new GBOpcode(0x70, "LD (HL),B",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, gb.B);
					return true;
				},
			})},
      {0x71, new GBOpcode(0x71, "LD (HL),C",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, gb.C);
					return true;
				},
			})},
      {0x72, new GBOpcode(0x72, "LD (HL),D",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, gb.D);
					return true;
				},
			})},
      {0x73, new GBOpcode(0x73, "LD (HL),E",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, gb.E);
					return true;
				},
			})},
      {0x74, new GBOpcode(0x74, "LD (HL),H",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, gb.H);
					return true;
				},
			})},
      {0x75, new GBOpcode(0x75, "LD (HL),L",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, gb.L);
					return true;
				},
			})},
      {0x77, new GBOpcode(0x77, "LD (HL),A",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, gb.A);
					return true;
				},
			})},
      {0x78, new GBOpcode(0x78, "LD A,B",1,4,new Step[] {
				(Gameboy gb) => {
					gb.A = gb.B;
					return true;
				},
			})},
      {0x79, new GBOpcode(0x79, "LD A,C",1,4,new Step[] {
				(Gameboy gb) => {
					gb.A = gb.C;
					return true;
				},
			})},
      {0x7A, new GBOpcode(0x7A, "LD A,D",1,4,new Step[] {
				(Gameboy gb) => {
					gb.A = gb.D;
					return true;
				},
			})},
      {0x7B, new GBOpcode(0x7B, "LD A,E",1,4,new Step[] {
				(Gameboy gb) => {
					gb.A = gb.E;
					return true;
				},
			})},
      {0x7C, new GBOpcode(0x7C, "LD A,H",1,4,new Step[] {
				(Gameboy gb) => {
					gb.A = gb.H;
					return true;
				},
			})},
      {0x7D, new GBOpcode(0x7D, "LD A,L",1,4,new Step[] {
				(Gameboy gb) => {
					gb.A = gb.L;
					return true;
				},
			})},
      {0x7E, new GBOpcode(0x7E, "LD A,(HL)",1,8,new Step[] {
				(Gameboy gb) => {
					gb.A = gb._memory.ReadByte(gb.HL);
					return true;
				},
			})},
      {0x7F, new GBOpcode(0x7F, "LD A,A",1,4,new Step[] {
				(Gameboy gb) => {
					gb.A = gb.A;
					return true;
				},
			})},
      {0xE0, new GBOpcode(0xE0, "LD (FF00+u8),A",2,12,new Step[] {
				(Gameboy gb) => {
					address = (ushort)(0xFF00 + gb._memory.ReadByte(gb.PC));
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(address, gb.A);
					return true;
				},
			})},
      {0xE2, new GBOpcode(0xE2, "LD (FF00+C),A",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte((ushort)(0xFF00 + gb.C), gb.A);
					return true;
				},
			})},
      {0xEA, new GBOpcode(0xEA, "LD (u16),A",3,16,new Step[] {
				(Gameboy gb) => {
					address = gb._memory.ReadUShort(gb.PC);
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(address, gb.A);
					return true;
				},
			})},
      {0xF0, new GBOpcode(0xF0, "LD A,(FF00+u8)",2,12,new Step[] {
				(Gameboy gb) => {
				address = (ushort)(0xFF00 + gb._memory.ReadByte(gb.PC++));
					return true;
				},
				(Gameboy gb) => {
				gb.A = gb._memory.ReadByte(address);
					return true;
				},
			})},
      {0xF2, new GBOpcode(0xF2, "LD A,(FF00+C)",1,8,new Step[] {
				(Gameboy gb) => {
					gb.A = gb._memory.ReadByte((ushort)(0xFF00 + gb.C));
					return true;
				},
			})},
      {0xFA, new GBOpcode(0xFA, "LD A,(u16)",3,16,new Step[] {
				(Gameboy gb) => {
					address = gb._memory.ReadUShort(gb.PC++);
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					gb.A = gb._memory.ReadByte((ushort)address);
					return true;
				},
			})}
    };
		
    public static Dictionary<byte, GBOpcode> X16opcodes = new Dictionary<byte, GBOpcode>()
    {
      {0x01, new GBOpcode(0x01, "LD BC,u16",3,12, new Step[] {
				(Gameboy gb) => {
					value = gb._memory.ReadUShort(gb.PC++);
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					gb.BC = (ushort)value;
					return true;
				},
			})},
      {0x08, new GBOpcode(0x08, "LD (u16),SP",3,20,new Step[] {
				(Gameboy gb) => {
					address = gb._memory.ReadUShort(gb.PC);
					return true;
				},
				(Gameboy gb) => {
					value = gb.SP;
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(address, (ushort)value);
					return true;
				},
			})},
      {0x11, new GBOpcode(0x11, "LD DE,u16",3,12,new Step[] {
				(Gameboy gb) => {
					value = gb._memory.ReadUShort(gb.PC++);
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					gb.DE = (ushort)value;
					return true;
				},
			})},
      {0x21, new GBOpcode(0x21, "LD HL,u16",3,12,new Step[] {
					(Gameboy gb) => {
					value = gb._memory.ReadUShort(gb.PC++);
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					gb.HL = (ushort)value;
					return true;
				},
			})},
      {0x31, new GBOpcode(0x31, "LD SP,u16",3,12,new Step[] {
					(Gameboy gb) => {
					value = gb._memory.ReadUShort(gb.PC++);
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					gb.SP = (ushort)value;
					return true;
				},
			})},
      {0xC1, new GBOpcode(0xC1, "POP BC",1,12,new Step[] {
				(Gameboy gb) => {
					value = gb._memory.ReadUShort(gb.SP++);
					return true;
				},
				(Gameboy gb) => {
					gb.SP +=1;
					return true;
				},
				(Gameboy gb) => {
					gb.BC = (ushort)value;
					return true;
				},
			})},
      {0xC5, new GBOpcode(0xC5, "PUSH BC",1,16,new Step[] {
				(Gameboy gb) => {
					value = gb.BC;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, (ushort)value);
					return true;
				},
			})},
      {0xD1, new GBOpcode(0xD1, "POP DE",1,12,new Step[] {
				(Gameboy gb) => {
					value = gb._memory.ReadUShort(gb.SP++);
					return true;
				},
				(Gameboy gb) => {
					gb.SP +=1;
					return true;
				},
				(Gameboy gb) => {
					gb.DE = (ushort)value;
					return true;
				},
			})},
      {0xD5, new GBOpcode(0xD5, "PUSH DE",1,16,new Step[] {
				(Gameboy gb) => {
					value = gb.DE;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, (ushort)value);
					return true;
				},
			})},
      {0xE1, new GBOpcode(0xE1, "POP HL",1,12,new Step[] {
				(Gameboy gb) => {
					value = gb._memory.ReadUShort(gb.SP++);
					return true;
				},
				(Gameboy gb) => {
					gb.SP +=1;
					return true;
				},
				(Gameboy gb) => {
					gb.HL = (ushort)value;
					return true;
				},
			})},
      {0xE5, new GBOpcode(0xE5, "PUSH HL",1,16,new Step[] {
				(Gameboy gb) => {
					value = gb.HL;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, (ushort)value);
					return true;
				},
			})},
      {0xF1, new GBOpcode(0xF1, "POP AF",1,12,new Step[] {
				(Gameboy gb) => {
					value = gb._memory.ReadUShort(gb.SP++);
					return true;
				},
				(Gameboy gb) => {
					gb.SP +=1;
					return true;
				},
				(Gameboy gb) => {
					gb.AF = (ushort)value;
					return true;
				},
			})},
      {0xF5, new GBOpcode(0xF5, "PUSH AF",1,16,new Step[] {
				(Gameboy gb) => {
					value = gb.AF;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, (ushort)value);
					return true;
				},
			})},
      {0xF9, new GBOpcode(0xF9, "LD SP,HL",1,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.SP = gb.HL;
					return true;
				},
			})}
    };
  }
}
