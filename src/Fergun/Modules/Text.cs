using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;

namespace Fergun.Modules
{
    [Ratelimit(3, FergunClient.GlobalCooldown, Measure.Minutes)]
    public class Text : FergunBase
    {
        [ThreadStatic]
        private static Random _rngInstance;

        private static Random RngInstance => _rngInstance ??= new Random();

        [Command("normalize")]
        [Summary("normalizeSummary")]
        [Alias("decancer")]
        public async Task Normalize([Remainder, Summary("normalizeParam1")] string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormKD);
            string normalized = "";

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    normalized += c;
                }
            }
            await ReplyAsync(normalized.Normalize(NormalizationForm.FormKC).Truncate(DiscordConfig.MaxMessageSize), allowedMentions: AllowedMentions.None);
        }

        [Command("randomize")]
        [Summary("randomizeSummary")]
        public async Task Randomize([Remainder, Summary("randomizeParam1")] string text)
        {
            await ReplyAsync(new string(text.ToCharArray().OrderBy(s => RngInstance.Next(2) == 0).ToArray()).Truncate(DiscordConfig.MaxMessageSize), allowedMentions: AllowedMentions.None);
        }

        [Command("repeat")]
        [Summary("repeatSummary")]
        public async Task Repeat([Summary("repeatParam1")] uint count,
            [Remainder, Summary("repeatParam2")] string text)
        {
            await ReplyAsync(text.Repeat(Math.Min(DiscordConfig.MaxMessageSize, (int)count)).Truncate(DiscordConfig.MaxMessageSize), allowedMentions: AllowedMentions.None);
        }

        [Command("reverse")]
        [Summary("reverseSummary")]
        public async Task Reverse([Remainder, Summary("reverseParam1")] string text)
        {
            await ReplyAsync(StringExtension.Reverse(text).Truncate(DiscordConfig.MaxMessageSize), allowedMentions: AllowedMentions.None);
        }

        [Command("reverselines")]
        [Summary("reverselinesSummary")]
        [Alias("rlines", "rline", "rel")]
        public async Task Reverselines([Remainder, Summary("reverselinesParam1")] string text)
        {
            await ReplyAsync(StringExtension.ReverseEachLine(text).Truncate(DiscordConfig.MaxMessageSize), allowedMentions: AllowedMentions.None);
        }

        [Command("reversewords")]
        [Summary("reversewordsSummary")]
        [Alias("rwords")]
        public async Task Reversewords([Remainder, Summary("reversewordsParam1")] string text)
        {
            await ReplyAsync(string.Join(" ", text.Split(' ').Reverse()).Truncate(DiscordConfig.MaxMessageSize), allowedMentions: AllowedMentions.None);
        }

        [Command("sarcasm")]
        [Summary("sarcasmSummary")]
        [Alias("randomcase", "sarcastic2")]
        public async Task Sarcasm([Remainder, Summary("sarcasmParam1")] string text)
        {
            await ReplyAsync(string.Concat(text.ToLowerInvariant().Select(x => RngInstance.Next(2) == 0 ? char.ToUpperInvariant(x) : x)).Truncate(DiscordConfig.MaxMessageSize), allowedMentions: AllowedMentions.None);
        }

        [Command("vapor")]
        [Summary("vaporwaveSummary")]
        [Alias("vaporwave", "aesthetic", "fullwidth")]
        public async Task Vapor([Remainder, Summary("vaporwaveParam1")] string text)
        {
            await ReplyAsync(text.Fullwidth().Truncate(DiscordConfig.MaxMessageSize));
            //await ReplyAsync(new Regex(@"[\uFF61-\uFF9F]+", RegexOptions.Compiled).Replace(text, m => m.Value.Normalize(NormalizationForm.FormKC)));
        }
    }
}