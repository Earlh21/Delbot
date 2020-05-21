using System;
using System.Collections.Generic;
using System.IO;

namespace Delbot
{
    public static class Tokens
    {
        public enum TokenType
        {
            DiscordToken,
            PayPalClientId,
            PayPalClientSecret
        }

        private static readonly Dictionary<TokenType, string> FILENAMES = new Dictionary<TokenType, string>()
        {
            {TokenType.DiscordToken, "discord_token.txt"},
            {TokenType.PayPalClientId, "paypal_client_id.txt"},
            {TokenType.PayPalClientSecret, "paypal_client_secret.txt"}
        };

        private static readonly string APP_DIRECTORY = AppDomain.CurrentDomain.BaseDirectory;

        public static string GetToken(TokenType type)
        {
            string path = APP_DIRECTORY + "/" + FILENAMES[type];

            return File.ReadAllText(path)
                .Replace(" ", "")
                .Replace("\t", "")
                .Replace(Environment.NewLine, "");
        }
    }
}