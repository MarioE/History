using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;

namespace History.Commands
{
	public abstract class HCommand
	{
		protected TSPlayer sender;

		public HCommand(TSPlayer sender)
		{
			this.sender = sender;
		}

		public void Error(string msg)
		{
			if (sender != null)
			{
				sender.SendErrorMessage(msg);
			}
		}
		public abstract void Execute();
	}
}
