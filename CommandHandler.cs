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

		public CommandHandler(DiscordSocketClient client, CommandService commands)
		{
			this.client = client;
			this.commands = commands;

			commands.AddModulesAsync(Assembly.GetExecutingAssembly(), null);
		}

		public async Task InstallCommandsAsync()
		{
			client.MessageReceived += HandleCommandAsync;
		}

		private async Task HandleCommandAsync(SocketMessage messageParam)
		{
			var message = messageParam as SocketUserMessage;
			if (message == null) return;

			int argPos = 0;

			if (!message.HasCharPrefix(COMMAND_PREFIX, ref argPos) || message.Author.IsBot)
			{
				return;
			}

			var context = new SocketCommandContext(client, message);

			await commands.ExecuteAsync(
				context: context, 
				argPos: argPos,
				services: null);
		}
	}
}