using Discord;
using Fergun.Extensions;

namespace Fergun.Utils
{
    public static class GuildUtils
    {
        public static GuildConfig GetGuildConfig(IMessageChannel channel)
        {
            if (channel.IsPrivate())
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
    }
}
