namespace GBOG.CPU.Opcodes
{
	public static class ControlOpcodes
	{
		private static int value;
		private static ushort address;
		
		public static Dictionary<byte, GBOpcode> MiscOpcodes = new Dictionary<byte, GBOpcode>()
		{
			{ 0x00, new GBOpcode(0x00, "NOP", 1, 4, new Step[] {(Gameboy gb) =>{ return true;}}) },
			{ 0x10, new GBOpcode(0x10, "STOP", 1, 4, new Step[] {(Gameboy gb) =>{ return true; } }) },
			{ 0x76, new GBOpcode(0x76, "HALT", 1, 4, new Step[] {(Gameboy gb) => {gb.Halt = true; return true; } }) },
			{ 0xF3, new GBOpcode(0xF3, "DI", 1, 4,  new Step[] {(Gameboy gb) =>
							{gb.InterruptMasterEnabled = false; return true;}}) },
			{ 0xFB, new GBOpcode(0xFB, "EI", 1, 4,  new Step[] {(Gameboy gb) =>
							{gb.InterruptMasterEnabled = true;return true;}}) },
		};

		public static Dictionary<byte, GBOpcode> BROpcodes = new Dictionary<byte, GBOpcode>()
		{
			{0x18, new GBOpcode(0x18, "JR {0:x2}", 2, 12, new Step[] {
				(Gameboy gb) => {
					value = gb._memory.ReadSByte(gb.PC);
					if (value > 127) value =-((~value + 1) & 0xFF);
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					gb.PC += (ushort)value;
					return true;
				}
			})},
			{0x20, new GBOpcode(0x20, "JR NZ, {0:x2}", 2, 12, new Step[] {
				(Gameboy gb) => {
					value = gb._memory.ReadSByte(gb.PC);
					if (value > 127) value =-((~value + 1) & 0xFF);
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					if (!gb.Z) {
						gb.PC += (ushort)value;
						return true;
					}
					return false;
				}
			})},
			{0x28, new GBOpcode(0x28, "JR Z, {0:x2}", 2, 12, new Step[] {
				(Gameboy gb) => {
					value = gb._memory.ReadSByte(gb.PC);
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					if (gb.Z) {
						gb.PC += (ushort)value;
						return true;
					}
					else
					{
						return false;
					}
				}
			})},
			{0x30, new GBOpcode(0x30, "JR NC, {0:x2}", 2, 12, new Step[] {
				(Gameboy gb) => {
					value = gb._memory.ReadSByte(gb.PC);
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					if (!gb.CF) {
						gb.PC += (ushort)value;
						return true;
					}
					else
					{
						return false;
					}
				}
			})},
			{0x38, new GBOpcode(0x38, "JR C, {0:x2}", 2, 12, new Step[] {
				(Gameboy gb) => {
					value = gb._memory.ReadSByte(gb.PC);
					return true;
				},
				(Gameboy gb) => {
					gb.PC += 1;
					return true;
				},
				(Gameboy gb) => {
					if (gb.CF) {
						gb.PC += (ushort)value;
						return true;
					}
					else
					{
						return false;
					}
				}
			})},
			{0xC0, new GBOpcode(0xC0, "RET NZ", 1, 20, new Step[] {
				(Gameboy gb) => true,
				(Gameboy gb) => {
					value = gb._memory.ReadUShort(gb.SP);
					return true;
				},
				(Gameboy gb) => {
					if (!gb.Z) {
						gb.SP +=1;
						return true;
					}
					return false;
				},
				(Gameboy gb) => {
					gb.SP +=1;
					return true;
				},
				(Gameboy gb) => {
						gb.PC = (ushort)value;
						return true;
				}
			})},
			{0xC2, new GBOpcode(0xC2, "JP NZ, {0:x4}", 3, 16, new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					if (!gb.Z) {
						gb.PC = gb._memory.ReadUShort(gb.PC);
						return true;
					}
					return false;
				},
			})},
			{0xC3, new GBOpcode(0xC3, "JP {0:x4}", 3, 16, new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
						gb.PC = gb._memory.ReadUShort(gb.PC);
					return true;
				},
			})},
			{0xC4, new GBOpcode(0xC4, "CALL NZ, {0:x4}", 3, 24, new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					address = gb._memory.ReadUShort(gb.PC);
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					if (!gb.Z) {
						gb.SP -= 1;
						return true;
					}
					gb.PC+=2;
					return false;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb.PC = address;
					return true;
				},
			})},
			{0xC7, new GBOpcode(0xC7, "RST 00H", 1, 16, new Step[] {
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 2;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, gb.PC);
					return true;
				},
				(Gameboy gb) => {
					gb.PC = 0x00;
					return true;
				},
			})},
			{0xC8, new GBOpcode(0xC8, "RET Z", 1, 20, new Step[] {
				(Gameboy gb) => {
						return true;
				},
				(Gameboy gb) => {
					gb._memory.ReadUShort(gb.SP);
					return true;
				},
				(Gameboy gb) => {
					if (gb.Z) {
						gb.SP +=1;
						return true;
					}
					return false;
				},
				(Gameboy gb) => {
						gb.SP +=1;
						return true;
				},
				(Gameboy gb) => {
						gb.PC = address;
						return true;
				},
			})},
			{0xC9, new GBOpcode(0xC9, "RET", 1, 16, new Step[] {
				(Gameboy gb) => {
						value = gb._memory.ReadUShort(gb.SP);
						return true;
				},
				(Gameboy gb) => {
					gb.SP +=1;
						return true;
				},
				(Gameboy gb) => {
					gb.SP +=1;
						return true;
				},
				(Gameboy gb) => {
						gb.PC = (ushort)value;
						return true;
				},
			})},
			{0xCA, new GBOpcode(0xCA, "JP Z, {0:x4}", 3, 16, new Step[]
			{
				(Gameboy gb) => {
						address = gb._memory.ReadUShort(gb.PC);
						return true;
				},
				(Gameboy gb) => {
					gb.PC+=1;
						return true;
				},
				(Gameboy gb) => {
					gb.PC+=1;
						return true;
				},
				(Gameboy gb) => {
					if (gb.Z) {
						gb.PC = address;
						return true;
					}
					return false;
				},
			})},
			{0xCC, new GBOpcode(0xCC, "CALL Z, {0:x4}", 3, 24, new Step[]
			{
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					address = gb._memory.ReadUShort(gb.PC);
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					if(gb.Z)
					{
						gb.SP -= 1;
						return true;
					}
					return false;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					gb._memory.WriteUShort(gb.SP, (ushort)(gb.PC + 2));
					return true;
				},
				(Gameboy gb) => {
						gb.PC = address;
						return true;
				},
			})},
			{0xCD, new GBOpcode(0xCD, "CALL {0:x4}", 3, 24, new Step[]
			{
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					address = gb._memory.ReadUShort(gb.PC);
					return true;
				},
				(Gameboy gb) => {
				gb.PC += 2;
					return true;
				},
				(Gameboy gb) => {
				gb.SP -= 2;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, gb.PC);
					return true;
				},
				(Gameboy gb) => {
					gb.PC = address;
					return true;
				},
			})},
			{0xCF, new GBOpcode(0xCF, "RST 08H", 1, 16, new Step[]
			{
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 2;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, gb.PC);
					return true;
				},
				(Gameboy gb) => {
					gb.PC = 0x08;
					return true;
				},
			})},
			{0xD0, new GBOpcode(0xD0, "RET NC", 1, 20, new Step[] {
				(Gameboy gb) => {
						return true;
				},
				(Gameboy gb) => {
						value = gb._memory.ReadUShort(gb.SP);
						return true;
				},
				(Gameboy gb) => {
					if (!gb.CF) {
						gb.SP +=1;
						return true;
					}
					return false;
				},
				(Gameboy gb) => {
					gb.SP +=1;
						return true;
				},
				(Gameboy gb) => {
						gb.PC = (ushort)value;
						return true;
				},
			})},
			{0xD2, new GBOpcode(0xD2, "JP NC, {0:x4}", 3, 16, new Step[]
			{
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
					if(!gb.CF) {
						gb.PC = address;
						return true;
					}
					return false;
				},
			})},
			{0xD4, new GBOpcode(0xD4, "CALL NC, {0:x4}", 3, 24, new Step[]
			{
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					gb.SP -=1;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -=1;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, (ushort)(gb.PC + 2));
					return true;
				},
				(Gameboy gb) => {
					if(!gb.CF) {
						address = gb._memory.ReadUShort(gb.PC);
						return true;
					}
					gb.PC += 2;
					return false;
				},
				(Gameboy gb) => {
					gb.PC = address;
					return true;
				},
			})},
			{0xD7, new GBOpcode(0xD7, "RST 10H", 1, 16, new Step[]
			{
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, gb.PC);
					return true;
				},
				(Gameboy gb) => {
					gb.PC = 0x10;
					return true;
				},
			})},
			{0xD8, new GBOpcode(0xD8, "RET C", 1, 20, new Step[]
			{
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					return true;
				},
				(Gameboy gb) => {
					if(gb.CF) {
						gb.PC = gb._memory.ReadUShort(gb.SP);
						return true;
					}
					return false;
				},
				(Gameboy gb) => {
					gb.SP+=1;
					return true;
				},
				(Gameboy gb) => {
					gb.SP+=1;
					return true;
				},
			})},
			{0xD9, new GBOpcode(0xD9, "RETI", 1, 16, new Step[]
			{
				(Gameboy gb) => {
					address = gb._memory.ReadUShort(gb.SP);
					return true;
				},
				(Gameboy gb) => {
					gb.SP += 1;
					return true;
				},
				(Gameboy gb) => {
					gb.SP += 1;
					return true;
				},
				(Gameboy gb) => {
					gb.PC = address;
					gb.InterruptMasterEnabled = true;
					return true;
				},
			})},
			{0xDA, new GBOpcode(0xDA, "JP C, {0:x4}", 3, 16, new Step[]
			{
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
					if (gb.CF) {
						gb.PC = address;
						return true;
					}
					return false;
				},
			})},
			{0xDC, new GBOpcode(0xDC, "CALL C, {0:x4}", 3, 24, new Step[]
			{
				(Gameboy gb) => {
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
					if (gb.CF) {
						address = gb._memory.ReadUShort(gb.PC);
						return true;
					}
					gb.PC += 2;
					return false;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, (ushort)(gb.PC + 2));
					return true;
				},
				(Gameboy gb) => {
					gb.PC = address;
					return true;
				},
			})},
			{0xDF, new GBOpcode(0xDF, "RST 18H", 1, 16, new Step[]
			{
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, gb.PC);
					return true;
				},
				(Gameboy gb) => {
					gb.PC = 0x18;
					return true;
				},
			})},
			{0xE7, new GBOpcode(0xE7, "RST 20H", 1, 16, new Step[]
			{
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, gb.PC);
					return true;
				},
				(Gameboy gb) => {
					gb.PC = 0x20;
					return true;
				},
			})},
			{0xE9, new GBOpcode(0xE9, "JP HL", 1, 4, new Step[]
			{
				(Gameboy gb) => {
					gb.PC = gb.HL;
					return true;
				},
			})},
			{0xEF, new GBOpcode(0xEF, "RST 28H", 1, 16, new Step[]
			{
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, gb.PC);
					return true;
				},
				(Gameboy gb) => {
					gb.PC = 0x28;
					return true;
				},
			})},
			{0xF7, new GBOpcode(0xF7, "RST 30H", 1, 16, new Step[]
			{
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, gb.PC);
					return true;
				},
				(Gameboy gb) => {
					gb.PC = 0x30;
					return true;
				},
			})},
			{0xFF, new GBOpcode(0xFF, "RST 38H", 1, 16, new Step[]
			{
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb.SP -= 1;
					return true;
				},
				(Gameboy gb) => {
					gb._memory.WriteUShort(gb.SP, gb.PC);
					return true;
				},
				(Gameboy gb) => {
					gb.PC = 0x38;
					return true;
				},
			})}
		};
	}
}
