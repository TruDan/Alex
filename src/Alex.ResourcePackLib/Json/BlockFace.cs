﻿using System;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Alex.ResourcePackLib.Json
{
	[JsonConverter(typeof(StringEnumConverter))]
	public enum BlockFace : int
	{
		Down = 0,
		Up = 1,
		East = 2,
		West = 3,
		North = 4,
		South = 5,
		None = 255,
	}

	public static class BlockFaceHelper
	{
		public static Vector3 GetVector3(this BlockFace face)
		{
			switch (face)
			{
				case BlockFace.Down:
					return Vector3.Down;
				case BlockFace.Up:
					return Vector3.Up;
				case BlockFace.East:
					return Vector3.Right;
				case BlockFace.West:
					return Vector3.Left;
				case BlockFace.North:
					return Vector3.Forward;
				case BlockFace.South:
					return Vector3.Backward;
				default:
					return Vector3.Zero;
			}
		}
	}
}
