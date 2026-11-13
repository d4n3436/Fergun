using Discord;

namespace Fergun.Extensions;

public static class ChannelExtensions
{
    extension(IChannel channel)
    {
        public bool IsPrivate => channel is IPrivateChannel;
    }

    extension(IMessageChannel channel)
    {
        public bool IsNsfw => channel is ITextChannel { IsNsfw: true } or IGuildChannel { Guild.NsfwLevel: NsfwLevel.AgeRestricted };
    }
}