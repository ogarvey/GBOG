namespace GBOG.Graphics
{
	public enum Interrupt : byte
	{
		VBlank = 0x00,  // V-Blank Interrupt, bit 0
		LCDStat = 0x01, // LCD STAT Interrupt, bit 1
		Timer = 0x02,   // Timer Interrupt, bit 2
		Serial = 0x03,  // Serial Interrupt, bit 3
		Joypad = 0x04   // Joypad Interrupt, bit 4
	}
}