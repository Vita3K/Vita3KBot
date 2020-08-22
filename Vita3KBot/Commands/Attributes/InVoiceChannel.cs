using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace Vita3KBot.Commands.Attributes
{
    public class InVoiceChannel : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            return Task.FromResult((context.User as IGuildUser)?.VoiceChannel is null
                ? PreconditionResult.FromError("You must be in a voice channel before invoking this command")
                : PreconditionResult.FromSuccess());
        }
    }
}
