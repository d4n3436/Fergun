using System.Diagnostics;
using Discord;
using Fergun.Common;

namespace Fergun.Extensions;

public static class InteractionCommandTypeExtensions
{
    extension(ApplicationCommandType type)
    {
        public InteractionCommandType ToInteractionCommandType()
        {
            return type switch
            {
                ApplicationCommandType.Slash => InteractionCommandType.Slash,
                ApplicationCommandType.User => InteractionCommandType.User,
                ApplicationCommandType.Message => InteractionCommandType.Message,
                _ => throw new UnreachableException()
            };
        }
    }

    extension(InteractionCommandType type)
    {
        public bool IsApplicationCommand => type is InteractionCommandType.Slash or InteractionCommandType.User or InteractionCommandType.Message;
    }
}