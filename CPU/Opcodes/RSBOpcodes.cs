﻿namespace GBOG.CPU.Opcodes
{
  public class RSBOpcodes
  {
    public static int value;
    public static Dictionary<byte, GBOpcode> X8opcodes = new Dictionary<byte, GBOpcode>()
    {
      {0x07, new GBOpcode(0x07, "RLCA", 1,4, new Step[] {
        (Gameboy gb) => {
					gb.A = (byte)((gb.A << 1) | (gb.A >> 7));
          gb.Z = false;
          gb.N = false;
          gb.HC = false;
          gb.CF = (gb.A & 0x01) == 1;
					return true;
        }
      })},
      {0x0F, new GBOpcode(0x0F, "RRCA",1,4,new Step[] {
				(Gameboy gb) => {
					gb.Z = false;
					gb.N = false;
					gb.HC = false;
					gb.CF = (gb.A & 0x01) > 0;
					gb.A = (byte)((gb.A >> 1) | (gb.A << 7));
					return true;
				}
			})},
      {0x17, new GBOpcode(0x17, "RLA",1,4,new Step[] {
				(Gameboy gb) => {
					var currentCarry = gb.CF;
					gb.Z = false;
					gb.N = false;
					gb.HC = false;
					gb.CF = (gb.A & 0x80) > 0;
					gb.A = (byte)((gb.A << 1) | (currentCarry ? 1 : 0));
					return true;
				}
			})},
      {0x1F, new GBOpcode(0x1F, "RRA",1,4,new Step[] {
				(Gameboy gb) => {
					var val = gb.A;
					gb.A = (byte)((gb.A >> 1) | (gb.CF ? 128 : 0));
					gb.Z = false;
					gb.N = false;
					gb.HC = false;
					gb.CF = (val & 1) == 1;
					return true;
				}
			})},
    };

    public static Dictionary<byte, GBOpcode> CBOpcodes = new Dictionary<byte, GBOpcode>()
    {
      {0x00, new GBOpcode(0x00, "RLC B",2,8,new Step[] {
        (Gameboy gb) => {
          return true;
        },
				(Gameboy gb) => {
          gb.B = Rlc(gb, gb.B);
					return true;
				},
			})},
      {0x01, new GBOpcode(0x01, "RLC C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
				  gb.C = Rlc(gb, gb.C);
					return true;
				},
			})},
      {0x02, new GBOpcode(0x02, "RLC D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Rlc(gb, gb.D);
					return true;
				},
			})},
      {0x03, new GBOpcode(0x03, "RLC E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Rlc(gb, gb.E);
					return true;
				},
			})},
      {0x04, new GBOpcode(0x04, "RLC H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Rlc(gb, gb.H);
					return true;
				},
			})},
      {0x05, new GBOpcode(0x05, "RLC L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Rlc(gb, gb.L);
					return true;
				},
			})},
      {0x06, new GBOpcode(0x06, "RLC (HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = gb._memory.ReadByte(gb.HL);
					return true;
				},
				(Gameboy gb) => {
					value = Rlc(gb, (byte)value);
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0x07, new GBOpcode(0x07, "RLC A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
				  gb.A = Rlc(gb, gb.A);
					return true;
				},
			})},
      {0x08, new GBOpcode(0x08, "RRC B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Rrc(gb, gb.B);
					return true;
				},
			})},
      {0x09, new GBOpcode(0x09, "RRC C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Rrc(gb, gb.C);
					return true;
				},
			})},
      {0x0A, new GBOpcode(0x0A, "RRC D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Rrc(gb, gb.D);
					return true;
				},
			})},
      {0x0B, new GBOpcode(0x0B, "RRC E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Rrc(gb, gb.E);
					return true;
				},
			})},
      {0x0C, new GBOpcode(0x0C, "RRC H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Rrc(gb, gb.H);
					return true;
				},
			})},
      {0x0D, new GBOpcode(0x0D, "RRC L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Rrc(gb, gb.L);
					return true;
				},
			})},
      {0x0E, new GBOpcode(0x0E, "RRC (HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = gb._memory.ReadByte(gb.HL);
					return true;
				},
				(Gameboy gb) => {
					value = Rrc(gb, (byte)value);
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0x0F, new GBOpcode(0x0F, "RRC A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Rrc(gb, gb.A);
					return true;
				},
			})},
      {0x10, new GBOpcode(0x10, "RL B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Rl(gb, gb.B);
					return true;
				},
			})},
      {0x11, new GBOpcode(0x11, "RL C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Rl(gb, gb.C);
					return true;
				},
			})},
      {0x12, new GBOpcode(0x12, "RL D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Rl(gb, gb.D);
					return true;
				},
			})},
      {0x13, new GBOpcode(0x13, "RL E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Rl(gb, gb.E);
					return true;
				},
			})},
      {0x14, new GBOpcode(0x14, "RL H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Rl(gb, gb.H);
					return true;
				},
			})},
      {0x15, new GBOpcode(0x15, "RL L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Rl(gb, gb.L);
					return true;
				},
			})},
      {0x16, new GBOpcode(0x16, "RL (HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = gb._memory.ReadByte(gb.HL);
					return true;
				},
				(Gameboy gb) => {
					value = Rl(gb, (byte)value);
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0x17, new GBOpcode(0x17, "RL A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Rl(gb, gb.A);
					return true;
				},
			})},
      {0x18, new GBOpcode(0x18, "RR B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
				  gb.B = Rr(gb, gb.B);
					return true;
				},
			})},
      {0x19, new GBOpcode(0x19, "RR C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Rr(gb, gb.C);
					return true;
				},
			})},
      {0x1A, new GBOpcode(0x1A, "RR D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Rr(gb, gb.D);
					return true;
				},
			})},
      {0x1B, new GBOpcode(0x1B, "RR E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Rr(gb, gb.E);
					return true;
				},
			})},
      {0x1C, new GBOpcode(0x1C, "RR H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Rr(gb, gb.H);
					return true;
				},
			})},
      {0x1D, new GBOpcode(0x1D, "RR L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Rr(gb, gb.L);
					return true;
				},
			})},
      {0x1E, new GBOpcode(0x1E, "RR (HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = gb._memory.ReadByte(gb.HL);
					return true;
				},
				(Gameboy gb) => {
					value = Rr(gb, (byte)value);
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0x1F, new GBOpcode(0x1F, "RR A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Rr(gb, gb.A);
					return true;
				},
			})},
      {0x20, new GBOpcode(0x20, "SLA B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Sla(gb, gb.B);
					return true;
				},
			})},
      {0x21, new GBOpcode(0x21, "SLA C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Sla(gb, gb.C);
					return true;
				},
			})},
      {0x22, new GBOpcode(0x22, "SLA D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Sla(gb, gb.D);
					return true;
				},
			})},
      {0x23, new GBOpcode(0x23, "SLA E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Sla(gb, gb.E);
					return true;
				},
			})},
      {0x24, new GBOpcode(0x24, "SLA H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Sla(gb, gb.H);
					return true;
				},
			})},
      {0x25, new GBOpcode(0x25, "SLA L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Sla(gb, gb.L);
					return true;
				},
			})},
      {0x26, new GBOpcode(0x26, "SLA (HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = gb._memory.ReadByte(gb.HL);
					return true;
				},
				(Gameboy gb) => {
					value = Sla(gb, (byte)value);
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0x27, new GBOpcode(0x27, "SLA A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Sla(gb, gb.A);
					return true;
				},
			})},
      {0x28, new GBOpcode(0x28, "SRA B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Sra(gb, gb.B);
					return true;
				},
			})},
      {0x29, new GBOpcode(0x29, "SRA C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Sra(gb, gb.C);
					return true;
				},
			})},
      {0x2A, new GBOpcode(0x2A, "SRA D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Sra(gb, gb.D);
					return true;
				},
			})},
      {0x2B, new GBOpcode(0x2B, "SRA E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Sra(gb, gb.E);
					return true;
				},
			})},
      {0x2C, new GBOpcode(0x2C, "SRA H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Sra(gb, gb.H);
					return true;
				},
			})},
      {0x2D, new GBOpcode(0x2D, "SRA L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Sra(gb, gb.L);
					return true;
				},
			})},
      {0x2E, new GBOpcode(0x2E, "SRA (HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = gb._memory.ReadByte(gb.HL);
					return true;
				},
				(Gameboy gb) => {
					value = Sra(gb, (byte)value);
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0x2F, new GBOpcode(0x2F, "SRA A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Sra(gb, gb.A);
					return true;
				},
			})},
      {0x30, new GBOpcode(0x30, "SWAP B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Swap(gb, gb.B);
					return true;
				},
			})},
      {0x31, new GBOpcode(0x31, "SWAP C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Swap(gb, gb.C);
					return true;
				},
			})},
      {0x32, new GBOpcode(0x32, "SWAP D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Swap(gb, gb.D);
					return true;
				},
			})},
      {0x33, new GBOpcode(0x33, "SWAP E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Swap(gb, gb.E);
					return true;
				},
			})},
      {0x34, new GBOpcode(0x34, "SWAP H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Swap(gb, gb.H);
					return true;
				},
			})},
      {0x35, new GBOpcode(0x35, "SWAP L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Swap(gb, gb.L);
					return true;
				},
			})},
      {0x36, new GBOpcode(0x36, "SWAP (HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = gb._memory.ReadByte(gb.HL);
					return true;
				},
				(Gameboy gb) => {
					value = Swap(gb, (byte)value);
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0x37, new GBOpcode(0x37, "SWAP A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Swap(gb, gb.A);
					return true;
				},
			})},
      {0x38, new GBOpcode(0x38, "SRL B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Srl(gb, gb.B);
					return true;
				},
			})},
      {0x39, new GBOpcode(0x39, "SRL C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Srl(gb, gb.C);
					return true;
				},
			})},
      {0x3A, new GBOpcode(0x3A, "SRL D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Srl(gb, gb.D);
					return true;
				},
			})},
      {0x3B, new GBOpcode(0x3B, "SRL E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Srl(gb, gb.E);
					return true;
				},
			})},
      {0x3C, new GBOpcode(0x3C, "SRL H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Srl(gb, gb.H);
					return true;
				},
			})},
      {0x3D, new GBOpcode(0x3D, "SRL L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Srl(gb, gb.L);
					return true;
				},
			})},
      {0x3E, new GBOpcode(0x3E, "SRL (HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = gb._memory.ReadByte(gb.HL);
					return true;
				},
				(Gameboy gb) => {
					value = Srl(gb, (byte)value);
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0x3F, new GBOpcode(0x3F, "SRL A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Srl(gb, gb.A);
					return true;
				},
			})},
      {0x40, new GBOpcode(0x40, "BIT 0,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 0, gb.B);
					return true;
				},
			})},
      {0x41, new GBOpcode(0x41, "BIT 0,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 0, gb.C);
					return true;
				},
			})},
      {0x42, new GBOpcode(0x42, "BIT 0,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 0, gb.D);
					return true;
				},
			})},
      {0x43, new GBOpcode(0x43, "BIT 0,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 0, gb.E);
					return true;
				},
			})},
      {0x44, new GBOpcode(0x44, "BIT 0,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 0, gb.H);
					return true;
				},
			})},
      {0x45, new GBOpcode(0x45, "BIT 0,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 0, gb.L);
					return true;
				},
			})},
      {0x46, new GBOpcode(0x46, "BIT 0,(HL)",2,12,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 0, gb._memory.ReadByte(gb.HL));
					return true;
				},
			})},
      {0x47, new GBOpcode(0x47, "BIT 0,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 0, gb.A);
					return true;
				},
			})},
      {0x48, new GBOpcode(0x48, "BIT 1,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 1, gb.B);
					return true;
				},
			})},
      {0x49, new GBOpcode(0x49, "BIT 1,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 1, gb.C);
					return true;
				},
			})},
      {0x4A, new GBOpcode(0x4A, "BIT 1,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 1, gb.D);
					return true;
				},
			})},
      {0x4B, new GBOpcode(0x4B, "BIT 1,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 1, gb.E);
					return true;
				},
			})},
      {0x4C, new GBOpcode(0x4C, "BIT 1,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 1, gb.H);
					return true;
				},
			})},
      {0x4D, new GBOpcode(0x4D, "BIT 1,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 1, gb.L);
					return true;
				},
			})},
      {0x4E, new GBOpcode(0x4E, "BIT 1,(HL)",2,12,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 1, gb._memory.ReadByte(gb.HL));
					return true;
				},
			})},
      {0x4F, new GBOpcode(0x4F, "BIT 1,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 1, gb.A);
					return true;
				},
			})},
      {0x50, new GBOpcode(0x50, "BIT 2,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 2, gb.B);
					return true;
				},
			})},
      {0x51, new GBOpcode(0x51, "BIT 2,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 2, gb.C);
					return true;
				},
			})},
      {0x52, new GBOpcode(0x52, "BIT 2,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 2, gb.D);
					return true;
				},
			})},
      {0x53, new GBOpcode(0x53, "BIT 2,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 2, gb.E);
					return true;
				},
			})},
      {0x54, new GBOpcode(0x54, "BIT 2,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 2, gb.H);
					return true;
				},
			})},
      {0x55, new GBOpcode(0x55, "BIT 2,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 2, gb.L);
					return true;
				},
			})},
      {0x56, new GBOpcode(0x56, "BIT 2,(HL)",2,12,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 2, gb._memory.ReadByte(gb.HL));
					return true;
				},
			})},
      {0x57, new GBOpcode(0x57, "BIT 2,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 2, gb.A);
					return true;
				},
			})},
      {0x58, new GBOpcode(0x58, "BIT 3,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 3, gb.B);
					return true;
				},
			})},
      {0x59, new GBOpcode(0x59, "BIT 3,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 3, gb.C);
					return true;
				},
			})},
      {0x5A, new GBOpcode(0x5A, "BIT 3,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 3, gb.D);
					return true;
				},
			})},
      {0x5B, new GBOpcode(0x5B, "BIT 3,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 3, gb.E);
					return true;
				},
			})},
      {0x5C, new GBOpcode(0x5C, "BIT 3,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 3, gb.H);
					return true;
				},
			})},
      {0x5D, new GBOpcode(0x5D, "BIT 3,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 3, gb.L);
					return true;
				},
			})},
      {0x5E, new GBOpcode(0x5E, "BIT 3,(HL)",2,12,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 3, gb._memory.ReadByte(gb.HL));
					return true;
				},
			})},
      {0x5F, new GBOpcode(0x5F, "BIT 3,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 3, gb.A);
					return true;
				},
			})},
      {0x60, new GBOpcode(0x60, "BIT 4,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 4, gb.B);
					return true;
				},
			})},
      {0x61, new GBOpcode(0x61, "BIT 4,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 4, gb.C);
					return true;
				},
			})},
      {0x62, new GBOpcode(0x62, "BIT 4,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 4, gb.D);
					return true;
				},
			})},
      {0x63, new GBOpcode(0x63, "BIT 4,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 4, gb.E);
					return true;
				},
			})},
      {0x64, new GBOpcode(0x64, "BIT 4,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 4, gb.H);
					return true;
				},
			})},
      {0x65, new GBOpcode(0x65, "BIT 4,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 4, gb.L);
					return true;
				},
			})},
      {0x66, new GBOpcode(0x66, "BIT 4,(HL)",2,12,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 4, gb._memory.ReadByte(gb.HL));
					return true;
				},
			})},
      {0x67, new GBOpcode(0x67, "BIT 4,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 4, gb.A);
					return true;
				},
			})},
      {0x68, new GBOpcode(0x68, "BIT 5,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 5, gb.B);
					return true;
				},
			})},
      {0x69, new GBOpcode(0x69, "BIT 5,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 5, gb.C);
					return true;
				},
			})},
      {0x6A, new GBOpcode(0x6A, "BIT 5,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 5, gb.D);
					return true;
				},
			})},
      {0x6B, new GBOpcode(0x6B, "BIT 5,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 5, gb.E);
					return true;
				},
			})},
      {0x6C, new GBOpcode(0x6C, "BIT 5,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 5, gb.H);
					return true;
				},
			})},
      {0x6D, new GBOpcode(0x6D, "BIT 5,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 5, gb.L);
					return true;
				},
			})},
      {0x6E, new GBOpcode(0x6E, "BIT 5,(HL)",2,12,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 5, gb._memory.ReadByte(gb.HL));
					return true;
				},
			})},
      {0x6F, new GBOpcode(0x6F, "BIT 5,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 5, gb.A);
					return true;
				},
			})},
      {0x70, new GBOpcode(0x70, "BIT 6,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 6, gb.B);
					return true;
				},
			})},
      {0x71, new GBOpcode(0x71, "BIT 6,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 6, gb.C);
					return true;
				},
			})},
      {0x72, new GBOpcode(0x72, "BIT 6,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 6, gb.D);
					return true;
				},
			})},
      {0x73, new GBOpcode(0x73, "BIT 6,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 6, gb.E);
					return true;
				},
			})},
      {0x74, new GBOpcode(0x74, "BIT 6,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 6, gb.H);
					return true;
				},
			})},
      {0x75, new GBOpcode(0x75, "BIT 6,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 6, gb.L);
					return true;
				},
			})},
      {0x76, new GBOpcode(0x76, "BIT 6,(HL)",2,12,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 6, gb._memory.ReadByte(gb.HL));
					return true;
				},
			})},
      {0x77, new GBOpcode(0x77, "BIT 6,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 6, gb.A);
					return true;
				},
			})},
      {0x78, new GBOpcode(0x78, "BIT 7,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 7, gb.B);
					return true;
				},
			})},
      {0x79, new GBOpcode(0x79, "BIT 7,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 7, gb.C);
					return true;
				},
			})},
      {0x7A, new GBOpcode(0x7A, "BIT 7,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 7, gb.D);
					return true;
				},
			})},
      {0x7B, new GBOpcode(0x7B, "BIT 7,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 7, gb.E);
					return true;
				},
			})},
      {0x7C, new GBOpcode(0x7C, "BIT 7,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 7, gb.H);
					return true;
				},
			})},
      {0x7D, new GBOpcode(0x7D, "BIT 7,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 7, gb.L);
					return true;
				},
			})},
      {0x7E, new GBOpcode(0x7E, "BIT 7,(HL)",2,12,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 7, gb._memory.ReadByte(gb.HL));
					return true;
				},
			})},
      {0x7F, new GBOpcode(0x7F, "BIT 7,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					Bit(gb, 7, gb.A);
					return true;
				},
			})},
      {0x80, new GBOpcode(0x80, "RES 0,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Res(0, gb.B);
					return true;
				},
			})},
      {0x81, new GBOpcode(0x81, "RES 0,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Res(0, gb.C);
					return true;
				},
			})},
      {0x82, new GBOpcode(0x82, "RES 0,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Res(0, gb.D);
					return true;
				},
			})},
      {0x83, new GBOpcode(0x83, "RES 0,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Res(0, gb.E);
					return true;
				},
			})},
      {0x84, new GBOpcode(0x84, "RES 0,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Res(0, gb.H);
					return true;
				},
			})},
      {0x85, new GBOpcode(0x85, "RES 0,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Res(0, gb.L);
					return true;
				},
			})},
      {0x86, new GBOpcode(0x86, "RES 0,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Res(0, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0x87, new GBOpcode(0x87, "RES 0,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Res(0, gb.A);
					return true;
				},
			})},
      {0x88, new GBOpcode(0x88, "RES 1,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Res(1, gb.B);
					return true;
				},
			})},
      {0x89, new GBOpcode(0x89, "RES 1,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Res(1, gb.C);
					return true;
				},
			})},
      {0x8A, new GBOpcode(0x8A, "RES 1,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Res(1, gb.D);
					return true;
				},
			})},
      {0x8B, new GBOpcode(0x8B, "RES 1,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Res(1, gb.E);
					return true;
				},
			})},
      {0x8C, new GBOpcode(0x8C, "RES 1,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Res(1, gb.H);
					return true;
				},
			})},
      {0x8D, new GBOpcode(0x8D, "RES 1,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Res(1, gb.L);
					return true;
				},
			})},
      {0x8E, new GBOpcode(0x8E, "RES 1,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Res(1, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0x8F, new GBOpcode(0x8F, "RES 1,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Res(1, gb.A);
					return true;
				},
			})},
      {0x90, new GBOpcode(0x90, "RES 2,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Res(2, gb.B);
					return true;
				},
			})},
      {0x91, new GBOpcode(0x91, "RES 2,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Res(2, gb.C);
					return true;
				},
			})},
      {0x92, new GBOpcode(0x92, "RES 2,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Res(2, gb.D);
					return true;
				},
			})},
      {0x93, new GBOpcode(0x93, "RES 2,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Res(2, gb.E);
					return true;
				},
			})},
      {0x94, new GBOpcode(0x94, "RES 2,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Res(2, gb.H);
					return true;
				},
			})},
      {0x95, new GBOpcode(0x95, "RES 2,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Res(2, gb.L);
					return true;
				},
			})},
      {0x96, new GBOpcode(0x96, "RES 2,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Res(2, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0x97, new GBOpcode(0x97, "RES 2,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Res(2, gb.A);
					return true;
				},
			})},
      {0x98, new GBOpcode(0x98, "RES 3,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Res(3, gb.B);
					return true;
				},
			})},
      {0x99, new GBOpcode(0x99, "RES 3,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Res(3, gb.C);
					return true;
				},
			})},
      {0x9A, new GBOpcode(0x9A, "RES 3,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Res(3, gb.D);
					return true;
				},
			})},
      {0x9B, new GBOpcode(0x9B, "RES 3,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Res(3, gb.E);
					return true;
				},
			})},
      {0x9C, new GBOpcode(0x9C, "RES 3,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Res(3, gb.H);
					return true;
				},
			})},
      {0x9D, new GBOpcode(0x9D, "RES 3,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Res(3, gb.L);
					return true;
				},
			})},
      {0x9E, new GBOpcode(0x9E, "RES 3,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Res(3, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0x9F, new GBOpcode(0x9F, "RES 3,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Res(3, gb.A);
					return true;
				},
			})},
      {0xA0, new GBOpcode(0xA0, "RES 4,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Res(4, gb.B);
					return true;
				},
			})},
      {0xA1, new GBOpcode(0xA1, "RES 4,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Res(4, gb.C);
					return true;
				},
			})},
      {0xA2, new GBOpcode(0xA2, "RES 4,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Res(4, gb.D);
					return true;
				},
			})},
      {0xA3, new GBOpcode(0xA3, "RES 4,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Res(4, gb.E);
					return true;
				},
			})},
      {0xA4, new GBOpcode(0xA4, "RES 4,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Res(4, gb.H);
					return true;
				},
			})},
      {0xA5, new GBOpcode(0xA5, "RES 4,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Res(4, gb.L);
					return true;
				},
			})},
      {0xA6, new GBOpcode(0xA6, "RES 4,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Res(4, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0xA7, new GBOpcode(0xA7, "RES 4,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Res(4, gb.A);
					return true;
				},
			})},
      {0xA8, new GBOpcode(0xA8, "RES 5,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Res(5, gb.B);
					return true;
				},
			})},
      {0xA9, new GBOpcode(0xA9, "RES 5,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Res(5, gb.C);
					return true;
				},
			})},
      {0xAA, new GBOpcode(0xAA, "RES 5,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Res(5, gb.D);
					return true;
				},
			})},
      {0xAB, new GBOpcode(0xAB, "RES 5,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Res(5, gb.E);
					return true;
				},
			})},
      {0xAC, new GBOpcode(0xAC, "RES 5,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Res(5, gb.H);
					return true;
				},
			})},
      {0xAD, new GBOpcode(0xAD, "RES 5,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Res(5, gb.L);
					return true;
				},
			})},
      {0xAE, new GBOpcode(0xAE, "RES 5,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Res(5, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0xAF, new GBOpcode(0xAF, "RES 5,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Res(5, gb.A);
					return true;
				},
			})},
      {0xB0, new GBOpcode(0xB0, "RES 6,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Res(6, gb.B);
					return true;
				},
			})},
      {0xB1, new GBOpcode(0xB1, "RES 6,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Res(6, gb.C);
					return true;
				},
			})},
      {0xB2, new GBOpcode(0xB2, "RES 6,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Res(6, gb.D);
					return true;
				},
			})},
      {0xB3, new GBOpcode(0xB3, "RES 6,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Res(6, gb.E);
					return true;
				},
			})},
      {0xB4, new GBOpcode(0xB4, "RES 6,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Res(6, gb.H);
					return true;
				},
			})},
      {0xB5, new GBOpcode(0xB5, "RES 6,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Res(6, gb.L);
					return true;
				},
			})},
      {0xB6, new GBOpcode(0xB6, "RES 6,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Res(6, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0xB7, new GBOpcode(0xB7, "RES 6,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Res(6, gb.A);
					return true;
				},
			})},
      {0xB8, new GBOpcode(0xB8, "RES 7,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Res(7, gb.B);
					return true;
				},
			})},
      {0xB9, new GBOpcode(0xB9, "RES 7,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Res(7, gb.C);
					return true;
				},
			})},
      {0xBA, new GBOpcode(0xBA, "RES 7,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Res(7, gb.D);
					return true;
				},
			})},
      {0xBB, new GBOpcode(0xBB, "RES 7,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Res(7, gb.E);
					return true;
				},
			})},
      {0xBC, new GBOpcode(0xBC, "RES 7,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Res(7, gb.H);
					return true;
				},
			})},
      {0xBD, new GBOpcode(0xBD, "RES 7,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Res(7, gb.L);
					return true;
				},
			})},
      {0xBE, new GBOpcode(0xBE, "RES 7,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Res(7, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0xBF, new GBOpcode(0xBF, "RES 7,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Res(7, gb.A);
					return true;
				},
			})},
      {0xC0, new GBOpcode(0xC0, "SET 0,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Set(0, gb.B);
					return true;
				},
			})},
      {0xC1, new GBOpcode(0xC1, "SET 0,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Set(0, gb.C);
					return true;
				},
			})},
      {0xC2, new GBOpcode(0xC2, "SET 0,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Set(0, gb.D);
					return true;
				},
			})},
      {0xC3, new GBOpcode(0xC3, "SET 0,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Set(0, gb.E);
					return true;
				},
			})},
      {0xC4, new GBOpcode(0xC4, "SET 0,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Set(0, gb.H);
					return true;
				},
			})},
      {0xC5, new GBOpcode(0xC5, "SET 0,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Set(0, gb.L);
					return true;
				},
			})},
      {0xC6, new GBOpcode(0xC6, "SET 0,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Set(0, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0xC7, new GBOpcode(0xC7, "SET 0,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Set(0, gb.A);
					return true;
				},
			})},
      {0xC8, new GBOpcode(0xC8, "SET 1,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Set(1, gb.B);
					return true;
				},
			})},
      {0xC9, new GBOpcode(0xC9, "SET 1,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Set(1, gb.C);
					return true;
				},
			})},
      {0xCA, new GBOpcode(0xCA, "SET 1,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Set(1, gb.D);
					return true;
				},
			})},
      {0xCB, new GBOpcode(0xCB, "SET 1,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Set(1, gb.E);
					return true;
				},
			})},
      {0xCC, new GBOpcode(0xCC, "SET 1,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Set(1, gb.H);
					return true;
				},
			})},
      {0xCD, new GBOpcode(0xCD, "SET 1,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Set(1, gb.L);
					return true;
				},
			})},
      {0xCE, new GBOpcode(0xCE, "SET 1,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Set(1, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0xCF, new GBOpcode(0xCF, "SET 1,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Set(1, gb.A);
					return true;
				},
			})},
      {0xD0, new GBOpcode(0xD0, "SET 2,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Set(2, gb.B);
					return true;
				},
			})},
      {0xD1, new GBOpcode(0xD1, "SET 2,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Set(2, gb.C);
					return true;
				},
			})},
      {0xD2, new GBOpcode(0xD2, "SET 2,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Set(2, gb.D);
					return true;
				},
			})},
      {0xD3, new GBOpcode(0xD3, "SET 2,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Set(2, gb.E);
					return true;
				},
			})},
      {0xD4, new GBOpcode(0xD4, "SET 2,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Set(2, gb.H);
					return true;
				},
			})},
      {0xD5, new GBOpcode(0xD5, "SET 2,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Set(2, gb.L);
					return true;
				},
			})},
      {0xD6, new GBOpcode(0xD6, "SET 2,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Set(2, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0xD7, new GBOpcode(0xD7, "SET 2,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Set(2, gb.A);
					return true;
				},
			})},
      {0xD8, new GBOpcode(0xD8, "SET 3,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Set(3, gb.B);
					return true;
				},
			})},
      {0xD9, new GBOpcode(0xD9, "SET 3,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Set(3, gb.C);
					return true;
				},
			})},
      {0xDA, new GBOpcode(0xDA, "SET 3,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Set(3, gb.D);
					return true;
				},
			})},
      {0xDB, new GBOpcode(0xDB, "SET 3,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Set(3, gb.E);
					return true;
				},
			})},
      {0xDC, new GBOpcode(0xDC, "SET 3,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Set(3, gb.H);
					return true;
				},
			})},
      {0xDD, new GBOpcode(0xDD, "SET 3,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Set(3, gb.L);
					return true;
				},
			})},
      {0xDE, new GBOpcode(0xDE, "SET 3,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Set(3, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0xDF, new GBOpcode(0xDF, "SET 3,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Set(3, gb.A);
					return true;
				},
			})},
      {0xE0, new GBOpcode(0xE0, "SET 4,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Set(4, gb.B);
					return true;
				},
			})},
      {0xE1, new GBOpcode(0xE1, "SET 4,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Set(4, gb.C);
					return true;
				},
			})},
      {0xE2, new GBOpcode(0xE2, "SET 4,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Set(4, gb.D);
					return true;
				},
			})},
      {0xE3, new GBOpcode(0xE3, "SET 4,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Set(4, gb.E);
					return true;
				},
			})},
      {0xE4, new GBOpcode(0xE4, "SET 4,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Set(4, gb.H);
					return true;
				},
			})},
      {0xE5, new GBOpcode(0xE5, "SET 4,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Set(4, gb.L);
					return true;
				},
			})},
      {0xE6, new GBOpcode(0xE6, "SET 4,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Set(4, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0xE7, new GBOpcode(0xE7, "SET 4,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Set(4, gb.A);
					return true;
				},
			})},
      {0xE8, new GBOpcode(0xE8, "SET 5,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Set(5, gb.B);
					return true;
				},
			})},
      {0xE9, new GBOpcode(0xE9, "SET 5,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Set(5, gb.C);
					return true;
				},
			})},
      {0xEA, new GBOpcode(0xEA, "SET 5,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Set(5, gb.D);
					return true;
				},
			})},
      {0xEB, new GBOpcode(0xEB, "SET 5,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Set(5, gb.E);
					return true;
				},
			})},
      {0xEC, new GBOpcode(0xEC, "SET 5,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Set(5, gb.H);
					return true;
				},
			})},
      {0xED, new GBOpcode(0xED, "SET 5,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Set(5, gb.L);
					return true;
				},
			})},
      {0xEE, new GBOpcode(0xEE, "SET 5,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Set(5, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0xEF, new GBOpcode(0xEF, "SET 5,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Set(5, gb.A);
					return true;
				},
			})},
      {0xF0, new GBOpcode(0xF0, "SET 6,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Set(6, gb.B);
					return true;
				},
			})},
      {0xF1, new GBOpcode(0xF1, "SET 6,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Set(6, gb.C);
					return true;
				},
			})},
      {0xF2, new GBOpcode(0xF2, "SET 6,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Set(6, gb.D);
					return true;
				},
			})},
      {0xF3, new GBOpcode(0xF3, "SET 6,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Set(6, gb.E);
					return true;
				},
			})},
      {0xF4, new GBOpcode(0xF4, "SET 6,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Set(6, gb.H);
					return true;
				},
			})},
      {0xF5, new GBOpcode(0xF5, "SET 6,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Set(6, gb.L);
					return true;
				},
			})},
      {0xF6, new GBOpcode(0xF6, "SET 6,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Set(6, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0xF7, new GBOpcode(0xF7, "SET 6,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Set(6, gb.A);
					return true;
				},
			})},
      {0xF8, new GBOpcode(0xF8, "SET 7,B",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.B = Set(7, gb.B);
					return true;
				},
			})},
      {0xF9, new GBOpcode(0xF9, "SET 7,C",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.C = Set(7, gb.C);
					return true;
				},
			})},
      {0xFA, new GBOpcode(0xFA, "SET 7,D",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.D = Set(7, gb.D);
					return true;
				},
			})},
      {0xFB, new GBOpcode(0xFB, "SET 7,E",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.E = Set(7, gb.E);
					return true;
				},
			})},
      {0xFC, new GBOpcode(0xFC, "SET 7,H",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.H = Set(7, gb.H);
					return true;
				},
			})},
      {0xFD, new GBOpcode(0xFD, "SET 7,L",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.L = Set(7, gb.L);
					return true;
				},
			})},
      {0xFE, new GBOpcode(0xFE, "SET 7,(HL)",2,16,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					value = Set(7, gb._memory.ReadByte(gb.HL));
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteByte(gb.HL, (byte)value);
					return true;
				},
			})},
      {0xFF, new GBOpcode(0xFF, "SET 7,A",2,8,new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.A = Set(7, gb.A);
					return true;
				},
			})},
    };

		private static byte Rlc(Gameboy gb, byte value)
		{
			var result = (byte)((value << 1) | (value >> 7));
			gb.Z = result == 0;
			gb.N = false;
			gb.HC = false;
			gb.CF = (value & 0x80) == 0x80;
			return result;
		}

		private static byte Rrc(Gameboy gb, byte value)
		{
			var result = (byte)((value >> 1) | (value << 7));
			gb.Z = result == 0;
			gb.N = false;
			gb.HC = false;
			gb.CF = (value & 0x01) == 0x01;
			return result;
		}

		private static byte Rl(Gameboy gb, byte value)
		{
			var result = (byte)((value << 1) | (gb.CF ? 1 : 0));
			gb.Z = result == 0;
			gb.N = false;
			gb.HC = false;
			gb.CF = (value & 0x80) == 0x80;
			return result;
		}

		private static byte Rr(Gameboy gb, byte value)
		{
			var result = (byte)((value >> 1) | (gb.CF ? 0x80 : 0x00));
			gb.Z = result == 0;
			gb.N = false;
			gb.HC = false;
			gb.CF = (value & 0x01) == 0x01;
			return result;
		}

		// set bit in value
		private static byte Set(int bitPosition, byte value)
		{
			value |= (byte)(1 << bitPosition);
			return value;
		}

		// reset bit in value
		private static byte Res(int bitPosition, byte value)
		{
			value &= (byte)~(1 << bitPosition);
			return value;
		}

		private static void Bit(Gameboy gb, int bitPosition, byte value)
		{
			gb.Z = (value & (1 << bitPosition)) == 0;
			gb.N = false;
			gb.HC = true;
		}

		private static byte Srl(Gameboy gb, byte b)
		{
			byte result = (byte)(b >> 1);
			gb.Z = result == 0;
			gb.N = false;
			gb.HC = false;
			gb.CF = (b & 0x01) != 0x00;
			return result;
		}

		private static byte Swap(Gameboy gb, byte b)
		{
			b = (byte)((b >> 4) | (b << 4));
			gb.Z = b == 0;
			gb.N = false;
			gb.HC = false;
			gb.CF = false;
			return b;
		}

		private static byte Sra(Gameboy gb, byte b)
		{
			gb.N = false;
			gb.HC = false;
			gb.CF = (b & 0x01) > 0;
			b = (byte)((b >> 1) | (b & 0x80));
			gb.Z = b == 0;
			return b;
		}

		private static byte Sla(Gameboy gb, byte b)
		{
			gb.N = false;
			gb.HC = false;
			gb.CF = (b & 0x80) == 0x80;
			b = (byte)(b << 1);
			gb.Z = b == 0;
			return b;
		}
	}
}
