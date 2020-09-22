using System.Linq;

using Discord.WebSocket;
using Discord.Commands;

namespace Vita3KBot {
    internal static class RolesUtils {
        private static readonly string[] WhitelistedRoles = { "admin", "developer", "contributor", "moderator", "tester" };
        private static readonly string[] ModeratorRoles = { "admin", "developer", "moderator" };
        public static bool IsWhitelisted(ICommandContext ctx, SocketGuild guild) {
            if (!(ctx.User is SocketGuildUser)) {
                return false;
            }
            var gUser = ctx.User as SocketGuildUser;

            if (gUser.Roles.Any(role => {
                return WhitelistedRoles.Any(str => {
                    return str == role.Name;
                });
            }))
                return true;
            return false;
        }

        public static bool IsWhitelisted(SocketUser user) {
            if (!(user is SocketGuildUser)) {
                return false;
            }
            var gUser = user as SocketGuildUser;

            if (gUser.Roles.Any(role => {
                return WhitelistedRoles.Any(str => {
                    return str == role.Name;
                });
            }))
                return true;
            return false;
        }

        public static bool IsModerator(ICommandContext ctx, SocketGuild guild) {
            if (!(ctx.User is SocketGuildUser)) {
                return false;
            }
            var gUser = ctx.User as SocketGuildUser;

            if (gUser.Roles.Any(role => {
                return ModeratorRoles.Any(str => {
                    return str == role.Name;
                });
            }))
                return true;
            return false;
        }

        public static bool IsModerator(SocketUser user) {
            if (!(user is SocketGuildUser)) {
                return false;
            }
            var gUser = user as SocketGuildUser;

            if (gUser.Roles.Any(role => {
                return ModeratorRoles.Any(str => {
                    return str == role.Name;
                });
            }))
                return true;
            return false;
        }
    }
}