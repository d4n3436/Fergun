using System.Diagnostics;
using Discord;
using Fergun.Common;

namespace Fergun.Extensions;

public static class InteractionCommandTypeExtensions
{
    public static InteractionCommandType ToInteractionCommandType(this ApplicationCommandType type)
    {
        return type switch
        {
            ApplicationCommandType.Slash => InteractionCommandType.Slash,
            ApplicationCommandType.User => InteractionCommandType.User,
            ApplicationCommandType.Message => InteractionCommandType.Message,
            _ => throw new UnreachableException()
        };
    }

    public static bool IsApplicationCommand(this InteractionCommandType commandType)
        => commandType is InteractionCommandType.Slash or InteractionCommandType.User or InteractionCommandType.Message;
}