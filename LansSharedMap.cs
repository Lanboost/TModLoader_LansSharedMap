using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;
using static LansSharedMap.LansSharedMap;

namespace LansSharedMap
{
	
	public class Position
	{
		public int x;
		public int y;

		public Position(int x, int y)
		{
			this.x = x;
			this.y = y;
		}
	}
	public class LansSharedMapSystem : ModSystem
	{
        Queue<Position> updates = new Queue<Position>();

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

                if (updates.Count < 100000)
                {
                    var newTile = new Position(x, y);
                    updates.Enqueue(newTile);
                }

            }
        }

        public override void PostUpdateEverything()
        {
            base.PostUpdateEverything();



            while (updates.Count > 0)
            {
                var maxUpdates = Math.Min(1000, updates.Count);
                int length = 2 + 4 + maxUpdates * 12;
                var packet = Mod.GetPacket(length);
                packet.Write((byte)LansSharedMapModMessageType.MapUpdate);
                packet.Write((byte)Main.myPlayer);
                packet.Write((int)length);

                for (var i = 0; i < maxUpdates; i++)
                {
                    var t = updates.Dequeue();
                    packet.Write((int)t.x);
                    packet.Write((int)t.y);
                    var mapTile = Main.Map[t.x, t.y];
                    packet.Write((ushort)mapTile.Type);
                    packet.Write((byte)mapTile.Light);
                    packet.Write((byte)mapTile.Color);

                }

                packet.Send();

                //this.Logger.Warn("Sent " + maxUpdates + " stuff");

            }

        }
		
    }

	public class LansSharedMap : Mod
	{
		
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
								if (isWorldMapTileDifferent(Main.Map[x, y], tile)) {
									Main.Map.SetTile(x, y, ref tile);
									var result = MyUpdateMapTile(x, y, true);
									//this.Logger.Warn("Update x:"+x+" y:"+y+" v:"+ result);
									
								}
							}
							
						}

						break;
					}
			}
		}

		bool isWorldMapTileDifferent(MapTile first, MapTile second)
		{
			return first.Type != second.Type || first.Light != second.Light || first.Color != second.Color;
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