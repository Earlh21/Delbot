namespace Delbot
{
	public struct Order
	{
		public int Amount { get; }
		public decimal Price { get; }
		public string InGameName { get; }
		public ulong DiscordId { get; }
		public ulong MessageId { get; }
		public string IslandName { get; }
		public string PayPalName { get; }

		public Order(int amount, decimal price, string in_game_name, ulong discord_id, ulong message_id,
			string island_name, string paypal_name)
		{
			Amount = amount;
			Price = price;
			InGameName = in_game_name;
			DiscordId = discord_id;
			MessageId = message_id;
			IslandName = island_name;
			PayPalName = paypal_name;
		}
	}
}