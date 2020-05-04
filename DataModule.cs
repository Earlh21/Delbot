using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
			[Summary("The id of the text channel containing the message.")]
			ulong channel_id,
			[Summary("The id of the message to check.")]
			ulong message_id, 
			[Summary("The emoji to check.")]
			string emoji)
		{
			IMessage message = await Context.Guild.GetTextChannel(channel_id).GetMessageAsync(message_id);
			
			var users = await message.GetReactionUsersAsync(new Emoji(emoji), Int32.MaxValue).FlattenAsync();
			List<string> usernames = users.Select(user => user.Username).ToList();

			string directory = "/root/DelbotOutput/";
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			
			string filename = DateTime.Now.ToString("yyyy-MM-dd hh-mm-ss") + " User Dump.txt";

			string path = directory + filename;
			await File.WriteAllLinesAsync(path, usernames);
		}
	}
}