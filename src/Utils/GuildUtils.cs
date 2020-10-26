using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Fergun.Extensions;

namespace Fergun.Utils
{
    public static class GuildUtils
    {
        /// <summary>
        /// This prefix may not be updated if its value has been changed externally.
        /// </summary>
        public static string CachedGlobalPrefix { get; set; }

        /// <summary>
        /// These prefixes may not be updated if their values has been changed externally.
        /// </summary>
        public static ConcurrentDictionary<ulong, string> PrefixCache { get; private set; }

        public static void Initialize()
        {
            CachedGlobalPrefix = FergunConfig.GlobalPrefix;
            var guilds = FergunClient.Database.LoadRecords<GuildConfig>("Guilds");
            PrefixCache = new ConcurrentDictionary<ulong, string>(guilds?.ToDictionary(x => x.ID, x => x.Prefix) ?? new Dictionary<ulong, string>());
        }

        public static string GetCachedPrefix(IMessageChannel channel)
            => channel.IsPrivate() ? CachedGlobalPrefix : PrefixCache.GetValueOrDefault(((IGuildChannel)channel).GuildId, CachedGlobalPrefix) ?? CachedGlobalPrefix;

        public static GuildConfig GetGuildConfig(IMessageChannel channel)
        {
            if (channel.IsPrivate())
            {
                return null;
            }
            return GetGuildConfig(((IGuildChannel)channel).GuildId);
        }

        public static GuildConfig GetGuildConfig(ulong guildId)
            => FergunClient.Database.Find<GuildConfig>("Guilds", x => x.ID == guildId);

        public static GuildConfig GetGuildConfig(IGuild guild)
            => GetGuildConfig(guild.Id);

        public static string GetPrefix(IMessageChannel channel)
            => GetGuildConfig(channel)?.Prefix ?? FergunConfig.GlobalPrefix;

        public static string GetLanguage(IMessageChannel channel)
            => GetGuildConfig(channel)?.Language ?? FergunConfig.Language ?? Constants.DefaultLanguage;

        public static string Locate(string key, IMessageChannel channel)
            => strings.ResourceManager.GetString(key, FergunClient.Locales[GetLanguage(channel)]) ?? key;

        public static string Locate(string key, string language)
            => strings.ResourceManager.GetString(key, FergunClient.Locales[language]) ?? key;
    }
}