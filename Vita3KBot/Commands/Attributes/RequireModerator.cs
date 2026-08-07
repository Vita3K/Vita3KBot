using System;
using System.Threading.Tasks;

using Discord.Commands;
using Discord.WebSocket;

using Vita3KBot.Utils;

namespace Vita3KBot.Commands.Attributes
{
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
  public class RequireModeratorRole : PreconditionAttribute
  {
    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
      if (context.User is SocketGuildUser guildUser)
      {
        if (RolesUtils.IsModerator(context))
        {
          return Task.FromResult(PreconditionResult.FromSuccess());
        }
        else
        {
          return Task.FromResult(PreconditionResult.FromError("You lack the permissions to execute this command"));
        }
      }
      else
      {
        return Task.FromResult(PreconditionResult.FromError("You must be in a server to execute this command"));
      }
    }
  }
}
