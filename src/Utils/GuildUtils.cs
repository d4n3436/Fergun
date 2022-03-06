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
        /// Gets or sets the cached global prefix.
        /// </summary>
        /// <remarks>This prefix may not be up-to-date if its value is modified externally.</remarks>
        public static string CachedGlobalPrefix { get; set; }

        public static int CachedRewriteWarnPercentage { get; set; }

        /// <summary>
        /// Gets or sets the guild prefix cache.
        /// </summary>
        /// <remarks>These prefixes may not be up-to-date if their values are modified externally.</remarks>
        public static ConcurrentDictionary<ulong, string> PrefixCache { get; private set; }

        /// <summary>
        /// Gets or sets the user config cache.
        /// </summary>
        /// <remarks>These configs may not be up-to-date if their values are modified externally.</remarks>
        public static ConcurrentDictionary<ulong, UserConfig> UserConfigCache { get; private set; }

        /// <summary>
        /// Gets or sets the cache of guilds where slash commands are known to be enabled or not.
        /// </summary>
        public static ConcurrentDictionary<ulong, bool> SlashCommandScopeCache { get; private set; }

        /// <summary>
        /// Initializes the prefix cache.
        /// </summary>
        public static void Initialize()
        {
            CachedGlobalPrefix = DatabaseConfig.GlobalPrefix;
            CachedRewriteWarnPercentage = DatabaseConfig.RewriteWarnPercentage;
            var guilds = FergunClient.Database.GetAllDocuments<GuildConfig>(Constants.GuildConfigCollection);
            PrefixCache = new ConcurrentDictionary<ulong, string>(guilds?.ToDictionary(x => x.Id, x => x.Prefix) ?? new Dictionary<ulong, string>());
            var users = FergunClient.Database.GetAllDocuments<UserConfig>(Constants.UserConfigCollection);
            UserConfigCache = new ConcurrentDictionary<ulong, UserConfig>(
                users?.Where(x => x != null).ToDictionary(x => x.Id, x => x) ?? new Dictionary<ulong, UserConfig>());
            SlashCommandScopeCache = new ConcurrentDictionary<ulong, bool>();
        }

        /// <summary>
        /// Returns a cached prefix corresponding to the specified channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <returns>The cached prefix of the channel.</returns>
        public static string GetCachedPrefix(IMessageChannel channel)
            => channel.IsPrivate() ? CachedGlobalPrefix : PrefixCache.GetValueOrDefault(((IGuildChannel)channel).GuildId, CachedGlobalPrefix) ?? CachedGlobalPrefix;

        /// <summary>
        /// Returns the configuration of a guild using the specified channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <returns>The configuration of the guild, or <c>null</c> if the guild cannot be found in the database.</returns>
        public static GuildConfig GetGuildConfig(IMessageChannel channel)
            => channel.IsPrivate() ? null : GetGuildConfig(((IGuildChannel)channel).GuildId);

        /// <summary>
        /// Returns the configuration of the specified guild Id.
        /// </summary>
        /// <param name="guildId">The Id of the guild.</param>
        /// <returns>The configuration of the guild, or <c>null</c> if the guild cannot be found in the database.</returns>
        public static GuildConfig GetGuildConfig(ulong guildId)
            => FergunClient.Database.FindDocument<GuildConfig>(Constants.GuildConfigCollection, x => x.Id == guildId);

        /// <summary>
        /// Returns the configuration of the specified guild.
        /// </summary>
        /// <param name="guild">The guild.</param>
        /// <returns>The configuration of the guild, or <c>null</c> if the guild cannot be found in the database.</returns>
        public static GuildConfig GetGuildConfig(IGuild guild)
            => GetGuildConfig(guild.Id);

        /// <summary>
        /// Returns the prefix of the specified channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <returns>The prefix of the channel.</returns>
        public static string GetPrefix(IMessageChannel channel)
            => GetGuildConfig(channel)?.Prefix ?? DatabaseConfig.GlobalPrefix;

        /// <summary>
        /// Returns the language of the specified channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <returns>The language of the channel.</returns>
        public static string GetLanguage(IMessageChannel channel)
            => GetGuildConfig(channel)?.Language ?? Constants.DefaultLanguage;

        /// <summary>
        /// Returns the localized value of a resource key in a channel.
        /// </summary>
        /// <param name="key">The resource key to localize.</param>
        /// <param name="channel">The channel.</param>
        /// <returns>The localized text, or <paramref name="key"/> if the value cannot be found.</returns>
        public static string Locate(string key, IMessageChannel channel)
            => Locate(key, GetLanguage(channel));

        /// <summary>
        /// Returns the localized value of a resource key in the specified language.
        /// </summary>
        /// <param name="key">The resource key to localize.</param>
        /// <param name="language">The language to localize the resource key.</param>
        /// <returns>The localized text, or <paramref name="key"/> if the value cannot be found.</returns>
        public static string Locate(string key, string language)
            => strings.ResourceManager.GetString(key, FergunClient.Languages.GetValueOrDefault(language, FergunClient.Languages[Constants.DefaultLanguage])) ?? key;
    }
}