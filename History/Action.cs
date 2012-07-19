using System;
using System.Text;
using Terraria;
using TShockAPI;

namespace History
{
    public class Action
    {
        public string account;
        public byte action;
        public byte data;
        public int time;
        public int x;
        public int y;

        public void Reenact()
        {
            switch (action)
            {
                case 0:
                case 4:
                    if (Main.tile[x, y].active)
                    {
                        WorldGen.KillTile(x, y, false, false, true);
                        TSPlayer.All.SendTileSquare(x, y, 1);
                    }
                    break;
                case 1:
                    if (!Main.tile[x, y].active)
                    {
                        WorldGen.PlaceTile(x, y, data);
                        TSPlayer.All.SendTileSquare(x, y, 3);
                    }
                    break;
                case 2:
                    if (Main.tile[x, y].wall != 0)
                    {
                        Main.tile[x, y].wall = 0;
                        TSPlayer.All.SendTileSquare(x, y, 1);
                    }
                    break;
                case 3:
                    if (Main.tile[x, y].wall == 0)
                    {
                        Main.tile[x, y].wall = data;
                        TSPlayer.All.SendTileSquare(x, y, 1);
                    }
                    break;
                case 5:
                    if (Main.tile[x, y].wire)
                    {
                        Main.tile[x, y].wire = false;
                        TSPlayer.All.SendTileSquare(x, y, 1);
                    }
                    break;
                case 6:
                    if (!Main.tile[x, y].wire)
                    {
                        Main.tile[x, y].wire = true;
                        TSPlayer.All.SendTileSquare(x, y, 1);
                    }
                    break;
            }
        }
        public void Rollback()
        {
            switch (action)
            {
                case 0:
                case 4:
                    if (!Main.tile[x, y].active)
                    {
                        WorldGen.PlaceTile(x, y, data);
                        TSPlayer.All.SendTileSquare(x, y, 3);
                    }
                    break;
                case 1:
                    if (Main.tile[x, y].active)
                    {
                        WorldGen.KillTile(x, y, false, false, true);
                        TSPlayer.All.SendTileSquare(x, y, 1);
                    }
                    break;
                case 2:
                    if (Main.tile[x, y].wall == 0)
                    {
                        Main.tile[x, y].wall = data;
                        TSPlayer.All.SendTileSquare(x, y, 1);
                    }
                    break;
                case 3:
                    if (Main.tile[x, y].wall != 0)
                    {
                        Main.tile[x, y].wall = 0;
                        TSPlayer.All.SendTileSquare(x, y, 1);
                    }
                    break;
                case 5:
                    if (!Main.tile[x, y].wire)
                    {
                        Main.tile[x, y].wire = true;
                        TSPlayer.All.SendTileSquare(x, y, 1);
                    }
                    break;
                case 6:
                    if (Main.tile[x, y].wire)
                    {
                        Main.tile[x, y].wire = false;
                        TSPlayer.All.SendTileSquare(x, y, 1);
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
                    return string.Format("{0} {1} broke wire. ({2})", date, account, dhms);
                case 6:
                    return string.Format("{0} {1} placed wire. ({2})", date, account, dhms);
                default:
                    return "";
            }
        }
    }
}
