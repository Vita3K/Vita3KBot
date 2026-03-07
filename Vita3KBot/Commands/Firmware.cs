using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using APIClients;
using DC = Discord.Commands;

namespace Vita3KBot.Commands {
    internal static class FirmwareData {
        internal static Embed BuildEmbed() {
            var fullFW       = PSNClient.GetFullFW();
            var systemDataFW = PSNClient.GetSystemDataFW();

            return new EmbedBuilder()
                .WithTitle($"The latest firmware version is {PSNClient.GetFWVersion()}")
                .WithColor(Color.Orange)
                .WithDescription("Installing the firmware packages in Vita3K allows the emulator to LLE the system modules.")
                .AddField("License Agreement", "Before downloading the firmware you must read and agree to the license agreement located " + "[here](https://doc.dl.playstation.net/doc/psvita-eula/)")
                .AddField("Modules Package", $"[Full Firmware Package ({fullFW.Item2}MB)]({fullFW.Item1})", true)
                .AddField("Fonts Package", $"[Systemdata Firmware Package ({systemDataFW.Item2}MB)]({systemDataFW.Item1})", true)
                .WithFooter("Both packages have to be installed in Vita3K in order for them to function properly")
                .Build();
        }
    }

    // ── Prefix command ───────────────────────────────────────────

    [DC.Group("firmware")]
    public class FirmwarePrefix : DC.ModuleBase<DC.SocketCommandContext> {
        [DC.Command, DC.Name("firmware")]
        [DC.Summary("Gets the latest firmware package for all regions")]
        public async Task Firmware()
            => await ReplyAsync(embed: FirmwareData.BuildEmbed());
    }

    // ── Slash command ────────────────────────────────────────────

    public class FirmwareSlash : InteractionModuleBase<SocketInteractionContext> {
        [SlashCommand("firmware", "Gets the latest firmware package for all regions")]
        public async Task Firmware()
            => await RespondAsync(embed: FirmwareData.BuildEmbed());
    }
}
