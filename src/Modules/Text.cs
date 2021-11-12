using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;

namespace Fergun.Modules
{
    [Order(0)]
    [RequireBotPermission(Constants.MinimumRequiredPermissions)]
    [Ratelimit(Constants.GlobalCommandUsesPerPeriod, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
    public class Text : FergunBase
    {
        [Command("normalize")]
        [Summary("normalizeSummary")]
        [Alias("decancer")]
        [Example("ａｅｓｔｈｅｔｉｃ")]
        public async Task Normalize([Remainder, Summary("normalizeParam1")] string text)
        {
            var normalized = new StringBuilder();

            foreach (char c in text.Normalize(NormalizationForm.FormKD))
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    normalized.Append(c);
                }
            }
            await ReplyAsync(normalized.ToString().Normalize(NormalizationForm.FormKC).Truncate(DiscordConfig.MaxMessageSize), allowedMentions: AllowedMentions.None);
        }

        [Command("randomize")]
        [Summary("randomizeSummary")]
        [Example("hello")]
        public async Task Randomize([Remainder, Summary("randomizeParam1")] string text)
        {
            await ReplyAsync(text.Randomize(), allowedMentions: AllowedMentions.None);
        }

        [Command("repeat")]
        [Summary("repeatSummary")]
        [Example("10 oof")]
        public async Task Repeat([Summary("repeatParam1")] int count, [Remainder, Summary("repeatParam2")] string text)
        {
            count = Math.Max(1, count);

            // Repeat the text to the max message size if the resulting text is too large
            text = text.Length * count > DiscordConfig.MaxMessageSize
                ? text.RepeatToLength(DiscordConfig.MaxMessageSize)
                : text.Repeat(count);

            await ReplyAsync(text, allowedMentions: AllowedMentions.None);
        }

        [Command("reverse")]
        [Summary("reverseSummary")]
        [Example("hello")]
        public async Task Reverse([Remainder, Summary("reverseParam1")] string text)
        {
            await ReplyAsync(text.Reverse(), allowedMentions: AllowedMentions.None);
        }

        [Command("reverselines")]
        [Summary("reverselinesSummary")]
        [Alias("rlines", "rline", "rel")]
        [Example("line 1\nline 2\nline 3")]
        public async Task ReverseLines([Remainder, Summary("reverselinesParam1")] string text)
        {
            await ReplyAsync(text.ReverseEachLine().Truncate(DiscordConfig.MaxMessageSize), allowedMentions: AllowedMentions.None);
        }

        [Command("reversewords")]
        [Summary("reversewordsSummary")]
        [Alias("rwords")]
        [Example("one two three")]
        public async Task ReverseWords([Remainder, Summary("reversewordsParam1")] string text)
        {
            await ReplyAsync(text.ReverseWords().Truncate(DiscordConfig.MaxMessageSize), allowedMentions: AllowedMentions.None);
        }

        [Command("sarcasm")]
        [Summary("sarcasmSummary")]
        [Alias("randomcase", "sarcastic")]
        [Example("you can't do that!")]
        public async Task Sarcasm([Remainder, Summary("sarcasmParam1")] string text)
        {
            await ReplyAsync(text.ToRandomCase(), allowedMentions: AllowedMentions.None);
        }

        [Command("vaporwave")]
        [Summary("vaporwaveSummary")]
        [Alias("vapor", "aesthetic", "fullwidth")]
        [Example("aesthetic")]
        public async Task Vaporwave([Remainder, Summary("vaporwaveParam1")] string text)
        {
            await ReplyAsync(text.ToFullWidth().Truncate(DiscordConfig.MaxMessageSize));
            //await ReplyAsync(new Regex(@"[\uFF61-\uFF9F]+", RegexOptions.Compiled).Replace(text, m => m.Value.Normalize(NormalizationForm.FormKC)));
        }
    }
}