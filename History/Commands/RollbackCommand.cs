using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace History.Commands
{
    public class RollbackCommand : HCommand
    {
        private string account;
        private int plr;
        private int radius;
        private bool reenact;
        private int time;

        public RollbackCommand(string account, int time, int radius, int plr, bool reenact = false)
        {
            this.account = account;
            this.time = time;
            this.plr = plr;
            this.radius = radius;
            this.reenact = reenact;
        }

        public override void Execute()
        {
            List<Action> actions = new List<Action>();
            int rollbackTime = (int)(DateTime.UtcNow - History.Date).TotalSeconds - time;

            int plrX = TShock.Players[plr].TileX;
            int plrY = TShock.Players[plr].TileY;
            int lowX = plrX - radius;
            int highX = plrX + radius;
            int lowY = plrY - radius;
            int highY = plrY + radius;
            string XYReq = string.Format("XY / 65536 BETWEEN {0} AND {1} AND XY & 65535 BETWEEN {2} AND {3}", lowX, highX, lowY, highY);

            using (QueryResult reader =
                History.Database.QueryReader("SELECT Action, Data, XY FROM History WHERE Account = @0 AND Time >= @1 AND " + XYReq + " AND WorldID = @2",
                account, rollbackTime, Main.worldID))
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
            if (!reenact)
            {
                History.Database.Query("DELETE FROM History WHERE Account = @0 AND Time >= @1 AND " + XYReq + " AND WorldID = @2",
                    account, rollbackTime, Main.worldID);
            }

            for (int i = History.Actions.Count - 1; i >= 0; i--)
            {
                Action action = History.Actions[i];
                if (action.account == account && action.time >= rollbackTime &&
                    lowX <= action.x && lowY <= action.y && action.x <= highX && action.y <= highY)
                {
                    actions.Add(action);
                    if (!reenact)
                    {
                        History.Actions.RemoveAt(i);
                    }
                }
            }
            if (!reenact)
            {
                foreach (Action action in actions)
                {
                    action.Rollback();
                }
            }
            else
            {
                foreach (Action action in actions)
                {
                    action.Reenact();
                }
            }

            string str = reenact ? "Reenacted " : "Rolled back ";
            TShock.Players[plr].SendMessage(str + actions.Count + " action" + (actions.Count == 1 ? "" : "s") + ".", Color.Yellow);
        }
    }
}
