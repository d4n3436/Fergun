using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Fergun.APIs.DiscordBots
{
    public class BotsResponse
    {
        [JsonProperty("count")]
        public long Count { get; set; }

        [JsonProperty("limit")]
        public long Limit { get; set; }

        [JsonProperty("page")]
        public long Page { get; set; }

        [JsonProperty("bots")]
        public List<Bot> Bots { get; set; }
    }

    public class Bot
    {
        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("clientId")]
        public string ClientId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("discriminator")]
        public string Discriminator { get; set; }

        [JsonProperty("avatarURL")]
        public string AvatarUrl { get; set; }

        [JsonProperty("coOwners")]
        public List<Owner> CoOwners { get; set; }

        [JsonProperty("prefix")]
        public string Prefix { get; set; }

        [JsonProperty("helpCommand")]
        public string HelpCommand { get; set; }

        [JsonProperty("libraryName")]
        public string LibraryName { get; set; }

        [JsonProperty("website")]
        public string Website { get; set; }

        [JsonProperty("supportInvite")]
        public string SupportInvite { get; set; }

        [JsonProperty("botInvite")]
        public string BotInvite { get; set; }

        [JsonProperty("shortDescription")]
        public string ShortDescription { get; set; }

        [JsonProperty("longDescription", NullValueHandling = NullValueHandling.Ignore)]
        public string LongDescription { get; set; } // only on /bots/:id

        [JsonProperty("openSource")]
        public string OpenSource { get; set; }

        [JsonProperty("shardCount")]
        public long ShardCount { get; set; }

        [JsonProperty("guildCount")]
        public long GuildCount { get; set; }

        [JsonProperty("verified")]
        public bool Verified { get; set; }

        [JsonProperty("online")]
        public bool Online { get; set; }

        [JsonProperty("inGuild")]
        public bool InGuild { get; set; }

        [JsonProperty("deleted")]
        public bool Deleted { get; set; }

        [JsonProperty("owner")]
        public Owner Owner { get; set; }

        [JsonProperty("addedDate")]
        public DateTimeOffset AddedDate { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
    }

    public class Owner
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("discriminator")]
        public string Discriminator { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; }
    }

    public class StatsResponse
    {
        [JsonProperty("shardCount")]
        public string ShardCount { get; set; }

        [JsonProperty("guildCount")]
        public string GuildCount { get; set; }
    }
}