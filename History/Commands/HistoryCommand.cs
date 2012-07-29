using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace History.Commands
{
    public class HistoryCommand : HCommand
    {
        private int plr;
        private int x;
        private int y;

        public HistoryCommand(int x, int y, int plr)
        {
            this.plr = plr;
            this.x = x;
            this.y = y;
        }

        public override void Execute()
        {
            List<Action> actions = new List<Action>();

            using (QueryResult reader =
                History.Database.QueryReader("SELECT Account, Action, Data, Time FROM History WHERE XY = @0 AND WorldID = @1",
                (x << 16) + y, Main.worldID))
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

            actions.AddRange(from a in History.Actions
                             where a.x == x && a.y == y
                             select a);
            TShock.Players[plr].SendMessage("Tile history (" + x + ", " + y + "):", Color.Green);
            foreach (Action a in actions)
            {
                TShock.Players[plr].SendMessage(a.ToString(), Color.Yellow);
            }
            if (actions.Count == 0)
            {
                TShock.Players[plr].SendMessage("No history available.", Color.Red);
            }
        }
    }
}
