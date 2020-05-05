using Alex.Graphics.Effect;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Alex.Worlds
{
	public class RenderingShaders
	{
		public BlockEffect AnimatedEffect            { get; }
		public BlockEffect AnimatedTranslucentEffect { get; }
		public BlockEffect TransparentEffect         { get; }
		public BlockEffect TranslucentEffect         { get; }
		public BlockEffect OpaqueEffect              { get; }

		public LightingEffect LightingEffect { get; }
		
		public RenderingShaders(GraphicsDevice device)
		{
			var fogStart = 0f;

			TransparentEffect = new BlockEffect(device)
			{
				//	Texture = stillAtlas,
				VertexColorEnabled = true,
				World = Matrix.Identity,
				AlphaFunction = CompareFunction.Greater,
				ReferenceAlpha = 32,
				FogStart = fogStart,
				FogEnabled = false,
				// TextureEnabled = true
			};

			TranslucentEffect = new BlockEffect(device)
			{
				//Texture = stillAtlas,
				VertexColorEnabled = true,
				World = Matrix.Identity,
				AlphaFunction = CompareFunction.Greater,
				ReferenceAlpha = 32,
				FogStart = fogStart,
				FogEnabled = false,

				//Alpha = 0.5f
			};

			AnimatedEffect = new BlockEffect(device)
			{
				//	Texture = Resources.Atlas.GetAtlas(0),
				VertexColorEnabled = true,
				World = Matrix.Identity,
				AlphaFunction = CompareFunction.Greater,
				ReferenceAlpha = 32,
				FogStart = fogStart,
				FogEnabled = false,
				// TextureEnabled = true
			};

			AnimatedTranslucentEffect = new BlockEffect(device)
			{
				//Texture = Resources.Atlas.GetAtlas(0),
				VertexColorEnabled = true,
				World = Matrix.Identity,
				AlphaFunction = CompareFunction.Greater,
				ReferenceAlpha = 127,
				FogStart = fogStart,
				FogEnabled = false,
				Alpha = 0.5f
			};

			OpaqueEffect = new BlockEffect(device)
			{
				//  TextureEnabled = true,
				//	Texture = stillAtlas,
				FogStart = fogStart,
				VertexColorEnabled = true,
				//  LightingEnabled = true,
				FogEnabled = false,
				ReferenceAlpha = 249
				//    AlphaFunction = CompareFunction.Greater,
				//    ReferenceAlpha = 127

				//  PreferPerPixelLighting = false
			};
			
			LightingEffect = new LightingEffect(device)
			{
				
			};
		}

		public void SetTextures(Texture2D texture)
		{
			TranslucentEffect.Texture = TransparentEffect.Texture = OpaqueEffect.Texture = texture;
		}

		public void SetAnimatedTextures(Texture2D texture)
		{
			AnimatedTranslucentEffect.Texture = AnimatedEffect.Texture = texture;
		}

		public void UpdateMatrix(Matrix view, Matrix projection)
		{
			TransparentEffect.View = view;
			TransparentEffect.Projection = projection;
		    
			AnimatedEffect.View = view;
			AnimatedEffect.Projection = projection;

			OpaqueEffect.View = view;
			OpaqueEffect.Projection = projection;

			TranslucentEffect.View = view;
			TranslucentEffect.Projection = projection;

			AnimatedTranslucentEffect.View = view;
			AnimatedTranslucentEffect.Projection = projection;

			LightingEffect.View = view;
			LightingEffect.Projection = projection;
		}
		
		public bool FogEnabled
		{
			get { return TransparentEffect.FogEnabled; }
			set
			{
				TransparentEffect.FogEnabled = value;
				TranslucentEffect.FogEnabled = value;
				AnimatedEffect.FogEnabled = value;
				AnimatedTranslucentEffect.FogEnabled = value;
				OpaqueEffect.FogEnabled = value;
			}
		}

		public Vector3 FogColor
		{
			get { return TransparentEffect.FogColor; }
			set
			{
				TransparentEffect.FogColor = value;
				OpaqueEffect.FogColor = value;
				AnimatedEffect.FogColor = value;
				TranslucentEffect.FogColor = value;
				AnimatedTranslucentEffect.FogColor = value;
			}
		}

		public float FogDistance
		{
			get { return TransparentEffect.FogEnd; }
			set
			{
				TransparentEffect.FogEnd = value;
				OpaqueEffect.FogEnd = value;
				AnimatedEffect.FogEnd = value;
				TranslucentEffect.FogEnd = value;
				AnimatedTranslucentEffect.FogEnd = value;
			}
		}

		public Vector3 AmbientLightColor
		{
			get { return TransparentEffect.DiffuseColor; }
			set
			{
				TransparentEffect.DiffuseColor = value;
				TranslucentEffect.DiffuseColor = value;

				OpaqueEffect.DiffuseColor = value;
				// OpaqueEffect.DiffuseColor = value;
				AnimatedEffect.DiffuseColor = value;
				AnimatedTranslucentEffect.DiffuseColor = value;
			}
		}

		public float BrightnessModifier
		{
			get
			{
				return TransparentEffect.LightOffset;
			}
			set
			{
				TransparentEffect.LightOffset = value;
				TranslucentEffect.LightOffset = value;

				OpaqueEffect.LightOffset = value;
				// OpaqueEffect.DiffuseColor = value;
				AnimatedEffect.LightOffset = value;
				AnimatedTranslucentEffect.LightOffset = value;
				LightingEffect.LightOffset = value;
			}
		}
	}
}