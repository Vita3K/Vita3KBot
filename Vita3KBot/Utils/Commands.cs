using System.Linq;

using Discord.Commands;

namespace Vita3KBot {
    internal static class CommandsUtils {
        public static bool CommandRequiresModerator(ModuleInfo command) {
            if (command.Preconditions.Any(precondition => {
                return precondition.ToString().Substring(precondition.ToString().Length - (precondition.ToString().Length - precondition.ToString().LastIndexOf(".")) + 1) == "RequireModeratorRole";
            }))
                return true;
            return false;
        }

        public static bool CommandRequiresWhitelistedRole(ModuleInfo command) {
            if (command.Preconditions.Any(precondition => {
                return precondition.ToString().Substring(precondition.ToString().Length - (precondition.ToString().Length - precondition.ToString().LastIndexOf(".")) + 1) == "RequireWhitelistedRole";
            }))
                return true;
            return false;
        }
    }
}