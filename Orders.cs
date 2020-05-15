using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Delbot
{
	public static class Orders
	{
		private static readonly string CURRENT_ORDERS_FILE_PATH =
			Environment.GetEnvironmentVariable("HOME") + "/debug_current_orders.txt";

		public static async Task WriteOrderUserAsync(string order_id, ulong discord_id)
		{
			if (!File.Exists(CURRENT_ORDERS_FILE_PATH))
			{
				File.Create(CURRENT_ORDERS_FILE_PATH).Close();
			}

			string line = order_id + ":" + discord_id;
			await File.AppendAllLinesAsync(CURRENT_ORDERS_FILE_PATH, new [] {line});
		}

		public static async Task<string> GetOrderUserAsync(string order_id)
		{
			if (!File.Exists(CURRENT_ORDERS_FILE_PATH))
			{
				return null;
			}

			string[] lines = await File.ReadAllLinesAsync(CURRENT_ORDERS_FILE_PATH);

			foreach (string line in lines)
			{
				string[] line_data = line.Split(":");

				if (line_data[0].Equals(order_id))
				{
					return line_data[1];
				}
			}

			return null;
		}

		public static async Task RemoveOrderAsync(string order_id)
		{
			if (!File.Exists(CURRENT_ORDERS_FILE_PATH))
			{
				return;
			}
			
			List<string> lines = (await File.ReadAllLinesAsync(CURRENT_ORDERS_FILE_PATH)).ToList();

			for (int i = 0; i < lines.Count; i++)
			{
				string[] line_data = lines[i].Split(":");

				if (line_data[0].Equals(order_id))
				{
					lines.RemoveAt(i);
					await File.WriteAllLinesAsync(CURRENT_ORDERS_FILE_PATH, lines);
					return;
				}
			}
		}
	}
}