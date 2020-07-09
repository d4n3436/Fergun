using Discord;

namespace Fergun.Services
{
    public static class Localizer
    {
        public static Guild GetGuild(IMessageChannel channel)
        {
            if (IsPrivate(channel))
            {
                return null;
            }
            return FergunClient.Database.Find<Guild>("Guilds", x => x.ID == (channel as IGuildChannel).Guild.Id);
        }

        public static string GetPrefix(IMessageChannel channel)
            => GetGuild(channel)?.Prefix ?? FergunConfig.GlobalPrefix;

        public static string GetLanguage(IMessageChannel channel)
            => GetGuild(channel)?.Language ?? FergunConfig.DefaultLanguage;

        public static string Locate(string key, IMessageChannel channel)
            => strings.ResourceManager.GetString(key, FergunClient.Locales[GetLanguage(channel)]) ?? key;

        public static string Locate(string key, string language)
            => strings.ResourceManager.GetString(key, FergunClient.Locales[language]) ?? key;

        public static bool IsPrivate(IMessageChannel channel) => channel is IPrivateChannel;
    }
}