namespace GBOG.Graphics
{
	public class SpriteAttributes
	{
		public int XPosition { get; private set; }
		public int YPosition { get; private set; }
		public int OamIndex { get; private set; }
		public byte TileNumber { get; private set; }
		public bool Priority { get; private set; }
		public bool YFlip { get; private set; }
		public bool XFlip { get; private set; }
		public byte PaletteNumber { get; private set; }
		public byte Flags {	get; private set;}

		public SpriteAttributes(int xPosition, int yPosition, byte tileNumber, byte flags, int oamIndex)
		{
			XPosition = xPosition;
			YPosition = yPosition;
			OamIndex = oamIndex;
			TileNumber = tileNumber;
			Flags = flags;
			Priority = (flags & 0x80) == 0x80;
			YFlip = (flags & 0x40) == 0x40;
			XFlip = (flags & 0x20) == 0x20;
			PaletteNumber = (byte)(flags & 0x10);
		}
	}
}
