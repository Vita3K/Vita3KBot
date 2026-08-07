using System.Linq;

using Discord.Commands;
using Discord.WebSocket;

namespace Vita3KBot.Utils
{
  internal static class RolesUtils
  {
    private static readonly string[] WhitelistedRoles = ["admin", "developer", "contributor", "moderator", "tester"];
    private static readonly string[] ModeratorRoles = ["admin", "developer", "moderator"];

    public static bool IsWhitelisted(ICommandContext ctx)
    {
      if (ctx.User is not SocketGuildUser gUser)
      {
        return false;
      }

      if (gUser.Roles.Any(role =>
      {
        return WhitelistedRoles.Any(str =>
        {
          return str == role.Name;
        });
      }))
      {
        return true;
      }

      return false;
    }

    public static bool IsWhitelisted(SocketUser user)
    {
      if (user is not SocketGuildUser gUser)
      {
        return false;
      }

      if (gUser.Roles.Any(role =>
      {
        return WhitelistedRoles.Any(str =>
        {
          return str == role.Name;
        });
      }))
      {
        return true;
      }

      return false;
    }

    public static bool IsModerator(ICommandContext ctx)
    {
      if (ctx.User is not SocketGuildUser gUser)
      {
        return false;
      }

      if (gUser.Roles.Any(role =>
      {
        return ModeratorRoles.Any(str =>
        {
          return str == role.Name;
        });
      }))
      {
        return true;
      }

      return false;
    }

    public static bool IsModerator(SocketUser user)
    {
      if (user is not SocketGuildUser gUser)
      {
        return false;
      }

      if (gUser.Roles.Any(role =>
      {
        return ModeratorRoles.Any(str =>
        {
          return str == role.Name;
        });
      }))
      {
        return true;
      }

      return false;
    }
  }
}
