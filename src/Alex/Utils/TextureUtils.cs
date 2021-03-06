﻿using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace Alex.Utils
{
	public static class TextureUtils
	{
		public static Texture2D BitmapToTexture2D(GraphicsDevice device, Bitmap bmp)
		{
			uint[] imgData = new uint[bmp.Width * bmp.Height];
			Texture2D texture = new Texture2D(device, bmp.Width, bmp.Height);

			unsafe
			{
				BitmapData origdata =
					bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, bmp.PixelFormat);

				uint* byteData = (uint*)origdata.Scan0;

				for (int i = 0; i < imgData.Length; i++)
				{
					var val = byteData[i];
					imgData[i] = (val & 0x000000FF) << 16 | (val & 0x0000FF00) | (val & 0x00FF0000) >> 16 | (val & 0xFF000000);
				}

				byteData = null;

				bmp.UnlockBits(origdata);
			}

			texture.SetData(imgData);

			return texture;
		}

		public static Texture2D ImageToTexture2D(GraphicsDevice device, Image bmp)
		{
			var image = new Bitmap(new Bitmap(bmp));
			return BitmapToTexture2D(device, image);
		}

		public static Texture2D ImageToTexture2D(GraphicsDevice device, byte[] bmp)
		{
			using (MemoryStream s = new MemoryStream(bmp))
			{
				var image = new Bitmap(new Bitmap(s));
				return BitmapToTexture2D(device, image);
			}
		}
	}
}
