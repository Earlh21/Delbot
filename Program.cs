using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Delbot
{
	internal class Program
	{
		private static bool sent_closing_message = false;
		private static bool sent_opening_message = false;

		private static SocketGuild server;
		private static DiscordSocketClient client;
		
		private const string CHECK_EMOJI = "☑️";
		private const string TIMER_EMOJI = "⏲️";
		private const string COMMAND_PREFIX = "!";

		private const ulong BOT_USER_ID = 701162359330177034;

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
		private static async Task Main(string[] args)
		{
			client = new DiscordSocketClient();

			client.UserJoined += UserJoined;
			client.ReactionAdded += ReactionAdded;
			client.Ready += Ready;
			client.MessageReceived += MessageReceived;
			client.Log += Log;
			
			string token = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "//token.txt");
			token = token.Replace(Environment.NewLine, "");
			token = token.Replace(" ", "");
			token = token.Replace("\t", "");

			await client.LoginAsync(TokenType.Bot, token);
			await client.StartAsync();

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

				await Task.Delay(1 * 1000);
			}
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

		private static async Task Ready()
		{
			server = client.GetGuild(SERVER_ID);
		}

		private static string MentionChannel(ulong channel_id)
		{
			return "<#" + channel_id + ">";
		}

		private static string MentionUser(ulong user_id)
		{
			return "<@" + user_id + ">";
		}
		
		private static Task MessageReceived(SocketMessage messageReceived)
		{
			if (messageReceived.Channel.Id != ORDER_CHANNEL_ID)
			{
				return Task.CompletedTask;
			}

			if (TimeBetween(OPENING_TIME_UTC, CLOSING_TIME_UTC, GetCurrentTime()))
			{
				return Task.CompletedTask;
			}

			if (messageReceived.Author.Id == BOT_USER_ID)
			{
				return Task.CompletedTask;
			}

			messageReceived.AddReactionAsync(new Emoji(TIMER_EMOJI));

			return Task.CompletedTask;
		}

		private static async Task UserJoined(SocketGuildUser userJoined)
		{
			SocketGuild guild = userJoined.Guild;

			if (guild.Id != SERVER_ID)
			{
				return;
			}

			SocketRole new_user_role = server.GetRole(NEW_USER_ROLE_ID);

			SocketTextChannel welcome_channel = server.GetTextChannel(WELCOME_CHANNEL_ID);

			await welcome_channel.SendMessageAsync(MentionUser(userJoined.Id) +
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
			await userJoined.AddRoleAsync(new_user_role);
		}

		private static async Task ReactionAdded(Cacheable<IUserMessage, ulong> message_cacheable,
			ISocketMessageChannel channel, SocketReaction reactionAdded)
		{
			if (channel.Id == ORDER_CHANNEL_ID)
			{
				IMessage message = await channel.GetMessageAsync(reactionAdded.MessageId);
				SocketGuildUser message_author = server.GetUser(message.Author.Id);

				//Add the buyer role to the user if the reaction is a check
				SocketRole buyer_role = server.GetRole(BUYER_ROLE_ID);
				if (reactionAdded.Emote.Name.Equals(CHECK_EMOJI))
				{
					await message_author.AddRoleAsync(buyer_role);
				}

				//Remove all other reactions from the order message

				//Copy all reactions in the message
				Dictionary<IEmote, ReactionMetadata> reactions = new Dictionary<IEmote, ReactionMetadata>();
				foreach (KeyValuePair<IEmote, ReactionMetadata> pair in message.Reactions)
				{
					reactions.Add(pair.Key, pair.Value);
				}

				//Remove any reactions from any users that don't match the recently added reaction
				foreach (KeyValuePair<IEmote, ReactionMetadata> pair in reactions)
				{
					if (!pair.Key.Equals(reactionAdded.Emote))
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
			Console.WriteLine(arg.ToString(null, true, true));
			return Task.CompletedTask;
		}
	}
}