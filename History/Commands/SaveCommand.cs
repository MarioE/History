using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace History.Commands
{
	public class SaveCommand : HCommand
	{
		private Action[] actions;

		public SaveCommand(Action[] actions)
			: base(null)
		{
			this.actions = actions;
		}

		public override void Execute()
		{
			if (TShock.DB.GetSqlType() == SqlType.Sqlite)
			{
				using (IDbConnection db = History.Database.CloneEx())
				{
					db.Open();
					using (SqliteTransaction transaction = (SqliteTransaction)db.BeginTransaction())
					{
						using (SqliteCommand command = (SqliteCommand)db.CreateCommand())
						{
							command.CommandText = "INSERT INTO History (Time, Account, Action, XY, Data, Style, Paint, WorldID, Text, Alternate, Random, Direction) VALUES (@0, @1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11)";
							for (int i = 0; i < 12; i++)
							{
								command.AddParameter("@" + i, null);
							}
							command.Parameters[7].Value = Main.worldID;

							foreach (Action a in actions)
							{
								command.Parameters[0].Value = a.time;
								command.Parameters[1].Value = a.account;
								command.Parameters[2].Value = a.action;
								command.Parameters[3].Value = (a.x << 16) + a.y;
								command.Parameters[4].Value = a.data;
								command.Parameters[5].Value = a.style;
								command.Parameters[6].Value = a.paint;
								command.Parameters[8].Value = a.text;
								command.Parameters[9].Value = a.alt;
								command.Parameters[10].Value = a.random;
								command.Parameters[11].Value = a.direction ? 1 : -1;
								command.ExecuteNonQuery();
							}
						}
						transaction.Commit();
					}
				}
			}
			else
			{
				using (IDbConnection db = History.Database.CloneEx())
				{
					db.Open();
					using (MySqlTransaction transaction = (MySqlTransaction)db.BeginTransaction())
					{
						using (MySqlCommand command = (MySqlCommand)db.CreateCommand())
						{
							command.CommandText = "INSERT INTO History (Time, Account, Action, XY, Data, Style, Paint, WorldID, Text, Alternate, Random, Direction) VALUES (@0, @1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11)";
							for (int i = 0; i < 12; i++)
							{
								command.AddParameter("@" + i, null);
							}
							command.Parameters[7].Value = Main.worldID;

							foreach (Action a in actions)
							{
								command.Parameters[0].Value = a.time;
								command.Parameters[1].Value = a.account;
								command.Parameters[2].Value = a.action;
								command.Parameters[3].Value = (a.x << 16) + a.y;
								command.Parameters[4].Value = a.data;
								command.Parameters[5].Value = a.style;
								command.Parameters[6].Value = a.paint;
								command.Parameters[8].Value = a.text;
								command.Parameters[9].Value = a.alt;
								command.Parameters[10].Value = a.random;
								command.Parameters[11].Value = a.direction ? 1 : -1;
								command.ExecuteNonQuery();
							}
						}
						transaction.Commit();
					}
				}
			}
		}
	}
}
