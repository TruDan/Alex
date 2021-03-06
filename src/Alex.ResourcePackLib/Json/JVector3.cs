﻿using Alex.ResourcePackLib.Json.Converters;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;

namespace Alex.ResourcePackLib.Json
{
	[JsonConverter(typeof(JVector3Converter))]
	public class JVector3
	{
		public static JVector3 Zero => new JVector3(0, 0, 0);
		public static JVector3 UnitX => new JVector3(1, 0, 0);
		public static JVector3 UnitZ => new JVector3(0, 0, 1);
		public static JVector3 UnitY => new JVector3(0, 1, 0);

		public static JVector3 Up => new JVector3(0, 1, 0);
		public static JVector3 Down => new JVector3(0, -1, 0);

		public static JVector3 North => new JVector3(0, 0, -1);
		public static JVector3 East => new JVector3(1, 0, 0);
		public static JVector3 South => new JVector3(0, 0, 1);
		public static JVector3 West => new JVector3(-1, 0, 0);

		public float X { get; set; }
		public float Y { get; set; }
		public float Z { get; set; }

		public JVector3()
		{

		}

		/*public JVector3(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}*/

		public JVector3(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public JVector3(JVector3 v) : this(v.X, v.Y, v.Z)
		{ }

		public static JVector3 FromBlockFace(BlockFace face)
		{
			switch (face)
			{
				case BlockFace.Down:
					return Down;
				case BlockFace.Up:
					return Up;
				case BlockFace.East:
					return East;
				case BlockFace.West:
					return West;
				case BlockFace.North:
					return North;
				case BlockFace.South:
					return South;
					default:
					return Zero;
			}
		}

		public static explicit operator Vector3(JVector3 b)
		{
			return new Vector3((float) b.X, (float) b.Y, (float) b.Z);
		}

		public static Vector3 operator +(JVector3 c1, Vector3 c2)
		{
			c2.X += c1.X;
			c2.Y += c1.Y;
			c2.Z += c1.Z;

			return c2;
		}

		public static Vector3 operator -(JVector3 c1, Vector3 c2)
		{
			c2.X -= c1.X;
			c2.Y -= c1.Y;
			c2.Z -= c1.Z;

			return c2;
		}

		public static Vector3 operator *(JVector3 c1, Vector3 c2)
		{
			c2.X *= c1.X;
			c2.Y *= c1.Y;
			c2.Z *= c1.Z;

			return c2;
		}

		public static Vector3 operator /(JVector3 c1, Vector3 c2)
		{
			c2.X /= c1.X;
			c2.Y /= c1.Y;
			c2.Z /= c1.Z;

			return c2;
		}
	}

	[JsonConverter(typeof(JVector2Converter))]
	public class JVector2
	{
		public static JVector2 Zero => new JVector2(0, 0);
		public static JVector2 UnitX => new JVector2(1, 0);
		public static JVector2 UnitY => new JVector2(0, 1);

		public float X { get; set; }
		public float Y { get; set; }

		public JVector2()
		{

		}

		/*public JVector2(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}*/

		public JVector2(float x, float y)
		{
			X = x;
			Y = y;
		}

		public JVector2(JVector2 v) : this(v.X, v.Y)
		{ }

		

		public static explicit operator Vector2(JVector2 b)
		{
			return new Vector2((float)b.X, (float)b.Y);
		}

		public static Vector2 operator +(JVector2 c1, Vector2 c2)
		{
			c2.X += c1.X;
			c2.Y += c1.Y;

			return c2;
		}

		public static Vector3 operator -(JVector2 c1, Vector3 c2)
		{
			c2.X -= c1.X;
			c2.Y -= c1.Y;

			return c2;
		}

		public static Vector2 operator *(JVector2 c1, Vector2 c2)
		{
			c2.X *= c1.X;
			c2.Y *= c1.Y;

			return c2;
		}

		public static Vector2 operator /(JVector2 c1, Vector2 c2)
		{
			c2.X /= c1.X;
			c2.Y /= c1.Y;

			return c2;
		}
	}
}
