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
using Hooks;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace History
{
    [APIVersion(1, 12)]
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
                GameHooks.Initialize -= OnInitialize;
                NetHooks.GetData -= OnGetData;
				WorldHooks.SaveWorld -= OnSaveWorld;
				CommandQueueThread.Abort();
            }
        }
        public override void Initialize()
        {
            GameHooks.Initialize += OnInitialize;
            NetHooks.GetData += OnGetData;
            WorldHooks.SaveWorld += OnSaveWorld;
        }
        void Queue(string account, int X, int Y, byte action, byte data = 0)
        {
            if (Actions.Count == SaveCount)
            {
                CommandQueue.Add(new SaveCommand(Actions.ToArray()));
                Actions.Clear();
            }
            Actions.Add(new Action { account = account, action = action, data = data, time = (int)(DateTime.UtcNow - Date).TotalSeconds, x = X, y = Y });
        }

        void OnGetData(GetDataEventArgs e)
        {
            if (!e.Handled && e.MsgID == PacketTypes.Tile)
            {
                int X = BitConverter.ToInt32(e.Msg.readBuffer, e.Index + 1);
                int Y = BitConverter.ToInt32(e.Msg.readBuffer, e.Index + 5);

                if (X >= 0 && Y >= 0 && X < Main.maxTilesX && Y < Main.maxTilesY)
                {
                    if (AwaitingHistory[e.Msg.whoAmI])
                    {
                        AwaitingHistory[e.Msg.whoAmI] = false;
                        TShock.Players[e.Msg.whoAmI].SendTileSquare(X, Y, 5);
                        CommandQueue.Add(new HistoryCommand(X, Y, TShock.Players[e.Msg.whoAmI]));
                        e.Handled = true;
                    }
                    else if (TShock.Regions.CanBuild(X, Y, TShock.Players[e.Msg.whoAmI]))
                    {
                        string account = TShock.Players[e.Msg.whoAmI].UserAccountName;
                        switch (e.Msg.readBuffer[e.Index])
                        {
                            case 0:
                            case 4:
                                if (!Main.tileCut[Main.tile[X, Y].type] && Main.tile[X, Y].type != 127 &&
                                    Main.tile[X, Y].active && e.Msg.readBuffer[e.Index + 9] == 0 && !Main.tileFrameImportant[Main.tile[X, Y].type])
                                {
                                    Queue(account, X, Y, 0, Main.tile[X, Y].type);
                                }
                                break;
                            case 1:
                                if ((!Main.tile[X, Y].active || Main.tileCut[Main.tile[X, Y].type]) && e.Msg.readBuffer[e.Index + 9] != 127
                                    && !Main.tileFrameImportant[e.Msg.readBuffer[e.Index + 9]])
                                {
                                    Queue(account, X, Y, 1, e.Msg.readBuffer[e.Index + 9]);
                                }
                                break;
                            case 2:
                                if (Main.tile[X, Y].wall != 0)
                                {
                                    Queue(account, X, Y, 2, Main.tile[X, Y].wall);
                                }
                                break;
                            case 3:
                                if (Main.tile[X, Y].wall == 0)
                                {
                                    Queue(account, X, Y, 3, e.Msg.readBuffer[e.Index + 9]);
                                }
                                break;
                            case 5:
                                if (Main.tile[X, Y].wire)
                                {
                                    Queue(account, X, Y, 5);
                                }
                                break;
                            case 6:
                                if (!Main.tile[X, Y].wire)
                                {
                                    Queue(account, X, Y, 6);
                                }
                                break;
                        }
                    }
                }
            }
        }
        void OnInitialize()
        {
            TShockAPI.Commands.ChatCommands.Add(new Command("history", HistoryCmd, "history"));
            TShockAPI.Commands.ChatCommands.Add(new Command("maintenance", Prune, "prunehist"));
            TShockAPI.Commands.ChatCommands.Add(new Command("rollback", Reenact, "reenact"));
            TShockAPI.Commands.ChatCommands.Add(new Command("rollback", Rollback, "rollback"));

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
        void OnSaveWorld(bool resetTime, HandledEventArgs e)
        {
            new SaveCommand(Actions.ToArray()).Execute();
            Actions.Clear();
        }

        void QueueCallback(object t)
        {
            if (WorldGen.genRand == null)
            {
                WorldGen.genRand = new Random();
            }
            while (!Netplay.disconnect)
            {
                CommandQueue.Take().Execute();
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
                CommandQueue.Add(new RollbackCommand(e.Parameters[0],  time, radius, e.Player, true));
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