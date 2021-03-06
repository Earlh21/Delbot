using System.IO;
using Discord;

namespace Delbot
{
	public class OrderDetails
	{
		public int Amount { get; set; }
		public decimal Price { get; set; }
		public string InGameName { get; set; }
		public ulong DiscordId { get; set; }
		public ulong MessageId { get; set; }
		public string IslandName { get; set; }
		public string PayPalName { get; set; }
		public string ProductName { get; set; }

		public OrderDetails(int amount, decimal price, string in_game_name, ulong discord_id, ulong message_id,
			string island_name, string paypal_name, string product_name)
		{
			Amount = amount;
			Price = price;
			InGameName = in_game_name;
			DiscordId = discord_id;
			MessageId = message_id;
			IslandName = island_name;
			PayPalName = paypal_name;
			ProductName = product_name;
		}
	}
}