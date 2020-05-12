using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Delbot
{
	internal static class Program
	{
		private static SocketGuild server;
		private static DiscordSocketClient client;
		
		private static readonly string[] BUYER_ROLE_EMOJIS = {"💰", "☑️"};
		private const string TIMER_EMOJI = "⏲️";

		private const ulong BOT_USER_ID = 701162359330177034;
		private const int OPENING_CLOSING_PERIOD = 1000 * 60;

#if DEBUG
		private static readonly TimeSpan OPENING_TIME_UTC = new TimeSpan(0, 0, 0, 0);
		private static readonly TimeSpan CLOSING_TIME_UTC = new TimeSpan(0, 0, 0, 1);

		private const ulong SERVER_ID = 701162087166247012;

		private const ulong NEW_USER_ROLE_ID = 701165126862241822;
		private const ulong WELCOME_CHANNEL_ID = 701167271867056158;
		private const ulong ORDER_INSTRUCTIONS_CHANNEL_ID = 701167238258098247;
		private const ulong SERVER_INFO_CHANNEL_ID = 701167513844711584;
		private const ulong ORDER_CHANNEL_ID = 702313424553771030;
		private const ulong BUYER_ROLE_ID = 703000426584211901;
#else
		private static readonly TimeSpan OPENING_TIME_UTC = new TimeSpan(0, 16, 0, 0);
		private static readonly TimeSpan CLOSING_TIME_UTC = new TimeSpan(0, 23, 59, 0);
		
		private const ulong SERVER_ID = 699760863925633044;

		private const ulong NEW_USER_ROLE_ID = 699783209029730384;
		private const ulong WELCOME_CHANNEL_ID = 699797686588538910;
		private const ulong ORDER_INSTRUCTIONS_CHANNEL_ID = 701117387663081522;
		private const ulong SERVER_INFO_CHANNEL_ID = 699765156447518784;
		private const ulong ORDER_CHANNEL_ID = 701118059938840646;
		private const ulong BUYER_ROLE_ID = 699784096166969658;
#endif
		private static async Task Main()
		{
			client = new DiscordSocketClient();

			client.UserJoined += UserJoined;
			client.ReactionAdded += ReactionAdded;
			client.Ready += Ready;
			client.MessageReceived += MessageReceived;
			client.Log += Log;
			
			string token = await File.ReadAllTextAsync(AppDomain.CurrentDomain.BaseDirectory + "//token.txt");
			token = token.Replace(Environment.NewLine, "");
			token = token.Replace(" ", "");
			token = token.Replace("\t", "");

			await client.LoginAsync(TokenType.Bot, token);
			await client.StartAsync();

			CommandHandler command_handler = new CommandHandler(client);
			await command_handler.InstallCommandsAsync();

			OpeningClosingLoopAsync();

			await Task.Delay(-1);
		}

		private static TimeSpan GetCurrentTime()
		{
			return DateTime.UtcNow.TimeOfDay;
		}

		private static bool TimeBetween(TimeSpan start, TimeSpan end, TimeSpan time)
		{
			if (end < start)
			{
				return time < end || time > start;
			}
			else
			{
				return time > start && time < end;
			}
		}

		private static string MentionChannel(ulong channel_id)
		{
			return "<#" + channel_id + ">";
		}

		private static string MentionUser(ulong user_id)
		{
			return "<@" + user_id + ">";
		}

		private static async void OpeningClosingLoopAsync()
		{
			bool sent_opening_message = false;
			bool sent_closing_message = false;
			
			//Fix for the bot to not send an opening/closing message on startup
			if (TimeBetween(OPENING_TIME_UTC, CLOSING_TIME_UTC, GetCurrentTime()))
			{
				sent_opening_message = true;
			}
			else
			{
				sent_closing_message = true;
			}
			
			while (true)
			{
				if (server != null)
				{
					if (TimeBetween(OPENING_TIME_UTC, CLOSING_TIME_UTC, GetCurrentTime()))
					{
						if (!sent_opening_message)
						{
							sent_opening_message = true;
							sent_closing_message = false;

							SocketTextChannel order_channel = server.GetTextChannel(ORDER_CHANNEL_ID);

							await order_channel.SendMessageAsync(
								"**Orders are open again. Same day delivery guaranteed" +
								" for any orders made between now and 7PM CST, gronk.**");
						}
					}
					else
					{
						if (!sent_closing_message)
						{
							sent_closing_message = true;
							sent_opening_message = false;

							SocketTextChannel order_channel = server.GetTextChannel(ORDER_CHANNEL_ID);

							await order_channel.SendMessageAsync(
								"**We are preparing to stop deliveries for the night." +
								" Orders from here on out may not be delivered until the morning." +
								" We will DM you letting you know an ETA if we happen to be up when" +
								" you submit your order. We will resume guaranteed at 11 AM CST." +
								" Thanks, gronk.**");
						}
					}
				}

				await Task.Delay(OPENING_CLOSING_PERIOD);
			}
		}
		
		private static async Task Ready()
		{
			server = client.GetGuild(SERVER_ID);
			await Task.CompletedTask;
		}
		
		private static Task MessageReceived(SocketMessage message_received)
		{
			if (message_received.Channel.Id != ORDER_CHANNEL_ID)
			{
				return Task.CompletedTask;
			}

			if (TimeBetween(OPENING_TIME_UTC, CLOSING_TIME_UTC, GetCurrentTime()))
			{
				return Task.CompletedTask;
			}

			if (message_received.Author.Id == BOT_USER_ID)
			{
				return Task.CompletedTask;
			}

			message_received.AddReactionAsync(new Emoji(TIMER_EMOJI));

			return Task.CompletedTask;
		}

		private static async Task UserJoined(SocketGuildUser user_joined)
		{
			SocketGuild guild = user_joined.Guild;

			if (guild.Id != SERVER_ID)
			{
				return;
			}

			SocketRole new_user_role = server.GetRole(NEW_USER_ROLE_ID);

			SocketTextChannel welcome_channel = server.GetTextChannel(WELCOME_CHANNEL_ID);

			await welcome_channel.SendMessageAsync(MentionUser(user_joined.Id) +
			                                       " Welcome to Del's Bells-- We sell Bells! " +
			                                       "If you'd like to purchase Bells, you may make your way to " +
			                                       MentionChannel(ORDER_INSTRUCTIONS_CHANNEL_ID) +
			                                       " after reading " +
			                                       MentionChannel(SERVER_INFO_CHANNEL_ID) +
			                                       ". If you aren't interested in purchasing Bells, ensure you still read " +
			                                       MentionChannel(SERVER_INFO_CHANNEL_ID) +
			                                       ", welcome to our ACNH community, and enjoy your stay, gronk." +
			                                       Environment.NewLine +
			                                       "------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
			await user_joined.AddRoleAsync(new_user_role);
		}

		private static async Task ReactionAdded(Cacheable<IUserMessage, ulong> message_cacheable,
			ISocketMessageChannel channel, SocketReaction reaction_added)
		{
			if (channel.Id == ORDER_CHANNEL_ID)
			{
				IMessage message = await channel.GetMessageAsync(reaction_added.MessageId);
				SocketGuildUser message_author = server.GetUser(message.Author.Id);

				//Add the buyer role to the user if the reaction matches
				SocketRole buyer_role = server.GetRole(BUYER_ROLE_ID);
				foreach (string buyer_role_emoji in BUYER_ROLE_EMOJIS)
				{
					if (reaction_added.Emote.Name.Equals(buyer_role_emoji))
					{
						await message_author.AddRoleAsync(buyer_role);
					}
				}
				
				//Copy all reactions in the message
				Dictionary<IEmote, ReactionMetadata> reactions = new Dictionary<IEmote, ReactionMetadata>();
				foreach (KeyValuePair<IEmote, ReactionMetadata> pair in message.Reactions)
				{
					reactions.Add(pair.Key, pair.Value);
				}

				//Remove any reactions from any users that don't match the recently added reaction
				foreach (KeyValuePair<IEmote, ReactionMetadata> pair in reactions)
				{
					if (!pair.Key.Equals(reaction_added.Emote))
					{
						var users = await message.GetReactionUsersAsync(pair.Key, Int32.MaxValue).FlattenAsync();

						foreach (IUser user in users)
						{
							await message.RemoveReactionAsync(pair.Key, user.Id);
						}
					}
				}
			}
		}
		
		private static Task Log(LogMessage arg)
		{
			Console.WriteLine(arg.ToString());
			return Task.CompletedTask;
		}
	}
}