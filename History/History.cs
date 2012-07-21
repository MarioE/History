using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
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
        public static DateTime Date = DateTime.UtcNow;
        public delegate void HistoryD(HistoryArgs e);
        public const int SaveCount = 10000;

        private List<Action> Actions = new List<Action>(SaveCount);
        private bool[] AwaitingHistory = new bool[256];
        public override string Author
        {
            get { return "MarioE"; }
        }
        private Queue<HistoryArgs> CommandArgsQueue = new Queue<HistoryArgs>();
        private Queue<HistoryD> CommandQueue = new Queue<HistoryD>();
        private IDbConnection Database;
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
            Order = 10;
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
                CommandArgsQueue.Enqueue(new HistoryArgs { actions = Actions.ToArray() });
                CommandQueue.Enqueue(SaveCallback);
                Actions.Clear();
            }
            Actions.Add(new Action { account = account, action = action, data = data, time = (int)(DateTime.UtcNow - Date).TotalSeconds, x = X, y = Y });
        }
        void SaveActions(Action[] actions)
        {
            if (TShock.DB.GetSqlType() == SqlType.Sqlite)
            {
                using (IDbConnection db = Database.CloneEx())
                {
                    db.Open();
                    using (SqliteTransaction transaction = (SqliteTransaction)db.BeginTransaction())
                    {
                        using (SqliteCommand command = (SqliteCommand)db.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO History (Time, Account, Action, XY, Data, WorldID) VALUES (@0, @1, @2, @3, @4, @5)";
                            for (int i = 0; i < 6; i++)
                            {
                                command.AddParameter("@" + i, null);
                            }
                            command.Parameters[5].Value = Main.worldID;

                            foreach (Action a in actions)
                            {
                                command.Parameters[0].Value = a.time;
                                command.Parameters[1].Value = a.account;
                                command.Parameters[2].Value = a.action;
                                command.Parameters[3].Value = (a.x << 16) + a.y;
                                command.Parameters[4].Value = a.data;
                                command.ExecuteNonQuery();
                            }
                        }
                        transaction.Commit();
                    }
                }
            }
            else
            {
                using (IDbConnection db = Database.CloneEx())
                {
                    db.Open();
                    using (MySqlTransaction transaction = (MySqlTransaction)db.BeginTransaction())
                    {
                        using (MySqlCommand command = (MySqlCommand)db.CreateCommand())
                        {
                            command.CommandText = "INSERT INTO History (Time, Account, Action, XY, Data, WorldID) VALUES (@0, @1, @2, @3, @4, @5)";
                            for (int i = 0; i < 6; i++)
                            {
                                command.AddParameter("@" + i, null);
                            }
                            command.Parameters[5].Value = Main.worldID;

                            foreach (Action a in actions)
                            {
                                command.Parameters[0].Value = a.time;
                                command.Parameters[1].Value = a.account;
                                command.Parameters[2].Value = a.action;
                                command.Parameters[3].Value = (a.x << 16) + a.y;
                                command.Parameters[4].Value = a.data;
                                command.ExecuteNonQuery();
                            }
                        }
                        transaction.Commit();
                    }
                }
            }
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
                        CommandQueue.Enqueue(GetHistoryCallback);
                        CommandArgsQueue.Enqueue(new HistoryArgs { player = TShock.Players[e.Msg.whoAmI], x = X, y = Y });
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
            Commands.ChatCommands.Add(new Command("history", HistoryCmd, "history"));
            Commands.ChatCommands.Add(new Command("maintenance", Prune, "prunehist"));
            Commands.ChatCommands.Add(new Command("rollback", Reenact, "reenact"));
            Commands.ChatCommands.Add(new Command("rollback", Rollback, "rollback"));

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
            ThreadPool.QueueUserWorkItem(CommandCallback);
        }
        void OnSaveWorld(bool resetTime, HandledEventArgs e)
        {
            SaveActions(Actions.ToArray());
            Actions.Clear();
        }

        void CommandCallback(object t)
        {
            if (WorldGen.genRand == null)
            {
                WorldGen.genRand = new Random();
            }
            while (!Netplay.disconnect)
            {
                if (CommandArgsQueue.Count != 0 && CommandQueue.Count != 0)
                {
                    CommandQueue.Dequeue().Invoke(CommandArgsQueue.Dequeue());
                }
            }
        }
        void GetHistoryCallback(HistoryArgs e)
        {
            List<Action> actions = new List<Action>();
            e.player.SendMessage("Tile history (" + e.x + ", " + e.y + "):", Color.Green);

            using (QueryResult reader =
                Database.QueryReader("SELECT Account, Action, Data, Time FROM History WHERE XY = @0 AND WorldID = @1",
                (e.x << 16) + e.y, Main.worldID))
            {
                while (reader.Read())
                {
                    actions.Add(new Action
                    {
                        account = reader.Get<string>("Account"),
                        action = (byte)reader.Get<int>("Action"),
                        data = (byte)reader.Get<int>("Data"),
                        time = reader.Get<int>("Time")
                    });
                }
            }

            actions.AddRange(from a in Actions
                             where a.x == e.x && a.y == e.y
                             select a);
            foreach (Action a in actions)
            {
                e.player.SendMessage(a.ToString(), Color.Yellow);
            }
            if (actions.Count == 0)
            {
                e.player.SendMessage("No history available.", Color.Red);
            }
        }
        void PruneCallback(HistoryArgs args)
        {
            int time = (int)(DateTime.UtcNow - Date).TotalSeconds - args.time;
            Database.Query("DELETE FROM History WHERE Time < @0 AND WorldID = @1", time, Main.worldID);
            Actions.RemoveAll(a => a.time < time);
            args.player.SendMessage("Pruned history.", Color.Green);
        }
        void ReenactCallback(HistoryArgs args)
        {
            List<Action> actions = new List<Action>();
            int reenactTime = (int)(DateTime.UtcNow - Date).TotalSeconds - args.time;

            int plrX = (int)args.player.TPlayer.position.X / 16;
            int plrY = (int)args.player.TPlayer.position.Y / 16 + 1;
            int lowX = plrX - args.radius;
            int highX = plrX + args.radius;
            int lowY = plrY - args.radius;
            int highY = plrY + args.radius;
            string XYReq = string.Format("XY / 65536 BETWEEN {0} AND {1} AND XY & 65535 BETWEEN {2} AND {3}", lowX, highX, lowY, highY);

            using (QueryResult reader =
                Database.QueryReader("SELECT Action, Data, XY FROM History WHERE Account = @0 AND Time >= @1 AND " + XYReq + " AND WorldID = @2",
                args.account, reenactTime, Main.worldID))
            {
                while (reader.Read())
                {
                    actions.Add(new Action
                    {
                        action = (byte)reader.Get<int>("Action"),
                        data = (byte)reader.Get<int>("Data"),
                        x = reader.Get<int>("XY") >> 16,
                        y = reader.Get<int>("XY") & 0xffff
                    });
                }
            }

            for (int i = Actions.Count - 1; i >= 0; i--)
            {
                Action action = Actions[i];
                if (action.account == args.account && action.time >= reenactTime &&
                    lowX <= action.x && lowY <= action.y && action.x <= highX && action.y <= highY)
                {
                    actions.Add(action);
                    Actions.RemoveAt(i);
                }
            }
            foreach (Action action in actions)
            {
                action.Reenact();
            }

            args.player.SendMessage("Reenacted " + actions.Count + " action" + (actions.Count == 1 ? "" : "s") + ".", Color.Green);
        }
        void RollbackCallback(HistoryArgs args)
        {
            List<Action> actions = new List<Action>();
            int rollbackTime = (int)(DateTime.UtcNow - Date).TotalSeconds - args.time;

            int plrX = (int)args.player.TPlayer.position.X / 16;
            int plrY = (int)args.player.TPlayer.position.Y / 16 + 1;
            int lowX = plrX - args.radius;
            int highX = plrX + args.radius;
            int lowY = plrY - args.radius;
            int highY = plrY + args.radius;
            string XYReq = string.Format("XY / 65536 BETWEEN {0} AND {1} AND XY & 65535 BETWEEN {2} AND {3}", lowX, highX, lowY, highY);

            using (QueryResult reader =
                Database.QueryReader("SELECT Action, Data, XY FROM History WHERE Account = @0 AND Time >= @1 AND " + XYReq + " AND WorldID = @2",
                args.account, rollbackTime, Main.worldID))
            {
                while (reader.Read())
                {
                    actions.Add(new Action
                    {
                        action = (byte)reader.Get<int>("Action"),
                        data = (byte)reader.Get<int>("Data"),
                        x = reader.Get<int>("XY") >> 16,
                        y = reader.Get<int>("XY") & 0xffff
                    });
                }
            }
            Database.Query("DELETE FROM History WHERE Account = @0 AND Time >= @1 AND " + XYReq + " AND WorldID = @2",
                args.account, rollbackTime, Main.worldID);

            for (int i = Actions.Count - 1; i >= 0; i--)
            {
                Action action = Actions[i];
                if (action.account == args.account && action.time >= rollbackTime &&
                    lowX <= action.x && lowY <= action.y && action.x <= highX && action.y <= highY)
                {
                    actions.Add(action);
                    Actions.RemoveAt(i);
                }
            }
            foreach (Action action in actions)
            {
                action.Rollback();
            }

            args.player.SendMessage("Rolled back " + actions.Count + " action" + (actions.Count == 1 ? "" : "s") + ".", Color.Green);
        }
        void SaveCallback(HistoryArgs e)
        {
            SaveActions(e.actions);
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
                CommandQueue.Enqueue(ReenactCallback);
                CommandArgsQueue.Enqueue(new HistoryArgs { account = e.Parameters[0], player = e.Player, radius = radius, time = time });
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
                CommandQueue.Enqueue(RollbackCallback);
                CommandArgsQueue.Enqueue(new HistoryArgs { account = e.Parameters[0], player = e.Player, radius = radius, time = time });
            }
        }
        void Prune(CommandArgs e)
        {
            if (e.Parameters.Count != 1)
            {
                e.Player.SendMessage("Invalid syntax! Proper syntax: /prunehist <time>", Color.Red);
                return;
            }
            int seconds;
            if (GetTime(e.Parameters[0], out seconds) && seconds > 0)
            {
                CommandQueue.Enqueue(PruneCallback);
                CommandArgsQueue.Enqueue(new HistoryArgs { player = e.Player, time = seconds });
            }
            else
            {
                e.Player.SendMessage("Invalid time.", Color.Red);
            }
        }
    }
}
