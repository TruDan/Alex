﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using Alex.API.Blocks.State;
using Alex.API.Data.Options;
using Alex.API.Data.Servers;
using Alex.API.Entities;
using Alex.API.Events;
using Alex.API.Events.World;
using Alex.API.Graphics;
using Alex.API.Gui;
using Alex.API.Network;
using Alex.API.Services;
using Alex.API.Utils;
using Alex.API.World;
using Alex.Blocks.Minecraft;
using Alex.Entities;
using Alex.GameStates;
using Alex.Graphics.Camera;
using Alex.Graphics.Models;
using Alex.Graphics.Models.Items;
using Alex.Gui.Forms.Bedrock;
using Alex.Utils;
using Alex.Worlds.Bedrock;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MiNET;
using MiNET.Utils;
using NLog;
using BlockCoordinates = Alex.API.Utils.BlockCoordinates;
using ChunkCoordinates = Alex.API.Utils.ChunkCoordinates;
using Color = Microsoft.Xna.Framework.Color;
using IBlockState = Alex.API.Blocks.State.IBlockState;
using MathF = System.MathF;
using Player = Alex.Entities.Player;
using PlayerLocation = Alex.API.Utils.PlayerLocation;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Skin = Alex.API.Utils.Skin;
using UUID = Alex.API.Utils.UUID;

//using System.Reflection.Metadata.Ecma335;

namespace Alex.Worlds
{
	public class World : IWorld, IWorldReceiver, IBlockAccess
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger(typeof(World));

		private GraphicsDevice Graphics { get; }
		public Camera Camera { get; set; }

		public LevelInfo WorldInfo { get; set; }

		public Player Player { get; set; }
		private AlexOptions Options { get; }
		
		private IEventDispatcher EventDispatcher { get; }
		public BedrockFormManager FormManager { get; }
		private SkyBox SkyRenderer { get; }
		private bool UseDepthMap { get; set; }
		//public SkyLightCalculations SkyLightCalculations { get; }
		private ServerType ServerType { get; }
		
		public bool DrowningDamage { get; set; } = true;
		public bool CommandblockOutput { get; set; } = true;
		public bool DoTiledrops { get; set; } = true;
		public bool DoMobloot { get; set; } = true;
		public bool KeepInventory { get; set; } = true;
		public bool DoDaylightcycle { get; set; } = true;
		public bool DoMobspawning { get; set; } = true;
		public bool DoEntitydrops { get; set; } = true;
		public bool DoFiretick { get; set; } = true;
		public bool DoWeathercycle { get; set; } = true;
		public bool Pvp { get; set; } = true;
		public bool Falldamage { get; set; } = true;
		public bool Firedamage { get; set; } = true;
		public bool Mobgriefing { get; set; } = true;
		public bool ShowCoordinates { get; set; } = true;
		public bool NaturalRegeneration { get; set; } = true;
		public bool TntExplodes { get; set; } = true;
		public bool SendCommandfeedback { get; set; } = true;
		public int RandomTickSpeed { get; set; } = 3;
		
		public World(IServiceProvider serviceProvider, GraphicsDevice graphics, AlexOptions options, Camera camera,
			INetworkProvider networkProvider)
		{
			Graphics = graphics;
			Camera = camera;
			Options = options;

			PhysicsEngine = new PhysicsManager(this);
			ChunkManager = new ChunkManager(serviceProvider, graphics, this);
			EntityManager = new EntityManager(graphics, this, networkProvider);
			Ticker = new TickManager(this);
			PlayerList = new PlayerList();

			ChunkManager.Start();
			var settings = serviceProvider.GetRequiredService<IOptionsProvider>();
			var profileService = serviceProvider.GetRequiredService<IPlayerProfileService>();
			var resources = serviceProvider.GetRequiredService<ResourceManager>();
			EventDispatcher = serviceProvider.GetRequiredService<IEventDispatcher>();
			
			string username = string.Empty;
			Skin skin = profileService?.CurrentProfile?.Skin;
			if (skin == null)
			{
				resources.ResourcePack.TryGetBitmap("entity/alex", out var rawTexture);
				var t = TextureUtils.BitmapToTexture2D(graphics, rawTexture);
				skin = new Skin()
				{
					Texture = t,
					Slim = true
				};
			}

			if (!string.IsNullOrWhiteSpace(profileService?.CurrentProfile?.Username))
			{
				username = profileService.CurrentProfile.Username;
			}

			Player = new Player(graphics, serviceProvider.GetRequiredService<Alex>().InputManager, username, this, skin, networkProvider, PlayerIndex.One);

			Player.KnownPosition = new PlayerLocation(GetSpawnPoint());
			Camera.MoveTo(Player.KnownPosition, Vector3.Zero);

			Options.FieldOfVision.ValueChanged += FieldOfVisionOnValueChanged;
			Camera.FOV = Options.FieldOfVision.Value;

			PhysicsEngine.AddTickable(Player);

			if (networkProvider is BedrockClient)
			{
				Player.SetInventory(new BedrockInventory(46));
			}
		//	Player.Inventory.IsPeInventory = true;
			/*if (ItemFactory.TryGetItem("minecraft:diamond_sword", out var sword))
			{
				Player.Inventory[Player.Inventory.SelectedSlot] = sword;
				Player.Inventory.MainHand = sword;
			}
			else
			{
				Log.Warn($"Could not get diamond sword!");
			}*/
			
			EventDispatcher.RegisterEvents(this);
			
			FormManager = new BedrockFormManager(networkProvider, serviceProvider.GetRequiredService<GuiManager>(), serviceProvider.GetService<Alex>().InputManager);
			
			SkyRenderer = new SkyBox(serviceProvider, graphics, this);
			//SkyLightCalculations = new SkyLightCalculations();

			UseDepthMap = options.VideoOptions.Depthmap;
			options.VideoOptions.Depthmap.Bind((old, newValue) => { UseDepthMap = newValue; });

			ServerType = (networkProvider is BedrockClient) ? ServerType.Bedrock : ServerType.Java;
		}

		private void FieldOfVisionOnValueChanged(int oldvalue, int newvalue)
		{
			Camera.FOV = newvalue;
		}

		//public long WorldTime { get; private set; } = 6000;

		public PlayerList PlayerList { get; }
		public TickManager Ticker { get; }
		public EntityManager EntityManager { get; }
		public ChunkManager ChunkManager { get; private set; }
		public PhysicsManager PhysicsEngine { get; set; }

		public long Vertices
        {
            get { return ChunkManager.Vertices; }
        }

		public int IndexBufferSize
		{
			get { return ChunkManager.IndexBufferSize; }
		}

		public int ChunkCount
        {
            get { return ChunkManager.ChunkCount; }
        }

		public int ConcurrentChunkUpdates
        {
            get { return ChunkManager.ConcurrentChunkUpdates; }
        }

		public int EnqueuedChunkUpdates
		{
			get { return ChunkManager.EnqueuedChunkUpdates; }
		}

		public void ResetChunks()
        {
            ChunkManager.ClearChunks();
        }

        public void RebuildChunks()
        {
            ChunkManager.RebuildAll();
        }

        public void ToggleWireFrame()
        {
	        ChunkManager.UseWireFrames = !ChunkManager.UseWireFrames;
        }
        
        public void Render(IRenderArgs args)
        {
			Graphics.DepthStencilState = DepthStencilState.Default;
            Graphics.SamplerStates[0] = SamplerState.PointWrap;

            if (UseDepthMap)
            {
	            ChunkManager.Draw(args, true);
            }

            SkyRenderer.Draw(args);
            ChunkManager.Draw(args, false);
            
			EntityManager.Render(args);

			//TestItemRender.Render(args.GraphicsDevice, (Camera.Position + (Camera.Direction * 2.5f)));
			
	        if (Camera is ThirdPersonCamera)
	        {
		        Player.RenderEntity = true;
	        }
	        else
	        {
		        Player.RenderEntity = false;
	        }
	        
	        Player.Render(args);
        }

        public void Render2D(IRenderArgs args)
        {
	        args.Camera = Camera;

	        EntityManager.Render2D(args);

	        if (UseDepthMap)
	        {
		        args.SpriteBatch.Begin();

		        try
		        {
			        args.SpriteBatch.Draw(ChunkManager.DepthMap, new Rectangle(0, 0, 256, 256), Color.White);
		        }
		        finally
		        {
			        args.SpriteBatch.End();
		        }
	        }
        }
        
		private float _fovModifier = -1;
		private bool UpdatingPriorities = false;
		private float BrightnessMod = 0f;
        public void Update(UpdateArgs args)
		{
			args.Camera = Camera;
			if (Player.FOVModifier != _fovModifier)
			{
				_fovModifier = Player.FOVModifier;

				Camera.FOV += _fovModifier;
				Camera.UpdateProjectionMatrix();
				Camera.FOV -= _fovModifier;
			}
			Camera.Update(args, Player);

			BrightnessMod = SkyRenderer.BrightnessModifier;
			
			SkyRenderer.Update(args);
			ChunkManager.Update(args);
			EntityManager.Update(args, SkyRenderer);
			PhysicsEngine.Update(args.GameTime);

			var diffuseColor = Color.White.ToVector3() * SkyRenderer.BrightnessModifier;
			ChunkManager.AmbientLightColor = diffuseColor;

			Player.ModelRenderer.DiffuseColor = diffuseColor;
			Player.Update(args);

			if (Player.IsInWater)
			{
				ChunkManager.FogColor = new Vector3(0.2666667F, 0.6862745F, 0.9607844F) * BrightnessModifier;
				ChunkManager.FogDistance = (float)Math.Pow(Options.VideoOptions.RenderDistance, 2) * 0.15f;
			}
			else
			{
				ChunkManager.FogColor = SkyRenderer.WorldFogColor.ToVector3();
				ChunkManager.FogDistance = (float) Options.VideoOptions.RenderDistance * 16f * 0.8f;
			}
			
			if (Ticker.Update(args))
			{
				if (DoDaylightcycle)
				{
					WorldInfo.Time++;
				}
			}
		}
        
        public Vector3 SpawnPoint { get; set; } = Vector3.Zero;

        public float BrightnessModifier
        {
	        get { return (BrightnessMod + ((Options.VideoOptions.Brightness - 50f) * (0.5f / 100f))); }
        }

        public Vector3 GetSpawnPoint()
        {
	        return SpawnPoint;
        }

        public ChunkColumn GetChunk(BlockCoordinates coordinates, bool cacheOnly = false)
        {
	        if (ChunkManager.TryGetChunk(new ChunkCoordinates(coordinates), out var c))
	        {
		        return (ChunkColumn) c;
	        }

	        return null;
        }

        public ChunkColumn GetChunk(ChunkCoordinates coordinates, bool cacheOnly = false)
        {
	        if (ChunkManager.TryGetChunk(coordinates, out var c))
	        {
		        return (ChunkColumn) c;
	        }

	        return null;
        }

        public void SetSkyLight(BlockCoordinates coordinates, byte p1)
        {
	        IChunkColumn chunk;
	        if (ChunkManager.TryGetChunk(new ChunkCoordinates(coordinates), out chunk))
	        {
		        chunk.SetSkyLight(coordinates.X & 0xf, coordinates.Y & 0xff, coordinates.Z & 0xf, p1);
	        }
        }
        
        public byte GetSkyLight(BlockCoordinates position)
        {
	        return GetSkyLight(position.X, position.Y, position.Z);
        }
        
        public byte GetSkyLight(Vector3 position)
        {
            return GetSkyLight(position.X, position.Y, position.Z);
        }

        public byte GetSkyLight(float x, float y, float z)
        {
            return GetSkyLight((int)x, (int)y, (int)z); // Fix. xd
        }

        public byte GetSkyLight(int x, int y, int z)
        {
            if (y < 0 || y > ChunkColumn.ChunkHeight) return 15;

			IChunkColumn chunk;
	        if (ChunkManager.TryGetChunk(new ChunkCoordinates(x >> 4, z >> 4), out chunk))
	        {
				return chunk.GetSkylight(x & 0xf, y & 0xff, z & 0xf);
            }
	        
            return 15;
        }

        public byte GetBlockLight(Vector3 position)
        {
            return GetBlockLight(position.X, position.Y, position.Z);
        }
        public byte GetBlockLight(float x, float y, float z)
        {
            return GetBlockLight((int)Math.Floor(x), (int)Math.Floor(y), (int)Math.Floor(z)); // Fix. xd
        }

        public byte GetBlockLight(int x, int y, int z)
        {
            if (y < 0 || y > ChunkColumn.ChunkHeight) return 0;

			IChunkColumn chunk;
	        if (ChunkManager.TryGetChunk(new ChunkCoordinates(x >> 4, z >> 4), out chunk))
	        {
                return chunk.GetBlocklight(x & 0xf, y & 0xff, z & 0xf);
            }
            return 0;
        }

		public IBlock GetBlock(BlockCoordinates position)
		{
			return GetBlock(position.X, position.Y, position.Z);
		}

		public IBlock GetBlock(Vector3 position)
        {
            return GetBlock(position.X, position.Y, position.Z);
        }

		public IBlock GetBlock(float x, float y, float z)
	    {
		    return GetBlock((int) Math.Floor(x), (int) Math.Floor(y), (int) Math.Floor(z)); // Fix. xd
	    }

		public IBlock GetBlock(int x, int y, int z)
        {
	      //  try
	      //  {
		        IChunkColumn chunk;
		        if (ChunkManager.TryGetChunk(new ChunkCoordinates(x >> 4, z >> 4), out chunk))
		        {
			        return chunk.GetBlock(x & 0xf, y & 0xff, z & 0xf);
		        }
			//}
		//	catch { }

	        return BlockFactory.GetBlockState("minecraft:air").Block;
        }
		
		public void SetBlockState(int x, int y, int z, IBlockState block)
		{
			SetBlockState(x, y, z, block, 0);
		}
		
		public void SetBlockState(int x, int y, int z, IBlockState block, int storage)
		{
			var chunkCoords = new ChunkCoordinates(x >> 4, z >> 4);

			IChunkColumn chunk;
			if (ChunkManager.TryGetChunk(chunkCoords, out chunk))
			{
				var cx = x & 0xf;
				var cy = y & 0xff;
				var cz = z & 0xf;

				chunk.SetBlockState(cx, cy, cz, block, storage);
				
				UpdateNeighbors(x,y,z);
				CheckForUpdate(chunkCoords, cx, cz);
				
				ChunkManager.ScheduleChunkUpdate(chunkCoords, ScheduleType.Scheduled | ScheduleType.Lighting, true);
			}
		}

		private void CheckForUpdate(ChunkCoordinates chunkCoords, int cx, int cz)
		{
			if (cx == 0)
			{
				ChunkManager.ScheduleChunkUpdate(chunkCoords - new ChunkCoordinates(1, 0), ScheduleType.Border | ScheduleType.Lighting, true);
			}
			else if (cx == 0xf)
			{
				ChunkManager.ScheduleChunkUpdate(chunkCoords + new ChunkCoordinates(1, 0), ScheduleType.Border | ScheduleType.Lighting, true);
			}

			if (cz == 0)
			{
				ChunkManager.ScheduleChunkUpdate(chunkCoords - new ChunkCoordinates(0, 1), ScheduleType.Border | ScheduleType.Lighting, true);
			}
			else if (cz == 0xf)
			{
				ChunkManager.ScheduleChunkUpdate(chunkCoords + new ChunkCoordinates(0, 1), ScheduleType.Border | ScheduleType.Lighting, true);
			}
        }

		private void UpdateNeighbors(int x, int y, int z)
		{
			var source = new BlockCoordinates(x, y, z);
			new SkyLightCalculations().Calculate(this, source);
			
			
			ScheduleBlockUpdate(source, new BlockCoordinates(x + 1, y, z));
			ScheduleBlockUpdate(source, new BlockCoordinates(x - 1, y, z));

			ScheduleBlockUpdate(source, new BlockCoordinates(x, y, z + 1));
			ScheduleBlockUpdate(source, new BlockCoordinates(x, y, z - 1));

			ScheduleBlockUpdate(source, new BlockCoordinates(x, y + 1, z));
			ScheduleBlockUpdate(source, new BlockCoordinates(x, y - 1, z));
		}

		private void ScheduleBlockUpdate(BlockCoordinates updatedBlock, BlockCoordinates block)
		{
			Ticker.ScheduleTick(() =>
			{
				GetBlockState(block).Block.BlockUpdate(this, block, updatedBlock);
				//GetBlock(block).BlockUpdate(this, block, updatedBlock);
			}, 1);
		}

		public IEnumerable<(IBlockState state, int storage)> GetBlockStates(int x, int y, int z)
		{
			IChunkColumn chunk;
			if (ChunkManager.TryGetChunk(new ChunkCoordinates(x >> 4, z >> 4), out chunk))
			{
				foreach (var bs in chunk.GetBlockStates(x & 0xf, y & 0xff, z & 0xf))
				{
					yield return bs;
				}
			}
			
			yield break;
		}

		public IBlockState GetBlockState(int x, int y, int z, int storage)
		{
			IChunkColumn chunk;
			if (ChunkManager.TryGetChunk(new ChunkCoordinates(x >> 4, z >> 4), out chunk))
			{
				return chunk.GetBlockState(x & 0xf, y & 0xff, z & 0xf, storage);
			}

			return BlockFactory.GetBlockState("minecraft:air");
		}
		
		public IBlockState GetBlockState(int x, int y, int z)
		{
			return GetBlockState(x, y, z, 0);
		}
		
		public IBlockState GetBlockState(BlockCoordinates coords)
		{
			return GetBlockState(coords.X, coords.Y, coords.Z);
		}

		public int GetHeight(BlockCoordinates coords)
		{
			IChunkColumn chunk;
			if (ChunkManager.TryGetChunk(new ChunkCoordinates(coords.X >> 4, coords.Z >> 4), out chunk))
			{
				//Worlds.ChunkColumn realColumn = (Worlds.ChunkColumn)chunk;
				return chunk.GetHeight(coords.X & 0xf, coords.Z & 0xf);
			}

			return 255;
		}

		public Block GetBlock(BlockCoordinates blockCoordinates, ChunkColumn tryChunk = null)
		{
			ChunkColumn chunk = null;

			var chunkCoordinates = new ChunkCoordinates(blockCoordinates.X >> 4, blockCoordinates.Z >> 4);
			if (tryChunk != null && tryChunk.X == chunkCoordinates.X && tryChunk.Z == chunkCoordinates.Z)
			{
				chunk = tryChunk;
			}
			else
			{
				chunk = GetChunk(chunkCoordinates);
			}
			
			if (chunk == null)
				return new Air
				{
					Coordinates = blockCoordinates
				};

			var block = (Block)chunk.GetBlock(blockCoordinates.X & 0x0f, blockCoordinates.Y & 0xff, blockCoordinates.Z & 0x0f);
			block.Coordinates = blockCoordinates;

			return block;
		}

		public void SetBlock(Block block, bool broadcast = true, bool applyPhysics = true, bool calculateLight = true,
			ChunkColumn possibleChunk = null)
		{
			ChunkColumn chunk;
			var chunkCoordinates = new ChunkCoordinates(block.Coordinates.X >> 4, block.Coordinates.Z >> 4);
			chunk = possibleChunk != null && possibleChunk.X == chunkCoordinates.X && possibleChunk.Z == chunkCoordinates.Z ? possibleChunk : GetChunk(chunkCoordinates);

			chunk.SetBlockState(block.Coordinates.X & 0x0f, block.Coordinates.Y & 0xff, block.Coordinates.Z & 0x0f, block.BlockState);
			
			if (calculateLight && chunk.GetHeight(block.Coordinates.X & 0x0f, block.Coordinates.Z & 0x0f) <= block.Coordinates.Y + 1)
			{
				chunk.RecalculateHeight(block.Coordinates.X & 0x0f, block.Coordinates.Z & 0x0f);
			}

			if (calculateLight)
			{
				//new SkyLightCalculations().Calculate(this, block.Coordinates);
			}
		}

		public int GetBiome(int x, int y, int z)
		{
			IChunkColumn chunk;
			if (ChunkManager.TryGetChunk(new ChunkCoordinates(x >> 4, z >> 4), out chunk))
			{
				Worlds.ChunkColumn realColumn = (Worlds.ChunkColumn) chunk;
				return	realColumn.GetBiome(x & 0xf, z & 0xf);
			}

			//Log.Debug($"Failed getting biome: {x} | {y} | {z}");
			return -1;
		}

		public bool HasBlock(int x, int y, int z)
		{
			
			IChunkColumn chunk;
			if (ChunkManager.TryGetChunk(new ChunkCoordinates(x >> 4, z >> 4), out chunk))
			{
				//Worlds.ChunkColumn realColumn = (Worlds.ChunkColumn)chunk;
				return true;
			}

			return false;
		}


		public bool IsTransparent(int x, int y, int z)
		{
			IChunkColumn chunk;
			if (ChunkManager.TryGetChunk(new ChunkCoordinates(x >> 4, z >> 4), out chunk))
			{
				return chunk.IsTransparent(x & 0xf, y & 0xff, z & 0xf);
              //  return true;
			}

			return true;
        }

		public bool IsSolid(int x, int y, int z)
		{
			IChunkColumn chunk;
			if (ChunkManager.TryGetChunk(new ChunkCoordinates(x >> 4, z >> 4), out chunk))
			{
				return chunk.IsSolid(x & 0xf, y & 0xff, z & 0xf);
				//  return true;
			}

			return false;
		}

	    public bool IsScheduled(int x, int y, int z)
	    {
	        IChunkColumn chunk;
	        if (ChunkManager.TryGetChunk(new ChunkCoordinates(x >> 4, z >> 4), out chunk))
	        {
	            return chunk.IsScheduled(x & 0xf, y & 0xff, z & 0xf);
	            //  return true;
	        }

	        return false;
        }

	    public void GetBlockData(int x, int y, int z, out bool transparent, out bool solid)
		{
			IChunkColumn chunk;
			if (ChunkManager.TryGetChunk(new ChunkCoordinates(x >> 4, z >> 4), out chunk))
			{
				chunk.GetBlockData(x & 0xf, y & 0xff, z & 0xf, out transparent, out solid);
				//return chunk.IsSolid(x & 0xf, y & 0xff, z & 0xf);
				//  return true;
				return;
			}

			transparent = false;
			solid = false;
			//return false;
		}

        public BlockCoordinates FindBlockPosition(BlockCoordinates coords, out IChunkColumn chunk)
		{
			ChunkManager.TryGetChunk(new ChunkCoordinates(coords.X >> 4, coords.Z >> 4), out chunk);
			return new BlockCoordinates(coords.X & 0xf, coords.Y & 0xff, coords.Z & 0xf);
		}
        
        public void SetGameRule(MiNET.GameRule gameRule)
        {
	        if (Enum.TryParse(gameRule.Name, out GameRulesEnum val))
	        {
		        if (gameRule is GameRule<bool> grb)
		        {
			        SetGameRule(val, grb.Value);
		        }
		        else if (gameRule is GameRule<int> gri)
		        {
			        SetGameRule(val, gri.Value);
		        }
	        }
        }
        
		public void SetGameRule(GameRulesEnum rule, bool value)
		{
			switch (rule)
			{
				case GameRulesEnum.DrowningDamage:
					DrowningDamage = value;
					break;
				case GameRulesEnum.CommandblockOutput:
					CommandblockOutput = value;
					break;
				case GameRulesEnum.DoTiledrops:
					DoTiledrops = value;
					break;
				case GameRulesEnum.DoMobloot:
					DoMobloot = value;
					break;
				case GameRulesEnum.KeepInventory:
					KeepInventory = value;
					break;
				case GameRulesEnum.DoDaylightcycle:
					DoDaylightcycle = value;
					break;
				case GameRulesEnum.DoMobspawning:
					DoMobspawning = value;
					break;
				case GameRulesEnum.DoEntitydrops:
					DoEntitydrops = value;
					break;
				case GameRulesEnum.DoFiretick:
					DoFiretick = value;
					break;
				case GameRulesEnum.DoWeathercycle:
					DoWeathercycle = value;
					break;
				case GameRulesEnum.Pvp:
					Pvp = value;
					break;
				case GameRulesEnum.Falldamage:
					Falldamage = value;
					break;
				case GameRulesEnum.Firedamage:
					Firedamage = value;
					break;
				case GameRulesEnum.Mobgriefing:
					Mobgriefing = value;
					break;
				case GameRulesEnum.ShowCoordinates:
					ShowCoordinates = value;
					break;
				case GameRulesEnum.NaturalRegeneration:
					NaturalRegeneration = value;
					break;
				case GameRulesEnum.TntExplodes:
					TntExplodes = value;
					break;
				case GameRulesEnum.SendCommandfeedback:
					SendCommandfeedback = value;
					break;
			}
		}

		public void SetGameRule(GameRulesEnum rule, int value)
		{
			switch (rule)
			{
				case GameRulesEnum.DrowningDamage:
					RandomTickSpeed = value;
					break;
			}
		}
		
		public bool GetGameRule(GameRulesEnum rule)
		{
			switch (rule)
			{
				case GameRulesEnum.DrowningDamage:
					return DrowningDamage;
				case GameRulesEnum.CommandblockOutput:
					return CommandblockOutput;
				case GameRulesEnum.DoTiledrops:
					return DoTiledrops;
				case GameRulesEnum.DoMobloot:
					return DoMobloot;
				case GameRulesEnum.KeepInventory:
					return KeepInventory;
				case GameRulesEnum.DoDaylightcycle:
					return DoDaylightcycle;
				case GameRulesEnum.DoMobspawning:
					return DoMobspawning;
				case GameRulesEnum.DoEntitydrops:
					return DoEntitydrops;
				case GameRulesEnum.DoFiretick:
					return DoFiretick;
				case GameRulesEnum.DoWeathercycle:
					return DoWeathercycle;
				case GameRulesEnum.Pvp:
					return Pvp;
				case GameRulesEnum.Falldamage:
					return Falldamage;
				case GameRulesEnum.Firedamage:
					return Firedamage;
				case GameRulesEnum.Mobgriefing:
					return Mobgriefing;
				case GameRulesEnum.ShowCoordinates:
					return ShowCoordinates;
				case GameRulesEnum.NaturalRegeneration:
					return NaturalRegeneration;
				case GameRulesEnum.TntExplodes:
					return TntExplodes;
				case GameRulesEnum.SendCommandfeedback:
					return SendCommandfeedback;
			}

			return false;
		}
        
        private bool _destroyed = false;
		public void Destroy()
		{
			if (_destroyed) return;
			_destroyed = true;

			EventDispatcher.UnregisterEvents(this);
			
			PhysicsEngine.Stop();
			EntityManager.Dispose();
			ChunkManager.Dispose();

			PhysicsEngine.Dispose();
			Player.Dispose();
		}

		#region IWorldReceiver (Handle WorldProvider callbacks)

		public IEntity GetPlayerEntity()
		{
			return Player;
		}

		public IChunkColumn GetChunkColumn(int x, int z)
		{
			if (ChunkManager.TryGetChunk(new ChunkCoordinates(x, z), out IChunkColumn val))
			{
				return val;
			}
			else
			{
				return null;
			}
		}

		[EventHandler(EventPriority.Highest)]
		private void OnChunkReceived(ChunkReceivedEvent e)
		{
			if (e.IsCancelled)
				return;
			
			ChunkManager.AddChunk(e.Chunk, e.Coordinates, e.DoUpdates);
		}

		[EventHandler(EventPriority.Highest, true)]
		private void OnChunkUnload(ChunkUnloadEvent e)
		{
			ChunkManager.RemoveChunk(e.Coordinates);
			EntityManager.UnloadEntities(e.Coordinates);
		}

		public void SpawnEntity(long entityId, IEntity entity)
		{
			if (EntityManager.AddEntity(entityId, (Entity) entity))
			{
				PhysicsEngine.AddTickable((Entity) entity);
			}
		}

		public void SpawnEntity(long entityId, Entity entity)
		{
			if (EntityManager.AddEntity(entityId, entity))
			{
				PhysicsEngine.AddTickable(entity);
			}
			//Log.Info($"Spawned entity {entityId} : {entity} at {entity.KnownPosition} with renderer {entity.GetModelRenderer()}");
		}

		public void DespawnEntity(long entityId)
		{
			if (EntityManager.TryGet(entityId, out IEntity entity))
			{
				PhysicsEngine.Remove(entity);
				entity.Dispose();
			}

			EntityManager.Remove(entityId);
			//	Log.Info($"Despawned entity {entityId}");
		}

		public void UpdatePlayerPosition(PlayerLocation location)
		{
			if (!ChunkManager.TryGetChunk(new ChunkCoordinates(location), out _))
			{
				Player.WaitingOnChunk = true;
			}

			Player.KnownPosition = location;
			
		}

		public void UpdateEntityPosition(long entityId, PlayerLocation position, bool relative = false, bool updateLook = false, bool updatePitch = false)
		{
			if (EntityManager.TryGet(entityId, out IEntity entity))
			{
				entity.KnownPosition.OnGround = position.OnGround;
				if (!relative)
				{
					var oldPosition = entity.KnownPosition;
					entity.KnownPosition = position;
					if (entity is PlayerMob p)
					{
						p.DistanceMoved += MathF.Abs(Vector3.Distance(oldPosition, position));
					}
				}
				else
				{
					var oldPosition = entity.KnownPosition;
					float offset = 0f;
					if (this.ServerType == ServerType.Bedrock)
					{
						offset = (float) entity.PositionOffset;
					}
					
					entity.KnownPosition.X += position.X;
					entity.KnownPosition.Y += (position.Y - offset);
					entity.KnownPosition.Z += position.Z;	
					
					if (entity is PlayerMob p)
					{
						p.DistanceMoved += MathF.Abs(Vector3.Distance(oldPosition, entity.KnownPosition));
					}
					//entity.KnownPosition.Move(position);
				}

				if (updateLook)
				{
					//entity.KnownPosition.Yaw = position.Yaw;
					if (updatePitch)
					{
						entity.KnownPosition.Pitch = position.Pitch;
					}

					entity.KnownPosition.HeadYaw = position.HeadYaw;
					//	entity.UpdateHeadYaw(position.HeadYaw);
				}
            }
		}

		public void UpdateEntityLook(long entityId, float yaw, float pitch, bool onGround)
		{
			if (EntityManager.TryGet(entityId, out IEntity entity))
			{
				entity.KnownPosition.OnGround = onGround;
				entity.KnownPosition.Pitch = pitch;
				entity.KnownPosition.HeadYaw = yaw;
			}
		}

		public bool TryGetEntity(long entityId, out IEntity entity)
		{
			return EntityManager.TryGet(entityId, out entity);
		}

		public void SetTime(long worldTime)
		{
			WorldInfo.Time = worldTime;
		}

		public void SetRain(bool raining)
		{
			WorldInfo.Raining = raining;
		}

		public void SetBlockState(BlockCoordinates coordinates, IBlockState blockState)
		{
			SetBlockState(coordinates.X, coordinates.Y, coordinates.Z, blockState);
		}

		public void SetBlockState(BlockCoordinates coordinates, IBlockState blockState, int storage)
		{
			SetBlockState(coordinates.X, coordinates.Y, coordinates.Z, blockState, storage);
		}

		public void AddPlayerListItem(PlayerListItem item)
		{
			PlayerList.Entries.Add(item.UUID, item);
		}

		public void RemovePlayerListItem(UUID item)
		{
			PlayerList.Entries.Remove(item);
		}

		#endregion
	}
}
