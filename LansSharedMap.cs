using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;

namespace LansSharedMap
{

	public struct WorldMapTile
	{
		public int x;
		public int y;
		public int type;
		public int lightning;
		public int extra;

		public WorldMapTile(int x, int y, int type, int lightning, int extra)
		{
			this.x = x;
			this.y = y;
			this.type = type;
			this.lightning = lightning;
			this.extra = extra;
		}
	}

	public class LansSharedMap : Mod
	{
		Queue<WorldMapTile> updates = new Queue<WorldMapTile>();

		WorldMapTile[,] sentMap = new WorldMapTile[Main.maxTilesX, Main.maxTilesY];
		bool[,] loadedMap = new bool[Main.maxTilesX, Main.maxTilesY];

		public LansSharedMap()
		{

			
		}

		public override void Load()
		{
			IL.Terraria.Map.WorldMap.ConsumeUpdate += AddOnConsumeUpdateMap;
		}

		public void AddOnConsumeUpdateMap(ILContext il)
		{
			var c = new ILCursor(il);
			c.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_1);

			c.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_2);

			c.EmitDelegate<Action<int, int>>(delegate (int x, int y) {
				onConsumeUpdateMap(x, y);
			});

		}

		void onConsumeUpdateMap(int x, int y)
		{
			if (Main.netMode != NetmodeID.SinglePlayer && Main.netMode != NetmodeID.Server)
			{
				

				MapTile t = Main.Map[x, y];
				var newTile = new WorldMapTile(x, y, t.Type, t.Light, t.Color);
				if (!loadedMap[x,y])
				{
					loadedMap[x, y] = true;
					sentMap[x, y] = newTile;
					updates.Enqueue(newTile);
					//this.Logger.Warn("Added maptile" + x + "," + y);
				}
				else
				{
					var oldTile = sentMap[x, y];
					if (isWorldMapTileDifferent(newTile, oldTile))
					{
						sentMap[x, y] = newTile;
						updates.Enqueue(newTile);
						//this.Logger.Warn("Added maptile" + x + "," + y);
					}
					else
					{
						//this.Logger.Warn("Skipped maptile" + x + "," + y);
					}
				}
			}
		}

		bool isWorldMapTileDifferent(WorldMapTile first, WorldMapTile second)
		{
			return first.type != second.type || first.lightning != second.lightning || first.extra != second.extra;
		}





		public override void PostUpdateEverything()
		{
			base.PostUpdateEverything();


			
			while (updates.Count > 0)
			{
				var maxUpdates = Math.Min(1000, updates.Count);
				int length = 2 + 4 + maxUpdates * 12;
				var packet = GetPacket(length);
				packet.Write((byte)LansSharedMapModMessageType.MapUpdate);
				packet.Write((byte)Main.myPlayer);
				packet.Write((int)length);

				for (var i = 0; i < maxUpdates; i++)
				{
					var t = updates.Dequeue();
					packet.Write((int)t.x);
					packet.Write((int)t.y);
					packet.Write((ushort)t.type);
					packet.Write((byte)t.lightning);
					packet.Write((byte)t.extra);
						
				}

				packet.Send();

				//this.Logger.Warn("Sent " + maxUpdates + " stuff");
					
			}
				
		}






		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			LansSharedMapModMessageType msgType = (LansSharedMapModMessageType)reader.ReadByte();
			switch (msgType)
			{

				case LansSharedMapModMessageType.MapUpdate:
					{


						byte playernumber = reader.ReadByte();
						if (Main.netMode == NetmodeID.Server)
						{
							//this.Logger.Warn("Recieveid as server ");

							int length = reader.ReadInt32();

							//this.Logger.Warn("Packet length is:"+length);

							var packet = GetPacket(length);
							packet.Write((byte)LansSharedMapModMessageType.MapUpdate);
							packet.Write((byte)playernumber);
							packet.Write((int)length);
							for(var i=6; i< length; i++)
							{
								byte b = reader.ReadByte();
								packet.Write(b);
							}
							packet.Send(-1, playernumber);
						}
						else
						{
							//this.Logger.Warn("Recieveid as client ");

							int length = reader.ReadInt32();
							length = (length - 6) / 12;

							for (int i = 0; i < length; i++)
							{
								int x = reader.ReadInt32();
								int y = reader.ReadInt32();
								ushort type = reader.ReadUInt16();
								byte light = reader.ReadByte();
								byte _extra = reader.ReadByte();

								var tile = MapTile.Create(type, light, _extra);
								Main.Map.SetTile(x, y, ref tile);
								var result = MyUpdateMapTile(x, y, true);
								//this.Logger.Warn("Update x:"+x+" y:"+y+" v:"+ result);

								MapTile t = Main.Map[x, y];
								var newTile = new WorldMapTile(x, y, t.Type, t.Light, t.Color);
								loadedMap[x, y] = true;
								sentMap[x, y] = newTile;
							}
							
						}

						break;
					}
			}
		}

		public static bool MyUpdateMapTile(int i, int j, bool addToList = true)
		{
			bool result = false;

			result = true;
			if (MapHelper.numUpdateTile < MapHelper.maxUpdateTile - 1)
			{
				MapHelper.updateTileX[MapHelper.numUpdateTile] = (short)i;
				MapHelper.updateTileY[MapHelper.numUpdateTile] = (short)j;
				MapHelper.numUpdateTile++;
			}
			else
			{
				Main.refreshMap = true;
			}

			return result;
		}



		internal enum LansSharedMapModMessageType : byte
		{
			MapUpdate,
		}
	}

	
}