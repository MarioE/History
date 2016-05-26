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
		private int x;
		private int y;

		public HistoryCommand(int x, int y, TSPlayer sender)
			: base(sender)
		{
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
						data = (ushort)(reader.Get<int>("Data")),
						time = reader.Get<int>("Time")
					});
				}
			}

			actions.AddRange(History.Actions.Where(a => a.x == x && a.y == y));
			sender.SendSuccessMessage("Tile history ({0}, {1}):", x, y);
			foreach (Action a in actions)
				sender.SendInfoMessage(a.ToString());
			if (actions.Count == 0)
				sender.SendErrorMessage("No history available.");
		}
	}
}
