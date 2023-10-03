namespace GBOG.Graphics
{
	public class SpriteAttributes
	{
		public byte XPosition { get; private set; }
		public byte YPosition { get; private set; }
		public byte TileNumber { get; private set; }
		public bool Priority { get; private set; }
		public bool YFlip { get; private set; }
		public bool XFlip { get; private set; }
		public byte PaletteNumber { get; private set; }
		public byte Flags {	get; private set;}

		public SpriteAttributes(byte xPosition, byte yPosition, byte tileNumber, byte flags)
		{
			XPosition = xPosition;
			YPosition = yPosition;
			TileNumber = tileNumber;
			Flags = flags;
			Priority = (flags & 0x80) == 0x80;
			YFlip = (flags & 0x40) == 0x40;
			XFlip = (flags & 0x20) == 0x20;
			PaletteNumber = (byte)(flags & 0x10);
		}
	}
}
