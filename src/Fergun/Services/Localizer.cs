using Discord;

namespace Fergun.Services
{
    public static class Localizer
    {
        public static GuildConfig GetGuildConfig(IMessageChannel channel)
        {
            if (IsPrivate(channel))
            {
                return null;
            }
            return GetGuildConfig((channel as IGuildChannel).Guild);
        }

        public static GuildConfig GetGuildConfig(IGuild guild)
            => FergunClient.Database.Find<GuildConfig>("Guilds", x => x.ID == guild.Id);

        public static string GetPrefix(IMessageChannel channel)
            => GetGuildConfig(channel)?.Prefix ?? FergunConfig.GlobalPrefix;

        public static string GetLanguage(IMessageChannel channel)
            => GetGuildConfig(channel)?.Language ?? FergunConfig.DefaultLanguage;

        public static string Locate(string key, IMessageChannel channel)
            => strings.ResourceManager.GetString(key, FergunClient.Locales[GetLanguage(channel)]) ?? key;

        public static string Locate(string key, string language)
            => strings.ResourceManager.GetString(key, FergunClient.Locales[language]) ?? key;

        public static bool IsPrivate(IMessageChannel channel) => channel is IPrivateChannel;
    }
}