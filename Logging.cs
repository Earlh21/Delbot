using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Delbot
{
	public static class Logging
	{
		private static readonly string LOG_DIRECTORY = Environment.GetEnvironmentVariable("HOME") + "/logs/";
		
		//TODO: Fill these in
#if DEBUG
		private const ulong LOG_CHANNEL_ID = 0;
#else
		private const ulong LOG_CHANNEL_ID = 0;
#endif

		public static async Task FileLogAsync(string log_file, string value)
		{
			if (!Directory.Exists(LOG_DIRECTORY))
			{
				Directory.CreateDirectory(LOG_DIRECTORY);
			}
			
			string filepath = LOG_DIRECTORY + log_file;

			if (!File.Exists(filepath))
			{
				File.Create(filepath).Close();
			}

			using (StreamWriter writer = new StreamWriter(filepath, true))
			{
				await writer.WriteLineAsync(DateTime.Now.ToString("[yyyy-MM-dd hh-mm-ss] ") + value);
			}
		}

		public static async Task DiscordLogAsync(IGuild server, string message)
		{
			SocketTextChannel log_channel = await server.GetTextChannelAsync(LOG_CHANNEL_ID) as SocketTextChannel;
			await log_channel.SendMessageAsync(message);
		}

		public static Task ConsoleLog(LogMessage message)
		{
			Console.WriteLine(message.ToString());
			return Task.CompletedTask;
		}
	}
}