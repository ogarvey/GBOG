using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using Color = Microsoft.Xna.Framework.Color;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;
using GBOG.CPU;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using System.Drawing;

namespace GBOG.Graphics.MonoGame
{
	internal class GameboyGame : Game
	{
		private GraphicsDeviceManager _graphics;
		private SpriteBatch _spriteBatch;
		private Gameboy _gb;
		private Texture2D _gameboyBuffer;
		private byte[] _backbuffer;

		public GameboyGame(Gameboy gb)
		{
			_gb = gb;
			_graphics = new GraphicsDeviceManager(this);
			Content.RootDirectory = "Content";
			IsMouseVisible = true;
			_graphics.PreferredBackBufferWidth = 800;
			_graphics.PreferredBackBufferHeight = 720;
			_graphics.ApplyChanges();
		}

		protected override void Initialize()
		{
			// TODO: Add your initialization logic here

			base.Initialize();
		}

		protected override void LoadContent()
		{
			_spriteBatch = new SpriteBatch(GraphicsDevice);
			_gameboyBuffer = new Texture2D(GraphicsDevice, 160, 144);

			// TODO: use this.Content to load your game content here
		}

		protected override void Update(GameTime gameTime)
		{
			if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
			{
				_gb.EndGame();
				Exit();
			}

			// TODO: Add your update logic here
			_backbuffer = _gb.GetDisplayArray();

			if (_backbuffer != null)
			{
				_gameboyBuffer.SetData(_backbuffer);
			}
			base.Update(gameTime);
		}

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(Color.CornflowerBlue);

			// compute bounds
			Microsoft.Xna.Framework.Rectangle bounds = GraphicsDevice.Viewport.Bounds;

			float aspectRatio = GraphicsDevice.Viewport.Bounds.Width / (float)GraphicsDevice.Viewport.Bounds.Height;
			float targetAspectRatio = 160.0f / 144.0f;

			if (aspectRatio > targetAspectRatio)
			{
				int targetWidth = (int)(bounds.Height * targetAspectRatio);
				bounds.X = (bounds.Width - targetWidth) / 2;
				bounds.Width = targetWidth;
			}
			else if (aspectRatio < targetAspectRatio)
			{
				int targetHeight = (int)(bounds.Width / targetAspectRatio);
				bounds.Y = (bounds.Height - targetHeight) / 2;
				bounds.Height = targetHeight;
			}

			// draw backbuffer
			_spriteBatch.Begin(samplerState: SamplerState.PointClamp);
			_spriteBatch.Draw(_gameboyBuffer, bounds, Color.White);
			_spriteBatch.End();

			base.Draw(gameTime);
		}
	}
}
