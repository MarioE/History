/*
 * Credit to MarioE for original plugin.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Threading;
using History.Commands;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Tile_Entities;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace History
{
	[ApiVersion(1, 23)]
	public class History : TerrariaPlugin
	{
		public static List<Action> Actions = new List<Action>(SaveCount);
		public static IDbConnection Database;
		public static DateTime Date = DateTime.UtcNow;
		public const int SaveCount = 10;

		private bool[] AwaitingHistory = new bool[256];
		public override string Author
		{
			get { return "Maintained by Cracker64 & Zaicon"; }
		}
		CancellationTokenSource Cancel = new CancellationTokenSource();
		private BlockingCollection<HCommand> CommandQueue = new BlockingCollection<HCommand>();
		private Thread CommandQueueThread;
		public override string Description
		{
			get { return "Logs actions such as tile editing."; }
		}
		public override string Name
		{
			get { return "History"; }
		}
		public override Version Version
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		public History(Main game)
			: base(game)
		{
			Order = 50;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
				ServerApi.Hooks.WorldSave.Deregister(this, OnSaveWorld);

				Cancel.Cancel();
			}
		}
		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.NetGetData.Register(this, OnGetData);
			ServerApi.Hooks.WorldSave.Register(this, OnSaveWorld);
			initBreaks();
		}
		void Queue(string account, int X, int Y, byte action, ushort data = 0, byte style = 0, short paint = 0, string text = null, int alternate = 0, int random = 0, bool direction = false)
		{
			if (Actions.Count == SaveCount)
			{
				CommandQueue.Add(new SaveCommand(Actions.ToArray()));
				Actions.Clear();
			}
			Actions.Add(new Action { account = account, action = action, data = data, time = (int)(DateTime.UtcNow - Date).TotalSeconds, x = X, y = Y, paint = paint, style = style, text = text, alt = alternate, direction = direction, random = (sbyte)random });
		}
		// 334 weapon rack done? weapon styles?
		static void getPlaceData(ushort type, ref int which, ref int div)
		{
			switch (type)
			{
				//WHICH block style is in 0:X   1:Y
				case 314: //minecart ????
					which = 0;
					div = 1;
					break;
				case 13: //bottle
				case 36: //present
				//case 49: //water candle Removing - No different styles?
				case 174: //platinum candle
				//case 78: //clay pot
				case 82: //herb
				case 83: //herb
				case 84: //herb
				case 91: //banner
				case 144: //timer
				case 149: //christmas light
				case 178: //gems
				case 184:
				case 239: //bars
				case 419:
					which = 0;
					div = 18;
					break;
				case 19: //platforms
				case 135: //pressure plates
				case 136: //switch (state)
				case 137: //traps
				case 141: //explosives
				case 210: //land mine
				case 380: //planter box
				case 420: //L Gate
				case 423: // L Sensor
				case 424: //Junction Box
				case 428: // Weighted Presure Plate
				case 429: //Wire bulb
				case 445: //Pixel Box
					which = 1;
					div = 18;
					break;
				case 4: //torch
				case 33: //candle
				case 324: //beach piles
					which = 1;
					div = 22;
					break;
				case 227: //dye plants
					which = 0;
					div = 34;
					break;
				case 16: //anvil
				case 18: //work bench
				case 21: //chest
				case 27: //sunflower (randomness)
				case 29:
				case 55: // sign
				case 85: //tombstone
				case 103:
				case 104: //grandfather
				case 128: // mannequin (orient)
				case 132: //lever (state)
				case 134:
				case 207: // water fountains
				case 245: //2x3 wall picture
				case 254:
				case 269: // womannequin
				case 320: //more statues
				case 337: //     statues
				case 376: //fishing crates
				case 378: //target dummy
				case 386: //trapdoor open
				case 395:
				case 410: //lunar monolith
				case 411: //Detonator
				case 425: // Announcement (Sign)
				case 441:
				case 443: //Geyser
					which = 0;
					div = 36;
					break;
				case 35://jack 'o lantern??
				case 42://lanterns
				case 79://beds (orient)
				case 90://bathtub (orient)
				case 139://music box
				case 246:// 3x2 wall painting
				case 270:
				case 271:
					which = 1;
					div = 36;
					break;
				case 172: //sinks
					which = 1;
					div = 38;
					break;
				case 15://chair
				case 20:
				case 216:
				case 338:
					which = 1;
					div = 40;
					break;
				case 14:
				case 17:
				case 26:
				case 86:
				case 87:
				case 88:
				case 89:
				case 114:
				case 186:
				case 187:
				case 215:
				case 217:
				case 218:
				case 237:
				case 244:
				case 285:
				case 286:
				case 298:
				case 299:
				case 310:
				case 106:
				case 170:
				case 171:
				//case 172:
				case 212:
				case 219:
				case 220:
				case 228:
				case 231:
				case 243:
				case 247:
				case 283:
				case 300:
				case 301:
				case 302:
				case 303:
				case 304:
				case 305:
				case 306:
				case 307:
				case 308:
				case 77:
				case 101:
				case 102:
				case 133:
				case 339:
				case 235: //teleporter
				case 377: //sharpening station
				case 405: //fireplace
					which = 0;
					div = 54;
					break;
				case 10:
				case 11: //door
				case 34: //chandelier
				case 93: //tikitorch
				case 241: //4x3 wall painting
					which = 1;
					div = 54;
					break;
				case 240: //3x3 painting, style stored in both
				case 440:
					which = 2;
					div = 54;
					break;
				case 209:
					which = 0;
					div = 72;
					break;
				case 242:
					which = 1;
					div = 72;
					break;
				case 388: //tall gate closed
				case 389: //tall gate open
					which = 1;
					div = 94;
					break;
				case 92: // lamp post
					which = 1;
					div = 108;
					break;
				case 105: // Statues
					which = 3;
					break;
				default:
					break;
			}
		}
		//This returns where the furniture is expected to be placed for worldgen
		static Vector2 destFrame(ushort type)
		{
			Vector2 dest;
			switch (type)//(x,y) is from top left
			{
				case 16:
				case 18:
				case 29:
				case 42:
				case 91:
				case 103:
				case 134:
				case 270:
				case 271:
				case 386:
				case 387:
				case 388:
				case 389:
				case 395:
				case 443:
					dest = new Vector2(0, 0);
					break;
				case 15:
				case 21:
				case 35:
				case 55:
				case 85:
				case 139:
				case 216:
				case 245:
				case 338:
				case 390:
				case 425:
					dest = new Vector2(0, 1);
					break;
				case 34:
				case 95:
				case 126:
				case 246:
				case 235:// (1,0)
					dest = new Vector2(1, 0);
					break;
				case 14:
				case 17:
				case 26:
				case 77:
				case 79:
				case 86:
				case 87:
				case 88:
				case 89:
				case 90:
				case 94:
				case 96:
				case 97:
				case 98:
				case 99:
				case 100:
				case 114:
				case 125:
				case 132:
				case 133:
				case 138:
				case 142:
				case 143:
				case 172: //sinks
				case 173:
				case 186:
				case 187:
				case 215:
				case 217:
				case 218:
				case 237:
				case 240:
				case 241:
				case 244:
				case 254:
				case 282:
				case 285:
				case 286:
				case 287:
				case 288:
				case 289:
				case 290:
				case 291:
				case 292:
				case 293:
				case 294:
				case 295:
				case 298:
				case 299:
				case 310:
				case 316:
				case 317:
				case 318:
				case 319:
				case 334:
				case 335:
				case 339:
				case 354:
				case 355:
				case 360:
				case 361:
				case 362:
				case 363:
				case 364:
				case 376:
				case 377:
				case 391:
				case 392:
				case 393:
				case 394:
				case 405:
				case 411:
				case 440:
				case 441:// (1,1)
					dest = new Vector2(1, 1);
					break;
				case 105:// Statues use (0,2) from PlaceTile, but (1,2) from PlaceObject, strange
				case 106:
				case 209:
				case 212:
				case 219:
				case 220:
				case 228:
				case 231:
				case 243:
				case 247:
				case 283:
				case 300:
				case 301:
				case 302:
				case 303:
				case 304:
				case 305:
				case 306:
				case 307:
				case 308:
				case 349:
				case 356:
				case 378:
				case 406:
				case 410:
				case 412:// (1,2)
					dest = new Vector2(1, 2);
					break;
				case 101:
				case 102:// (1,3)
					dest = new Vector2(1, 3);
					break;
				case 242:// (2,2)
					dest = new Vector2(2, 2);
					break;
				case 10:
				case 11: // Door, Ignore framex*18 for 10, not 11
				case 93:
				case 128:
				case 269:
				case 320:
				case 337: // (0,2)
					dest = new Vector2(0, 2);
					break;
				case 207:
				case 27: // (0,3)
					dest = new Vector2(0, 3);
					break;
				case 104: // (0,4)
					dest = new Vector2(0, 4);
					break;
				case 92: // (0,5)
					dest = new Vector2(0, 5);
					break;
				case 275:
				case 276:
				case 277:
				case 278:
				case 279:
				case 280:
				case 281:
				case 296:
				case 297:
				case 309:// (3,1)
					dest = new Vector2(3, 1);
					break;
				case 358:
				case 359:
				case 413:
				case 414:
					dest = new Vector2(3, 2);
					break;
				default:
					dest = new Vector2(-1, -1);
					break;
			}
			return dest;
		}
		static Vector2 furnitureDimensions(ushort type, byte style)
		{
			Vector2 dim = new Vector2(0, 0);
			switch (type)
			{
				case 15: //1x2
				case 20:
				case 42:
				case 216:
				case 270://top
				case 271://top
				case 338:
				case 390:
					dim = new Vector2(0, 1);
					break;
				case 91: //1x3
				case 93: //1x3
					dim = new Vector2(0, 2);
					break;
				case 388:
				case 389:
					dim = new Vector2(0, 4);
					break;
				case 92: //1x4
					dim = new Vector2(0, 5);
					break;
				case 16: //2x1
				case 18:
				case 29:
				case 103:
				case 134:
				case 387:
				case 443:
					dim = new Vector2(1, 0);
					break;
				case 21: //2x2
				case 35:
				case 55:
				case 85:
				case 94:
				case 95:
				case 96:
				case 97:
				case 98:
				case 99:
				case 100:
				case 125:
				case 126:
				case 132:
				case 138:
				case 139:
				case 142:
				case 143:
				case 173:
				case 254:
				case 287:
				case 288:
				case 289:
				case 290:
				case 291:
				case 292:
				case 293:
				case 294:
				case 295:
				case 316:
				case 317:
				case 318:
				case 319:
				case 335:
				case 360:
				case 376:
				case 386:
				case 395:
				case 411:
				case 425:
				case 441:
					dim = new Vector2(1, 1);
					break;
				case 105: //2x3
				case 128:
				case 245:
				case 269:
				case 320:
				case 337:
				case 349:
				case 356:
				case 378:
				case 410:
					dim = new Vector2(1, 2);
					break;
				case 27:
				case 207:
					dim = new Vector2(1, 3);
					break;
				case 104:
					dim = new Vector2(1, 4);
					break;
				case 10:
					dim = new Vector2(0, 2);
					break;
				case 14:
					if (style == 25)//dynasty table
						dim = new Vector2(2, 0);
					else
						dim = new Vector2(2, 1);
					break;
				case 17:
				case 26:
				case 77:
				case 86:
				case 87:
				case 88:
				case 89:
				case 114:
				case 133:
				case 186:
				case 187:
				case 215:
				case 217:
				case 218:
				case 237:
				case 244:
				case 246:
				case 285:
				case 286:
				case 298:
				case 299:
				case 310:
				case 339:
				case 354:
				case 355:
				case 361:
				case 362:
				case 363:
				case 364:
				case 377:
				case 391:
				case 392:
				case 393:
				case 394:
				case 405:
					dim = new Vector2(2, 1);
					break;
				case 235:
					dim = new Vector2(2, 0);
					break;
				case 34:
				case 106:
				case 212:
				case 219:
				case 220:
				case 228:
				case 231:
				case 240:
				case 243:
				case 247:
				case 283:
				case 300:
				case 301:
				case 302:
				case 303:
				case 304:
				case 305:
				case 306:
				case 307:
				case 308:
				case 334:
				case 406:
				case 412:
				case 440:
					dim = new Vector2(2, 2);
					break;
				case 101:
				case 102:
					dim = new Vector2(2, 3);
					break;
				case 79:
				case 90:
					dim = new Vector2(3, 1);
					break;
				case 209:
				case 241:
					dim = new Vector2(3, 2);
					break;
				case 275:
				case 276:
				case 277:
				case 278:
				case 279:
				case 280:
				case 281:
				case 296:
				case 297:
				case 309:
				case 358:
				case 359:
				case 413:
				case 414:
					dim = new Vector2(5, 2);
					break;
				case 242:
					dim = new Vector2(5, 3);
					break;
				default:
					break;
			}
			return dim;
		}
		//This finds the 0,0 of a furniture
		static Vector2 adjustDest(ref Vector2 dest, Tile tile, int which, int div, byte style)
		{
			Vector2 relative = new Vector2(0, 0);
			if (dest.X < 0)
			{
				//no destination
				dest.X = dest.Y = 0;
				return relative;
			}
			int frameX = tile.frameX;
			int frameY = tile.frameY;
			int relx = 0;
			int rely = 0;
			//Remove data from Mannequins before adjusting
			if (tile.type == 128 || tile.type == 269)
				frameX %= 100;
			switch (which)
			{
				case 0:
					relx = (frameX % div) / 18;
					rely = (frameY) / 18;
					break;
				case 1:
					relx = (frameX) / 18;
					rely = (frameY % div) / 18;
					break;
				case 2:
					relx = (frameX % div) / 18;
					rely = (frameY % div) / 18;
					break;
				case 3: // Statues have style split, possibly more use this?
					rely = (frameY % 54) / 18;
					relx = (frameX % 36) / 18;
					break;
				default:
					relx = (frameX) / 18;
					rely = (frameY) / 18;
					break;
			}
			if (tile.type == 55 || tile.type == 425)//sign
			{
				switch (style)
				{
					case 1:
					case 2:
						dest.Y--;
						break;
					case 3:
						dest.Y--;
						dest.X++;
						break;
				}
			}
			else if (tile.type == 11)//opened door
			{
				if (frameX / 36 > 0)
				{
					relx -= 2;
					dest.X++;
				}
			}
			else if (tile.type == 10 || tile.type == 15)// random frames, ignore X
			{
				relx = 0;
			}
			else if (tile.type == 209)//cannoonn
			{
				rely = (frameY % 37) / 18;
			}
			else if (tile.type == 79 || tile.type == 90)//bed,bathtub
			{
				relx = (frameX % 72) / 18;
			}
			else if (tile.type == 14 && style == 25)
			{
				dest.Y--;
			}
			else if (tile.type == 334)
			{
				rely = frameY / 18;
				int tx = frameX;
				if (frameX > 5000)
					tx = ((frameX / 5000) - 1) * 18;
				if (tx >= 54)
					tx = (tx - 54);
				relx = tx / 18;
			}
			relative = new Vector2(relx, rely);

			return relative;
		}
		//This takes a coordinate on top of a furniture and returns the correct "placement" location it would have used.
		static void adjustFurniture(ref int x, ref int y, ref byte style, bool origin = false)
		{
			int which = 10; // An invalid which, to skip cases if it never changes.
			int div = 1;
			Tile tile = Main.tile[x, y];
			getPlaceData(tile.type, ref which, ref div);
			switch (which)
			{
				case 0:
					style = (byte)(tile.frameX / div);
					break;
				case 1:
					style = (byte)(tile.frameY / div);
					break;
				case 2:
					style = (byte)((tile.frameY / div) * 36 + (tile.frameX / div));
					break;
				case 3: //Just statues for now
					style = (byte)((tile.frameX / 36) + (tile.frameY / 54) * 55);
					break;
				default:
					break;
			}
			if (style < 0) style = 0;
			if (!Main.tileFrameImportant[tile.type]) return;
			if (div == 1) div = 0xFFFF;
			Vector2 dest = destFrame(tile.type);
			Vector2 relative = adjustDest(ref dest, tile, which, div, style);
			if (origin) dest = new Vector2(0, 0);
			x += (int)(dest.X - relative.X);
			y += (int)(dest.Y - relative.Y);

		}
		public static void paintFurniture(ushort type, int x, int y, byte paint)
		{

			byte style = 0;
			adjustFurniture(ref x, ref y, ref style, true);
			Vector2 size = furnitureDimensions(type, style);
			for (int j = x; j <= x + size.X; j++)
			{
				for (int k = y; k <= y + size.Y; k++)
				{
					if (Main.tile[j, k].type == type)
					{
						Main.tile[j, k].color(paint);
					}
				}
			}
		}

		bool[] breakableBottom = new bool[Main.maxTileSets];
		bool[] breakableTop = new bool[Main.maxTileSets];
		bool[] breakableSides = new bool[Main.maxTileSets];
		bool[] breakableWall = new bool[Main.maxTileSets];
		void initBreaks()
		{
			breakableBottom[4] = true;
			breakableBottom[10] = true;
			breakableBottom[11] = true;
			breakableBottom[13] = true;
			breakableBottom[14] = true;
			breakableBottom[15] = true;
			breakableBottom[16] = true;
			breakableBottom[17] = true;
			breakableBottom[18] = true;
			breakableBottom[21] = true;
			breakableBottom[26] = true;
			breakableBottom[27] = true;
			breakableBottom[29] = true;
			breakableBottom[33] = true;
			breakableBottom[35] = true;
			breakableBottom[49] = true;
			breakableBottom[50] = true;
			breakableBottom[55] = true;
			breakableBottom[77] = true;
			breakableBottom[78] = true;
			breakableBottom[79] = true;
			breakableBottom[81] = true;
			breakableBottom[82] = true;
			breakableBottom[85] = true;
			breakableBottom[86] = true;
			breakableBottom[87] = true;
			breakableBottom[88] = true;
			breakableBottom[89] = true;
			breakableBottom[90] = true;
			breakableBottom[92] = true;
			breakableBottom[93] = true;
			breakableBottom[94] = true;
			breakableBottom[96] = true;
			breakableBottom[97] = true;
			breakableBottom[98] = true;
			breakableBottom[99] = true;
			breakableBottom[100] = true;
			breakableBottom[101] = true;
			breakableBottom[102] = true;
			breakableBottom[103] = true;
			breakableBottom[104] = true;
			breakableBottom[105] = true;
			breakableBottom[106] = true;
			breakableBottom[114] = true;
			breakableBottom[125] = true;
			breakableBottom[128] = true;
			breakableBottom[129] = true;
			breakableBottom[132] = true;
			breakableBottom[133] = true;
			breakableBottom[134] = true;
			breakableBottom[135] = true;
			breakableBottom[136] = true;
			breakableBottom[138] = true;
			breakableBottom[139] = true;
			breakableBottom[142] = true;
			breakableBottom[143] = true;
			breakableBottom[144] = true;
			breakableBottom[149] = true;
			breakableBottom[173] = true;
			breakableBottom[174] = true;
			breakableBottom[178] = true;
			breakableBottom[186] = true;
			breakableBottom[187] = true;
			breakableBottom[207] = true;
			breakableBottom[209] = true;
			breakableBottom[212] = true;
			breakableBottom[215] = true;
			breakableBottom[216] = true;
			breakableBottom[217] = true;
			breakableBottom[218] = true;
			breakableBottom[219] = true;
			breakableBottom[220] = true;
			//breakableBottom[227] = true; DYES, SOME GROW ON TOP?
			breakableBottom[228] = true;
			breakableBottom[231] = true;
			breakableBottom[235] = true;
			breakableBottom[237] = true;
			breakableBottom[239] = true;
			breakableBottom[243] = true;
			breakableBottom[244] = true;
			breakableBottom[247] = true;
			breakableBottom[254] = true;
			breakableBottom[269] = true;
			breakableBottom[275] = true;
			breakableBottom[276] = true;
			breakableBottom[278] = true;
			breakableBottom[279] = true;
			breakableBottom[280] = true;
			breakableBottom[281] = true;
			breakableBottom[283] = true;
			breakableBottom[285] = true;
			breakableBottom[286] = true;
			breakableBottom[287] = true;
			breakableBottom[296] = true;
			breakableBottom[297] = true;
			breakableBottom[298] = true;
			breakableBottom[299] = true;
			breakableBottom[300] = true;
			breakableBottom[301] = true;
			breakableBottom[302] = true;
			breakableBottom[303] = true;
			breakableBottom[304] = true;
			breakableBottom[305] = true;
			breakableBottom[306] = true;
			breakableBottom[307] = true;
			breakableBottom[308] = true;
			breakableBottom[309] = true;
			breakableBottom[310] = true;
			breakableBottom[316] = true;
			breakableBottom[317] = true;
			breakableBottom[318] = true;
			breakableBottom[319] = true;
			breakableBottom[320] = true;
			breakableBottom[335] = true;
			breakableBottom[337] = true;
			breakableBottom[338] = true;
			breakableBottom[339] = true;
			breakableBottom[349] = true;
			breakableBottom[354] = true;
			breakableBottom[355] = true;
			breakableBottom[356] = true;
			breakableBottom[358] = true;
			breakableBottom[359] = true;
			breakableBottom[360] = true;
			breakableBottom[361] = true;
			breakableBottom[362] = true;
			breakableBottom[363] = true;
			breakableBottom[364] = true;
			breakableBottom[372] = true;
			breakableBottom[376] = true;
			breakableBottom[377] = true;
			breakableBottom[378] = true;
			breakableBottom[380] = true;
			breakableBottom[380] = true;
			breakableBottom[388] = true;
			breakableBottom[389] = true;
			breakableBottom[390] = true;
			breakableBottom[391] = true;
			breakableBottom[392] = true;
			breakableBottom[393] = true;
			breakableBottom[394] = true;
			breakableBottom[405] = true;
			breakableBottom[406] = true;
			breakableBottom[410] = true;
			breakableBottom[413] = true;
			breakableBottom[414] = true;
			breakableBottom[419] = true;
			breakableBottom[425] = true;
			breakableBottom[441] = true;
			breakableBottom[442] = true;
			breakableBottom[443] = true;

			breakableTop[10] = true;
			breakableTop[11] = true;
			breakableTop[34] = true;
			breakableTop[42] = true;
			breakableTop[55] = true;
			breakableTop[91] = true;
			breakableTop[95] = true;//chinese lantern
			breakableTop[126] = true;
			breakableTop[129] = true;
			breakableTop[149] = true;
			breakableTop[270] = true;
			breakableTop[271] = true;
			breakableTop[380] = true;
			breakableTop[388] = true;
			breakableTop[389] = true;
			breakableTop[425] = true;
			breakableTop[443] = true;

			breakableSides[4] = true;
			breakableSides[55] = true;
			breakableSides[129] = true;
			breakableSides[136] = true;
			breakableSides[149] = true;
			breakableSides[380] = true;
			breakableSides[386] = true;
			breakableSides[387] = true;
			breakableSides[425] = true;

			breakableWall[4] = true;
			breakableWall[132] = true;
			breakableWall[136] = true;
			breakableWall[240] = true;
			breakableWall[241] = true;
			breakableWall[242] = true;
			breakableWall[245] = true;
			breakableWall[246] = true;
			breakableWall[334] = true;
			breakableWall[380] = true;
			breakableWall[395] = true;
			breakableWall[440] = true;
		}
		bool regionCheck(TSPlayer who, int x, int y)
		{
			return who.Group.HasPermission(Permissions.editregion) || TShock.Regions.CanBuild(x, y, who);
		}
		void logEdit(byte etype, Tile tile, int X, int Y, ushort type, string account, List<Vector2> done, byte style = 0, int alt = 0, int random = -1, bool direction = false)
		{
			switch (etype)
			{
				case 0: //killtile
				case 4: //killtilenoitem
					ushort tileType = Main.tile[X, Y].type;
					byte pStyle = 0;
					if (Main.tile[X, Y].active() && !Main.tileCut[tileType] && tileType != 127)
					{
						adjustFurniture(ref X, ref Y, ref pStyle);
						//Don't repeat the same tile, and it is possible to create something that breaks thousands of tiles with one edit, is this a sane limit?
						if (done.Contains(new Vector2(X, Y)) || done.Count > 2000)
							return;
						done.Add(new Vector2(X, Y));
						//TODO: Sand falling from a solid tile broken below
						switch (tileType)
						{
							case 10: //doors don't break anything anyway
							case 11: //close open doors
								tileType = 10;
								break;
							case 55: //Signs
							case 85: //Gravestones
							case 425: // Announcement
								int signI = Sign.ReadSign(X, Y);
								Queue(account, X, Y, 0, tileType, pStyle, (short)(Main.tile[X, Y].color()), text: Main.sign[signI].text);
								return;
							case 124: //wooden beam, breaks sides only
								if (Main.tile[X - 1, Y].active() && breakableSides[Main.tile[X - 1, Y].type])
									logEdit(0, Main.tile[X - 1, Y], X - 1, Y, 0, account, done);
								if (Main.tile[X + 1, Y].active() && breakableSides[Main.tile[X + 1, Y].type])
									logEdit(0, Main.tile[X + 1, Y], X + 1, Y, 0, account, done);
								break;
							case 128:
							case 269: //Mannequins
								int headSlot = Main.tile[X, Y - 2].frameX / 100;
								int bodySlot = Main.tile[X, Y - 1].frameX / 100;
								int legSlot = Main.tile[X, Y].frameX / 100;
								// The vars 'style' and 'random' cause mannequins to place improperly and can't be used.
								Queue(account, X, Y, 0, tileType, paint: (short)headSlot, alternate: bodySlot + (legSlot<<10), direction: (Main.tile[X, Y].frameX % 100) > 0);
								return;
							case 138: //boulder, 2x2
								for (int i = -1; i <= 0; i++)
									if (Main.tile[X + i, Y - 2].active() && breakableBottom[Main.tile[X + i, Y - 2].type])
										logEdit(0, Main.tile[X + i, Y - 2], X + i, Y - 2, 0, account, done);
								for (int i = -1; i <= 0; i++)
								{
									if (Main.tile[X - 2, Y + i].active() && breakableSides[Main.tile[X - 2, Y + i].type])
										logEdit(0, Main.tile[X - 2, Y + i], X - 2, Y + i, 0, account, done);
									if (Main.tile[X, Y + i].active() && breakableSides[Main.tile[X, Y + i].type])
										logEdit(0, Main.tile[X, Y + i], X, Y + i, 0, account, done);
								}
								break;
							case 235: //teleporter, 3x1
								for (int i = -1; i <= 1; i++)
									if (Main.tile[X + i, Y - 1].active() && breakableBottom[Main.tile[X + i, Y - 1].type])
										logEdit(0, Main.tile[X + i, Y - 1], X + i, Y - 1, 0, account, done);
								if (Main.tile[X - 2, Y].active() && breakableSides[Main.tile[X - 2, Y].type])
									logEdit(0, Main.tile[X - 2, Y], X - 2, Y, 0, account, done);
								if (Main.tile[X + 2, Y].active() && breakableSides[Main.tile[X + 2, Y].type])
									logEdit(0, Main.tile[X + 2, Y], X + 2, Y, 0, account, done);
								break;
							case 53: //sand, silt, slush
							case 112:
							case 116:
							case 123:
							case 224:
							case 234:
								List<int> types = new List<int>() { 53, 112, 116, 123, 224, 234 };
								int topY = Y;//Find top of stack
								while (topY >= 0 && Main.tile[X, topY].active() && types.Contains(Main.tile[X, topY].type))
									topY--;
								//Break anything at top
								if (Main.tile[X, topY].active() && breakableBottom[Main.tile[X, topY].type])
									logEdit(0, Main.tile[X, topY], X, topY, 0, account, done);
								//TO-DO: Atm, we'll just keep the record saying they broke the top block. We lose some data (type of sand), but I don't feel like
								// making a workaround for that just yet.
								topY++;
								return;
							case 239: //bars
								topY = Y;//Find top of stack
								while (topY >= 0 && Main.tile[X, topY].active() && Main.tile[X, topY].type == 239)
									topY--;
								//Break anything at top
								if (Main.tile[X, topY].active() && breakableBottom[Main.tile[X, topY].type])
									logEdit(0, Main.tile[X, topY], X, topY, 0, account, done);
								topY++;
								while (topY <= Y)
								{
									//log from top of stack down, so reverting goes bottom->top
									if (Main.tile[X - 1, topY].active() && breakableSides[Main.tile[X - 1, topY].type])
										logEdit(0, Main.tile[X - 1, topY], X - 1, topY, 0, account, done);
									if (Main.tile[X + 1, topY].active() && breakableSides[Main.tile[X + 1, topY].type])
										logEdit(0, Main.tile[X + 1, topY], X + 1, topY, 0, account, done);
									Queue(account, X, topY, 0, 239, pStyle, (short)(Main.tile[X, topY].color() + ((Main.tile[X, topY].halfBrick() ? 1 : 0) << 7)));
									topY++;
								}
								return;
							case 314: //Minecart Track
								for (int i = -1; i < 2; i++)
									for (int j = -1; j < 2; j++)
									{
										if (Main.tile[X + i, Y + j].active() && Main.tile[X + i, Y + j].type == 314)
										{
											Queue(account, X + i, Y + j, 0, 314, (byte)(Main.tile[X + i, Y + j].frameX + 1), (short)(Main.tile[X + i, Y + j].color() + ((Main.tile[X + i, Y + j].frameY + 1) << 8)));
										}
									}
								return;
							case 334: //Weapon Racks
								//X and Y are already normalized to the center, Center is item prefix, X-1 is NetID
								short prefix = (short)(Main.tile[X, Y].frameX % 5000);
								int netID = (Main.tile[X - 1, Y].frameX % 5000) - 100;
								if (netID < 0) break;
								Queue(account, X, Y, 0, 334, paint: prefix, alternate: netID, direction: Main.tile[X, Y + 1].frameX > 54);
								return;
							//case 395: //Item Frame
							//TEItemFrame tEItemFrame = (TEItemFrame)TileEntity.ByPosition[new Point16(X, Y)];
							//Console.WriteLine(tEItemFrame.ToString());
							//Queue(account, X, Y, 0, 395, paint: (short)(Main.tile[X, Y].color()), random: tEItemFrame.item.prefix, alternate: tEItemFrame.item.type);
							//return;
							default:
								if (Main.tileSolid[tileType])
								{
									if (Main.tile[X, Y - 1].active() && breakableBottom[Main.tile[X, Y - 1].type])
										logEdit(0, Main.tile[X, Y - 1], X, Y - 1, 0, account, done);
									if (Main.tile[X, Y + 1].active() && breakableTop[Main.tile[X, Y + 1].type])
										logEdit(0, Main.tile[X, Y + 1], X, Y + 1, 0, account, done);
									if (Main.tile[X - 1, Y].active() && breakableSides[Main.tile[X - 1, Y].type])
										logEdit(0, Main.tile[X - 1, Y], X - 1, Y, 0, account, done);
									if (Main.tile[X + 1, Y].active() && breakableSides[Main.tile[X + 1, Y].type])
										logEdit(0, Main.tile[X + 1, Y], X + 1, Y, 0, account, done);
								}
								else if (Main.tileTable[tileType])
								{
									int baseStart = -1;
									int baseEnd = 1;
									int height = 2;
									switch (tileType)
									{
										case 18://workbench
											baseStart = 0;
											height = 1;
											break;
										case 19://platform
											baseStart = baseEnd = 0;
											height = 1;
											break;
										case 101://bookcase
											height = 4;
											break;
										case 14://table
											if (style == 25)//dynasty table
												height = 1;
											break;
										default://3X2
											break;
									}
									for (int i = baseStart; i <= baseEnd; i++)
									{
										if (Main.tile[X + i, Y - height].active() && breakableBottom[Main.tile[X + i, Y - height].type])
											logEdit(0, Main.tile[X + i, Y - height], X + i, Y - height, 0, account, done);
									}
								}
								break;
						}
						Queue(account, X, Y, 0, tileType, pStyle, (short)(Main.tile[X, Y].color() + (Main.tile[X, Y].halfBrick() ? 128 : 0) + (Main.tile[X, Y].slope() << 8)), null, alt, random, direction);
					}
					break;
				case 1://add tile
					if ((!Main.tile[X, Y].active() || Main.tileCut[Main.tile[X, Y].type]) && type != 127)
					{
						Queue(account, X, Y, 1, type, style);
					}
					break;
				case 2://del wall
					if (Main.tile[X, Y].wall != 0)
					{
						//break things on walls
						if (Main.tile[X, Y].active() && breakableWall[Main.tile[X, Y].type])
							logEdit(0, tile, X, Y, 0, account, done);
						Queue(account, X, Y, 2, Main.tile[X, Y].wall, 0, Main.tile[X, Y].wallColor());
					}
					break;
				case 3://add wall
					if (Main.tile[X, Y].wall == 0)
					{
						Queue(account, X, Y, 3, type);
					}
					break;
				case 5:
					if (!Main.tile[X, Y].wire())
					{
						Queue(account, X, Y, 5);
					}
					break;
				case 6:
					if (Main.tile[X, Y].wire())
					{
						Queue(account, X, Y, 6);
					}
					break;
				case 7:
					Queue(account, X, Y, 7);
					break;
				case 8:
					if (!Main.tile[X, Y].actuator())
					{
						Queue(account, X, Y, 8);
					}
					break;
				case 9:
					if (Main.tile[X, Y].actuator())
					{
						Queue(account, X, Y, 9);
					}
					break;
				case 10:
					if (!Main.tile[X, Y].wire2())
					{
						Queue(account, X, Y, 10);
					}
					break;
				case 11:
					if (Main.tile[X, Y].wire2())
					{
						Queue(account, X, Y, 11);
					}
					break;
				case 12:
					if (!Main.tile[X, Y].wire3())
					{
						Queue(account, X, Y, 12);
					}
					break;
				case 13:
					if (Main.tile[X, Y].wire3())
					{
						Queue(account, X, Y, 13);
					}
					break;
				case 14:
					//save previous state of slope
					Queue(account, X, Y, 14, type, 0, (short)(((Main.tile[X, Y].halfBrick() ? 1 : 0) << 7) + (Main.tile[X, Y].slope() << 8)));
					break;
				case 15:
					Queue(account, X, Y, 15);
					break;
				case 16:
					if (!Main.tile[X, Y].wire4())
					{
						Queue(account, X, Y, 16);
					}
					break;
				case 17:
					if (Main.tile[X, Y].wire4())
					{
						Queue(account, X, Y, 17);
					}
					break;
			}
		}

		void OnGetData(GetDataEventArgs e)
		{
			if (!e.Handled)
			{
				switch (e.MsgID)
				{
					case PacketTypes.PlaceItemFrame:
						//TSPlayer.All.SendInfoMessage("Placing item frame!");
						break;
					case PacketTypes.PlaceTileEntity:
						//TSPlayer.All.SendInfoMessage("Placing tile entity!");
						break;
					case PacketTypes.UpdateTileEntity:
						//TSPlayer.All.SendInfoMessage("Updating tile entity!");
						break;
					case PacketTypes.Tile:
						{
							byte etype = e.Msg.readBuffer[e.Index];
							int X = BitConverter.ToInt16(e.Msg.readBuffer, e.Index + 1);
							int Y = BitConverter.ToInt16(e.Msg.readBuffer, e.Index + 3);
							ushort type = BitConverter.ToUInt16(e.Msg.readBuffer, e.Index + 5);
							byte style = e.Msg.readBuffer[e.Index + 7];
							if (type == 1 && (etype == 0 || etype == 4))
							{
								if (Main.tile[X, Y].type == 21 || Main.tile[X, Y].type == 88)
									return; //Chests and dressers handled separately
								//else if (Main.tile[X, Y].type == 2699)
								//TSPlayer.All.SendInfoMessage("Weapon rack place");
							}
							//DEBUG
							//TSPlayer.All.SendInfoMessage($"Type: {type}");
							if (X >= 0 && Y >= 0 && X < Main.maxTilesX && Y < Main.maxTilesY)
							{
								if (AwaitingHistory[e.Msg.whoAmI])
								{
									AwaitingHistory[e.Msg.whoAmI] = false;
									TShock.Players[e.Msg.whoAmI].SendTileSquare(X, Y, 5);
									//DEBUG
									//TSPlayer.All.SendInfoMessage($"X: {X}, Y: {Y}, FrameX: {Main.tile[X, Y].frameX}, FrameY: {Main.tile[X, Y].frameY}");
									e.Handled = true;
									//END DEBUG
									if (type == 0 && (etype == 0 || etype == 4))
										adjustFurniture(ref X, ref Y, ref style);
									CommandQueue.Add(new HistoryCommand(X, Y, TShock.Players[e.Msg.whoAmI]));
									e.Handled = true;
								}
								else if (regionCheck(TShock.Players[e.Msg.whoAmI], X, Y))
								{
									//effect only
									if (type == 1 && (etype == 0 || etype == 2 || etype == 4))
										return;
									logEdit(etype, Main.tile[X, Y], X, Y, type, TShock.Players[e.Msg.whoAmI].User.Name, new List<Vector2>(), style);
								}
							}
						}
						break;
					case PacketTypes.PlaceObject:
						{
							int X = BitConverter.ToInt16(e.Msg.readBuffer, e.Index);
							int Y = BitConverter.ToInt16(e.Msg.readBuffer, e.Index + 2);
							ushort type = BitConverter.ToUInt16(e.Msg.readBuffer, e.Index + 4);
							int style = BitConverter.ToInt16(e.Msg.readBuffer, e.Index + 6);
							//DEBUG:
							//TSPlayer.All.SendInfoMessage($"Style: {style}");
							int alt = (byte)e.Msg.readBuffer[e.Index + 8];
							//TSPlayer.All.SendInfoMessage($"Alternate: {alt}");
							int rand = (sbyte)e.Msg.readBuffer[e.Index + 9];
							//TSPlayer.All.SendInfoMessage($"Random: {rand}");
							bool dir = BitConverter.ToBoolean(e.Msg.readBuffer, e.Index + 10);
							if (X >= 0 && Y >= 0 && X < Main.maxTilesX && Y < Main.maxTilesY)
							{
								if (AwaitingHistory[e.Msg.whoAmI])
								{
									AwaitingHistory[e.Msg.whoAmI] = false;
									TShock.Players[e.Msg.whoAmI].SendTileSquare(X, Y, 5);
									CommandQueue.Add(new HistoryCommand(X, Y, TShock.Players[e.Msg.whoAmI]));
									e.Handled = true;
								}
								else if (regionCheck(TShock.Players[e.Msg.whoAmI], X, Y))
								{
									logEdit(1, Main.tile[X, Y], X, Y, type, TShock.Players[e.Msg.whoAmI].User.Name, new List<Vector2>(), (byte)style, alt, rand, dir);
								}
							}
						}
						break;
					//chest delete
					case PacketTypes.TileKill:
						{
							byte flag = e.Msg.readBuffer[e.Index];
							int X = BitConverter.ToInt16(e.Msg.readBuffer, e.Index + 1);
							int Y = BitConverter.ToInt16(e.Msg.readBuffer, e.Index + 3);
							int style = BitConverter.ToInt16(e.Msg.readBuffer, e.Index + 5);
							byte style2 = (byte)style;
							if (X >= 0 && Y >= 0 && X < Main.maxTilesX && Y < Main.maxTilesY)
							{
								//PlaceChest
								if (flag == 0 && regionCheck(TShock.Players[e.Msg.whoAmI], X, Y))
								{
									if (AwaitingHistory[e.Msg.whoAmI])
									{
										AwaitingHistory[e.Msg.whoAmI] = false;
										TShock.Players[e.Msg.whoAmI].SendTileSquare(X, Y, 5);
										CommandQueue.Add(new HistoryCommand(X, Y, TShock.Players[e.Msg.whoAmI]));
										e.Handled = true;
									}
									else if (regionCheck(TShock.Players[e.Msg.whoAmI], X, Y))
									{
										logEdit(1, Main.tile[X, Y], X, Y, 21, TShock.Players[e.Msg.whoAmI].User.Name, new List<Vector2>(), style2);
									}
									return;
								}
								//KillChest
								if (flag == 1 && regionCheck(TShock.Players[e.Msg.whoAmI], X, Y) && Main.tile[X, Y].type == 21)
								{
									if (AwaitingHistory[e.Msg.whoAmI])
									{
										AwaitingHistory[e.Msg.whoAmI] = false;
										TShock.Players[e.Msg.whoAmI].SendTileSquare(X, Y, 5);
										adjustFurniture(ref X, ref Y, ref style2);
										CommandQueue.Add(new HistoryCommand(X, Y, TShock.Players[e.Msg.whoAmI]));
										e.Handled = true;
										return;
									}
									adjustFurniture(ref X, ref Y, ref style2);
									Queue(TShock.Players[e.Msg.whoAmI].User.Name, X, Y, 0, Main.tile[X, Y].type, style2, Main.tile[X, Y].color());
									return;
								}
								//PlaceDresser
								if (flag == 2 && regionCheck(TShock.Players[e.Msg.whoAmI], X, Y))
								{
									if (AwaitingHistory[e.Msg.whoAmI])
									{
										AwaitingHistory[e.Msg.whoAmI] = false;
										TShock.Players[e.Msg.whoAmI].SendTileSquare(X, Y, 5);
										CommandQueue.Add(new HistoryCommand(X, Y, TShock.Players[e.Msg.whoAmI]));
										e.Handled = true;
									}
									else if (regionCheck(TShock.Players[e.Msg.whoAmI], X, Y))
									{
										logEdit(1, Main.tile[X, Y], X, Y, 88, TShock.Players[e.Msg.whoAmI].User.Name, new List<Vector2>(), style2);
									}
									return;
								}
								//KillDresser
								if (flag == 3 && regionCheck(TShock.Players[e.Msg.whoAmI], X, Y) && Main.tile[X, Y].type == 88)
								{
									if (AwaitingHistory[e.Msg.whoAmI])
									{
										AwaitingHistory[e.Msg.whoAmI] = false;
										TShock.Players[e.Msg.whoAmI].SendTileSquare(X, Y, 5);
										adjustFurniture(ref X, ref Y, ref style2);
										CommandQueue.Add(new HistoryCommand(X, Y, TShock.Players[e.Msg.whoAmI]));
										e.Handled = true;
										return;
									}
									adjustFurniture(ref X, ref Y, ref style2);
									Queue(TShock.Players[e.Msg.whoAmI].User.Name, X, Y, 0, Main.tile[X, Y].type, style2, Main.tile[X, Y].color());
									return;
								}

							}
						}
						break;
					case PacketTypes.PaintTile:
						{
							int X = BitConverter.ToInt16(e.Msg.readBuffer, e.Index);
							int Y = BitConverter.ToInt16(e.Msg.readBuffer, e.Index + 2);
							byte color = e.Msg.readBuffer[e.Index + 4];
							if (regionCheck(TShock.Players[e.Msg.whoAmI], X, Y))
							{
								Queue(TShock.Players[e.Msg.whoAmI].User.Name, X, Y, 25, color, 0, Main.tile[X, Y].color());
							}
						}
						break;
					case PacketTypes.PaintWall:
						{
							int X = BitConverter.ToInt16(e.Msg.readBuffer, e.Index);
							int Y = BitConverter.ToInt16(e.Msg.readBuffer, e.Index + 2);
							byte color = e.Msg.readBuffer[e.Index + 4];
							if (regionCheck(TShock.Players[e.Msg.whoAmI], X, Y))
							{
								Queue(TShock.Players[e.Msg.whoAmI].User.Name, X, Y, 26, color, 0, Main.tile[X, Y].wallColor());
							}
						}
						break;
					case PacketTypes.SignNew:
						{
							ushort signI = BitConverter.ToUInt16(e.Msg.readBuffer, e.Index);
							int X = BitConverter.ToInt16(e.Msg.readBuffer, e.Index + 2);
							int Y = BitConverter.ToInt16(e.Msg.readBuffer, e.Index + 4);
							byte s = 0;
							adjustFurniture(ref X, ref Y, ref s); //Adjust coords so history picks it up, readSign() adjusts back to origin anyway
							Queue(TShock.Players[e.Msg.whoAmI].User.Name, X, Y, 27, data: signI, text: Main.sign[signI].text);
						}
						break;
					case PacketTypes.MassWireOperation:
						{
							int X1 = BitConverter.ToInt16(e.Msg.readBuffer, e.Index);
							int Y1 = BitConverter.ToInt16(e.Msg.readBuffer, e.Index + 2);
							int X2 = BitConverter.ToInt16(e.Msg.readBuffer, e.Index + 4);
							int Y2 = BitConverter.ToInt16(e.Msg.readBuffer, e.Index + 6);
							byte toolMode = e.Msg.readBuffer[e.Index + 8];
							//Modes Red=1, Green=2, Blue=4, Yellow=8, Actuator=16, Cutter=32

							bool direction = Main.player[e.Msg.whoAmI].direction == 1;
							int minX = X1, maxX = X2, minY = Y1, maxY = Y2;
							int drawX = direction ? minX : maxX;
							int drawY = direction ? maxY : minY;
							if (X2 < X1)
							{
								minX = X2;
								maxX = X1;
							}
							if (Y2 < Y1)
							{
								minY = Y2;
								maxY = Y1;
							}
							int wires = 0, acts = 0;
							if ((toolMode & 32) == 0)
								countPlayerWires(Main.player[e.Msg.whoAmI], ref wires, ref acts);

							for (int starty = minY; starty <= maxY; starty++)
							{
								if (regionCheck(TShock.Players[e.Msg.whoAmI], drawX, starty))
								{
									logAdvancedWire(drawX, starty, toolMode, TShock.Players[e.Msg.whoAmI].User.Name, ref wires, ref acts);
								}
							}
							for (int startx = minX; startx <= maxX; startx++)
							{
								if (startx == drawX)
									continue;
								if (regionCheck(TShock.Players[e.Msg.whoAmI], startx, drawY))
								{
									logAdvancedWire(startx, drawY, toolMode, TShock.Players[e.Msg.whoAmI].User.Name, ref wires, ref acts);
								}
							}
						}
						break;
				}
			}
		}
		void countPlayerWires(Player p, ref int wires, ref int acts)
		{
			wires = 0;
			acts = 0;
			for (int i = 0; i < 58; i++)
			{
				if (p.inventory[i].type == 530)
				{
					wires += p.inventory[i].stack;
				}
				if (p.inventory[i].type == 849)
				{
					acts += p.inventory[i].stack;
				}
			}
		}
		void logAdvancedWire(int x, int y, byte mode, string account, ref int wires, ref int acts)
		{
			bool delete = (mode & 32) == 32;
			if ((mode & 1) == 1 && Main.tile[x, y].wire() == delete) // RED
			{
				if (!delete)
				{
					if (wires <= 0)
						return;
					wires--;
				}
				//5 6
				Queue(account, x, y, (byte)(delete ? 6 : 5));
			}
			if ((mode & 2) == 2 && Main.tile[x, y].wire3() == delete) // GREEN
			{
				if (!delete)
				{
					if (wires <= 0)
						return;
					wires--;
				}
				//12 13
				Queue(account, x, y, (byte)(delete ? 13 : 12));
			}
			if ((mode & 4) == 4 && Main.tile[x, y].wire2() == delete) // BLUE
			{
				if (!delete)
				{
					if (wires <= 0)
						return;
					wires--;
				}
				//10 11
				Queue(account, x, y, (byte)(delete ? 11 : 10));
			}
			if ((mode & 8) == 8 && Main.tile[x, y].wire4() == delete) // YELLOW
			{
				if (!delete)
				{
					if (wires <= 0)
						return;
					wires--;
				}
				//16 and 17
				Queue(account, x, y, (byte)(delete ? 17 : 16));
			}
			if ((mode & 16) == 16 && Main.tile[x, y].actuator() == delete) // ACTUATOR
			{
				if (!delete)
				{
					if (acts <= 0)
						return;
					acts--;
				}
				//8 9
				Queue(account, x, y, (byte)(delete ? 9 : 8));
			}
		}
		void OnInitialize(EventArgs e)
		{
			TShockAPI.Commands.ChatCommands.Add(new Command("history.get", HistoryCmd, "history"));
			TShockAPI.Commands.ChatCommands.Add(new Command("history.prune", Prune, "prunehist"));
			TShockAPI.Commands.ChatCommands.Add(new Command("history.reenact", Reenact, "reenact"));
			TShockAPI.Commands.ChatCommands.Add(new Command("history.rollback", Rollback, "rollback"));
			TShockAPI.Commands.ChatCommands.Add(new Command("history.rollback", Undo, "rundo"));

			switch (TShock.Config.StorageType.ToLower())
			{
				case "mysql":
					string[] host = TShock.Config.MySqlHost.Split(':');
					Database = new MySqlConnection()
					{
						ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
							host[0],
							host.Length == 1 ? "3306" : host[1],
							TShock.Config.MySqlDbName,
							TShock.Config.MySqlUsername,
							TShock.Config.MySqlPassword)
					};
					break;
				case "sqlite":
					string sql = Path.Combine(TShock.SavePath, "history.sqlite");
					Database = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
					break;
			}
			SqlTableCreator sqlcreator = new SqlTableCreator(Database,
				Database.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
			sqlcreator.EnsureTableStructure(new SqlTable("History",
				new SqlColumn("Time", MySqlDbType.Int32),
				new SqlColumn("Account", MySqlDbType.VarChar) { Length = 50 },
				new SqlColumn("Action", MySqlDbType.Int32),
				new SqlColumn("XY", MySqlDbType.Int32),
				new SqlColumn("Data", MySqlDbType.Int32),
				new SqlColumn("Style", MySqlDbType.Int32),
				new SqlColumn("Paint", MySqlDbType.Int32),
				new SqlColumn("WorldID", MySqlDbType.Int32),
				new SqlColumn("Text", MySqlDbType.VarChar) { Length = 50 },
				new SqlColumn("Alternate", MySqlDbType.Int32),
				new SqlColumn("Random", MySqlDbType.Int32),
				new SqlColumn("Direction", MySqlDbType.Int32)));

			string datePath = Path.Combine(TShock.SavePath, "date.dat");
			if (!File.Exists(datePath))
			{
				File.WriteAllText(datePath, Date.ToString());
			}
			else
			{
				if (!DateTime.TryParse(File.ReadAllText(datePath), out Date))
				{
					Date = DateTime.UtcNow;
					File.WriteAllText(datePath, Date.ToString());
				}
			}
			CommandQueueThread = new Thread(QueueCallback);
			CommandQueueThread.Start();
		}
		void OnSaveWorld(WorldSaveEventArgs e)
		{
			new SaveCommand(Actions.ToArray()).Execute();
			Actions.Clear();
		}

		void QueueCallback(object t)
		{
			while (!Netplay.disconnect)
			{
				HCommand command;
				try
				{
					if (!CommandQueue.TryTake(out command, -1, Cancel.Token))
						return;
					try
					{
						command.Execute();
					}
					catch (Exception ex)
					{
						command.Error("An error occurred. Check the logs for more details.");
						TShock.Log.ConsoleError(ex.ToString());
					}
				}
				catch (OperationCanceledException)
				{
					return;
				}
			}
		}

		void HistoryCmd(CommandArgs e)
		{
			if (e.Parameters.Count > 0)
			{
				if (e.Parameters.Count != 2 && e.Parameters.Count != 3)
				{
					e.Player.SendErrorMessage("Invalid syntax! Proper syntax: /history [account] [time] [radius]");
					return;
				}
				int radius = 10000;
				int time;
				if (!TShock.Utils.TryParseTime(e.Parameters[1], out time) || time <= 0)
					e.Player.SendErrorMessage("Invalid time.");
				else if (e.Parameters.Count == 3 && (!int.TryParse(e.Parameters[2], out radius) || radius <= 0))
					e.Player.SendErrorMessage("Invalid radius.");
				else
					CommandQueue.Add(new InfoCommand(e.Parameters[0], time, radius, e.Player));
			}
			else
			{
				e.Player.SendMessage("Hit a block to get its history.", Color.LimeGreen);
				AwaitingHistory[e.Player.Index] = true;
			}
		}
		void Reenact(CommandArgs e)
		{
			if (e.Parameters.Count != 2 && e.Parameters.Count != 3)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: /reenact <account> <time> [radius]");
				return;
			}
			int radius = 10000;
			int time;
			if (!TShock.Utils.TryParseTime(e.Parameters[1], out time) || time <= 0)
			{
				e.Player.SendErrorMessage("Invalid time.");
			}
			else if (e.Parameters.Count == 3 && (!int.TryParse(e.Parameters[2], out radius) || radius <= 0))
			{
				e.Player.SendErrorMessage("Invalid radius.");
			}
			else
			{
				CommandQueue.Add(new RollbackCommand(e.Parameters[0], time, radius, e.Player, true));
			}
		}
		void Rollback(CommandArgs e)
		{
			if (e.Parameters.Count != 2 && e.Parameters.Count != 3)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: /rollback <account> <time> [radius]");
				return;
			}
			int radius = 10000;
			int time;
			if (!TShock.Utils.TryParseTime(e.Parameters[1], out time) || time <= 0)
				e.Player.SendErrorMessage("Invalid time.");
			else if (e.Parameters.Count == 3 && (!int.TryParse(e.Parameters[2], out radius) || radius <= 0))
				e.Player.SendErrorMessage("Invalid radius.");
			else
				CommandQueue.Add(new RollbackCommand(e.Parameters[0], time, radius, e.Player));
		}
		void Prune(CommandArgs e)
		{
			if (e.Parameters.Count != 1)
			{
				e.Player.SendErrorMessage("Invalid syntax! Proper syntax: /prunehist <time>");
				return;
			}
			int time;
			if (TShock.Utils.TryParseTime(e.Parameters[0], out time) && time > 0)
				CommandQueue.Add(new PruneCommand(time, e.Player));
			else
				e.Player.SendErrorMessage("Invalid time.");
		}
		void Undo(CommandArgs e)
		{
			if (UndoCommand.LastRollBack != null)
				CommandQueue.Add(new UndoCommand(e.Player));
			else
				e.Player.SendErrorMessage("Nothing to undo!");
		}
	}
}