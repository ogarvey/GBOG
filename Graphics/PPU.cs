namespace GBOG.Graphics
{
	// The PPU (which stands for Picture Processing Unit) is the part of the Gameboy that’s responsible for everything you see on screen.
	internal class PPU
	{
		public const byte MODE_HBLANK = 0;
		public const byte MODE_VBLANK = 1;
		public const byte MODE_SCANLINE_OAM = 2;
		public const byte MODE_SCANLINE_VRAM = 3;

		// for the STATUS register
		private const byte _STATUS_COINCIDENCE_BITPOS = 2;
		private const byte _STATUS_HBLANK_BITPOS = 3;
		private const byte _STATUS_VBLANK_BITPOS = 4;
		private const byte _STATUS_SCANLINE_OAM_BITPOS = 5;
		private const byte _STATUS_LC_EQUALS_LYC = 6;

		public int modeTicks;
		public int lineTicks;
		public int enableDelay = 0;
		public bool wasDisabled = false;

		private readonly byte[] frameBuffer = new byte[160 * 144 * 4];

		private readonly byte modeMask = 0x03;
	}
}
