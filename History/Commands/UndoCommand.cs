using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace History.Commands
{
	public class UndoCommand : HCommand
	{
		public static List<Action> LastRollBack = null;
		public static bool LastWasReenact = false;

		public UndoCommand(TSPlayer sender)
			: base(sender)
		{
		}

		public override void Execute()
		{
			//Redo saved actions
			if (LastWasReenact)
			{
				for (int i = LastRollBack.Count - 1; i >= 0; i--)
					LastRollBack[i].Rollback();
			}
			else
			{
				foreach (Action action in LastRollBack)
					action.Reenact();
			}
			//Resave actions into database
			SaveCommand undo = new SaveCommand(LastRollBack.ToArray());
			undo.Execute();

			sender.SendSuccessMessage("Undo complete! {0} actions redone.", LastRollBack.Count);
			LastRollBack = null;
		}
	}
}
