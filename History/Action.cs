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
                        Main.tile[x, y].active = false;
                        Main.tile[x, y].type = 0;
                        TSPlayer.All.SendTileSquare(x, y, 1);
                    }
                    break;
                case 1:
                    if (!Main.tile[x, y].active)
                    {
                        Main.tile[x, y].active = true;
                        Main.tile[x, y].type = data;
                        TSPlayer.All.SendTileSquare(x, y, 1);
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
                        Main.tile[x, y].active = true;
                        Main.tile[x, y].type = data;
                        TSPlayer.All.SendTileSquare(x, y, 1);
                    }
                    break;
                case 1:
                    if (Main.tile[x, y].active)
                    {
                        Main.tile[x, y].active = false;
                        Main.tile[x, y].type = 0;
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
            StringBuilder date = new StringBuilder();
            TimeSpan span = DateTime.UtcNow - History.Date.AddSeconds(time);

            if (span.Days > 0)
            {
                date.Append(span.Days + " day(s) ");
            }
            if (span.Hours > 0)
            {
                date.Append(span.Hours + " hour(s) ");
            }
            if (span.Minutes > 0)
            {
                date.Append(span.Minutes + " minute(s) ");
            }
            if (span.Seconds > 0)
            {
                date.Append(span.Seconds + " second(s) ");
            }

            switch (action)
            {
                case 0:
                case 4:
                    return string.Format("{0} broke tile {1} {2}ago.", account, data, date);
                case 1:
                    return string.Format("{0} placed tile {1} {2}ago.", account, data, date);
                case 2:
                    return string.Format("{0} broke wall {1} {2}ago.", account, data, date);
                case 3:
                    return string.Format("{0} placed wall {1} {2}ago.", account, data, date);
                case 5:
                    return string.Format("{0} broke wire {1}ago.", account, date);
                case 6:
                    return string.Format("{0} placed wire {1}ago.", account, date);
                default:
                    return "";
            }
        }
    }
}
