using Microsoft.Xna.Framework.Graphics;
using SFML.Audio;
using SFML.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.DataFormats;
using System.Windows.Forms.Design.Behavior;
using GBOG.CPU;
using static System.Windows.Forms.AxHost;

namespace GBOG.Graphics
{
	// The PPU (which stands for Picture Processing Unit) is the part of the Gameboy that’s responsible for everything you see on screen.
	internal class PPU
	{
		private byte[] _spriteFIFO;
		private byte[] _bgFIFO;

		private void OAMScan()
		{
			// The OAM (Object Attribute Memory) is a part of the Gameboy’s memory that stores information about the sprites that are currently on screen.
			// The OAM is 160 bytes in size, and each sprite takes up 4 bytes of space in the OAM.
			// The first byte is the sprite’s Y position on screen, the second byte is the sprite’s X position on screen,
			// the third byte is the sprite’s tile number, and the fourth byte is a bit field that contains information about the sprite.
			// The first bit of the fourth byte is the sprite’s priority, the second bit is the sprite’s Y flip,
			// the third bit is the sprite’s X flip, and the fourth bit is the sprite’s palette number.
			// The remaining four bits are unused.
			// The OAM is scanned 40 times per frame, and each scanline can display up to 10 sprites.
			// The OAM is scanned in order, and the first 10 sprites that are found are displayed.
			// If more than 10 sprites are found, the remaining sprites are not displayed.
			// This mode (Mode 2) is entered at the start of every scanline(except for V - Blank) before pixels are actually drawn to the screen.
			// During this mode the PPU searches OAM memory for sprites that should be rendered on the current scanline and stores them in a buffer.
			// This procedure takes a total amount of 80 T - Cycles, meaning that the PPU checks a new OAM entry every 2 T - Cycles.

			// A sprite is only added to the buffer if all of the following conditions apply:

			// Sprite X-Position must be greater than 0
			// LY + 16 must be greater than or equal to Sprite Y - Position
			// LY + 16 must be less than Sprite Y-Position + Sprite Height(8 in Normal Mode, 16 in Tall - Sprite - Mode)
			// The amount of sprites already stored in the OAM Buffer must be less than 10
		}

		private void Draw()
		{
			// The Drawing (Mode 3) is where the PPU transfers pixels to the LCD.
			// The duration of this mode changes depending on multiple variables, such as background scrolling,
			// the amount of sprites on the scanline, whether or not the window should be rendered, etc.
		}

		private void HBlank()
		{
			// This mode (Mode 0) takes up the remainder of the scanline after the Drawing Mode finishes,
			// more or less “padding” the duration of the scanline to a total of 456 T-Cycles.
			// The PPU effectively pauses during this mode.
		}

		private void VBlank()
		{
			// V - Blank mode is the same as H - Blank in the way that the PPU does not draw any pixels to the LCD during its duration.
			// However, instead of it taking place at the end of every scanline, it’s a much longer period at the end of every frame.

			// As the Gameboy has a vertical resolution of 144 pixels, it would be expected that the amount of scanlines the PPU handles would be equal
			// - 144 scanlines. However, this is not the case.In reality there are 154 scanlines, the last 10 of which are “pseudo - scanlines”
			// during which no pixels are drawn as the PPU is in the V-Blank state during their duration.
			// A V-Blank scanline takes the same amount of time as any other scanline -456 T - Cycles.
		}
	}
}
