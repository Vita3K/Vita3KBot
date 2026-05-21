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

            // Fire-and-forget to avoid blocking the gateway task
            _ = Task.Run(async () => {
                try {
                    var warning = await context.Channel.SendMessageAsync(
                        $"{context.User.Mention} ⚠️ This command can only be used in <#577624167541637158>."
                    );

                    // Delete the warning after 10 seconds to keep the channel clean
                    await Task.Delay(10000);
                    await warning.DeleteAsync();
                } catch {
                    // Ignore failures
                }
            });

            return DC.PreconditionResult.FromError("Insufficient permissions");
        }

        // User is in a DM, always allow
        return DC.PreconditionResult.FromSuccess();
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
                    "⚠️ This command can only be used in <#577624167541637158>.",
                    ephemeral: true
                );

                return DI.PreconditionResult.FromError("Insufficient permissions");
            }

            // User is in a DM, always allow
            return DI.PreconditionResult.FromSuccess();
        }
    }
}
