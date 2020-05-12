using System.Reflection;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace Delbot
{
	public class CommandHandler
	{
		private DiscordSocketClient client;
		private readonly CommandService commands;

		private const char COMMAND_PREFIX = '!';

		public CommandHandler(DiscordSocketClient client)
		{
			this.client = client;
			
			commands = new CommandService();
			commands.AddModulesAsync(Assembly.GetExecutingAssembly(), null);
		}

		public async Task InstallCommandsAsync()
		{
			client.MessageReceived += HandleCommandAsync;
			await Task.CompletedTask;
		}

		private async Task HandleCommandAsync(SocketMessage message_param)
		{
			var message = message_param as SocketUserMessage;
			if (message == null) return;

			int arg_pos = 0;

			if (!message.HasCharPrefix(COMMAND_PREFIX, ref arg_pos) || message.Author.IsBot)
			{
				return;
			}

			var context = new SocketCommandContext(client, message);

			await commands.ExecuteAsync(
				context: context, 
				argPos: arg_pos,
				services: null);
		}
	}
}