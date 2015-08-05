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
		private int radius;
		private bool reenact;
		private int time;

		public RollbackCommand(string account, int time, int radius, TSPlayer sender, bool reenact = false)
			: base(sender)
		{
			this.account = account;
			this.time = time;
			this.radius = radius;
			this.reenact = reenact;
		}

		public override void Execute()
		{
			List<Action> actions = new List<Action>();
			int rollbackTime = (int)(DateTime.UtcNow - History.Date).TotalSeconds - time;

			int plrX = sender.TileX;
			int plrY = sender.TileY;
			int lowX = plrX - radius;
			int highX = plrX + radius;
			int lowY = plrY - radius;
			int highY = plrY + radius;
			string XYReq = string.Format("XY / 65536 BETWEEN {0} AND {1} AND XY & 65535 BETWEEN {2} AND {3}", lowX, highX, lowY, highY);

			using (QueryResult reader =
				History.Database.QueryReader("SELECT * FROM History WHERE Account = @0 AND Time >= @1 AND " + XYReq + " AND WorldID = @2",
				account, rollbackTime, Main.worldID))
			{
				while (reader.Read())
				{
					actions.Add(new Action
					{
						account = reader.Get<string>("Account"),
						action = (byte)reader.Get<int>("Action"),
						data = (ushort)reader.Get<int>("Data"),
						style = (byte)reader.Get<int>("Style"),
						paint = (short)reader.Get<int>("Paint"),
						time = reader.Get<int>("Time"),
						x = reader.Get<int>("XY") >> 16,
						y = reader.Get<int>("XY") & 0xffff,
						text = reader.Get<string>("Text"),
                        alt = (byte)reader.Get<int>("Alternate"),
                        random = (sbyte)reader.Get<int>("Random"),
                        direction = reader.Get<int>("Direction") == 1 ? true : false
					});
				}
			}
			if (!reenact)
			{
				History.Database.Query("DELETE FROM History WHERE Account = @0 AND Time >= @1 AND " + XYReq + " AND WorldID = @2",
					account, rollbackTime, Main.worldID);
			}
			if (Main.rand == null)
				Main.rand = new Random();
			if (WorldGen.genRand == null)
				WorldGen.genRand = new Random();

			for (int i = 0; i >= 0 && i < History.Actions.Count; i++)
			{
				Action action = History.Actions[i];
				if (action.account == account && action.time >= rollbackTime &&
					lowX <= action.x && lowY <= action.y && action.x <= highX && action.y <= highY)
				{
					actions.Add(action);
					if (!reenact)
					{
						History.Actions.RemoveAt(i);
						i--;
					}
				}
			}
			if (!reenact)
			{
				for (int i = actions.Count - 1; i >= 0; i--)
					actions[i].Rollback();
				UndoCommand.LastWasReenact = false;
			}
			else
			{
				foreach (Action action in actions)
					action.Reenact();
				UndoCommand.LastWasReenact = true;
			}
			UndoCommand.LastRollBack = actions;
			sender.SendInfoMessage("R{0} {1} action{2}.", reenact ? "eenacted" : "olled back", actions.Count, actions.Count == 1 ? "" : "s");
		}
	}
}
