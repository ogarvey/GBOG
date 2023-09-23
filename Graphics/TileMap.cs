using SFML.Graphics;
using SFML.System;

namespace GBOG.Graphics
{
	public class TileMap : Transformable, Drawable
	{
		private Texture? _tileset;
		private VertexArray? _vertices;
		private uint _width;
		private uint _height;
		private Vector2u _tileSize;

		public bool Load(string tilesetPath, Vector2u tileSize, byte[] tiles, uint width, uint height)
		{
			// load the tileset texture
			_tileset = new Texture(tilesetPath);
			_tileSize = tileSize;

			// resize the vertex array to fit the level size
			_vertices = new VertexArray(PrimitiveType.Quads, width * height * 4);
			_width =  width;
			_height = height;

			// populate the vertex array, with one quad per tile
			for (uint i = 0; i < width; ++i)
			{
				for (uint j = 0; j < height; ++j)
				{
					// get the current tile number
					byte tileNumber = tiles[i + j * width];

					// find its position in the tileset texture
					uint tu = tileNumber % (_tileset.Size.X / tileSize.X);
					uint tv = tileNumber / (_tileset.Size.X / tileSize.X);

					// get a pointer to the current tile's quad
					var position = (i + j * width) * 4;

					// define its 4 corners
					_vertices[position] = new Vertex(new Vector2f(i * tileSize.X, j * tileSize.Y), new Vector2f(tu * tileSize.X, tv * tileSize.Y));
					_vertices[position + 1] = new Vertex(new Vector2f((i + 1) * tileSize.X, j * tileSize.Y), new Vector2f((tu + 1) * tileSize.X, tv * tileSize.Y));
					_vertices[position + 2] = new Vertex(new Vector2f((i + 1) * tileSize.X, (j + 1) * tileSize.Y), new Vector2f((tu + 1) * tileSize.X, (tv + 1) * tileSize.Y));
					_vertices[position + 3] = new Vertex(new Vector2f(i * tileSize.X, (j + 1) * tileSize.Y), new Vector2f(tu * tileSize.X, (tv + 1) * tileSize.Y));
				}
			}

			return true;
		}

		public void Update(byte[] newPixels)
		{
			// repopulate the vertex array, with one quad per tile
			for (uint i = 0; i < _width; ++i)
			{
				for (uint j = 0; j < _height; ++j)
				{
					// get the current tile number
					byte tileNumber = newPixels[i + j * _width];

					// find its position in the tileset texture
					uint tu = tileNumber % (_tileset.Size.X / _tileSize.X);
					uint tv = tileNumber / (_tileset.Size.X / _tileSize.X);

					// get a pointer to the current tile's quad
					var position = (i + j * _width) * 4;

					_vertices = new VertexArray(PrimitiveType.Quads, _width * _height * 4);
					// define its 4 corners
					_vertices[position] = new Vertex(new Vector2f(i * _tileSize.X, j * _tileSize.Y), new Vector2f(tu * _tileSize.X, tv * _tileSize.Y));
					_vertices[position + 1] = new Vertex(new Vector2f((i + 1) * _tileSize.X, j * _tileSize.Y), new Vector2f((tu + 1) * _tileSize.X, tv * _tileSize.Y));
					_vertices[position + 2] = new Vertex(new Vector2f((i + 1) * _tileSize.X, (j + 1) * _tileSize.Y), new Vector2f((tu + 1) * _tileSize.X, (tv + 1) * _tileSize.Y));
					_vertices[position + 3] = new Vertex(new Vector2f(i * _tileSize.X, (j + 1) * _tileSize.Y), new Vector2f(tu * _tileSize.X, (tv + 1) * _tileSize.Y));
				}
			}
		}

		void Drawable.Draw(RenderTarget target, RenderStates states)
		{
			// apply the transform
			states.Transform *= Transform;

			// apply the tileset texture
			states.Texture = _tileset;

			// draw the vertex array
			target.Draw(_vertices, states);
		}
	}
}
