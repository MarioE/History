using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace History.Commands
{
	public class PruneCommand : HCommand
	{
		private int time;

		public PruneCommand(int time, TSPlayer sender)
			: base(sender)
		{
			this.time = time;
		}

		public override void Execute()
		{
			int time = (int)(DateTime.UtcNow - History.Date).TotalSeconds - this.time;
			History.Database.Query("DELETE FROM History WHERE Time < @0 AND WorldID = @1", time, Main.worldID);
			History.Actions.RemoveAll(a => a.time < time);
			sender.SendSuccessMessage("Pruned history.");
		}
	}
}
