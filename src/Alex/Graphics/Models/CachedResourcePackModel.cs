﻿using System.Collections.Generic;
using System.Linq;
using Alex.API.Graphics;
using Alex.API.World;
using Alex.Blocks;
using Alex.ResourcePackLib.Json;
using Alex.ResourcePackLib.Json.BlockStates;
using Alex.Utils;
using Alex.Worlds;
using log4net;
using Microsoft.Xna.Framework;
using MiNET.Utils;
using MiNET.Worlds;
using Axis = Alex.ResourcePackLib.Json.Axis;

namespace Alex.Graphics.Models
{
	public class CachedResourcePackModel : ResourcePackModel
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(CachedResourcePackModel));
		static CachedResourcePackModel()
		{
			
		}

		public CachedResourcePackModel(ResourceManager resources, BlockStateModel[] variant) : base(resources, variant)
		{
			Cache();
			
		}

		private class FaceCache
		{
			public VertexPositionNormalTextureColor[] Up;
			public VertexPositionNormalTextureColor[] Down;
			public VertexPositionNormalTextureColor[] West;
			public VertexPositionNormalTextureColor[] East;
			public VertexPositionNormalTextureColor[] South;
			public VertexPositionNormalTextureColor[] North;
			public VertexPositionNormalTextureColor[] None;
		}

		private Dictionary<int, FaceCache> _elementCache = new Dictionary<int, FaceCache>();

		private void Cache()
		{
			var c = new Vector3(8f, 8f, 8f);
			foreach (var var in Variant)
			{
			//	var faces = var.Model.Elements.Sum(x => x.Faces.Count);
				

			//	var c = new Vector3(8f, 8f, 8f);
				var modelRotationMatrix = Matrix.CreateTranslation(-c) * GetModelRotationMatrix(var) * Matrix.CreateTranslation(c);

				foreach (var element in var.Model.Elements)
				{
					var elementFrom = new Vector3((element.From.X), (element.From.Y),
						(element.From.Z));

					var elementTo = new Vector3((element.To.X), (element.To.Y),
						(element.To.Z));

					var elementRotation = element.Rotation;
					Matrix elementRotationMatrix = GetElementRotationMatrix(elementRotation, out float scalingFactor);

					FaceCache elementCache = new FaceCache();
					foreach (var face in element.Faces)
					{
						var uv = face.Value.UV;
						var uvmap = GetTextureUVMap(Resources, ResolveTexture(var, face.Value.Texture), uv.X1, uv.X2, uv.Y1, uv.Y2);

						VertexPositionNormalTextureColor[] faceVertices =
							GetFaceVertices(face.Key, elementFrom, elementTo, uvmap, face.Value.Rotation);

						for (var index = 0; index < faceVertices.Length; index++)
						{
							var vert = faceVertices[index];

							//Apply element rotation
							if (elementRotation.Axis != Axis.Undefined)
							{
								vert.Position = Vector3.Transform(vert.Position, elementRotationMatrix);

								//Scale the texture back to its correct size
								if (elementRotation.Rescale) 
								{
									if (elementRotation.Axis == Axis.X || elementRotation.Axis == Axis.Z)
									{
										vert.Position.Y *= scalingFactor;
									}

									if (elementRotation.Axis == Axis.Y || elementRotation.Axis == Axis.Z)
									{
										vert.Position.X *= scalingFactor;
									}

									if (elementRotation.Axis == Axis.Y || elementRotation.Axis == Axis.X)
									{
										vert.Position.Z *= scalingFactor;
									}
								}
							}

							//Apply model rotation
							vert.Position = Vector3.Transform(vert.Position, modelRotationMatrix);
							
							//Scale the position
							vert.Position = (vert.Position / 16f);

							faceVertices[index] = vert;
						}

						switch (face.Key)
						{
							case BlockFace.Down:
								elementCache.Down = faceVertices;
								break;
							case BlockFace.Up:
								elementCache.Up = faceVertices;
								break;
							case BlockFace.East:
								elementCache.East = faceVertices;
								break;
							case BlockFace.West:
								elementCache.West = faceVertices;
								break;
							case BlockFace.North:
								elementCache.North = faceVertices;
								break;
							case BlockFace.South:
								elementCache.South = faceVertices;
								break;
							case BlockFace.None:
								elementCache.None = faceVertices;
								break;
						}
					}

					_elementCache.Add(element.GetHashCode(), elementCache);
				}
			}
		}

		public override VertexPositionNormalTextureColor[] GetVertices(IWorld world, Vector3 position, Block baseBlock)
		{
			var verts = new List<VertexPositionNormalTextureColor>();

			// MaxY = 0;
			Vector3 worldPosition = new Vector3(position.X, position.Y, position.Z);

			foreach (var var in Variant)
			{
				var modelRotationMatrix =
					GetModelRotationMatrix(var);
				foreach (var element in var.Model.Elements)
				{
					var elementCache = _elementCache[element.GetHashCode()];
					foreach (var face in element.Faces)
					{
						GetFaceValues(face.Value.CullFace, face.Key, out var cull, out var cullFace);

						cullFace = Vector3.Transform(cullFace, modelRotationMatrix);

						if (cullFace != Vector3.Zero && !CanRender(world, baseBlock, worldPosition + cullFace))
							continue;

						VertexPositionNormalTextureColor[] faceVertices;
						switch (face.Key)
						{
							case BlockFace.Down:
								faceVertices = (VertexPositionNormalTextureColor[]) elementCache.Down.Clone();
								break;
							case BlockFace.Up:
								faceVertices = (VertexPositionNormalTextureColor[]) elementCache.Up.Clone();
								break;
							case BlockFace.East:
								faceVertices = (VertexPositionNormalTextureColor[]) elementCache.East.Clone();
								break;
							case BlockFace.West:
								faceVertices = (VertexPositionNormalTextureColor[]) elementCache.West.Clone();
								break;
							case BlockFace.North:
								faceVertices = (VertexPositionNormalTextureColor[]) elementCache.North.Clone();
								break;
							case BlockFace.South:
								faceVertices = (VertexPositionNormalTextureColor[]) elementCache.South.Clone();
								break;
							default:
								faceVertices = (VertexPositionNormalTextureColor[]) elementCache.None.Clone();
								break;
						}

						Color faceColor = faceVertices[0].Color;

						if (face.Value.TintIndex >= 0)
						{
							int biomeId = world.GetBiome((int)worldPosition.X, 0, (int)worldPosition.Z);

							if (biomeId != -1) 
							{
								var biome = BiomeUtils.GetBiomeById(biomeId);

								if (baseBlock.BlockId == 2)
								{
									faceColor = Resources.ResourcePack.GetGrassColor(biome.Temperature, biome.Downfall, (int) worldPosition.Y);
								}
								else
								{
									faceColor = Resources.ResourcePack.GetFoliageColor(biome.Temperature, biome.Downfall, (int) worldPosition.Y);
								}
							}
						}

						faceColor = UvMapHelp.AdjustColor(faceColor, cull, GetLight(world, worldPosition + cullFace), element.Shade);

						for (var index = 0; index < faceVertices.Length; index++)
						{
							var vert = faceVertices[index];
							vert.Color = faceColor;

							vert.Position = worldPosition + vert.Position;

							verts.Add(vert);
						}
					}
				}
			}

			return verts.ToArray();
		}
	}
}