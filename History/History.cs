using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using History.Commands;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace History
{
    [ApiVersion(1, 15)]
    public class History : TerrariaPlugin
    {
        public static List<Action> Actions = new List<Action>(SaveCount);
        public static IDbConnection Database;
        public static DateTime Date = DateTime.UtcNow;
        public const int SaveCount = 10;

        private bool[] AwaitingHistory = new bool[256];
        public override string Author
        {
            get { return "MarioE"; }
        }
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

        bool GetTime(string str, out int time)
        {
            int seconds;
            if (int.TryParse(str, out seconds))
            {
                time = seconds;
                return true;
            }

            StringBuilder timeConv = new StringBuilder();
            for (int i = 0; i < str.Length; i++)
            {
                if (char.IsDigit(str[i]) || (str[i] == '-' || str[i] == '+'))
                {
                    timeConv.Append(str[i]);
                }
                else
                {
                    int num;
                    if (!int.TryParse(timeConv.ToString(), out num))
                    {
                        time = 0;
                        return false;
                    }
                    timeConv.Clear();
                    switch (str[i])
                    {
                        case 's':
                            seconds += num;
                            break;
                        case 'm':
                            seconds += num * 60;
                            break;
                        case 'h':
                            seconds += num * 60 * 60;
                            break;
                        case 'd':
                            seconds += num * 60 * 60 * 24;
                            break;
                        default:
                            time = 0;
                            return false;
                    }
                }
            }
            time = seconds;
            return true;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.WorldSave.Deregister(this, OnSaveWorld);

                CommandQueueThread.Abort();
            }
        }
        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.WorldSave.Register(this, OnSaveWorld);
            initBreaks();
        }
        void Queue(string account, int X, int Y, byte action, ushort data = 0, byte style = 0, byte paint = 0)
        {
            if (Actions.Count == SaveCount)
            {
                CommandQueue.Add(new SaveCommand(Actions.ToArray()));
                Actions.Clear();
            }
            Actions.Add(new Action { account = account, action = action, data = data, time = (int)(DateTime.UtcNow - Date).TotalSeconds, x = X, y = Y, paint = paint, style = style });
        }
        static void getPlaceData(ushort type, ref int which, ref int div)
        {
            switch (type)
            {
                //WHICH 0:X   1:Y
                case 13: //bottle
                case 49: //blue candle
                case 174: //fancy candle
                case 78: //clay pot
                case 82: //herb
                case 83: //herb
                case 84: //herb
                case 91: //banner
                case 92: //lamppost
                case 93: //tikitorch
                case 144: //timer
                case 149: //christmas light
                case 178: //gems
                case 184:
                case 239: //bars
                    which = 0;
                    div = 18;
                    break;
                case 19:
                case 135:
                case 137:
                case 141:
                case 210:
                    which = 1;
                    div = 18;
                    break;
                case 4: //torch
                case 33: //candle
                    which = 1;
                    div = 22;
                    break;
                case 227:
                    which = 0;
                    div = 34;
                    break;
                case 16:
                case 18:
                case 21://chest
                case 27: //sunflower (randomness)
                case 29:
                case 55:// chest
                case 85: //tombstone
                case 103:
                case 104://grandfather
                case 105://statues
                case 128: //manniquin (orient)
                case 134:
                case 207:// water fountains
                case 245: //2x3 wall picture
                case 254:
                case 269:
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
                case 15:
                case 20:
                case 216:
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
                case 36:
                case 106:
                case 170:
                case 171:
                case 172:
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
                case 235://teleporter
                    which = 0;
                    div = 54;
                    break;
                case 10:
                case 11://door
                case 34://chandelier
                case 241://4x3 wall painting
                    which = 1;
                    div = 54;
                    break;
                case 240://painting, style stored in both
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
                default:
                    break;
            }
        }
        static Vector2 destFrame(ushort type)
        {
            Vector2 dest;
            switch (type)//(x,y) is from top left
            {
                case 42:
                case 16:
                case 18:
                case 29:
                case 103:
                case 134:
                case 270:
                case 271:
                case 91: // (0,0)
                    dest = new Vector2(0, 0);
                    break;

                case 139:
                case 35:
                case 21:
                case 85:
                case 55:
                case 216:
                case 245:
                case 15:
                case 10:
                case 11:// (0,1) DOOR, IGNORE FRAMEX*18 for 10
                    dest = new Vector2(0, 1);
                    break;
                case 34:
                case 95:
                case 126:
                case 246:
                case 235:// (1,0)
                    dest = new Vector2(1, 0);
                    break;
                case 132:
                case 138:
                case 142:
                case 143:
                case 282:
                case 288:
                case 289:
                case 290:
                case 291:
                case 292:
                case 293:
                case 294:
                case 295:
                case 94:
                case 79:
                case 90:
                case 240:
                case 241:
                case 97:
                case 98:
                case 99:
                case 100:
                case 125:
                case 254:
                case 96:
                case 14:
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
                case 285:
                case 286:
                case 287:
                case 298:
                case 299:
                case 310:
                case 173:// (1,1)
                    dest = new Vector2(1, 1);
                    break;
                case 106:
                case 212:
                case 219:
                case 220:
                case 228:
                case 231:
                case 243:
                case 209:
                case 247:// (1,2)
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
                    dest = new Vector2(1, 2);
                    break;
                case 101:
                case 102:// (1,3)
                    dest = new Vector2(1, 3);
                    break;
                case 242:// (2,2)
                    dest = new Vector2(2, 2);
                    break;
                case 128:
                case 105:
                case 269:
                case 93: // (0,2)
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
                case 281:// (1,3)
                    dest = new Vector2(1, 3);
                    break;
                default:
                    dest = new Vector2(-1, -1);
                    break;
            }
            return dest;
        }
        static Vector2 furnitureDimensions(ushort type)
        {
            Vector2 dim = new Vector2(0, 0);
            switch (type)
            {
                case 15:
                case 20:
                case 42:
                case 216:
                case 270://top
                case 271://top
                    dim = new Vector2(0, 1);
                    break;
                case 91:
                case 93:
                    dim = new Vector2(0, 2);
                    break;
                case 92:
                    dim = new Vector2(0, 5);
                    break;
                case 16:
                case 18:
                case 29:
                case 103:
                case 134:
                    dim = new Vector2(1, 0);
                    break;
                case 21:
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
                    dim = new Vector2(1, 1);
                    break;
                case 128:
                case 245:
                case 269:
                    dim = new Vector2(1, 2);
                    break;
                case 105:
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
        static void adjustFurniture(ref int x, ref int y, ref byte style)
        {
            int which = 10;
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
                default:
                    break;
            }
            if (!Main.tileFrameImportant[tile.type]) return;
            if (div == 1) div = 0xFFFF;
            Vector2 dest = destFrame(tile.type);
            if (dest.X >= 0)
            {
                int frameX = tile.frameX;
                int frameY = tile.frameY;
                int relx = 0;
                int rely = 0;
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
                    default:
                        relx = (frameX) / 18;
                        rely = (frameY) / 18;
                        break;
                }
                if (tile.type == 55)//sign
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
                else if (tile.type == 10)//closed door
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
                x += ((int)dest.X - relx);
                y += ((int)dest.Y - rely);
            }
        }
        public static void paintFurniture(ushort type, int x, int y, byte paint)
        {
            Vector2 dest = destFrame(type);
            Vector2 size = furnitureDimensions(type);
            //no destination
            if (dest.X < 0)
                dest = new Vector2(0, 0);
            for (int j = (int)(x - dest.X); j <= (x - dest.X + size.X); j++)
            {
                for (int k = (int)(y - dest.Y); k <= (y - dest.Y + size.Y); k++)
                {
                    Main.tile[j, k].color(paint);
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

            breakableSides[4] = true;
            breakableSides[55] = true;
            breakableSides[129] = true;
            breakableSides[136] = true;
            breakableSides[149] = true;

            breakableWall[4] = true;
            breakableWall[240] = true;
            breakableWall[241] = true;
            breakableWall[242] = true;
            breakableWall[245] = true;
            breakableWall[246] = true;
        }
        bool regionCheck(TSPlayer who, int x, int y)
        {
            return who.Group.HasPermission(Permissions.editregion) || TShock.Regions.CanBuild(x, y, who);
        }
        void logEdit(byte etype, Tile tile, int X, int Y, ushort type, string account, List<Vector2> done, byte style = 0)
        {
            switch (etype)
            {
                case 0:
                case 4://del tile
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
                            case 10://doors don't break anything anyway
                            case 11://close open doors
                                tileType = 10;
                                break;
                            case 124://wooden beam, breaks sides only
                                if (Main.tile[X - 1, Y].active() && breakableSides[Main.tile[X - 1, Y].type])
                                    logEdit(0, Main.tile[X - 1, Y], X - 1, Y, 0, account, done);
                                if (Main.tile[X + 1, Y].active() && breakableSides[Main.tile[X + 1, Y].type])
                                    logEdit(0, Main.tile[X + 1, Y], X + 1, Y, 0, account, done);
                                break;
                            case 138://boulder, 2x2
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
                            case 235://teleporter, 3x1
                                for (int i = -1; i <= 1; i++)
                                    if (Main.tile[X + i, Y - 1].active() && breakableBottom[Main.tile[X + i, Y - 1].type])
                                        logEdit(0, Main.tile[X + i, Y - 1], X + i, Y - 1, 0, account, done);
                                if (Main.tile[X - 2, Y].active() && breakableSides[Main.tile[X - 2, Y].type])
                                    logEdit(0, Main.tile[X - 2, Y], X - 2, Y, 0, account, done);
                                if (Main.tile[X + 2, Y].active() && breakableSides[Main.tile[X + 2, Y].type])
                                    logEdit(0, Main.tile[X + 2, Y], X + 2, Y, 0, account, done);
                                break;
                            case 239://bars
                                int topY = Y;//Find top of stack
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
                                    Queue(account, X, topY, 0, 239, pStyle, Main.tile[X, topY].color());
                                    topY++;
                                }
                                return;
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
                        Queue(account, X, Y, 0, tileType, pStyle, Main.tile[X, Y].color());
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
            }
        }

        void OnGetData(GetDataEventArgs e)
        {
            if (!e.Handled)
            {
                if (e.MsgID == PacketTypes.Tile)
                {
                    byte etype = e.Msg.readBuffer[e.Index];
                    int X = BitConverter.ToInt32(e.Msg.readBuffer, e.Index + 1);
                    int Y = BitConverter.ToInt32(e.Msg.readBuffer, e.Index + 5);
                    ushort type = BitConverter.ToUInt16(e.Msg.readBuffer, e.Index + 9);
                    byte style = e.Msg.readBuffer[e.Index + 11];
                    if (X >= 0 && Y >= 0 && X < Main.maxTilesX && Y < Main.maxTilesY)
                    {
                        if (AwaitingHistory[e.Msg.whoAmI])
                        {
                            AwaitingHistory[e.Msg.whoAmI] = false;
                            TShock.Players[e.Msg.whoAmI].SendTileSquare(X, Y, 5);
                            //See furniture edits on delete only
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
                            logEdit(etype, Main.tile[X, Y], X, Y, type, TShock.Players[e.Msg.whoAmI].UserAccountName, new List<Vector2>(), style);
                        }
                    }
                }
                //chest delete
                else if (e.MsgID == PacketTypes.TileKill)
                {
                    byte flag = e.Msg.readBuffer[e.Index];
                    int X = BitConverter.ToInt32(e.Msg.readBuffer, e.Index + 1);
                    int Y = BitConverter.ToInt32(e.Msg.readBuffer, e.Index + 5);
                    if (X >= 0 && Y >= 0 && X < Main.maxTilesX && Y < Main.maxTilesY)
                    {
                        if (flag == 0 && regionCheck(TShock.Players[e.Msg.whoAmI], X, Y) && Main.tile[X, Y].type == 21)//chest kill!
                        {
                            byte style = 0;
                            adjustFurniture(ref X, ref Y, ref style);
                            Queue(TShock.Players[e.Msg.whoAmI].UserAccountName, X, Y, 0, Main.tile[X, Y].type, style, Main.tile[X, Y].color());
                        }
                    }
                }
                else if (e.MsgID == PacketTypes.PaintTile)
                {
                    int X = BitConverter.ToInt32(e.Msg.readBuffer, e.Index);
                    int Y = BitConverter.ToInt32(e.Msg.readBuffer, e.Index + 4);
                    byte color = e.Msg.readBuffer[e.Index + 9];
                    if (regionCheck(TShock.Players[e.Msg.whoAmI], X, Y))
                    {
                        Queue(TShock.Players[e.Msg.whoAmI].UserAccountName, X, Y, 25, color, 0, Main.tile[X, Y].color());
                    }
                }
                else if (e.MsgID == PacketTypes.PaintWall)
                {
                    int X = BitConverter.ToInt32(e.Msg.readBuffer, e.Index);
                    int Y = BitConverter.ToInt32(e.Msg.readBuffer, e.Index + 4);
                    byte color = e.Msg.readBuffer[e.Index + 9];
                    if (regionCheck(TShock.Players[e.Msg.whoAmI], X, Y))
                    {
                        Queue(TShock.Players[e.Msg.whoAmI].UserAccountName, X, Y, 26, color, 0, Main.tile[X, Y].wallColor());
                    }
                }
            }
        }
        void OnInitialize(EventArgs e)
        {
            TShockAPI.Commands.ChatCommands.Add(new Command("history.get", HistoryCmd, "history"));
            TShockAPI.Commands.ChatCommands.Add(new Command("history.prune", Prune, "prunehist"));
            TShockAPI.Commands.ChatCommands.Add(new Command("history.reenact", Reenact, "reenact"));
            TShockAPI.Commands.ChatCommands.Add(new Command("history.rollback", Rollback, "rollback"));

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
            sqlcreator.EnsureExists(new SqlTable("History",
                new SqlColumn("Time", MySqlDbType.Int32),
                new SqlColumn("Account", MySqlDbType.Text),
                new SqlColumn("Action", MySqlDbType.Int32),
                new SqlColumn("XY", MySqlDbType.Int32),
                new SqlColumn("Data", MySqlDbType.Int32),
                new SqlColumn("Style", MySqlDbType.Int32),
                new SqlColumn("Paint", MySqlDbType.Int32),
                new SqlColumn("WorldID", MySqlDbType.Int32)));

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
            if (Main.rand == null)
            {
                Main.rand = new Random();
            }
            if (WorldGen.genRand == null)
            {
                WorldGen.genRand = new Random();
            }
            while (!Netplay.disconnect)
            {
                HCommand command = CommandQueue.Take();
                try
                {
                    command.Execute();
                }
                catch (Exception ex)
                {
                    command.Error("An error occurred. Check the logs for more details.");
                    Log.ConsoleError(ex.ToString());
                }
            }
        }

        void HistoryCmd(CommandArgs e)
        {
            e.Player.SendMessage("Hit a block to get its history.", Color.LimeGreen);
            AwaitingHistory[e.Player.Index] = true;
        }
        void Reenact(CommandArgs e)
        {
            if (e.Parameters.Count != 2 && e.Parameters.Count != 3)
            {
                e.Player.SendMessage("Invalid syntax! Proper syntax: /reenact <account> <time> [radius]", Color.Red);
                return;
            }
            int radius = 10000;
            int time;
            if (!GetTime(e.Parameters[1], out time) || time <= 0)
            {
                e.Player.SendMessage("Invalid time.", Color.Red);
            }
            else if (e.Parameters.Count == 3 && (!int.TryParse(e.Parameters[2], out radius) || radius <= 0))
            {
                e.Player.SendMessage("Invalid radius.", Color.Red);
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
                e.Player.SendMessage("Invalid syntax! Proper syntax: /rollback <account> <time> [radius]", Color.Red);
                return;
            }
            int radius = 10000;
            int time;
            if (!GetTime(e.Parameters[1], out time) || time <= 0)
            {
                e.Player.SendMessage("Invalid time.", Color.Red);
            }
            else if (e.Parameters.Count == 3 && (!int.TryParse(e.Parameters[2], out radius) || radius <= 0))
            {
                e.Player.SendMessage("Invalid radius.", Color.Red);
            }
            else
            {
                CommandQueue.Add(new RollbackCommand(e.Parameters[0], time, radius, e.Player));
            }
        }
        void Prune(CommandArgs e)
        {
            if (e.Parameters.Count != 1)
            {
                e.Player.SendMessage("Invalid syntax! Proper syntax: /prunehist <time>", Color.Red);
                return;
            }
            int time;
            if (GetTime(e.Parameters[0], out time) && time > 0)
            {
                CommandQueue.Add(new PruneCommand(time, e.Player));
            }
            else
            {
                e.Player.SendMessage("Invalid time.", Color.Red);
            }
        }
    }
}