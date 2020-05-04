using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Delbot
{
	public class DataModule : ModuleBase<SocketCommandContext>
	{
		[Command("reactionlist")]
		[Summary("Gives a list of the users that reacted to the given message id with the given emoji.")]
		public async Task ReactionList(
			[Summary("The id of the message to check.")]
			ulong message_id, 
			[Summary("The emoji to check.")]
			string emoji)
		{
			SocketMessage message = Context.Channel.GetCachedMessage(message_id);
			
			var users = await message.GetReactionUsersAsync(new Emoji(emoji + "️"), Int32.MaxValue).FlattenAsync();
			
			
		}
	}
}