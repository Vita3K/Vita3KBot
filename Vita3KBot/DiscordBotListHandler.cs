using System.Threading.Tasks;
using Discord.WebSocket;
using DiscordBotsList.Api;

namespace Vita3KBot
{
    public class DiscordBotListHandler
    {
        private readonly AuthDiscordBotListApi _authDiscordBotListApi;
        private readonly DiscordSocketClient _client;
        
        public DiscordBotListHandler(ulong botId, string botDblToken, DiscordSocketClient client)
        {
            _authDiscordBotListApi = new AuthDiscordBotListApi(botId, botDblToken);
            _client = client;
        }

        public async Task UpdateAsync()
        {
            await _authDiscordBotListApi.GetMeAsync().Result
                .UpdateStatsAsync(_client.Guilds.Count * 3);
        }
    }
}