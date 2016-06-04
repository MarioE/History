using System;
using System.Text;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Tile_Entities;
using TShockAPI;

namespace History
{
	public class Action
	{
		public string account;
		public byte action;
		public ushort data;
		public byte style;
		public short paint;
		public int time;
		public int x;
		public int y;
		public string text;
		public int alt;
		public int random;
		public bool direction;

		public void Reenact()
		{
			switch (action)
			{
				case 0:
				case 4://del tile
					if (Main.tile[x, y].active())
					{
						Main.tile[x, y].active(false);
						Main.tile[x, y].type = 0;
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 1://add tile
					//Don't place if already there
					if (Main.tile[x, y].active() && Main.tile[x, y].type == data)
						break;
					WorldGen.PlaceTile(x, y, data, false, true, style: style);
					if (Main.tileFrameImportant[data])
						TSPlayer.All.SendTileSquare(x, y, 8);
					else
						TSPlayer.All.SendTileSquare(x, y, 1);
					break;
				case 2://del wall
					if (Main.tile[x, y].wall != 0)
					{
						Main.tile[x, y].wall = 0;
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 3://add wall
					if (Main.tile[x, y].wall == 0)
					{
						Main.tile[x, y].wall = (byte)data;
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 5://placewire
					if (!Main.tile[x, y].wire())
					{
						WorldGen.PlaceWire(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 6://killwire
					if (Main.tile[x, y].wire())
					{
						WorldGen.KillWire(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 7://poundtile
					WorldGen.PoundTile(x, y);
					TSPlayer.All.SendTileSquare(x, y, 1);
					break;
				case 8://placeactuator
					if (!Main.tile[x, y].actuator())
					{
						WorldGen.PlaceActuator(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 9://killactuator
					if (Main.tile[x, y].actuator())
					{
						WorldGen.KillActuator(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 10://placewire2
					if (!Main.tile[x, y].wire2())
					{
						WorldGen.PlaceWire2(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 11://killwire2
					if (Main.tile[x, y].wire2())
					{
						WorldGen.KillWire2(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 12://placewire3
					if (!Main.tile[x, y].wire3())
					{
						WorldGen.PlaceWire3(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 13://killwire3
					if (Main.tile[x, y].wire3())
					{
						WorldGen.KillWire3(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 14://slopetile
					WorldGen.SlopeTile(x, y, data);
					TSPlayer.All.SendTileSquare(x, y, 1);
					break;
				case 15:
					//Uh wtf does "frame track" mean
					//Too lazy to find atm
					break;
				case 16:
					if (!Main.tile[x, y].wire4())
					{
						WorldGen.PlaceWire4(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 17:
					if (Main.tile[x, y].wire4())
					{
						WorldGen.KillWire4(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 25://paint tile
					if (Main.tile[x, y].active())
					{
						Main.tile[x, y].color((byte)data);
						NetMessage.SendData(63, -1, -1, "", x, y, data, 0f, 0);
					}
					break;
				case 26://paint wall
					if (Main.tile[x, y].wall > 0)
					{
						Main.tile[x, y].wallColor((byte)data);
						NetMessage.SendData(64, -1, -1, "", x, y, data, 0f, 0);
					}
					break;
				case 27://update sign
					break;//Does not save the new text currently, can't reenact.
			}
		}
		public void Rollback()
		{
			switch (action)
			{
				case 0:
				case 4://del tile
					if (Main.tileSand[data])//sand falling compensation (need to check up for top of sand)
					{
						int newY = y;
						while (newY > 0 && Main.tile[x, newY].active() && Main.tile[x, newY].type == data)
						{
							newY--;
						}
						if (Main.tile[x, newY].active())
							break;
						y = newY;
					}
					else if (data == 5)//tree, grow another?
					{
						WorldGen.GrowTree(x, y + 1);
						break;
					}
					else if (data == 2 || data == 23 || data == 60 || data == 70 || data == 109 || data == 199)// grasses need to place manually, not from placeTile
					{
						Main.tile[x, y].type = data;
						Main.tile[x, y].color((byte)(paint & 127));
						Main.tile[x, y].active(true);
						TSPlayer.All.SendTileSquare(x, y, 1);
						break;
					}
					//maybe already repaired?
					if (Main.tile[x, y].active() && Main.tile[x, y].type == data)
					{
						if (data == 314 || data == 395)
							goto frameOnly;
						break;
					}

					bool success = false;

					if (Terraria.ObjectData.TileObjectData.CustomPlace(data, style) && data != 82)
						WorldGen.PlaceObject(x, y, data, false, style: style, alternate: alt, random: random, direction: direction ? 1 : -1);
					else
						WorldGen.PlaceTile(x, y, data, false, true, -1, style: style);

					History.paintFurniture(data, x, y, (byte)(paint & 127));

				frameOnly:
					//restore slopes
					if ((paint & 128) == 128)
					{
						Main.tile[x, y].halfBrick(true);
					}
					else if (data == 314)
					{
						Main.tile[x, y].frameX = (short)(style - 1);
						Main.tile[x, y].frameY = (short)((paint >> 8) - 1);
					}
					else
						Main.tile[x, y].slope((byte)(paint >> 8));

					//restore sign text
					if (data == 55 || data == 85 || data == 425)
					{
						int signI = Sign.ReadSign(x, y);
						if (signI >= 0)
							Sign.TextSign(signI, text);
					}
					//Mannequins
					else if (data == 128 || data == 269)
					{
						//x,y should be bottom left, Direction is already done via PlaceObject so we add the item values.
						Main.tile[x, y - 2].frameX += (short)(paint * 100);
						Main.tile[x, y - 1].frameX += (short)((alt & 0x3FF) * 100);
						Main.tile[x, y].frameX += (short)((alt >> 10) * 100);
					}
					// Restore Weapon Rack if it had a netID
					else if (data == 334 && alt > 0)
					{
						int mask = 5000;// +(direction ? 15000 : 0);
						Main.tile[x - 1, y].frameX = (short)(alt + mask + 100);
						Main.tile[x, y].frameX = (short)(paint + mask + 5000);
					}
					// Restore Item Frame
					else if (data == 395)
					{
						/*TileEntity TE;
						// PlaceObject should already place a blank entity.
						if (TileEntity.ByPosition.TryGetValue(new Point16(x, y), out TE))
						{
							Console.WriteLine("Frame had Entity, changing item.");
							TEItemFrame frame = (TEItemFrame)TE;
							frame.item.netDefaults(alt);
							frame.item.Prefix(random);
							frame.item.stack = 1;
							NetMessage.SendData(86, -1, -1, "", frame.ID, (float)x, (float)y, 0f, 0, 0, 0);
						}
						else
							Console.WriteLine("This Frame restore had no entity");*/
					}
					//Send larger area for furniture
					if (Main.tileFrameImportant[data])
						if (data == 104)
							TSPlayer.All.SendTileSquare(x, y - 2, 8);
						else
							TSPlayer.All.SendTileSquare(x, y, 8);//This can be very large, or too small in some cases
					else
						TSPlayer.All.SendTileSquare(x, y, 1);
					break;
				case 1://add tile
					bool delete = Main.tile[x, y].active();
					if (!delete && Main.tileSand[data])//sand falling compensation (it may have fallen down)
					{
						int newY = y + 1;
						while (newY < Main.maxTilesY - 1 && !Main.tile[x, newY].active())
						{
							newY++;
						}
						if (Main.tile[x, newY].type == data)
						{
							y = newY;
							delete = true;
						}
					}
					if (delete)
					{
						WorldGen.KillTile(x, y, false, false, true);
						NetMessage.SendData(17, -1, -1, "", 0, x, y);
					}
					break;
				case 2://del wall
					if (Main.tile[x, y].wall != data) //change if not what was deleted
					{
						Main.tile[x, y].wall = (byte)data;
						Main.tile[x, y].wallColor((byte)paint);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 3://add wall
					if (Main.tile[x, y].wall != 0)
					{
						Main.tile[x, y].wall = 0;
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 5://placewire
					if (Main.tile[x, y].wire())
					{
						WorldGen.KillWire(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 6://killwire
					if (!Main.tile[x, y].wire())
					{
						WorldGen.PlaceWire(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 7://poundtile
					WorldGen.PoundTile(x, y);
					TSPlayer.All.SendTileSquare(x, y, 1);
					break;
				case 8://placeactuator
					if (Main.tile[x, y].actuator())
					{
						WorldGen.KillActuator(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 9://killactuator
					if (!Main.tile[x, y].actuator())
					{
						WorldGen.PlaceActuator(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 10://placewire2
					if (Main.tile[x, y].wire2())
					{
						WorldGen.KillWire2(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 11://killwire2
					if (!Main.tile[x, y].wire2())
					{
						WorldGen.PlaceWire2(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 12://placewire3
					if (Main.tile[x, y].wire3())
					{
						WorldGen.KillWire3(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 13://killwire3
					if (!Main.tile[x, y].wire3())
					{
						WorldGen.PlaceWire3(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 14://slopetile
					Main.tile[x, y].slope((byte)(paint >> 8));
					Main.tile[x, y].halfBrick((paint & 128) == 128);
					TSPlayer.All.SendTileSquare(x, y, 1);
					break;
				case 15: //frame track
					//see above
					break;
				case 16:
					if (Main.tile[x, y].wire4())
					{
						WorldGen.KillWire4(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 17:
					if (!Main.tile[x, y].wire4())
					{
						WorldGen.PlaceWire4(x, y);
						TSPlayer.All.SendTileSquare(x, y, 1);
					}
					break;
				case 25://paint tile
					if (Main.tile[x, y].active())
					{
						Main.tile[x, y].color((byte)paint);
						NetMessage.SendData(63, -1, -1, "", x, y, paint, 0f, 0);
					}
					break;
				case 26://paint wall
					if (Main.tile[x, y].wall > 0)
					{
						Main.tile[x, y].wallColor((byte)paint);
						NetMessage.SendData(64, -1, -1, "", x, y, paint, 0f, 0);
					}
					break;
				case 27://updatesign
					int sI = Sign.ReadSign(x, y); //This should be an existing sign, but use coords instead of index anyway
					if (sI >= 0)
					{
						Sign.TextSign(sI, text);
					}
					break;
			}
		}
		public override string ToString()
		{
			DateTime dateTime = History.Date.AddSeconds(time);
			string date = string.Format("{0}-{1} @ {3}:{4}:{5} (UTC):",
				dateTime.Month, dateTime.Day, dateTime.Year,
				dateTime.Hour, dateTime.Minute.ToString("00"), dateTime.Second.ToString("00"));

			TimeSpan timeDiff = DateTime.UtcNow - dateTime;
			StringBuilder dhms = new StringBuilder();
			if (timeDiff.Days != 0)
			{
				dhms.Append(timeDiff.Days + "d");
			}
			if (timeDiff.Hours != 0)
			{
				dhms.Append(timeDiff.Hours + "h");
			}
			if (timeDiff.Minutes != 0)
			{
				dhms.Append(timeDiff.Minutes + "m");
			}
			if (timeDiff.Seconds != 0)
			{
				dhms.Append(timeDiff.Seconds + "s");
			}

			switch (action)
			{
				case 0:
				case 4:
					return string.Format("{0} {1} broke tile {2}. ({3})", date, account, data, dhms);
				case 1:
					return string.Format("{0} {1} placed tile {2}. ({3})", date, account, data, dhms);
				case 2:
					return string.Format("{0} {1} broke wall {2}. ({3})", date, account, data, dhms);
				case 3:
					return string.Format("{0} {1} placed wall {2}. ({3})", date, account, data, dhms);
				case 5:
				case 10:
				case 12:
				case 16:
					return string.Format("{0} {1} placed wire. ({2})", date, account, dhms);
				case 6:
				case 11:
				case 13:
				case 17:
					return string.Format("{0} {1} broke wire. ({2})", date, account, dhms);
				case 7:
				case 14:
					return string.Format("{0} {1} slope/pound tile. ({2})", date, account, dhms);
				case 8:
					return string.Format("{0} {1} placed actuator. ({2})", date, account, dhms);
				case 9:
					return string.Format("{0} {1} broke actuator. ({2})", date, account, dhms);
				case 25:
				case 26:
					return string.Format("{0} {1} painted tile/wall. ({2})", date, account, dhms);
				case 27:
					return string.Format("{0} {1} changed sign text. ({2})", date, account, dhms);
				default:
					return "";
			}
		}
	}
}
