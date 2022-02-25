using Discord;

namespace Fergun.Extensions;

public static class ChannelExtensions
{
    public static bool IsPrivate(this IChannel channel) => channel is IPrivateChannel;

    public static bool IsNsfw(this IMessageChannel channel) => channel is ITextChannel { IsNsfw: true };
}