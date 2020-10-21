﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Fergun.APIs.DiscordBots
{
    public class DiscordBotsApi
    {
        public const string ApiEndpoint = "https://discord.bots.gg/api/v1/";

        private static readonly HttpClient _client = new HttpClient() { BaseAddress = new Uri(ApiEndpoint) };

        public DiscordBotsApi()
        {
        }

        public DiscordBotsApi(string apiToken)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(apiToken);
        }

        public async Task<BotsResponse> GetBotsAsync(string query = "", int page = 0, int limit = 50,
            ulong? authorId = null, string authorName = "", bool unverified = false, string lib = "",
            SortKey sort = SortKey.None, SortOrder order = SortOrder.Asc)
        {
            string q = "";
            if (!string.IsNullOrEmpty(query))
            {
                q += $"query={Uri.EscapeDataString(query)}";
            }
            if (page != 0)
            {
                q += $"&page={page}";
            }
            if (limit != 50)
            {
                q += $"&limit={limit}";
            }
            if (authorId.HasValue)
            {
                q += $"&authorId={authorId.Value}";
            }
            if (!string.IsNullOrEmpty(authorName))
            {
                q += $"&authorName={Uri.EscapeDataString(authorName)}";
            }
            if (unverified)
            {
                q += $"&unverified={unverified}";
            }
            if (!string.IsNullOrEmpty(lib))
            {
                q += $"&lib={Uri.EscapeDataString(lib)}";
            }
            if (sort != SortKey.None)
            {
                q += $"&sort={sort.ToString().ToLowerInvariant()}";
            }
            if (order != SortOrder.Asc)
            {
                q += $"&order={order.ToString().ToUpperInvariant()}";
            }
            string json = await _client.GetStringAsync(new Uri($"bots?{q}", UriKind.Relative));
            return JsonConvert.DeserializeObject<BotsResponse>(json);
        }

        public async Task<Bot> GetBotAsync(ulong id, bool sanitized = false)
        {
            string q = "";
            if (sanitized)
            {
                q += $"sanitized={sanitized}";
            }
            string json = await _client.GetStringAsync(new Uri($"bots/{id}?{q}", UriKind.Relative));
            return JsonConvert.DeserializeObject<Bot>(json);
        }

        public async Task<StatsResponse> UpdateStatsAsync(ulong id, int guildCount, int shardCount = 1, int shardId = 0)
        {
            if (_client.DefaultRequestHeaders.Authorization == null)
            {
                throw new NullReferenceException("You must provide a token.");
            }
            var dict = new Dictionary<string, string>
            {
                { "guildCount", guildCount.ToString() }
            };
            if (shardCount != 1)
            {
                dict.Add("shardCount", shardCount.ToString());
            }
            if (shardId != 0)
            {
                dict.Add("shardId", shardId.ToString());
            }
            using (var content = new StringContent(JsonConvert.SerializeObject(dict), Encoding.UTF8, "application/json"))
            {
                var response = await _client.PostAsync(new Uri($"bots/{id}/stats", UriKind.Relative), content);
                string json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<StatsResponse>(json);
            }
        }

        public enum SortKey
        {
            None,
            Username,
            Id,
            Guildcount,
            Library,
            Author
        }

        public enum SortOrder
        {
            Asc,
            Desc
        }
    }
}