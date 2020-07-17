using System;
using System.Threading.Tasks;

using Discord.Commands;
using Discord.WebSocket;

namespace Vita3KBot.Commands.Attributes {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireModeratorRole : PreconditionAttribute {

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services) {
            if (context.User is SocketGuildUser guildUser) {
                if (RolesUtils.IsModerator(context, context.Guild as SocketGuild)) {
                    return Task.FromResult(PreconditionResult.FromSuccess());
                } else {
                    return Task.FromResult(PreconditionResult.FromError("You lack the permissions to exectue this command"));
                }
            } else {
                return Task.FromResult(PreconditionResult.FromError("You must be in a server to execute this command"));
            }
        }
    }
}