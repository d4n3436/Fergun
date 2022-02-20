using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Fergun.APIs;
using Fergun.APIs.Dictionary;
using Fergun.APIs.UrbanDictionary;
using Fergun.APIs.WaybackMachine;
using Fergun.Attributes;
using Fergun.Attributes.Preconditions;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Interactive.Selection;
using Fergun.Responses;
using Fergun.Services;
using Fergun.Utils;
using GScraper;
using GScraper.Brave;
using GScraper.DuckDuckGo;
using GScraper.Google;
using GTranslate;
using GTranslate.Results;
using GTranslate.Translators;
using NCalc;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Search;

namespace Fergun.Modules
{
    [Order(1)]
    [RequireBotPermission(Constants.MinimumRequiredPermissions)]
    [Ratelimit(Constants.GlobalCommandUsesPerPeriod, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
    public class Utility : FergunBase
    {
        private static readonly Regex _bracketRegex = new Regex(@"\[(.+?)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled); // \[(\[*.+?]*)\]
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = Constants.HttpClientTimeout };
        private static readonly YoutubeClient _ytClient = new YoutubeClient();
        private static readonly GoogleScraper _googleScraper = new GoogleScraper();
        private static readonly DuckDuckGoScraper _ddgScraper = new DuckDuckGoScraper();
        private static readonly BraveScraper _braveScraper = new BraveScraper();
        private static Language[] _filteredLanguages;
        private static Dictionary<string, string> _commandListCache;
        private static int _cachedVisibleCmdCount = -1;
        private static XkcdComic _lastComic;
        private static DateTimeOffset _timeToCheckComic = DateTimeOffset.UtcNow;
        private readonly CommandService _cmdService;
        private readonly LogService _logService;
        private readonly AggregateTranslator _translator;
        private readonly GoogleTranslator _googleTranslator;
        private readonly GoogleTranslator2 _googleTranslator2;
        private readonly BingTranslator _bingTranslator;
        private readonly MicrosoftTranslator _microsoftTranslator;
        private readonly YandexTranslator _yandexTranslator;
        private readonly MessageCacheService _messageCache;
        private readonly InteractiveService _interactive;

        public Utility(CommandService commands, LogService logService, MessageCacheService messageCache,
            InteractiveService interactive, AggregateTranslator translator, GoogleTranslator googleTranslator,
            GoogleTranslator2 googleTranslator2, BingTranslator bingTranslator, MicrosoftTranslator microsoftTranslator,
            YandexTranslator yandexTranslator)
        {
            _cmdService ??= commands;
            _logService ??= logService;
            _messageCache ??= messageCache;
            _interactive ??= interactive;
            _translator = translator;
            _googleTranslator = googleTranslator;
            _googleTranslator2 = googleTranslator2;
            _bingTranslator = bingTranslator;
            _microsoftTranslator = microsoftTranslator;
            _yandexTranslator = yandexTranslator;
        }

        [RequireNsfw(ErrorMessage = "NSFWOnly")]
        [RequireBotPermission(ChannelPermission.AttachFiles, ErrorMessage = "BotRequireAttachFiles")]
        [LongRunning]
        [Command("archive", RunMode = RunMode.Async), Ratelimit(1, 1.5, Measure.Minutes)]
        [Summary("archiveSummary")]
        [Remarks("TimestampFormat")]
        [Alias("waybackmachine", "wb")]
        [Example("https://www.youtube.com 2008")]
        public async Task<RuntimeResult> Archive([Summary("archiveParam1")] string url, [Summary("archiveParam2")] ulong timestamp)
        {
            double length = Math.Floor(Math.Log10(timestamp) + 1);
            if (length < 4 || length > 14)
            {
                return FergunResult.FromError($"{Locate("InvalidTimestamp")} {Locate("TimestampFormat")}");
            }

            Uri uri;
            try
            {
                uri = new UriBuilder(Uri.UnescapeDataString(url)).Uri;
            }
            catch (UriFormatException)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Archive: Invalid url: {Uri.UnescapeDataString(url)}"));
                return FergunResult.FromError(Locate("InvalidUrl"));
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Archive: Url: {uri.AbsoluteUri}"));

            WaybackResponse waybackResponse;
            try
            {
                waybackResponse = await WaybackApi.GetSnapshotAsync(uri.AbsoluteUri, timestamp);
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error in Wayback Machine API, url: {url}", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error in Wayback Machine API, url: {url}", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            var snapshot = waybackResponse.ArchivedSnapshots?.Closest;

            if (snapshot == null)
            {
                return FergunResult.FromError(Locate("NoSnapshots"));
            }

            bool success = DateTimeOffset.TryParseExact(snapshot.Timestamp, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var datetime);

            var builder = new EmbedBuilder()
                .WithTitle("Wayback Machine")
                .AddField("Url", Format.Url(Locate("ClickHere"), snapshot.Url))
                .AddField(Locate("Timestamp"), success ? $"{datetime.ToDiscordTimestamp()} ({datetime.ToDiscordTimestamp('R')})" : "?")
                .WithColor(FergunClient.Config.EmbedColor);

            if (string.IsNullOrEmpty(FergunClient.Config.ApiFlashAccessKey))
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", "Archive: ApiFlash access key is null or empty, sending only the embed."));

                await ReplyAsync(embed: builder.Build());

                return FergunResult.FromSuccess();
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Archive: Requesting screenshot of url: {snapshot.Url}"));
            ApiFlashResponse response;
            try
            {
                response = await ApiFlash.UrlToImageAsync(FergunClient.Config.ApiFlashAccessKey, snapshot.Url, ApiFlash.FormatType.Png, "400,403,404,500-511");
            }
            catch (ArgumentException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Archive: Error in ApiFlash API", e));
                return FergunResult.FromError(Locate("InvalidUrl"));
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Archive: Error in ApiFlash API", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Archive: Error in ApiFlash API", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            if (response.ErrorMessage != null)
            {
                return FergunResult.FromError(response.ErrorMessage);
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", "Archive: Sending the embed..."));
            try
            {
                builder.ImageUrl = "attachment://screenshot.png";
                await using var image = await _httpClient.GetStreamAsync(new Uri(response.Url));
                await Context.Channel.SendCachedFileAsync(Cache, Context.Message.Id, image, "screenshot.png", embed: builder.Build());
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the image from url: {url}", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the image from url: {url}", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            return FergunResult.FromSuccess();
        }

        [Command("avatar", RunMode = RunMode.Async)]
        [Summary("avatarSummary")]
        [Example("Fergun#6839")]
        public async Task<RuntimeResult> Avatar([Remainder, Summary("avatarParam1")] IUser user = null)
        {
            user ??= Context.User;
            Discord.Color avatarColor = default;
            string avatarUrl = user.GetAvatarUrl(ImageFormat.Auto, 2048) ?? user.GetDefaultAvatarUrl();

            if (user is RestUser restUser && restUser.AccentColor != null)
            {
                avatarColor = restUser.AccentColor.Value;
            }

            if (avatarColor == default)
            {
                if (!(user is RestUser))
                {
                    // Prevent getting error 404 while downloading the avatar getting the user from REST.
                    user = await Context.Client.Rest.GetUserAsync(user.Id);
                    avatarUrl = user.GetAvatarUrl(ImageFormat.Auto, 2048) ?? user.GetDefaultAvatarUrl();
                }

                string thumbnail = user.GetAvatarUrl(ImageFormat.Png) ?? user.GetDefaultAvatarUrl();

                try
                {
                    await using var response = await _httpClient.GetStreamAsync(new Uri(thumbnail));
                    using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(response);
                    var average = img.GetAverageColor().ToPixel<Rgba32>();
                    avatarColor = new Discord.Color(average.R, average.G, average.B);
                }
                catch (HttpRequestException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the avatar from user {user}", e));
                    return FergunResult.FromError(e.Message);
                }
                catch (TaskCanceledException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the avatar from user {user}", e));
                    return FergunResult.FromError(Locate("RequestTimedOut"));
                }
            }

            var builder = new EmbedBuilder
            {
                Title = user.ToString(),
                ImageUrl = avatarUrl,
                Color = avatarColor
            };

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("badtranslator", RunMode = RunMode.Async), Ratelimit(1, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("badtranslatorSummary")]
        [Alias("bt")]
        [Example("i don't know what to say lol")]
        public async Task<RuntimeResult> BadTranslator([Remainder, Summary("badtranslatorParam1")] string text)
        {
            // Get languages that all services support
            _filteredLanguages ??= Language.LanguageDictionary
                .Values
                .Where(x => x.SupportedServices == (TranslationServices.Google | TranslationServices.Bing | TranslationServices.Yandex | TranslationServices.Microsoft))
                .ToArray();

            // Create an aggregated translator manually so we can randomize the initial order of the translators and shift them.
            // Bing Translator is not included because it only allows max. 1000 chars per translation
            var translators = new ITranslator[] { _googleTranslator, _googleTranslator2, _microsoftTranslator, _yandexTranslator };
            translators.Shuffle();

            var badTranslator = new AggregateTranslator(translators);
            var languageChain = new List<ILanguage>();
            const int chainCount = 8;
            ILanguage sourceLanguage = null;
            for (int i = 0; i < chainCount; i++)
            {
                ILanguage targetLanguage;
                if (i == chainCount - 1)
                {
                    targetLanguage = sourceLanguage;
                }
                else
                {
                    // Get unique and random languages.
                    do
                    {
                        targetLanguage = _filteredLanguages[Random.Shared.Next(_filteredLanguages.Length)];
                    } while (languageChain.Contains(targetLanguage));
                }

                // Shift the translators to avoid spamming them and get more variety
                var last = translators[^1];
                Array.Copy(translators, 0, translators, 1, translators.Length - 1);
                translators[0] = last;

                ITranslationResult result;
                try
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Info, "BadTranslator", $"Translating to: {targetLanguage!.ISO6391}"));
                    result = await badTranslator.TranslateAsync(text, targetLanguage);
                }
                catch (Exception e) when (e is TranslatorException || e is HttpRequestException)
                {
                    return FergunResult.FromError(e.Message);
                }
                catch (AggregateException e)
                {
                    return FergunResult.FromError(e.InnerExceptions.FirstOrDefault()?.Message ?? e.Message);
                }

                if (i == 0)
                {
                    sourceLanguage = result.SourceLanguage;
                    await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Badtranslator: Original language: {sourceLanguage.ISO6391}"));
                    languageChain.Add(sourceLanguage);
                }
                text = result.Translation;
                languageChain.Add(targetLanguage);
            }

            string description = $"**{Locate("LanguageChain")}**\n" +
                                 $"{string.Join(" -> ", languageChain.Select(x => x.ISO6391))}\n\n" +
                                 $"**{Locate("Result")}**\n";

            if (description.Length + text.Length > EmbedBuilder.MaxDescriptionLength)
            {
                try
                {
                    var hastebinUrl = await Hastebin.UploadAsync(text);
                    text = Format.Url(Locate("HastebinLink"), hastebinUrl);
                }
                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Paste: Error while uploading text to Hastebin", e));
                }
            }

            var builder = new EmbedBuilder()
                .WithTitle("Bad translator")
                .WithDescription($"{description}{text.Truncate(EmbedBuilder.MaxDescriptionLength - description.Length)}")
                .WithThumbnailUrl(Constants.BadTranslatorLogoUrl)
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("base64decode")]
        [Summary("base64decodeSummary")]
        [Alias("b64decode", "b64d")]
        [Example("aGVsbG8gd29ybGQ=")]
        public async Task<RuntimeResult> Base64Decode([Remainder, Summary("base64decodeParam1")] string text)
        {
            if (!text.TryBase64Decode(out string decoded))
            {
                return FergunResult.FromError(Locate("base64decodeInvalid"));
            }

            await ReplyAsync(decoded, allowedMentions: AllowedMentions.None);
            return FergunResult.FromSuccess();
        }

        [Command("base64encode")]
        [Summary("base64encodeSummary")]
        [Alias("b64encode", "b64e")]
        [Example("hello")]
        public async Task Base64Encode([Remainder, Summary("base64encodeParam1")] string text)
        {
            text = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            if (text.Length > DiscordConfig.MaxMessageSize)
            {
                try
                {
                    text = await Hastebin.UploadAsync(text);
                }
                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Badtranslator: Error while uploading text to Hastebin", e));
                    text = text.Truncate(DiscordConfig.MaxMessageSize);
                }
            }
            await ReplyAsync(text);
        }

        [Command("bigeditsnipe", RunMode = RunMode.Async)]
        [Summary("bigeditsnipeSummary")]
        [Alias("besnipe", "bes")]
        public async Task<RuntimeResult> BigEditSnipe([Summary("bigeditsnipeParam1")] IMessageChannel channel = null)
        {
            channel ??= Context.Channel;
            var messages = _messageCache
                .GetCacheForChannel(channel, MessageSourceEvent.MessageUpdated)
                .Values
                .OrderByDescending(x => x.CachedAt)
                .Where(x => !GuildUtils.UserConfigCache.TryGetValue(x.Author.Id, out var config) || !config.IsOptedOutSnipe)
                .Take(20)
                .ToArray();

            var builder = new EmbedBuilder();
            if (messages.Length == 0)
            {
                builder.WithDescription(string.Format(Locate("NothingToSnipe"), MentionUtils.MentionChannel(channel.Id)));
            }
            else
            {
                var text = messages.Select(x =>
                    $"{Format.Bold(x.Author.ToString())} ({(x.OriginalMessage?.CreatedAt ?? x.CreatedAt).ToDiscordTimestamp('R')})" +
                    $"\n{(x.OriginalMessage?.Content ?? x.Content).Truncate(200)}\n\n");

                builder.WithTitle("Big edit snipe")
                    .WithDescription(string.Concat(text).Truncate(EmbedBuilder.MaxDescriptionLength))
                    .WithFooter($"{Locate("In")} #{channel.Name}");

                if (Random.Shared.Next(5) == 4)
                {
                    builder.AddField(Locate("Privacy"), Locate("SnipePrivacy"));
                }
            }
            builder.WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [Command("bigsnipe", RunMode = RunMode.Async)]
        [Summary("bigsnipeSummary")]
        [Alias("bsnipe", "bs")]
        public async Task<RuntimeResult> BigSnipe([Summary("bigsnipeParam1")] IMessageChannel channel = null)
        {
            channel ??= Context.Channel;
            var messages = _messageCache
                .GetCacheForChannel(channel, MessageSourceEvent.MessageDeleted)
                .Values
                .OrderByDescending(x => x.CachedAt)
                .Where(x => !GuildUtils.UserConfigCache.TryGetValue(x.Author.Id, out var config) || !config.IsOptedOutSnipe)
                .Take(20)
                .ToArray();

            var builder = new EmbedBuilder();
            if (messages.Length == 0)
            {
                builder.WithDescription(string.Format(Locate("NothingToSnipe"), MentionUtils.MentionChannel(channel.Id)));
            }
            else
            {
                string text = "";
                foreach (var msg in messages)
                {
                    text += $"{Format.Bold(msg.Author.ToString())} ({msg.CreatedAt.ToDiscordTimestamp('R')})\n";
                    text += !string.IsNullOrEmpty(msg.Content) ? msg.Content.Truncate(200) : msg.Attachments.Count > 0 ? $"({Locate("Attachment")})" : "?";
                    text += "\n\n";
                }
                builder.WithTitle("Big snipe")
                    .WithDescription(text.Truncate(EmbedBuilder.MaxDescriptionLength))
                    .WithFooter($"{Locate("In")} #{channel.Name}");

                if (Random.Shared.Next(5) == 4)
                {
                    builder.AddField(Locate("Privacy"), Locate("SnipePrivacy"));
                }
            }
            builder.WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [Command("calc", RunMode = RunMode.Async)]
        [Summary("calcSummary")]
        [Alias("calculate")]
        [Example("2 * 2 - 1")]
        public async Task<RuntimeResult> Calc([Remainder, Summary("calcParam1")] string expression)
        {
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Calc: expression: {expression}"));

            var sw = Stopwatch.StartNew();
            var ex = new Expression(expression);
            if (ex.HasErrors())
            {
                return FergunResult.FromError(Locate("InvalidExpression"));
            }
            string result;
            try
            {
                result = ex.Evaluate().ToString();
            }
            catch (Exception e) when (e is ArgumentException || e is EvaluationException || e is OverflowException)
            {
                return FergunResult.FromError(Locate("InvalidExpression"));
            }

            sw.Stop();
            if (result == null)
            {
                return FergunResult.FromError(Locate("InvalidExpression"));
            }

            if (result.Length > EmbedFieldBuilder.MaxFieldValueLength)
            {
                result = result.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 3) + "...";
            }

            var builder = new EmbedBuilder()
                .WithTitle(Locate("CalcResults"))
                .AddField(Locate("Input"), Format.Code(expression.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md"))
                .AddField(Locate("Output"), Format.Code(result.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md"))
                .WithFooter(string.Format(Locate("EvalFooter"), sw.ElapsedMilliseconds))
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("channelinfo")]
        [Summary("channelinfoSummary")]
        [Alias("channel")]
        [Example("#general")]
        public async Task<RuntimeResult> ChannelInfo([Remainder, Summary("channelinfoParam1")] IChannel channel = null)
        {
            channel ??= Context.Channel;

            var builder = new EmbedBuilder()
                .WithTitle(Locate("ChannelInfo"))
                .AddField(Locate("Name"), channel.Name ?? "?", true)
                .AddField("ID", channel.Id, true);

            switch (channel)
            {
                case IThreadChannel threadChannel:
                    builder.AddField(Locate("Type"), $"{Locate("Thread")} ({threadChannel.Type})", true)
                        .AddField(Locate("Archived"), Locate(threadChannel.IsArchived), true)
                        .AddField(Locate("IsNSFW"), Locate(threadChannel.IsNsfw), true)
                        .AddField(Locate("SlowMode"), TimeSpan.FromSeconds(threadChannel.SlowModeInterval).ToShortForm2(), true)
                        .AddField(Locate("AutoArchive"), TimeSpan.FromMinutes((int)threadChannel.AutoArchiveDuration).ToShortForm2(), true);
                    break;

                case IStageChannel stageChannel:
                    builder.AddField(Locate("Type"), Locate("StageChannel"), true)
                        .AddField(Locate("Topic"), string.IsNullOrEmpty(stageChannel.Topic) ? Locate("None") : stageChannel.Topic, true)
                        .AddField(Locate("IsLive"), Locate(stageChannel.IsLive), true)
                        .AddField(Locate("Bitrate"), $"{stageChannel.Bitrate / 1000}kbps", true)
                        .AddField(Locate("UserLimit"), stageChannel.UserLimit?.ToString() ?? Locate("NoLimit"), true);
                    break;

                case ITextChannel textChannel:
                    builder.AddField(Locate("Type"), Locate(channel is INewsChannel ? "AnnouncementChannel" : "TextChannel"), true)
                        .AddField(Locate("Topic"), string.IsNullOrEmpty(textChannel.Topic) ? Locate("None") : textChannel.Topic, true)
                        .AddField(Locate("IsNSFW"), Locate(textChannel.IsNsfw), true)
                        .AddField(Locate("SlowMode"), TimeSpan.FromSeconds(channel is INewsChannel ? 0 : textChannel.SlowModeInterval).ToShortForm2(), true)
                        .AddField(Locate("Category"), textChannel.CategoryId.HasValue ? Context.Guild.GetCategoryChannel(textChannel.CategoryId.Value).Name : Locate("None"), true);
                    break;

                case IVoiceChannel voiceChannel:
                    builder.AddField(Locate("Type"), Locate("VoiceChannel"), true)
                        .AddField(Locate("Bitrate"), $"{voiceChannel.Bitrate / 1000}kbps", true)
                        .AddField(Locate("UserLimit"), voiceChannel.UserLimit?.ToString() ?? Locate("NoLimit"), true);
                    break;

                case SocketCategoryChannel categoryChannel:
                    builder.AddField(Locate("Type"), Locate("Category"), true)
                        .AddField(Locate("Channels"), categoryChannel.Channels.Count, true);
                    break;

                case IDMChannel _:
                    builder.AddField(Locate("Type"), Locate("DMChannel"), true);
                    break;

                default:
                    builder.AddField(Locate("Type"), "?", true);
                    break;
            }
            if (channel is IGuildChannel guildChannel)
            {
                builder.AddField(Locate("Position"), guildChannel.Position, true);
            }
            builder.AddField(Locate("CreatedAt"), channel.CreatedAt.ToDiscordTimestamp(), true)
                .AddField(Locate("Mention"), MentionUtils.MentionChannel(channel.Id), true)
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("choice")]
        [Summary("choiceSummary")]
        [Alias("choose")]
        [Example("c c++ c#")]
        public async Task<RuntimeResult> Choice([Summary("choiceParam1")] params string[] choices)
        {
            if (choices.Length == 0)
            {
                return FergunResult.FromError(Locate("NoChoices"));
            }
            await ReplyAsync($"{Locate("IChoose")} **{choices[Random.Shared.Next(0, choices.Length)]}**{(choices.Length == 1 ? Locate("OneChoice") : "")}", allowedMentions: AllowedMentions.None);
            return FergunResult.FromSuccess();
        }

        [Command("color")]
        [Summary("colorSummary")]
        [Example("#ff983e")]
        public async Task<RuntimeResult> Color([Summary("colorParam1")] string color = null)
        {
            System.Drawing.Color argbColor;
            if (string.IsNullOrWhiteSpace(color))
            {
                argbColor = System.Drawing.Color.FromArgb(Random.Shared.Next(0, 256), Random.Shared.Next(0, 256), Random.Shared.Next(0, 256));
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Color: Generated random color: {argbColor}"));
            }
            else
            {
                color = color.TrimStart('#');
                if (!int.TryParse(color, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rawColor)
                    && !int.TryParse(color, NumberStyles.Integer, CultureInfo.InvariantCulture, out rawColor))
                {
                    rawColor = System.Drawing.Color.FromName(color).ToArgb();
                    if (rawColor == 0)
                    {
                        rawColor = color.ToColor();
                        await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Color: Converted string to color: {rawColor}"));
                        //rawColor = uint.Parse(color.ToColor(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    }
                    //return FergunResult.FromError(Locate("InvalidColor"));
                }
                argbColor = System.Drawing.Color.FromArgb(rawColor);
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Color: {color.Truncate(30)} -> {rawColor} -> {argbColor}"));
            }

            using var image = new Image<Rgba32>(500, 500);

            var graphicsOptions = new GraphicsOptions
            {
                AlphaCompositionMode = PixelAlphaCompositionMode.Src,
                ColorBlendingMode = PixelColorBlendingMode.Normal
            };

            image.Mutate(x => x.Fill(graphicsOptions, SixLabors.ImageSharp.Color.FromRgb(argbColor.R, argbColor.G, argbColor.B)));
            await using var stream = new MemoryStream();
            await image.SaveAsPngAsync(stream);
            stream.Seek(0, SeekOrigin.Begin);

            string hex = $"{argbColor.R:X2}{argbColor.G:X2}{argbColor.B:X2}";

            var builder = new EmbedBuilder()
                .WithTitle($"#{hex}")
                .WithImageUrl($"attachment://{hex}.png")
                .WithFooter($"R: {argbColor.R}, G: {argbColor.G}, B: {argbColor.B}")
                .WithColor(new Discord.Color(argbColor.R, argbColor.G, argbColor.B));

            await Context.Channel.SendCachedFileAsync(Cache, Context.Message.Id, stream, $"{hex}.png", embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [AlwaysEnabled]
        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [RequireUserPermission(GuildPermission.ManageGuild, ErrorMessage = "UserRequireManageServer")]
        [LongRunning]
        [Command("config", RunMode = RunMode.Async), Ratelimit(1, 2, Measure.Minutes)]
        [Summary("configSummary")]
        [Alias("configuration", "settings")]
        public async Task<RuntimeResult> Config()
        {
            string language = GetLanguage();
            string[] configList = Locate("ConfigList", language).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string menuOptions = "";
            for (int i = 0; i < configList.Length; i++)
            {
                menuOptions += $"**{i + 1}.** {configList[i]}\n";
            }

            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            var guildConfig = GetGuildConfig() ?? new GuildConfig(Context.Guild.Id);
            var options = Enumerable.Range(1, 2).ToDictionary(x => new Emoji($"{x}\ufe0f\u20e3") as IEmote, y => y);
            options.Add(new Emoji("❌"), -1);

            InteractiveMessageResult<KeyValuePair<IEmote, int>> result = null;
            IUserMessage message = null;

            while (result == null || result.Status == InteractiveStatus.Success)
            {
                var selection = new EmoteSelectionBuilder<int>()
                    .WithActionOnCancellation(ActionOnStop.DisableInput)
                    .WithActionOnTimeout(ActionOnStop.DisableInput)
                    .WithSelectionPage(CreateMenuPage())
                    .AddUser(Context.User)
                    .WithOptions(options)
                    .WithAllowCancel(true)
                    .Build();

                result = await SendSelectionAsync(selection, TimeSpan.FromMinutes(5), message, cts.Token);
                message = result.Message;

                if (!result.IsSuccess) break;

                guildConfig = GetGuildConfig() ?? new GuildConfig(Context.Guild.Id);

                switch (result.Value.Value)
                {
                    case 1:
                        guildConfig.AidAutoTranslate = !guildConfig.AidAutoTranslate;
                        break;

                    case 2:
                        guildConfig.TrackSelection = !guildConfig.TrackSelection;
                        break;
                }

                FergunClient.Database.InsertOrUpdateDocument(Constants.GuildConfigCollection, guildConfig);
            }

            return FergunResult.FromSuccess();

            PageBuilder CreateMenuPage()
            {
                string valueList =
                    $"{Locate(guildConfig.AidAutoTranslate ? "Yes" : "No", language)}\n" +
                    $"{Locate(guildConfig.TrackSelection ? "Yes" : "No", language)}";

                var builder = new EmbedBuilder()
                    .WithAuthor(Context.User)
                    .WithTitle(Locate("FergunConfig", language))
                    .WithDescription(Locate("ConfigPrompt", language))
                    .AddField(Locate("Option", language), menuOptions, true)
                    .AddField(Locate("Value", language), valueList, true)
                    .WithColor(FergunClient.Config.EmbedColor);

                return PageBuilder.FromEmbedBuilder(builder);
            }
        }

        [LongRunning]
        [Command("define", RunMode = RunMode.Async), Ratelimit(2, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("defineSummary")]
        [Alias("def", "definition", "dictionary")]
        [Example("hi")]
        public async Task<RuntimeResult> Define([Remainder, Summary("defineParam1")] string word)
        {
            IReadOnlyList<DefinitionCategory> results;
            try
            {
                results = await DictionaryApi.GetDefinitionsAsync(word, GetLanguage(), true);
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error calling Dictionary API", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error calling Dictionary API", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error deserializing Dictionary API response", e));
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }

            var definitions = new List<SimpleDefinitionInfo>();
            foreach (var result in results ?? Enumerable.Empty<DefinitionCategory>())
            {
                foreach (var meaning in result.Meanings)
                {
                    foreach (var definition in meaning.Definitions)
                    {
                        definitions.Add(new SimpleDefinitionInfo
                        {
                            Word = result.Word,
                            PartOfSpeech = meaning.PartOfSpeech,
                            Definition = definition.Definition,
                            Example = definition.Example,
                            Synonyms = definition.Synonyms,
                            Antonyms = definition.Antonyms
                        });
                    }
                }
            }

            if (definitions.Count == 0)
            {
                return FergunResult.FromError(Locate("NoResultsFound"));
            }

            string wordText = Locate("Word");
            string definitionText = Locate("Definition");
            string paginatorFooter = Locate("PaginatorFooter");
            string exampleText = Locate("Example");
            string synonymsText = Locate("Synonyms");
            string antonymsText = Locate("Antonyms");

            Task<PageBuilder> GeneratePageAsync(int index)
            {
                var info = definitions[index];

                var pageBuilder = new PageBuilder()
                    .WithColor(new Discord.Color(FergunClient.Config.EmbedColor))
                    .WithTitle("Define")
                    .AddField(wordText, info.PartOfSpeech == null || info.PartOfSpeech == "undefined" ? info.Word : $"{info.Word} ({info.PartOfSpeech})")
                    .AddField(definitionText, info.Definition)
                    .WithFooter(string.Format(paginatorFooter, index + 1, definitions.Count));

                if (!string.IsNullOrEmpty(info.Example))
                {
                    pageBuilder.AddField(exampleText, info.Example);
                }
                if (info.Synonyms.Count > 0)
                {
                    pageBuilder.AddField(synonymsText, string.Join(", ", info.Synonyms));
                }
                if (info.Antonyms.Count > 0)
                {
                    pageBuilder.AddField(antonymsText, string.Join(", ", info.Antonyms));
                }

                return Task.FromResult(pageBuilder);
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(Context.User)
                .WithOptions(CommandUtils.GetFergunPaginatorEmotes(FergunClient.Config))
                .WithMaxPageIndex(definitions.Count - 1)
                .WithPageFactory(GeneratePageAsync)
                .WithFooter(PaginatorFooter.None)
                .WithActionOnCancellation(ActionOnStop.DisableInput)
                .WithActionOnTimeout(ActionOnStop.DisableInput)
                .WithDeletion(DeletionOptions.Valid)
                .Build();

            _ = SendPaginatorAsync(paginator, Constants.PaginatorTimeout);

            return FergunResult.FromSuccess();
        }

        [Command("editsnipe", RunMode = RunMode.Async)]
        [Summary("editsnipeSummary")]
        [Alias("esnipe")]
        [Example("#bots")]
        public async Task EditSnipe([Summary("snipeParam1")] IMessageChannel channel = null)
        {
            channel ??= Context.Channel;
            var message = _messageCache
                .GetCacheForChannel(channel, MessageSourceEvent.MessageUpdated)
                .Values
                .OrderByDescending(x => x.CachedAt)
                .FirstOrDefault(x => !GuildUtils.UserConfigCache.TryGetValue(x.Author.Id, out var config) || !config.IsOptedOutSnipe);

            var builder = new EmbedBuilder();
            if (message == null)
            {
                builder.WithDescription(string.Format(Locate("NothingToSnipe"), MentionUtils.MentionChannel(channel.Id)));
            }
            else
            {
                builder.WithAuthor(message.Author)
                    .WithDescription((message.OriginalMessage?.Content ?? message.Content).Truncate(EmbedBuilder.MaxDescriptionLength))
                    .WithFooter($"{Locate("In")} #{message.Channel.Name}")
                    .WithTimestamp(message.CreatedAt);

                if (Random.Shared.Next(5) == 4)
                {
                    builder.AddField(Locate("Privacy"), Locate("SnipePrivacy"));
                }
            }
            builder.WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());
        }

        [AlwaysEnabled]
        [LongRunning]
        [Command("help", RunMode = RunMode.Async)]
        [Summary("helpSummary")]
        [Example("help")]
        public async Task<RuntimeResult> Help([Remainder, Summary("helpParam1")] string commandName = null)
        {
            var builder = new EmbedBuilder();
            if (commandName == null)
            {
                builder.Title = Locate("CommandList");

                InitializeCmdListCache();

                foreach (var module in _commandListCache)
                {
                    string name = module.Key == "AIDungeon"
                        ? string.Format(Locate($"{module.Key}Commands"), GetPrefix())
                        : Locate($"{module.Key}Commands");

                    builder.AddField(name, module.Value);
                }

                MessageComponent component = null;

                if (!FergunClient.IsDebugMode)
                {
                    component = CommandUtils.BuildLinks(Context.Channel);
                }
                builder.WithFooter(string.Format(Locate("HelpFooter"), $"v{Constants.Version}", _cachedVisibleCmdCount))
                    .WithColor(FergunClient.Config.EmbedColor);

                await ReplyAsync(embed: builder.Build(), component: component);
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Help: Getting help for command: {commandName}"));

                var command = _cmdService.Commands.FirstOrDefault(x => // Match commands ignoring their groups.
                    x.Aliases.Any(y => (y.Split(' ').ElementAtOrDefault(1) ?? y) == commandName.ToLowerInvariant()) &&
                    x.Module.Name != Constants.DevelopmentModuleName);
                if (command == null)
                {
                    return FergunResult.FromError(string.Format(Locate("CommandNotFound"), GetPrefix()));
                }
                var embed = command.ToHelpEmbed(GetLanguage(), GetPrefix());
                await ReplyAsync(embed: embed);
            }
            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("img", RunMode = RunMode.Async), Ratelimit(1, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("imgSummary")]
        [Alias("im", "image")]
        [Example("discord")]
        public async Task<RuntimeResult> Img([Remainder, Summary("imgParam1")] string query)
        {
            query = query.Trim();
            bool isNsfwChannel = Context.IsNsfw();
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Img: Query \"{query}\", NSFW channel: {isNsfwChannel}"));

            IEnumerable<GoogleImageResult> images;
            try
            {
                images = await _googleScraper.GetImagesAsync(query, isNsfwChannel ? SafeSearchLevel.Off : SafeSearchLevel.Strict, language: GetLanguage());
            }
            catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException || e is GScraperException)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error searching images. Using DuckDuckGo", e));
                return await Img2(query);
            }

            var filteredImages = images
                .Where(x =>
                    Uri.IsWellFormedUriString(x.Url, UriKind.Absolute) &&
                    x.Url.StartsWith("http", StringComparison.Ordinal) &&
                    Uri.IsWellFormedUriString(x.SourceUrl, UriKind.Absolute) &&
                    x.SourceUrl.StartsWith("http", StringComparison.Ordinal))
                .ToArray();

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Google Images: Results count: {filteredImages.Length}"));

            if (filteredImages.Length == 0)
            {
                return FergunResult.FromError(Locate("NoResultsFound"));
            }

            string imageSearch = Locate("ImageSearch");
            string paginatorFooter = Locate("PaginatorFooter");

            Task<PageBuilder> GeneratePageAsync(int index)
            {
                var pageBuilder = new PageBuilder()
                    .WithAuthor(Context.User)
                    .WithColor(new Discord.Color(FergunClient.Config.EmbedColor))
                    .WithTitle(filteredImages[index].Title.Truncate(EmbedBuilder.MaxTitleLength))
                    .WithUrl(filteredImages[index].SourceUrl)
                    .WithDescription(imageSearch)
                    .WithImageUrl(filteredImages[index].Url)
                    .WithFooter(string.Format(paginatorFooter, index + 1, filteredImages.Length), Constants.GoogleLogoUrl);

                return Task.FromResult(pageBuilder);
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(Context.User)
                .WithOptions(CommandUtils.GetFergunPaginatorEmotes(FergunClient.Config))
                .WithMaxPageIndex(filteredImages.Length - 1)
                .WithPageFactory(GeneratePageAsync)
                .WithFooter(PaginatorFooter.None)
                .WithActionOnCancellation(ActionOnStop.DisableInput)
                .WithActionOnTimeout(ActionOnStop.DisableInput)
                .WithDeletion(DeletionOptions.Valid)
                .Build();

            _ = SendPaginatorAsync(paginator, Constants.PaginatorTimeout);

            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("img2", RunMode = RunMode.Async), Ratelimit(1, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("img2Summary")]
        [Alias("im2", "image2", "ddgi")]
        [Example("discord")]
        public async Task<RuntimeResult> Img2([Remainder, Summary("img2Param1")] string query)
        {
            query = query.Replace("!", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (string.IsNullOrEmpty(query))
            {
                return FergunResult.FromError(Locate("NoResultsFound"));
            }
            if (query.Length > DuckDuckGoScraper.MaxQueryLength)
            {
                return FergunResult.FromError(string.Format(Locate("MustBeLowerThan"), nameof(query), DuckDuckGoScraper.MaxQueryLength));
            }

            bool isNsfwChannel = Context.IsNsfw();
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Img2: Query \"{query}\", NSFW channel: {isNsfwChannel}"));

            IEnumerable<DuckDuckGoImageResult> images;
            try
            {
                images = await _ddgScraper.GetImagesAsync(query, isNsfwChannel ? SafeSearchLevel.Off : SafeSearchLevel.Strict);
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error searching images", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error searching images", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }
            catch (GScraperException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error searching images", e));
                return FergunResult.FromError(Locate("NoResultsFound"));
            }

            var filteredImages = images
                .Where(x =>
                    Uri.IsWellFormedUriString(x.Url, UriKind.Absolute) &&
                    x.Url.StartsWith("http", StringComparison.Ordinal) &&
                    Uri.IsWellFormedUriString(x.SourceUrl, UriKind.Absolute) &&
                    x.SourceUrl.StartsWith("http", StringComparison.Ordinal))
                .ToArray();

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"DuckDuckGo Images: Results count: {filteredImages.Length}"));

            if (filteredImages.Length == 0)
            {
                return FergunResult.FromError(Locate("NoResultsFound"));
            }

            string imageSearch = Locate("ImageSearch");
            string paginatorFooter = Locate("PaginatorFooter");

            Task<PageBuilder> GeneratePageAsync(int index)
            {
                var pageBuilder = new PageBuilder()
                    .WithAuthor(Context.User)
                    .WithColor(new Discord.Color(FergunClient.Config.EmbedColor))
                    .WithTitle(filteredImages[index].Title.Truncate(EmbedBuilder.MaxTitleLength))
                    .WithUrl(filteredImages[index].SourceUrl)
                    .WithDescription(imageSearch)
                    .WithImageUrl(filteredImages[index].Url)
                    .WithFooter(string.Format(paginatorFooter, index + 1, filteredImages.Length), Constants.DuckDuckGoLogoUrl);

                return Task.FromResult(pageBuilder);
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(Context.User)
                .WithOptions(CommandUtils.GetFergunPaginatorEmotes(FergunClient.Config))
                .WithMaxPageIndex(filteredImages.Length - 1)
                .WithPageFactory(GeneratePageAsync)
                .WithFooter(PaginatorFooter.None)
                .WithActionOnCancellation(ActionOnStop.DisableInput)
                .WithActionOnTimeout(ActionOnStop.DisableInput)
                .WithDeletion(DeletionOptions.Valid)
                .Build();

            _ = SendPaginatorAsync(paginator, Constants.PaginatorTimeout);

            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("img3", RunMode = RunMode.Async), Ratelimit(1, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("img3Summary")]
        [Alias("im3", "image3", "brave")]
        [Example("discord")]
        public async Task<RuntimeResult> Img3([Remainder, Summary("img3Param1")] string query)
        {
            query = query.Trim();
            bool isNsfwChannel = Context.IsNsfw();
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Img3: Query \"{query}\", NSFW channel: {isNsfwChannel}"));

            IEnumerable<BraveImageResult> images;
            try
            {
                images = await _braveScraper.GetImagesAsync(query, isNsfwChannel ? SafeSearchLevel.Off : SafeSearchLevel.Strict);
            }
            catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException || e is GScraperException)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error searching images. Using Google", e));
                return await Img(query);
            }

            var filteredImages = images
                .Where(x =>
                    Uri.IsWellFormedUriString(x.Url, UriKind.Absolute) &&
                    x.Url.StartsWith("http", StringComparison.Ordinal) &&
                    Uri.IsWellFormedUriString(x.SourceUrl, UriKind.Absolute) &&
                    x.SourceUrl.StartsWith("http", StringComparison.Ordinal))
                .ToArray();

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Brave Search: Results count: {filteredImages.Length}"));

            if (filteredImages.Length == 0)
            {
                return FergunResult.FromError(Locate("NoResultsFound"));
            }

            string imageSearch = Locate("ImageSearch");
            string paginatorFooter = Locate("PaginatorFooter");

            Task<PageBuilder> GeneratePageAsync(int index)
            {
                var pageBuilder = new PageBuilder()
                    .WithAuthor(Context.User)
                    .WithColor(new Discord.Color(FergunClient.Config.EmbedColor))
                    .WithTitle(filteredImages[index].Title.Truncate(EmbedBuilder.MaxTitleLength))
                    .WithUrl(filteredImages[index].SourceUrl)
                    .WithDescription(imageSearch)
                    .WithImageUrl(filteredImages[index].Url)
                    .WithFooter(string.Format(paginatorFooter, index + 1, filteredImages.Length), Constants.BraveLogoUrl);

                return Task.FromResult(pageBuilder);
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(Context.User)
                .WithOptions(CommandUtils.GetFergunPaginatorEmotes(FergunClient.Config))
                .WithMaxPageIndex(filteredImages.Length - 1)
                .WithPageFactory(GeneratePageAsync)
                .WithFooter(PaginatorFooter.None)
                .WithActionOnCancellation(ActionOnStop.DisableInput)
                .WithActionOnTimeout(ActionOnStop.DisableInput)
                .WithDeletion(DeletionOptions.Valid)
                .Build();

            _ = SendPaginatorAsync(paginator, Constants.PaginatorTimeout);

            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [RequireBotPermission(ChannelPermission.AttachFiles, ErrorMessage = "BotRequireAttachFiles")]
        [Command("invert", RunMode = RunMode.Async), Ratelimit(2, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("invertSummary")]
        [Alias("negate", "negative")]
        [Example("https://www.fergun.com/image.png")]
        public async Task<RuntimeResult> Invert([Remainder, Summary("invertParam1")] string url = null)
        {
            UrlFindResult result;
            (url, result) = await Context.GetLastUrlAsync(FergunClient.Config.MessagesToSearchLimit, _messageCache, true, url);
            if (result != UrlFindResult.UrlFound)
            {
                return FergunResult.FromError(string.Format(Locate(result.ToString()), FergunClient.Config.MessagesToSearchLimit));
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Invert: url to use: {url}"));

            Stream response;
            try
            {
                response = await _httpClient.GetStreamAsync(new Uri(url));
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the image from url: {url}", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the image from url: {url}", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            var (img, format) = await SixLabors.ImageSharp.Image.LoadWithFormatAsync(response);
            img.Mutate(x => x.Invert());

            await using var stream = new MemoryStream();
            await img.SaveAsync(stream, format);
            stream.Seek(0, SeekOrigin.Begin);

            if (stream.Length > Constants.AttachmentSizeLimit)
            {
                return FergunResult.FromError("The file is too large.");
            }

            string fileName = $"invert.{format.FileExtensions.FirstOrDefault() ?? "dat"}";

            var builder = new EmbedBuilder()
                .WithTitle("Invert")
                .WithImageUrl($"attachment://{fileName}")
                .WithColor(FergunClient.Config.EmbedColor);

            await Context.Channel.SendCachedFileAsync(Cache, Context.Message.Id, stream, fileName, embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("lmgtfy", RunMode = RunMode.Async)]
        [Summary("lmgtfySummary")]
        public async Task Lmgtfy([Remainder, Summary("lmgtfyParam1")] string query)
        {
            await ReplyAsync($"https://lmgtfy.com/?q={Uri.EscapeDataString(query)}", allowedMentions: AllowedMentions.None);
        }

        [LongRunning]
        [Command("ocr", RunMode = RunMode.Async), Ratelimit(2, 1, Measure.Minutes)]
        [Summary("ocrSummary")]
        [Remarks("NoUrlPassed")]
        [Example("https://www.fergun.com/image.png")]
        public async Task<RuntimeResult> Ocr([Summary("ocrParam1")] string url = null)
        {
            UrlFindResult result;
            (url, result) = await Context.GetLastUrlAsync(FergunClient.Config.MessagesToSearchLimit, _messageCache, true, url, long.MaxValue);
            if (result != UrlFindResult.UrlFound)
            {
                return FergunResult.FromError(string.Format(Locate(result.ToString()), FergunClient.Config.MessagesToSearchLimit));
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Ocr: url to use: {url}"));

            (string error, string text) = await BingOcrAsync(url);
            if (!int.TryParse(error, out int processTime))
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Ocr: Failed to get OCR from Bing, using Yandex..."));

                (error, text) = await YandexOcrAsync(url);
                if (!int.TryParse(error, out processTime))
                {
                    string message = Locate(error);
                    if (!string.IsNullOrEmpty(text))
                        message += $"\n{Locate("ErrorMessage")}: {text}";

                    return FergunResult.FromError(message);
                }
            }

            if (text.Length > EmbedFieldBuilder.MaxFieldValueLength - 10)
            {
                try
                {
                    var hastebinUrl = await Hastebin.UploadAsync(text);
                    text = Format.Url(Locate("HastebinLink"), hastebinUrl);
                }
                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Ocr: Error while uploading text to Hastebin", e));
                    text = Format.Code(text.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md");
                }
            }
            else
            {
                text = Format.Code(text.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md");
            }

            var builder = new EmbedBuilder()
                .WithTitle(Locate("OcrResults"))
                .AddField(Locate("Output"), text)
                .WithFooter(string.Format(Locate("ProcessingTime"), processTime))
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("ocrtranslate", RunMode = RunMode.Async), Ratelimit(2, 1, Measure.Minutes)]
        [Summary("ocrtranslateSummary")]
        [Alias("ocrtr")]
        [Remarks("NoUrlPassed")]
        [Example("en https://www.fergun.com/image.png")]
        public async Task<RuntimeResult> OcrTranslate([Summary("ocrtranslateParam1")] string target,
            [Summary("ocrtranslateParam2")] string url = null)
        {
            if (!_translator.IsLanguageSupported(target))
            {
                return FergunResult.FromError(string.Format(Locate("InvalidLanguage"), GetPrefix()));
            }

            UrlFindResult result;
            (url, result) = await Context.GetLastUrlAsync(FergunClient.Config.MessagesToSearchLimit, _messageCache, true, url, long.MaxValue);
            if (result != UrlFindResult.UrlFound)
            {
                return FergunResult.FromError(string.Format(Locate(result.ToString()), FergunClient.Config.MessagesToSearchLimit));
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Orctranslate: url to use: {url}"));

            (string error, string text) = await BingOcrAsync(url);
            if (!int.TryParse(error, out int processTime))
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Ocrtranslate: Failed to get OCR from Bing, using Yandex..."));

                (error, text) = await YandexOcrAsync(url);
                if (!int.TryParse(error, out processTime))
                {
                    string message = Locate(error);
                    if (!string.IsNullOrEmpty(text))
                        message += $"\n{Locate("ErrorMessage")}: {text}";

                    return FergunResult.FromError(message);
                }
            }

            var sw = Stopwatch.StartNew();
            ITranslationResult translationResult;
            try
            {
                translationResult = await _translator.TranslateAsync(text, target);
            }
            catch (Exception e) when (e is TranslatorException || e is HttpRequestException)
            {
                return FergunResult.FromError(e.Message);
            }
            catch (AggregateException e)
            {
                return FergunResult.FromError(e.InnerExceptions.FirstOrDefault()?.Message ?? e.Message);
            }
            finally
            {
                sw.Stop();
            }

            string translation = translationResult.Translation;
            if (text.Length > EmbedFieldBuilder.MaxFieldValueLength - 10)
            {
                try
                {
                    var hastebinUrl = await Hastebin.UploadAsync(translation);
                    translation = Format.Url(Locate("HastebinLink"), hastebinUrl);
                }
                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Ocrtranslate: Error while uploading text to Hastebin", e));
                    translation = Format.Code(translation.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md");
                }
            }
            else
            {
                translation = Format.Code(translation.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md");
            }

            var builder = new EmbedBuilder()
                .WithTitle(Locate("OcrtrResults"))
                .AddField(Locate("Input"), Format.Code(text.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md"))
                .AddField(Locate("SourceLanguage"), translationResult.SourceLanguage.Name)
                .AddField(Locate("TargetLanguage"), translationResult.TargetLanguage.Name)
                .AddField(Locate("Result"), translation)
                .WithFooter(string.Format(Locate("ProcessingTime"), processTime + sw.ElapsedMilliseconds))
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        //[LongRunning]
        [Command("paste", RunMode = RunMode.Async), Ratelimit(1, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("pasteSummary")]
        [Alias("haste")]
        public async Task<RuntimeResult> Paste([Remainder, Summary("pasteParam1")] string text)
        {
            var message = await SendEmbedAsync($"{FergunClient.Config.LoadingEmote} {Locate("Uploading")}");
            try
            {
                string hastebinUrl = await Hastebin.UploadAsync(text);
                var builder = new EmbedBuilder()
                    .WithDescription(Format.Url(Locate("HastebinLink"), hastebinUrl))
                    .WithColor(FergunClient.Config.EmbedColor);

                await message.ModifyOrResendAsync(embed: builder.Build(), cache: _messageCache);
            }
            catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Paste: Error while uploading text to Hastebin", e));
                return FergunResult.FromError(e.Message);
            }
            return FergunResult.FromSuccess();
        }

        [Command("ping", RunMode = RunMode.Async)]
        [Summary("pingSummary")]
        public async Task Ping()
        {
            var sw = Stopwatch.StartNew();
            var message = await SendEmbedAsync(Format.Bold("Pong!"));
            sw.Stop();

            var sw2 = Stopwatch.StartNew();
            FergunClient.Database.FindDocument<GuildConfig>(Constants.GuildConfigCollection, _ => true);
            sw2.Stop();

            var builder = new EmbedBuilder()
                .WithDescription($"⏱{Format.Bold("Message")}: {sw.ElapsedMilliseconds}ms\n\n" +
                                 $"{FergunClient.Config.WebSocketEmote}{Format.Bold("WebSocket")}: {Context.Client.Latency}ms\n\n" +
                                 $"{FergunClient.Config.MongoDbEmote}{Format.Bold("Database")}: {Math.Round(sw2.Elapsed.TotalMilliseconds, 2)}ms")
                .WithColor(FergunClient.Config.EmbedColor);

            await message.ModifyAsync(x => x.Embed = builder.Build());
        }

        [LongRunning]
        [Command("resize", RunMode = RunMode.Async), Ratelimit(1, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("resizeSummary")]
        [Alias("waifu2x", "w2x")]
        [Remarks("NoUrlPassed")]
        [Example("https://www.fergun.com/image.png")]
        public async Task<RuntimeResult> Resize([Summary("resizeParam1")] string url = null)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.DeepAiApiKey))
            {
                return FergunResult.FromError(string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.DeepAiApiKey)));
            }

            UrlFindResult result;
            (url, result) = await Context.GetLastUrlAsync(FergunClient.Config.MessagesToSearchLimit, _messageCache, true, url);
            if (result != UrlFindResult.UrlFound)
            {
                return FergunResult.FromError(string.Format(Locate(result.ToString()), FergunClient.Config.MessagesToSearchLimit));
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Resize: url to use: {url}"));

            string json;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepai.org/api/waifu2x");
                request.Headers.Add("Api-Key", FergunClient.Config.DeepAiApiKey);
                request.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("image", url) });
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                json = await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error calling waifu2x API", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error calling waifu2x API", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            using var document = JsonDocument.Parse(json);

            string resultUrl = document
                .RootElement
                .GetPropertyOrDefault("output_url")
                .GetStringOrDefault();

            if (string.IsNullOrWhiteSpace(resultUrl))
            {
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }

            var builder = new EmbedBuilder()
                .WithTitle(Locate("ResizeResults"))
                .WithImageUrl(resultUrl)
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [Command("roleinfo")]
        [Summary("roleinfoSummary")]
        [Alias("role")]
        [Example("Devs")]
        public async Task<RuntimeResult> RoleInfo([Remainder, Summary("roleinfoParam1")] SocketRole role)
        {
            int memberCount = role.Members.Count();

            var builder = new EmbedBuilder()
                .WithTitle(Locate("RoleInfo"))
                .AddField(Locate("Name"), role.Name, true)
                .AddField(Locate("Color"), $"{role.Color} ({role.Color.R}, {role.Color.G}, {role.Color.B})", true)
                .AddField(Locate("IsMentionable"), Locate(role.IsMentionable), true)
                .AddField("ID", role.Id, true)
                .AddField(Locate("IsHoisted"), Locate(role.IsHoisted), true)
                .AddField(Locate("Position"), role.Position, true)
                .AddField(Locate("Permissions"), role.Permissions.RawValue == 0 ? Locate("None") : Format.Code(string.Join("`, `", role.Permissions.ToList())))
                .AddField(Locate("MemberCount"), Context.Guild.HasAllMembers ? memberCount.ToString() : memberCount == 0 ? "?" : "~" + memberCount, true)
                .AddField(Locate("CreatedAt"), role.CreatedAt.ToDiscordTimestamp(), true)
                .AddField(Locate("Mention"), role.Mention, true)
                .WithColor(role.Color)
                .WithThumbnailUrl(role.GetIconUrl());

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        // The attribute order matters
        [RequireNsfw(ErrorMessage = "NSFWOnly")]
        [RequireBotPermission(ChannelPermission.AttachFiles, ErrorMessage = "BotRequireAttachFiles")]
        [LongRunning]
        [Command("screenshot", RunMode = RunMode.Async), Ratelimit(2, 1, Measure.Minutes)]
        [Summary("screenshotSummary")]
        [Alias("ss")]
        [Example("https://www.fergun.com")]
        public async Task<RuntimeResult> Screenshot([Summary("screenshotParam1")] string url)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.ApiFlashAccessKey))
            {
                return FergunResult.FromError(string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.ApiFlashAccessKey)));
            }

            Uri uri;
            try
            {
                uri = new UriBuilder(Uri.UnescapeDataString(url)).Uri;
            }
            catch (UriFormatException)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Screenshot: Invalid url: {Uri.UnescapeDataString(url)}"));
                return FergunResult.FromError(Locate("InvalidUrl"));
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Screenshot: Url: {uri.AbsoluteUri}"));

            ApiFlashResponse response;
            try
            {
                response = await ApiFlash.UrlToImageAsync(FergunClient.Config.ApiFlashAccessKey, uri.AbsoluteUri, ApiFlash.FormatType.Png, "400,403,404,500-511");
            }
            catch (ArgumentException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Screenshot: Error in API", e));
                return FergunResult.FromError(Locate("InvalidUrl"));
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Screenshot: Error in API", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Screenshot: Error in API", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            if (response.ErrorMessage != null)
            {
                return FergunResult.FromError(response.ErrorMessage);
            }

            try
            {
                var builder = new EmbedBuilder()
                    .WithTitle("Screenshot")
                    .WithDescription($"Url: {uri}".Truncate(EmbedBuilder.MaxDescriptionLength))
                    .WithImageUrl("attachment://screenshot.png")
                    .WithColor(FergunClient.Config.EmbedColor);

                await using var image = await _httpClient.GetStreamAsync(new Uri(response.Url));
                await Context.Channel.SendCachedFileAsync(Cache, Context.Message.Id, image, "screenshot.png", embed: builder.Build());
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the image from url: {url}", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the image from url: {url}", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            return FergunResult.FromSuccess();
        }

        [RequireContext(ContextType.Guild, ErrorMessage = "NotSupportedInDM")]
        [Command("serverinfo", RunMode = RunMode.Async)]
        [Summary("serverinfoSummary")]
        [Alias("server", "guild", "guildinfo")]
        public async Task<RuntimeResult> ServerInfo(string serverId = null)
        {
            SocketGuild server;
            if (Context.User.Id == (await Context.Client.GetApplicationInfoAsync()).Owner.Id)
            {
                server = serverId == null ? Context.Guild : Context.Client.GetGuild(ulong.Parse(serverId));
                if (server == null)
                {
                    return FergunResult.FromError(Locate("GuildNotFound"));
                }
            }
            else
            {
                server = Context.Guild;
            }

            string features = server.Features.Value == GuildFeature.None ? Locate("None") : string.Join(", ", server.Features.Value);

            string channelCountInfo = $"{server.TextChannels.Count + server.VoiceChannels.Count} " +
                                      $"({FergunClient.Config.TextEmote} {server.TextChannels.Count} **|** " +
                                      $"{FergunClient.Config.VoiceEmote} {server.VoiceChannels.Count})";

            var builder = new EmbedBuilder()
                .WithTitle(Locate("ServerInfo"))

                .AddField(Locate("Name"), server.Name, true)
                .AddField(Locate("Owner"), MentionUtils.MentionUser(server.OwnerId), true)
                .AddField("ID", server.Id, true)

                .AddField(Locate("CategoryCount"), server.CategoryChannels.Count, true)
                .AddField(Locate("ChannelCount"), channelCountInfo, true)
                .AddField(Locate("RoleCount"), server.Roles.Count, true)

                .AddField(Locate("DefaultChannel"), server.DefaultChannel?.Mention ?? Locate("None"), true)
                .AddField(Locate("Region"), Format.Code(server.VoiceRegionId), true)
                .AddField(Locate("VerificationLevel"), Locate(server.VerificationLevel.ToString()), true)

                .AddField(Locate("BoostTier"), (int)server.PremiumTier, true)
                .AddField(Locate("BoostCount"), server.PremiumSubscriptionCount, true)
                .AddField(Locate("ServerFeatures"), features, true);

            if (server.HasAllMembers && FergunClient.Config.PresenceIntent)
            {
                builder.AddField(Locate("Members"), $"{server.MemberCount} (Bots: {server.Users.Count(x => x.IsBot)}) **|** " +
                $"{FergunClient.Config.OnlineEmote} {server.Users.Count(x => x.Status == UserStatus.Online)} **|** " +
                $"{FergunClient.Config.IdleEmote} {server.Users.Count(x => x.Status == UserStatus.Idle)} **|** " +
                $"{FergunClient.Config.DndEmote} {server.Users.Count(x => x.Status == UserStatus.DoNotDisturb)} **|** " +
                $"{FergunClient.Config.StreamingEmote} {server.Users.Count(x => x.Activities.Any(y => y.Type == ActivityType.Streaming))} **|** " +
                $"{FergunClient.Config.OfflineEmote} {server.Users.Count(x => x.Status == UserStatus.Offline)}");
            }
            else
            {
                builder.AddField(Locate("Members"), server.HasAllMembers ? "~" : "" + server.MemberCount, true);
            }

            builder.AddField(Locate("CreatedAt"), $"{server.CreatedAt.ToDiscordTimestamp()} ({server.CreatedAt.ToDiscordTimestamp('R')})", true)
                .WithThumbnailUrl($"https://cdn.discordapp.com/icons/{server.Id}/{server.IconId}")
                .WithImageUrl(server.BannerUrl)
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("shorten", RunMode = RunMode.Async), Ratelimit(1, 1, Measure.Minutes)]
        [Summary("shortenSummary")]
        [Alias("short")]
        [Example("https://www.fergun.com")]
        public async Task<RuntimeResult> Shorten([Summary("shortenParam1")] string url)
        {
            Uri uri;
            try
            {
                uri = new UriBuilder(Uri.UnescapeDataString(url)).Uri;
            }
            catch (UriFormatException)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Shorten: Invalid url: {Uri.UnescapeDataString(url)}"));
                return FergunResult.FromError(Locate("InvalidUrl"));
            }
            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Shorten: Url: {uri.AbsoluteUri}"));

            HttpResponseMessage response;
            try
            {
                // GetStringAsync() hides the content message in case of error.
                response = await _httpClient.GetAsync(new Uri($"https://is.gd/create.php?format=simple&url={Uri.EscapeDataString(uri.AbsoluteUri)}"));
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error in is.gd API, url: {url}", e));
                return FergunResult.FromError(e.Message);
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error in is.gd API, url: {url}", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            string shortenedUrl = await response.Content.ReadAsStringAsync();

            if (shortenedUrl.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
            {
                return FergunResult.FromError(shortenedUrl);
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Shorten: Shortened Url: {shortenedUrl}"));
            await ReplyAsync(shortenedUrl);

            return FergunResult.FromSuccess();
        }

        [Command("snipe", RunMode = RunMode.Async)]
        [Summary("snipeSummary")]
        [Example("#help")]
        public async Task Snipe([Summary("snipeParam1")] IMessageChannel channel = null)
        {
            channel ??= Context.Channel;

            var message = _messageCache
                .GetCacheForChannel(channel, MessageSourceEvent.MessageDeleted)
                .Values
                .OrderByDescending(x => x.CachedAt)
                .FirstOrDefault(x => !GuildUtils.UserConfigCache.TryGetValue(x.Author.Id, out var config) || !config.IsOptedOutSnipe);

            var builder = new EmbedBuilder();
            if (message == null)
            {
                builder.WithDescription(string.Format(Locate("NothingToSnipe"), MentionUtils.MentionChannel(channel.Id)));
            }
            else
            {
                string text = !string.IsNullOrEmpty(message.Content) ? message.Content : message.Attachments.Count > 0 ? $"({Locate("Attachment")})" : "?";

                builder.WithAuthor(message.Author)
                    .WithDescription(text.Truncate(EmbedBuilder.MaxDescriptionLength))
                    .WithFooter($"{Locate("In")} #{message.Channel.Name}")
                    .WithTimestamp(message.CreatedAt);

                if (Random.Shared.Next(5) == 4)
                {
                    builder.AddField(Locate("Privacy"), Locate("SnipePrivacy"));
                }
            }
            builder.WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());
        }

        [LongRunning]
        [Command("translate", RunMode = RunMode.Async)]
        [Summary("translateSummary")]
        [Alias("tr")]
        [Example("es hello world")]
        public async Task<RuntimeResult> Translate([Summary("translateParam1")] string target,
            [Remainder, Summary("translateParam2")] string text)
        {
            if (target == "language" && text == "codes")
            {
                var languagesBuilder = new EmbedBuilder()
                    .WithTitle(Locate("LanguageList"))
                    .WithColor(FergunClient.Config.EmbedColor);

                var orderedLangs = Language.LanguageDictionary.Values.OrderBy(x => x.Name).ToArray();
                string tempLangs = "";

                for (int i = 1; i <= orderedLangs.Length; i++)
                {
                    tempLangs += $"{Format.Code(orderedLangs[i - 1].ISO6391)}: {orderedLangs[i - 1].Name}\n";
                    if (i % 15 == 0)
                    {
                        languagesBuilder.AddField("\u200b", tempLangs, true);
                        tempLangs = "";
                    }
                    else if (i == orderedLangs.Length)
                    {
                        languagesBuilder.AddField("\u200b", tempLangs, true);
                    }
                }

                await ReplyAsync(embed: languagesBuilder.Build());
                return FergunResult.FromSuccess();
            }
            if (!_translator.IsLanguageSupported(target))
            {
                return FergunResult.FromError(string.Format(Locate("InvalidLanguage"), GetPrefix()));
            }

            ITranslationResult result;
            try
            {
                result = await _translator.TranslateAsync(text, target);
            }
            catch (Exception e) when (e is TranslatorException || e is HttpRequestException)
            {
                return FergunResult.FromError(e.Message);
            }
            catch (AggregateException e)
            {
                return FergunResult.FromError(e.InnerExceptions.FirstOrDefault()?.Message ?? e.Message);
            }

            string translation = result.Translation;
            if (text.Length > EmbedFieldBuilder.MaxFieldValueLength - 10)
            {
                try
                {
                    var hastebinUrl = await Hastebin.UploadAsync(translation);
                    translation = Format.Url(Locate("HastebinLink"), hastebinUrl);
                }
                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Translate: Error while uploading text to Hastebin", e));
                    translation = Format.Code(translation.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md");
                }
            }
            else
            {
                translation = Format.Code(translation.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md");
            }

            string thumbnail = result.Service switch
            {
                "BingTranslator" => Constants.BingTranslatorLogoUrl,
                "YandexTranslator" => Constants.YandexTranslateLogoUrl,
                "MicrosoftTranslator" => Constants.MicrosoftAzureLogoUrl,
                _ => Constants.GoogleTranslateLogoUrl
            };

            var builder = new EmbedBuilder()
                .WithTitle(Locate("TranslationResults"))
                .AddField(Locate("SourceLanguage"), result.SourceLanguage.Name)
                .AddField(Locate("TargetLanguage"), result.TargetLanguage.Name)
                .AddField(Locate("Result"), translation)
                .WithThumbnailUrl(thumbnail)
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [RequireBotPermission(ChannelPermission.AttachFiles, ErrorMessage = "BotRequireAttachFiles")]
        [LongRunning]
        [Command("tts", RunMode = RunMode.Async), Ratelimit(2, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("Text to speech.")]
        [Alias("texttospeech", "t2s")]
        [Example("en hello world")]
        public async Task<RuntimeResult> Tts([Summary("ttsParam1")] string target,
            [Remainder, Summary("ttsParam2")] string text)
        {
            target = target.ToLowerInvariant();
            text = text.ToLowerInvariant();

            if (!Language.TryGetLanguage(target, out var lang) || !GoogleTranslator2.TextToSpeechLanguages.Contains(lang))
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"TTS: Target language not supported ({target})"));
                //return CustomResult.FromError(GetValue("InvalidLanguage"));
                text = $"{target} {text}";
                target = "en";
            }

            try
            {
                var stream = await _googleTranslator2.TextToSpeechAsync(text, target);
                await Context.Channel.SendCachedFileAsync(Cache, Context.Message.Id, stream, "tts.mp3");
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "TTS: Error while getting TTS", e));
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }
            catch (TaskCanceledException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "TTS: Error while getting TTS", e));
                return FergunResult.FromError(Locate("RequestTimedOut"));
            }

            return FergunResult.FromSuccess();
        }

        // The attribute order matters
        [LongRunning]
        [Command("urban", RunMode = RunMode.Async)]
        [Summary("urbanSummary")]
        [Alias("ud")]
        [Example("pog")]
        public async Task<RuntimeResult> Urban([Remainder, Summary("urbanParam1")] string query = null)
        {
            UrbanResponse search;

            query = query?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", "Urban: Getting random words..."));
                try
                {
                    search = await UrbanApi.GetRandomWordsAsync();
                }
                catch (HttpRequestException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Urban: Error in API", e));
                    return FergunResult.FromError($"Error in Urban Dictionary API: {e.Message}");
                }
            }
            else
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Urban: Query \"{query}\""));
                try
                {
                    search = await UrbanApi.SearchWordAsync(query);
                }
                catch (HttpRequestException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Urban: Error in API", e));
                    return FergunResult.FromError($"Error in Urban Dictionary API: {e.Message}");
                }
                if (search.Definitions.Count == 0)
                {
                    return FergunResult.FromError(Locate("NoResults"));
                }
            }

            string by = Locate("By");
            string noExampleText = Locate("NoExample");
            string exampleText = Locate("Example");
            string paginatorFooter = $"Urban Dictionary {(string.IsNullOrWhiteSpace(query) ? "(Random words)" : "")} - {Locate("PaginatorFooter")}";

            Task<PageBuilder> GeneratePageAsync(int index)
            {
                var info = search.Definitions[index];

                // Replace all occurrences of a term in brackets with a hyperlink directing to the definition of that term.
                string definition = _bracketRegex.Replace(info.Definition,
                        m => Format.Url(m.Groups[1].Value, $"https://urbandictionary.com/define.php?term={Uri.EscapeDataString(m.Groups[1].Value)}"))
                    .Truncate(EmbedBuilder.MaxDescriptionLength);

                string example;
                if (string.IsNullOrEmpty(info.Example))
                {
                    example = noExampleText;
                }
                else
                {
                    example = _bracketRegex.Replace(info.Example,
                            m => Format.Url(m.Groups[1].Value, $"https://urbandictionary.com/define.php?term={Uri.EscapeDataString(m.Groups[1].Value)}"))
                        .Truncate(EmbedFieldBuilder.MaxFieldValueLength);
                }

                var pageBuilder = new PageBuilder()
                    .WithAuthor($"{by} {info.Author}")
                    .WithColor(new Discord.Color(FergunClient.Config.EmbedColor))
                    .WithTitle(info.Word.Truncate(EmbedBuilder.MaxTitleLength))
                    .WithUrl(info.Permalink)
                    .WithDescription(definition)
                    .AddField(exampleText, example)
                    .AddField("👍", info.ThumbsUp, true)
                    .AddField("👎", info.ThumbsDown, true)
                    .WithFooter(string.Format(paginatorFooter, index + 1, search.Definitions.Count))
                    .WithTimestamp(info.WrittenOn);

                return Task.FromResult(pageBuilder);
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(Context.User)
                .WithOptions(CommandUtils.GetFergunPaginatorEmotes(FergunClient.Config))
                .WithMaxPageIndex(search.Definitions.Count - 1)
                .WithPageFactory(GeneratePageAsync)
                .WithFooter(PaginatorFooter.None)
                .WithActionOnCancellation(ActionOnStop.DisableInput)
                .WithActionOnTimeout(ActionOnStop.DisableInput)
                .WithDeletion(DeletionOptions.Valid)
                .Build();

            _ = SendPaginatorAsync(paginator, Constants.PaginatorTimeout);

            return FergunResult.FromSuccess();
        }

        [Command("userinfo", RunMode = RunMode.Async)]
        [Summary("userinfoSummary")]
        [Alias("ui", "user", "whois")]
        [Example("Fergun#6839")]
        public async Task<RuntimeResult> UserInfo([Remainder, Summary("userinfoParam1")] IUser user = null)
        {
            user ??= Context.User;

            string activities = "";
            if (user.Activities.Count > 0)
            {
                activities = string.Join('\n', user.Activities.Select(x =>
                    x.Type == ActivityType.CustomStatus
                        ? ((CustomStatusGame)x).ToString()
                        : $"{x.Type} {x.Name}"));
            }

            if (string.IsNullOrWhiteSpace(activities))
                activities = Locate("None");

            string clients = "?";
            if (user.ActiveClients.Count > 0)
            {
                clients = string.Join(' ', user.ActiveClients.Select(x =>
                    x switch
                    {
                        ClientType.Desktop => "🖥",
                        ClientType.Mobile => "📱",
                        ClientType.Web => "🌐",
                        _ => ""
                    }));
            }

            if (string.IsNullOrWhiteSpace(clients))
                clients = "?";

            var flags = user.PublicFlags ?? UserProperties.None;

            string badges = flags == UserProperties.None ? null
                : string.Join(' ',
                Enum.GetValues(typeof(UserProperties))
                    .Cast<Enum>()
                    .Where(flags.HasFlag)
                    .Select(x => FergunClient.Config.UserFlagsEmotes.TryGetValue(x.ToString(), out string emote) ? emote : null)
                    .Distinct());

            var guildUser = user as IGuildUser;

            if (guildUser?.PremiumSince != null)
            {
                badges += " " + FergunClient.Config.BoosterEmote;
            }
            if (string.IsNullOrWhiteSpace(badges))
            {
                badges = Locate("None");
            }

            Discord.Color avatarColor = default;
            string avatarUrl = user.GetAvatarUrl(ImageFormat.Auto, 2048) ?? user.GetDefaultAvatarUrl();

            if (user is RestUser restUser && restUser.AccentColor != null)
            {
                avatarColor = restUser.AccentColor.Value;
            }

            if (avatarColor == default)
            {
                if (!(user is RestUser))
                {
                    // Prevent getting error 404 while downloading the avatar getting the user from REST.
                    user = await Context.Client.Rest.GetUserAsync(user.Id);
                    avatarUrl = user.GetAvatarUrl(ImageFormat.Auto, 2048) ?? user.GetDefaultAvatarUrl();
                }

                string thumbnail = user.GetAvatarUrl(ImageFormat.Png) ?? user.GetDefaultAvatarUrl();

                try
                {
                    await using var response = await _httpClient.GetStreamAsync(new Uri(thumbnail));
                    using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(response);
                    var average = img.GetAverageColor().ToPixel<Rgba32>();
                    avatarColor = new Discord.Color(average.R, average.G, average.B);
                }
                catch (HttpRequestException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the avatar from user {user}", e));
                    return FergunResult.FromError(e.Message);
                }
                catch (TaskCanceledException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Error getting the avatar from user {user}", e));
                    return FergunResult.FromError(Locate("RequestTimedOut"));
                }
            }

            var builder = new EmbedBuilder()
                .WithTitle(Locate("UserInfo"))
                .AddField(Locate("Name"), user.ToString())
                .AddField("Nickname", guildUser?.Nickname ?? Locate("None"))
                .AddField("ID", user.Id)
                .AddField(Locate("Activity"), activities, true)
                .AddField(Locate("ActiveClients"), clients, true)
                .AddField(Locate("Badges"), badges)
                .AddField(Locate("IsBot"), Locate(user.IsBot))
                .AddField(Locate("CreatedAt"), GetTimestamp(user.CreatedAt))
                .AddField(Locate("GuildJoinDate"), GetTimestamp(guildUser?.JoinedAt))
                .AddField(Locate("BoostingSince"), GetTimestamp(guildUser?.PremiumSince))
                .WithThumbnailUrl(avatarUrl)
                .WithColor(avatarColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();

            static string GetTimestamp(DateTimeOffset? dateTime)
                => dateTime == null ? "N/A" : $"{dateTime.ToDiscordTimestamp()} ({dateTime.ToDiscordTimestamp('R')})";
        }

        [LongRunning]
        [Command("wikipedia", RunMode = RunMode.Async)]
        [Summary("wikipediaSummary")]
        [Alias("wiki")]
        [Example("Discord")]
        public async Task<RuntimeResult> Wikipedia([Remainder, Summary("wikipediaParam1")] string query)
        {
            string language = GetLanguage();
            (string Title, string Extract, string ImageUrl, int Id)[] articles;
            try
            {
                articles = await GetArticlesAsync(query, language);
            }
            catch (HttpRequestException e)
            {
                return FergunResult.FromError(e.Message);
            }

            if (articles.Length == 0)
            {
                return FergunResult.FromError(Locate("NoResults"));
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Command", $"Wikipedia: Results count: {articles.Length}"));

            // Cache localized strings
            string wikipediaSearch = Locate("WikipediaSearch");
            string paginatorFooter = Locate("PaginatorFooter");

            var paginator = new LazyPaginatorBuilder()
                .AddUser(Context.User)
                .WithOptions(CommandUtils.GetFergunPaginatorEmotes(FergunClient.Config))
                .WithMaxPageIndex(articles.Length - 1)
                .WithPageFactory(GeneratePageAsync)
                .WithFooter(PaginatorFooter.None)
                .WithActionOnCancellation(ActionOnStop.DisableInput)
                .WithActionOnTimeout(ActionOnStop.DisableInput)
                .WithDeletion(DeletionOptions.Valid)
                .Build();

            _ = SendPaginatorAsync(paginator, Constants.PaginatorTimeout);

            return FergunResult.FromSuccess();

            static async Task<(string Title, string Extract, string ImageUrl, int Id)[]> GetArticlesAsync(string query, string language)
            {
                string url = $"https://{language}.wikipedia.org/w/api.php?" +
                    "action=query" +
                    "&generator=prefixsearch" + // https://www.mediawiki.org/wiki/API:Prefixsearch
                    "&format=json" +
                    "&formatversion=2" +
                    "&prop=extracts|pageimages|description" + // Get article extract, page images and short description
                    //"&exchars=1200" + // Return max 1200 characters
                    "&exintro" + // Return only content before the first section
                    "&explaintext" + // Return extracts as plain text
                    "&redirects" + // Automatically resolve redirects
                    $"&gpssearch={Uri.EscapeDataString(query)}" + // Search string
                    "&pilicense=any" + // Get images with any license
                    "&piprop=original"; // Get original images

                byte[] bytes = await _httpClient.GetByteArrayAsync(url);

                using var document = JsonDocument.Parse(bytes);

                var pages = document
                    .RootElement
                    .GetPropertyOrDefault("query")
                    .GetPropertyOrDefault("pages");

                // When there are no results, the API returns {"batchcomplete":true} instead of an empty array
                var articles = pages.ValueKind != JsonValueKind.Array
                    ? Array.Empty<(string, string, string, int)>()
                    : pages.EnumerateArray()
                    .Select(x => (
                    Title: x.GetProperty("title").GetString(),
                    Extract: x.GetPropertyOrDefault("extract").GetStringOrDefault(),
                    ImageUrl: x.GetPropertyOrDefault("original").GetPropertyOrDefault("source").GetStringOrDefault(),
                    Id: x.GetPropertyOrDefault("pageid").GetInt32OrDefault()))
                    .ToArray();

                return articles.Length == 0 && language != "en"
                    ? await GetArticlesAsync(query, "en")
                    : articles;
            }

            Task<PageBuilder> GeneratePageAsync(int index)
            {
                bool isMobile = Context.User.ActiveClients.Contains(ClientType.Mobile);

                var builder = new PageBuilder()
                        .WithAuthor(Context.User)
                        .WithColor(new Discord.Color(FergunClient.Config.EmbedColor))
                        .WithTitle(articles[index].Title.Truncate(EmbedBuilder.MaxTitleLength))
                        .WithUrl($"https://{language}.{(isMobile ? "m." : "")}wikipedia.org?curid={articles[index].Id}")
                        .WithDescription(articles[index].Extract?.Truncate(EmbedBuilder.MaxDescriptionLength) ?? "?")
                        .WithThumbnailUrl($"https://commons.wikimedia.org/w/index.php?title=Special:Redirect/file/Wikipedia-logo-v2-{language}.png")
                        .WithFooter($"{wikipediaSearch} - {string.Format(paginatorFooter, index + 1, articles.Length)}");

                if (Context.IsNsfw() && !string.IsNullOrEmpty(articles[index].ImageUrl))
                {
                    string decodedUrl = Uri.UnescapeDataString(articles[index].ImageUrl);
                    if (Uri.IsWellFormedUriString(decodedUrl, UriKind.Absolute))
                    {
                        builder.ThumbnailUrl = decodedUrl;
                    }
                }

                return Task.FromResult(builder);
            }
        }

        [LongRunning]
        [Command("wolframalpha", RunMode = RunMode.Async), Ratelimit(1, Constants.GlobalRatelimitPeriod, Measure.Minutes)]
        [Summary("wolframalphaSummary")]
        [Alias("wolfram", "wa")]
        [Example("2 + 2")]
        public async Task<RuntimeResult> WolframAlpha([Remainder, Summary("wolframalphaParam1")] string query)
        {
            if (string.IsNullOrEmpty(FergunClient.Config.WolframAlphaAppId))
            {
                return FergunResult.FromError(string.Format(Locate("ValueNotSetInConfig"), nameof(FergunConfig.WolframAlphaAppId)));
            }

            string output;
            try
            {
                var response = await _httpClient.GetAsync(new Uri($"https://api.wolframalpha.com/v1/result?i={Uri.EscapeDataString(query)}&appid={FergunClient.Config.WolframAlphaAppId}"));
                if (response.StatusCode == HttpStatusCode.NotImplemented)
                {
                    return FergunResult.FromError("The input could not be interpreted by the API.");
                }

                response.EnsureSuccessStatusCode();
                output = await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", "Error calling WolframAlpha API", e));
                return FergunResult.FromError(e.Message);
            }

            var builder = new EmbedBuilder()
                .WithAuthor("Wolfram Alpha", Constants.WolframAlphaLogoUrl)
                .AddField(Locate("Input"), Format.Code(query.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md"))
                .AddField(Locate("Output"), Format.Code(output.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 10), "md"))
                .WithFooter("wolframalpha.com")
                .WithColor(FergunClient.Config.EmbedColor);

            await ReplyAsync(embed: builder.Build());

            return FergunResult.FromSuccess();
        }

        [Command("xkcd")]
        [Summary("xkcdSummary")]
        [Example("1000")]
        public async Task<RuntimeResult> Xkcd([Summary("xkcdParam1")] int? number = null)
        {
            await UpdateLastComicAsync();
            if (_lastComic == null)
            {
                return FergunResult.FromError(Locate("AnErrorOccurred"));
            }
            if (number != null && (number < 1 || number > _lastComic.Num))
            {
                return FergunResult.FromError(string.Format(Locate("InvalidxkcdNumber"), _lastComic.Num));
            }
            if (number == 404)
            {
                return FergunResult.FromError("404 Not Found");
            }
            string response = await _httpClient.GetStringAsync($"https://xkcd.com/{number ?? Random.Shared.Next(1, _lastComic.Num)}/info.0.json");

            var comic = JsonConvert.DeserializeObject<XkcdComic>(response);

            var builder = new EmbedBuilder()
                .WithTitle(comic.Title.Truncate(EmbedBuilder.MaxTitleLength))
                .WithUrl($"https://xkcd.com/{comic.Num}/")
                .WithImageUrl(comic.Img)
                .WithFooter(comic.Alt.Truncate(EmbedFooterBuilder.MaxFooterTextLength))
                .WithTimestamp(new DateTime(int.Parse(comic.Year), int.Parse(comic.Month), int.Parse(comic.Day)));

            await ReplyAsync(embed: builder.Build());
            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("youtube", RunMode = RunMode.Async)]
        [Summary("youtubeSummary")]
        [Alias("yt")]
        [Example("discord")]
        public async Task<RuntimeResult> YouTube([Remainder, Summary("youtubeParam1")] string query)
        {
            string[] urls;
            try
            {
                urls = await _ytClient.Search.GetVideosAsync(query).Take(10).Select(x => x.Url).ToArrayAsync();
            }
            catch (HttpRequestException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Youtube: Error obtaining videos (query: {query})", e));
                return FergunResult.FromError(e.Message);
            }
            catch (YoutubeExplodeException e)
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Youtube: Error obtaining videos (query: {query})", e));
                return FergunResult.FromError(Locate("ErrorInAPI"));
            }

            switch (urls.Length)
            {
                case 0:
                    return FergunResult.FromError(Locate("NoResultsFound"));

                case 1:
                    await ReplyAsync(urls[0]);
                    break;

                default:
                {
                    string paginatorFooter = Locate("PaginatorFooter");

                    Task<PageBuilder> GeneratePageAsync(int index)
                    {
                        var pageBuilder = new PageBuilder()
                            .WithText($"{urls[index]}\n{string.Format(paginatorFooter, index + 1, urls.Length)}");

                        return Task.FromResult(pageBuilder);
                    }

                    var paginator = new LazyPaginatorBuilder()
                        .AddUser(Context.User)
                        .WithOptions(CommandUtils.GetFergunPaginatorEmotes(FergunClient.Config))
                        .WithMaxPageIndex(urls.Length - 1)
                        .WithPageFactory(GeneratePageAsync)
                        .WithFooter(PaginatorFooter.None)
                        .WithActionOnCancellation(ActionOnStop.DisableInput)
                        .WithActionOnTimeout(ActionOnStop.DisableInput)
                        .WithDeletion(DeletionOptions.Valid)
                        .Build();

                    _ = SendPaginatorAsync(paginator, Constants.PaginatorTimeout);
                        break;
                }
            }

            return FergunResult.FromSuccess();
        }

        [LongRunning]
        [Command("ytrandom", RunMode = RunMode.Async)]
        [Summary("ytrandomSummary")]
        [Alias("ytrand")]
        public async Task<RuntimeResult> YtRandom()
        {
            for (int i = 0; i < 10; i++)
            {
                string randStr = StringUtils.RandomString(Random.Shared.Next(5, 7));
                IReadOnlyList<VideoSearchResult> videos;
                try
                {
                    videos = await _ytClient.Search.GetVideosAsync(randStr).Take(5);
                }
                catch (HttpRequestException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Ytrandom: Error obtaining videos (query: {randStr})", e));
                    return FergunResult.FromError(e.Message);
                }
                catch (YoutubeExplodeException e)
                {
                    await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "Command", $"Ytrandom: Error obtaining videos (query: {randStr})", e));
                    return FergunResult.FromError(Locate("ErrorInAPI"));
                }

                if (videos.Count != 0)
                {
                    string id = videos[Random.Shared.Next(videos.Count)].Id;
                    await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Ytrandom", $"Using id: {id} (random string: {randStr}, search count: {videos.Count})"));

                    await ReplyAsync($"https://www.youtube.com/watch?v={id}");
                    return FergunResult.FromSuccess();
                }

                await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "Ytrandom", $"No videos found on random string ({randStr})"));
            }

            return FergunResult.FromError(Locate("AnErrorOccurred"));
        }

        private async Task<(string, string)> BingOcrAsync(string url)
        {
            string jsonRequest = $"{{\"imageInfo\":{{\"url\":\"{url}\",\"source\":\"Url\"}},\"knowledgeRequest\":{{\"invokedSkills\":[\"OCR\"]}}}}";
            using var content = new MultipartFormDataContent
            {
                { new StringContent(jsonRequest), "knowledgeRequest" }
            };

            using var request = new HttpRequestMessage();
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri("https://www.bing.com/images/api/custom/knowledge?skey=ZbQI4MYyHrlk2E7L-vIV2VLrieGlbMfV8FcK-WCY3ug");
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36");
            request.Headers.Referrer = new Uri($"https://www.bing.com/images/search?view=detailv2&iss=sbi&q=imgurl:{url}");
            request.Content = content;

            var sw = Stopwatch.StartNew();
            var response = await _httpClient.SendAsync(request);
            sw.Stop();
            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            using var document = JsonDocument.Parse(bytes);

            string imageCategory = document
                .RootElement
                .GetPropertyOrDefault("imageQualityHints")
                .FirstOrDefault()
                .GetPropertyOrDefault("category")
                .GetStringOrDefault();

            // UnknownFormat (Only JPEG, PNG o BMP allowed)
            // ImageDimensionsExceedLimit (Max. 4000px)
            // ImageByteSizeExceedsLimit (Max. 20MB)
            // ImageDownloadFailed
            // JunkImage
            if (!string.IsNullOrEmpty(imageCategory))
            {
                await _logService.LogAsync(new LogMessage(LogSeverity.Warning, "SimpleOcr", $"Bing Visual Search returned image category \"{imageCategory}\" for url: {url}."));
            }

            var textRegions = document
                .RootElement
                .GetPropertyOrDefault("tags")
                .FirstOrDefault(x => x.GetPropertyOrDefault("displayName").GetStringOrDefault() == "##TextRecognition")
                .GetPropertyOrDefault("actions")
                .FirstOrDefault()
                .GetPropertyOrDefault("data")
                .GetPropertyOrDefault("regions")
                .EnumerateArrayOrEmpty()
                .Select(x => string.Join('\n',
                    x.GetPropertyOrDefault("lines")
                    .EnumerateArrayOrEmpty()
                    .Select(y => y.GetPropertyOrDefault("text").GetStringOrDefault())));

            string joinedText = string.Join("\n\n", textRegions);
            if (!string.IsNullOrEmpty(joinedText))
            {
                return (sw.ElapsedMilliseconds.ToString(), joinedText);
            }

            await _logService.LogAsync(new LogMessage(LogSeverity.Verbose, "SimpleOcr", $"Bing Visual Search didn't return text for url: {url}."));

            return (string.IsNullOrEmpty(imageCategory) ? "OcrEmpty" : "OcrApiError", imageCategory);
        }

        private static async Task<(string, string)> YandexOcrAsync(string url)
        {
            // Get CBIR ID
            const string cbirJsonRequest = @"{""blocks"":[{""block"":""content_type_search-by-image"",""params"":{},""version"":2}]}";
            string requestUrl = $"https://yandex.com/images/search?rpt=imageview&url={Uri.EscapeDataString(url)}&format=json&request={cbirJsonRequest}&yu=0";
            using var searchRequest = new HttpRequestMessage();
            searchRequest.Method = HttpMethod.Get;
            searchRequest.RequestUri = new Uri(requestUrl);
            searchRequest.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36");

            var sw = Stopwatch.StartNew();
            var response = await _httpClient.SendAsync(searchRequest);
            sw.Stop();
            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            using var document = JsonDocument.Parse(bytes);

            var html = document
                .RootElement
                .GetProperty("blocks")[0]
                .GetProperty("html")
                .GetString() ?? "";

            int startIndex = html.IndexOf("\"cbirId\":\"", StringComparison.Ordinal);

            if (startIndex == -1)
            {
                return ("OcrApiError", null);
            }

            startIndex += 10;
            int endIndex = html.IndexOf("\"", startIndex + 10, StringComparison.Ordinal);

            if (startIndex == -1)
            {
                return ("OcrApiError", null);
            }

            string cbirId = html[startIndex..endIndex];

            // Get OCR text
            const string ocrJsonRequest = @"{""blocks"":[{""block"":{""block"":""i-react-ajax-adapter:ajax""},""params"":{""type"":""CbirOcr""},""version"":2}]}";
            requestUrl = $"https://yandex.com/images/search?format=json&request={ocrJsonRequest}&rpt=ocr&cbir_id={cbirId}";
            using var ocrRequest = new HttpRequestMessage();
            ocrRequest.Method = HttpMethod.Get;
            ocrRequest.RequestUri = new Uri(requestUrl);
            ocrRequest.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36");

            sw.Start();
            response = await _httpClient.SendAsync(ocrRequest);
            sw.Stop();
            bytes = await response.Content.ReadAsByteArrayAsync();
            using var ocrDocument = JsonDocument.Parse(bytes);

            string ocrText = ocrDocument
                .RootElement
                .GetProperty("blocks")[0]
                .GetProperty("params")
                .GetPropertyOrDefault("adapterData")
                .GetPropertyOrDefault("plainText")
                .GetStringOrDefault();

            if (!string.IsNullOrEmpty(ocrText))
            {
                return (sw.ElapsedMilliseconds.ToString(), ocrText);
            }

            return ("OcrEmpty", ocrText);
        }

        private static async Task UpdateLastComicAsync()
        {
            if (_timeToCheckComic >= DateTimeOffset.UtcNow) return;

            string response;
            try
            {
                response = await _httpClient.GetStringAsync("https://xkcd.com/info.0.json");
            }
            catch (HttpRequestException) { return; }

            _lastComic = JsonConvert.DeserializeObject<XkcdComic>(response);
            _timeToCheckComic = DateTimeOffset.UtcNow.AddDays(1);
        }

        private void InitializeCmdListCache()
        {
            if (_commandListCache == null)
            {
                _commandListCache = new Dictionary<string, string>();

                var modules = _cmdService.Modules
                    .Where(x => x.Name != Constants.DevelopmentModuleName && x.Commands.Count > 0)
                    .OrderBy(x => x.Attributes.OfType<OrderAttribute>().FirstOrDefault()?.Order ?? int.MaxValue);

                foreach (var module in modules)
                {
                    _commandListCache.Add(module.Name, string.Join(", ", module.Commands.Select(x => x.Name)));
                }
            }
            if (_cachedVisibleCmdCount == -1)
            {
                _cachedVisibleCmdCount = _cmdService.Commands.Count(x => x.Module.Name != Constants.DevelopmentModuleName);
            }
        }
    }
}
