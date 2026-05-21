using System;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;

using DC = Discord.Commands;
using DI = Discord.Interactions;

namespace Vita3KBot.Commands.Attributes {

    // ── Prefix command ───────────────────────────────────────────

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class PrefixRequireRoleOrChannel : DC.PreconditionAttribute {

    // Allowed channel ID
    private const ulong AllowedChannelId = 577624167541637158; // # bot-spam

    public override async Task<DC.PreconditionResult> CheckPermissionsAsync(
        DC.ICommandContext context,
        DC.CommandInfo command,
        IServiceProvider services)
    {
        if (context.User is SocketGuildUser) {
            // Allow if the user has a whitelisted role
            if (RolesUtils.IsWhitelisted(context, context.Guild as SocketGuild))
                return DC.PreconditionResult.FromSuccess();

            // Allow if the command is used in the allowed channel
            if (context.Channel.Id == AllowedChannelId)
                return DC.PreconditionResult.FromSuccess();

            // Delete the original message and warn the user with a mention
            await context.Message.DeleteAsync();
            var warning = await context.Channel.SendMessageAsync(
                $"{context.User.Mention} ⚠️ This command can only be used in the specified channel."
            );
        }

        return DC.PreconditionResult.FromError("This command can only be used in a server");
    }
}

    // ── Slash command ────────────────────────────────────────────

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class SlashRequireRoleOrChannel : DI.PreconditionAttribute {

        private const ulong AllowedChannelId = 577624167541637158; // # bot-spam

        public override async Task<DI.PreconditionResult> CheckRequirementsAsync(
            IInteractionContext context,
            ICommandInfo command,
            IServiceProvider services)
        {
            if (context.User is SocketGuildUser) {
                // Allow if the user has a whitelisted role
                if (RolesUtils.IsWhitelisted((SocketUser)context.User))
                    return DI.PreconditionResult.FromSuccess();

                // Allow if the command is used in the allowed channel
                if (context.Channel.Id == AllowedChannelId)
                    return DI.PreconditionResult.FromSuccess();

                // Warn the user with an ephemeral message (only visible to them)
                await context.Interaction.RespondAsync(
                    "⚠️ This command can only be used in the specified channel.",
                    ephemeral: true
                );

                return DI.PreconditionResult.FromError("Insufficient permissions");
            }

            // Warn the user if the command is used outside a server
            await context.Interaction.RespondAsync(
                "⚠️ This command can only be used in a server.",
                ephemeral: true
            );

            return DI.PreconditionResult.FromError("This command can only be used in a server");
        }
    }
}
