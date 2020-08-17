using System.Threading.Tasks;

using Discord;
using Discord.Commands;

using APIClients;

namespace Vita3KBot.Commands {
    [Group("firmware")]
    public class FirmwareModule : ModuleBase<SocketCommandContext> {

        [Command, Name("firmware")]
        [Summary("Gets the latest firmware package for all regions")]
        public async Task firmware() {
            Embed fwEmbed = new EmbedBuilder()
            .WithTitle($"The latest firmware version is {PSNClient.GetFWVersion()}")
            .WithColor(Color.Orange)
            .WithDescription("Installing the firmware packages in Vita3K allows the emulator to LLE the system modules.")
            .AddField("License Agreement", "Before downloading the firmware you must read and agree to the license agreement located [here](https://doc.dl.playstation.net/doc/psvita-eula/)")
            .AddField("Modules Package", $"[Full Firmware Package ({PSNClient.GetFullFW().Item2}MB)]({PSNClient.GetFullFW().Item1})", true)
            .AddField("Fonts Package", $"[Systemdata Firmware Package ({PSNClient.GetSystemDataFW().Item2}MB)]({PSNClient.GetSystemDataFW().Item1})", true)
            .WithFooter("Both packages have to be installed in Vita3K in order for them to function properly")
            .Build();
            await ReplyAsync(embed: fwEmbed);
        }
    }
}