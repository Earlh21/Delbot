﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using PayPalTest;

namespace Delbot
{
	internal static class Program
	{
		private static SocketGuild server;
		private static DiscordSocketClient discord_client;
		private static PayPalClient paypal_client;

		private static readonly string[] BUYER_ROLE_EMOJIS = {"💰", "☑️"};

		private const string TIMER_EMOJI = "⏲️";
		private const string START_ORDER_EMOJI = "👍";
		private const string INVALID_ORDER_EMOJI = "❌";

		private const ulong BOT_USER_ID = 701162359330177034;
		private const int OPENING_CLOSING_PERIOD = 1000 * 60;
		private const int PAYMENT_CAPTURE_PERIOD = 1000 * 3;

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
		private const ulong ADMIN_ROLE_ID = 710668697613762571;
		private const ulong PROGRAMMER_ROLE_ID = 710668776332460032;

		private static readonly string APPROVED_ORDERS_DIRECTORY =
			Environment.GetEnvironmentVariable("HOME") + "/debug_approvals/";
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
		private const ulong ADMIN_ROLE_ID = 706640860476997683;
		private const ulong PROGRAMMER_ROLE_ID = 699796932087906336;
		
		private static readonly string APPROVED_ORDERS_DIRECTORY =
			Environment.GetEnvironmentVariable("HOME") + "/approvals/";
#endif
		private static async Task Main()
		{
			string paypal_client_id = Tokens.GetToken(Tokens.TokenType.PayPalClientId);
			string paypal_client_secret = Tokens.GetToken(Tokens.TokenType.PayPalClientSecret);
			paypal_client = new PayPalClient(paypal_client_id, paypal_client_secret);
			
			discord_client = new DiscordSocketClient();

			discord_client.UserJoined += UserJoined;
			discord_client.ReactionAdded += ReactionAdded;
			discord_client.Ready += Ready;
			discord_client.MessageReceived += MessageReceived;
			discord_client.Log += Logging.ConsoleLog;

			await discord_client.LoginAsync(TokenType.Bot, Tokens.GetToken(Tokens.TokenType.DiscordToken));
			await discord_client.StartAsync();

			CommandHandler command_handler = new CommandHandler(discord_client);
			await command_handler.InstallCommandsAsync();

			OpeningClosingLoopAsync();
			PaymentCaptureLoopAsync();

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

		//TODO: Refactor these out, the objects have a Mention property
		private static string MentionChannel(ulong channel_id)
		{
			return "<#" + channel_id + ">";
		}

		private static string MentionUser(ulong user_id)
		{
			return "<@" + user_id + ">";
		}

		private static async void PaymentCaptureLoopAsync()
		{
			while (server == null)
			{
				await Task.Delay(1000);
			}
			
			while (true)
			{
				string[] approved_order_paths = Directory.GetFiles(APPROVED_ORDERS_DIRECTORY);

				foreach (string approved_order_path in approved_order_paths)
				{
					string order_id = Path.GetFileNameWithoutExtension(approved_order_path);
					await paypal_client.CaptureOrderAsync(order_id);
					File.Delete(approved_order_path);
					
					await Logging.FileLogAsync("capture_log.txt", "Captured payment for order " + order_id);
					
					EmbedBuilder embed_builder = new EmbedBuilder();
					embed_builder.Color = Color.Green;
					embed_builder.Title = "Order payment processed";
					embed_builder.Description =
						"Your payment has been processed. " +
						"Del's Bells admins have been notified and will contact you at their first opportunity.";
					Embed embed = embed_builder.Build();
					
					string discord_id_string = await Orders.GetOrderUserAsync(order_id);
					await Orders.RemoveOrderAsync(order_id);
					
					if (discord_id_string == null)
					{
						SocketRole programmer_role = server.GetRole(PROGRAMMER_ROLE_ID);
						string log_message =
							"Order payment captured but order wasn't found in the current orders file. " +
							"Order ID: " + order_id + " " + programmer_role.Mention;
						await Logging.DiscordLogAsync(server, log_message);
						continue;
					}

					ulong discord_id = Convert.ToUInt64(discord_id_string);
					SocketUser user = server.GetUser(discord_id);
					if (user == null)
					{
						SocketRole programmer_role = server.GetRole(PROGRAMMER_ROLE_ID);
						string log_message =
							"Order payment captured but order user wasn't found in the server. " +
							"Order ID: " + order_id + " " + programmer_role.Mention;
						await Logging.DiscordLogAsync(server, log_message);
						return;
					}

					await user.SendMessageAsync("", false, embed);

					SocketRole admin_role = server.GetRole(ADMIN_ROLE_ID);
					string message = "Order " + order_id + " approved and payment captured. User: " +
					                 user.Mention + ". " + admin_role.Mention;
					await Logging.DiscordLogAsync(server, message);
				}
				
				await Task.Delay(PAYMENT_CAPTURE_PERIOD);
			}
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
			server = discord_client.GetGuild(SERVER_ID);
			await Task.CompletedTask;
		}

		private static async Task MessageReceived(SocketMessage message_received)
		{
			SocketUserMessage message = message_received as SocketUserMessage;
			if (message == null)
			{
				return;
			}
			
			if (message.Channel.Id != ORDER_CHANNEL_ID)
			{
				return;
			}

			//TODO: This should be removable because of the first check, but leaving it in just for now
			//Test removing it later
			if (message.Author.Id == BOT_USER_ID)
			{
				return;
			}

			try
			{
				ParseOrder(message);
			}
			catch (ArgumentException ex)
			{
				await message.AddReactionAsync(new Emoji(INVALID_ORDER_EMOJI));
				SocketTextChannel order_channel = server.GetTextChannel(ORDER_CHANNEL_ID);
				await order_channel.SendMessageAsync(ex.Message);
				return;
			}

			await Task.CompletedTask;
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

		private static OrderDetails ParseOrder(IMessage message)
		{
			
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

				//Create and send PayPal order if the reaction matches
				if (reaction_added.Emote.Name.Equals(START_ORDER_EMOJI))
				{
					try
					{
						//TODO: Add a check here for null
						OrderDetails order_details = ParseOrder(message);
						PayPalClient.CreateOrderResponse response = await paypal_client.CreateOrderAsync(order_details.Amount, order_details.Price);
						
						if (!response.successful)
						{
							await Logging.DiscordLogAsync(server, "Order creation failed: " + response.raw_content);
						}
						else
						{
							await Orders.WriteOrderUserAsync(response.id, order_details.DiscordId);
							string log_message = "Created order " + response.id + ": amount:" + order_details.Amount +
							                     " price:" + order_details.Price.ToString("0.00") + " uid:" + order_details.DiscordId;
							await Logging.FileLogAsync("create_log.txt", log_message);
							await Logging.DiscordLogAsync(server, log_message);

							PayPalClient.LinkDescription approval_link = response.links[1];
							if (!approval_link.rel.Equals("approve"))
							{
								SocketRole programmer_role = server.GetRole(PROGRAMMER_ROLE_ID);
								log_message =
									"Failed to find approval link after creating order " + response.id +
									programmer_role.Mention;
								await Logging.DiscordLogAsync(server, log_message);
							}
							else
							{
								EmbedBuilder embed_builder = new EmbedBuilder();
								embed_builder.Color = Color.Gold;
								embed_builder.Url = approval_link.href;
								embed_builder.Description = "Please pay for your Del's Bells order here.";
								embed_builder.Title = "Del's Bells: " + order_details.Amount + " Million Bells";
								Embed embed = embed_builder.Build();

								SocketUser buyer = server.GetUser(order_details.DiscordId);
								await buyer.SendMessageAsync("", false, embed);
							}
						}
					}
					catch (ArgumentException ex)
					{
						SocketTextChannel order_channel = server.GetTextChannel(ORDER_CHANNEL_ID);
						await order_channel.SendMessageAsync(ex.Message);
						return;
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
	}
}