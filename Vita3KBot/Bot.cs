using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;

namespace Vita3KBot {
    public class Bot {
        private readonly string _token;

        private DiscordSocketClient _client;
        private MessageHandler _handler;

        // Initializes Discord.Net
        private async Task Start() {
            _client = new DiscordSocketClient();
            _handler = new MessageHandler(_client);

            await _handler.Init();
            
            await _client.LoginAsync(TokenType.Bot, _token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }
        
        private Bot(string token) {
            _token = token;
        }
        
        public static void Main(string[] args) {
            // Init command with token.
            if (args.Length >= 2 && args[0] == "init") {
                File.WriteAllText("token.txt", args[1]);
            }
            
            // Start bot with token from "token.txt" in working folder.
            try {
                var bot = new Bot(File.ReadAllText("token.txt"));
                bot.Start().GetAwaiter().GetResult();
            } catch (IOException e) {
                Console.WriteLine("Could not read from token.txt. Did you run `init <token>`?");
            }
        }
    }
}