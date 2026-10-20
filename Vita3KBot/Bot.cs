using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;

using Vita3KBot.APIClients.PSNClient;
using Vita3KBot.Services;

namespace Vita3KBot
{
  public class Bot
  {
    private readonly string _token;

    private Bot(string token)
    {
      _token = token;
    }

    public static void Main(string[] args)
    {
      // Init command with token.
      if (args.Length >= 2 && args[0] == "init")
      {
        File.WriteAllText("token.txt", args[1]);
        Console.WriteLine("Token saved to token.txt");
        return;
      }

      // Start bot with token from the TOKEN env variable, or "token.txt" in working folder.
      try
      {
        string token = Environment.GetEnvironmentVariable("TOKEN");

        // If env not set, fall back to the file
        if (string.IsNullOrWhiteSpace(token))
        {
          token = File.ReadAllText("token.txt").Trim();
        }

        var bot = new Bot(token);
        bot.Start().GetAwaiter().GetResult();
      }
      catch (IOException)
      {
        Console.WriteLine("Could not read token.txt and TOKEN env not set.");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error: {ex.Message}");
      }
    }

    // Initializes Discord.Net
    private async Task Start()
    {
      // This is required to pass Koyeb's health check.
      var listener = new System.Net.HttpListener();
      listener.Prefixes.Add("http://*:8000/");
      listener.Start();
      _ = Task.Run(async () =>
      {
        while (true)
        {
          var ctx = await listener.GetContextAsync();
          ctx.Response.StatusCode = 200;
          ctx.Response.Close();
        }
      });

      using var services = ConfigureServices();

      var client = services.GetRequiredService<DiscordSocketClient>();
      client.Log += LogAsync;
      services.GetRequiredService<CommandService>().Log += LogAsync;

      // Is this really the best place for this?
      client.SelectMenuExecuted += HandleSelectMenuExecutedAsync;

      await client.LoginAsync(TokenType.Bot, _token);
      await client.StartAsync();

      await services.GetRequiredService<CommandHandlingService>().InitializeAsync();
      services.GetRequiredService<MessageHandlingService>().Initialize();

      await Task.Delay(Timeout.Infinite);
    }

    // Handles clicks on the title-update select menu, keeping the menu in place
    // on the original message so it can be reused for subsequent lookups.
    private static async Task HandleSelectMenuExecutedAsync(SocketMessageComponent interaction)
    {
      if (interaction.Data.CustomId != "update_select")
      {
        return;
      }

      var selectedId = interaction.Data.Values.First();
      var (embed, _) = PSNClient.GetTitlePatch(selectedId);

      // Preserve the original select menu from the message
      var originalComponents = interaction.Message.Components;
      var components = new ComponentBuilder();
      foreach (var row in originalComponents)
      {
        if (row is ActionRowComponent actionRow)
        {
          foreach (var component in actionRow.Components)
          {
            if (component is SelectMenuComponent menu)
            {
              components.WithSelectMenu(menu.CustomId,
                [.. menu.Options.Select(o => new SelectMenuOptionBuilder()
                  .WithLabel(o.Label)
                  .WithValue(o.Value)
                  .WithDescription(o.Description)
                  .WithEmote(o.Emote)
                  .WithDefault(o.Value == selectedId)
                )],
                menu.Placeholder);
            }
          }
        }
      }

      // Update the message in-place so the menu remains and can be reused
      await interaction.UpdateAsync(props =>
      {
        props.Embed = embed;
        props.Components = components.Build();
      });
    }

    // Called by Discord.Net when it wants to log something.
    private static Task LogAsync(LogMessage log)
    {
      Console.WriteLine(log.ToString());

      return Task.CompletedTask;
    }

    private static ServiceProvider ConfigureServices()
    {
      var config = new DiscordSocketConfig
      {
        GatewayIntents = GatewayIntents.Guilds
          | GatewayIntents.DirectMessages
          | GatewayIntents.GuildMessages
          | GatewayIntents.GuildMembers
          | GatewayIntents.MessageContent,
      };

      var client = new DiscordSocketClient(config);

      return new ServiceCollection()
        .AddSingleton(client)
        .AddSingleton<CommandService>()
        .AddSingleton(new InteractionService(client))
        .AddSingleton<CommandHandlingService>()
        .AddSingleton<MessageHandlingService>()
        .BuildServiceProvider();
    }
  }
}
